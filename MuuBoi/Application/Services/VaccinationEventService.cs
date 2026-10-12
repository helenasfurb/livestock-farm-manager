using System.ComponentModel.DataAnnotations;
using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class VaccinationEventService : IVaccinationEventService
    {
        private readonly IVaccinationEventRepository _repository;
        private readonly IVaccineRepository _vaccineRepository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IMapper _mapper;

        public VaccinationEventService(
            IVaccinationEventRepository repository,
            IVaccineRepository vaccineRepository,
            IAnimalRepository animalRepository,
            IMapper mapper)
        {
            _repository = repository;
            _vaccineRepository = vaccineRepository;
            _animalRepository = animalRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<VaccinationEventListItemDto>> GetAllAsync(VaccinationEventFilterDto filter)
        {
            var events = await _repository.GetAllAsync(filter);
            var now = DateTime.UtcNow;

            // Status is derived in memory over already-loaded events (no per-row query).
            var items = events.Select(e =>
            {
                var dto = _mapper.Map<VaccinationEventListItemDto>(e);
                dto.Status = BuildStatus(e, now);
                return dto;
            });

            if (filter.Status.HasValue)
                items = items.Where(i => i.Status!.Value == (int)filter.Status.Value);

            return items.ToList();
        }

        public async Task<VaccinationEventDto> GetByIdAsync(int id)
        {
            var vaccinationEvent = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Evento de vacinação com id '{id}' não encontrado.");

            var child = await _repository.GetActiveChildByParentIdAsync(id);
            var now = DateTime.UtcNow;

            var dto = _mapper.Map<VaccinationEventDto>(vaccinationEvent);
            dto.Status = BuildStatus(vaccinationEvent, now);
            dto.ParentEvent = BuildLineage(vaccinationEvent.ParentEvent, now);
            dto.ChildEvent = BuildLineage(child, now);
            return dto;
        }

        public async Task<VaccinationEventDto> CreateAsync(VaccinationEventCreateDto dto)
        {
            if (dto.SyncId.HasValue)
            {
                var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return await GetByIdAsync(existing.Id);
            }

            await EnsureVaccineExistsAsync(dto.VaccineId!.Value);
            await EnsureAnimalsExistAsync(dto.AnimalIds);

            var vaccinationEvent = new VaccinationEvent
            {
                SyncId = dto.SyncId ?? Guid.NewGuid(),
                VaccineId = dto.VaccineId!.Value,
                // No parent on a manual event → default FirstDose, editable (D6).
                DoseType = dto.DoseType ?? DoseType.FirstDose,
                ApplicationDate = dto.ApplicationDate,
                PredictedDate = dto.PredictedDate,
                Notes = dto.Notes,
                EventAnimals = dto.AnimalIds
                    .Select(animalId => new VaccinationEventAnimal { AnimalId = animalId })
                    .ToList()
            };

            var created = await _repository.CreateAsync(vaccinationEvent);
            return await GetByIdAsync(created.Id);
        }

        public async Task<VaccinationEventDto> CreateBoosterAsync(int parentId, VaccinationBoosterCreateDto dto)
        {
            if (dto.SyncId.HasValue)
            {
                var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return await GetByIdAsync(existing.Id);
            }

            var parent = await _repository.GetByIdAsync(parentId)
                ?? throw new NotFoundException($"Evento de vacinação com id '{parentId}' não encontrado.");

            var predictedDate = dto.PredictedDate!.Value;

            // Only rule: the booster's predicted date cannot precede either of the parent's dates.
            if (parent.ApplicationDate.HasValue && predictedDate.Date < parent.ApplicationDate.Value.Date)
                throw new BusinessRuleException(
                    "A data prevista do reforço não pode ser anterior à data de aplicação do evento pai.");

            if (parent.PredictedDate.HasValue && predictedDate.Date < parent.PredictedDate.Value.Date)
                throw new BusinessRuleException(
                    "A data prevista do reforço não pode ser anterior à data prevista do evento pai.");

            // One booster per parent.
            var existingChild = await _repository.GetActiveChildByParentIdAsync(parentId);
            if (existingChild != null)
                return await ExistingBySyncIdOrConflictAsync(dto.SyncId);

            var animalIds = parent.EventAnimals?.Select(a => a.AnimalId).ToList() ?? new List<int>();
            var booster = new VaccinationEvent
            {
                SyncId = dto.SyncId ?? Guid.NewGuid(),
                VaccineId = parent.VaccineId,
                DoseType = DoseType.Booster,
                PredictedDate = predictedDate,
                ApplicationDate = null,
                ParentEventId = parentId,
                Notes = dto.Notes,
                EventAnimals = animalIds
                    .Select(animalId => new VaccinationEventAnimal { AnimalId = animalId })
                    .ToList()
            };

            var created = await _repository.CreateBoosterAsync(booster);
            if (created == null)
                return await ExistingBySyncIdOrConflictAsync(dto.SyncId);

            return await GetByIdAsync(created.Id);
        }

        private async Task<VaccinationEventDto> ExistingBySyncIdOrConflictAsync(Guid? syncId)
        {
            if (syncId.HasValue)
            {
                var existing = await _repository.GetBySyncIdAsync(syncId.Value);
                if (existing != null)
                    return await GetByIdAsync(existing.Id);
            }

            throw new ConflictException("Já existe um reforço para este evento.");
        }

        public async Task<VaccinationEventDto> UpdateAsync(int id, VaccinationEventUpdateDto dto)
        {
            var vaccinationEvent = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Evento de vacinação com id '{id}' não encontrado.");

            var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
            if (SyncTimestampResolver.IsOutdated(editedAt, vaccinationEvent))
                return await GetByIdAsync(id);

            if (dto.ApplicationDate.HasValue)
                vaccinationEvent.ApplicationDate = dto.ApplicationDate.Value;

            if (dto.PredictedDate.HasValue)
                vaccinationEvent.PredictedDate = dto.PredictedDate.Value;

            if (dto.DoseType.HasValue)
                vaccinationEvent.DoseType = dto.DoseType.Value;

            if (dto.Notes != null)
                vaccinationEvent.Notes = dto.Notes;

            vaccinationEvent.UpdatedAt = editedAt;
            await _repository.UpdateAsync(vaccinationEvent);

            return await GetByIdAsync(id);
        }

        public async Task<SyncPageDto<VaccinationEventSyncDto>> GetChangesAsync(string? since, int? limit)
        {
            if (!SyncPaging.TryDecodeCursor(since, out var cursor))
                throw new ValidationException("Cursor de sincronização inválido.");

            var take = SyncPaging.ResolveLimit(limit);
            var fetched = await _repository.GetChangesAsync(cursor, take + 1);
            var animalIds = await _repository.GetAnimalIdsByEventIdsAsync(fetched.Select(e => e.Id).ToList());

            return SyncPaging.BuildPage(fetched, take, cursor, e =>
            {
                var dto = _mapper.Map<VaccinationEventSyncDto>(e);
                dto.AnimalIds = animalIds.TryGetValue(e.Id, out var ids) ? ids : new List<int>();
                return dto;
            });
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var vaccinationEvent = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Evento de vacinação com id '{id}' não encontrado.");

            if (!vaccinationEvent.IsActive)
                return true;

            vaccinationEvent.IsActive = false;
            await _repository.UpdateAsync(vaccinationEvent);
            return true;
        }

        public async Task<IEnumerable<VaccinationHistoryItemDto>> GetAnimalHistoryAsync(int animalId)
        {
            _ = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            var events = (await _repository.GetAppliedByAnimalAsync(animalId)).ToList();

            // Next-dose date per line comes from the booster child, fetched in a single query.
            var childDates = await _repository.GetChildPredictedDatesAsync(
                events.Select(e => e.Id).ToList());

            return events.Select(e =>
            {
                var dto = _mapper.Map<VaccinationHistoryItemDto>(e);
                if (childDates.TryGetValue(e.Id, out var nextDoseDate))
                    dto.NextDoseDate = nextDoseDate;
                return dto;
            }).ToList();
        }

        private static EnumValueDto BuildStatus(VaccinationEvent e, DateTime now)
        {
            var status = VaccinationEventStatusResolver.Resolve(e.ApplicationDate, e.PredictedDate, now);
            return new EnumValueDto { Value = (int)status, Label = status.GetDescription() };
        }

        private static VaccinationEventLineageDto? BuildLineage(VaccinationEvent? e, DateTime now)
        {
            if (e == null)
                return null;

            var status = VaccinationEventStatusResolver.Resolve(e.ApplicationDate, e.PredictedDate, now);
            return new VaccinationEventLineageDto
            {
                Id = e.Id,
                DoseType = new EnumValueDto { Value = (int)e.DoseType, Label = e.DoseType.GetDescription() },
                PredictedDate = e.PredictedDate,
                ApplicationDate = e.ApplicationDate,
                Status = new EnumValueDto { Value = (int)status, Label = status.GetDescription() }
            };
        }

        private async Task EnsureVaccineExistsAsync(int vaccineId)
        {
            _ = await _vaccineRepository.GetVaccineByIdAsync(vaccineId)
                ?? throw new NotFoundException($"Vacina com id '{vaccineId}' não encontrada.");
        }

        private async Task EnsureAnimalsExistAsync(IReadOnlyCollection<int> animalIds)
        {
            var distinctIds = animalIds.Distinct().ToList();
            var existingIds = await _animalRepository.GetExistingAnimalIdsAsync(distinctIds);
            var missing = distinctIds.Except(existingIds).ToList();

            if (missing.Count > 0)
                throw new NotFoundException($"Animais não encontrados: {string.Join(", ", missing)}.");
        }
    }
}
