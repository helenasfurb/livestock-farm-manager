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
    public class BreedingEventService : IBreedingEventService
    {
        private readonly IBreedingEventRepository _repository;
        private readonly IAnimalRepository _animalRepository;
        private readonly ISemenSampleRepository _semenSampleRepository;
        private readonly IAnimalPregnancyService _pregnancyService;
        private readonly IAnimalPregnancyRepository _pregnancyRepository;
        private readonly IMapper _mapper;

        public BreedingEventService(
            IBreedingEventRepository repository,
            IAnimalRepository animalRepository,
            ISemenSampleRepository semenSampleRepository,
            IAnimalPregnancyService pregnancyService,
            IAnimalPregnancyRepository pregnancyRepository,
            IMapper mapper)
        {
            _repository = repository;
            _animalRepository = animalRepository;
            _semenSampleRepository = semenSampleRepository;
            _pregnancyService = pregnancyService;
            _pregnancyRepository = pregnancyRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<BreedingEventListItemDto>> GetByAnimalIdAsync(int animalId)
        {
            _ = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            var events = await _repository.GetByAnimalIdAsync(animalId);
            return _mapper.Map<IEnumerable<BreedingEventListItemDto>>(events);
        }

        public async Task<IEnumerable<BreedingEventListItemDto>> GetAllAsync(BreedingEventFilterDto filter)
        {
            var events = await _repository.GetAllAsync(filter);
            return _mapper.Map<IEnumerable<BreedingEventListItemDto>>(events);
        }

        public async Task<IEnumerable<AnimalAutocompleteItemDto>> GetEligibleAnimalsAsync(string? search)
        {
            var animals = await _animalRepository.GetBreedingEligibleAnimalsAsync(search);
            return _mapper.Map<IEnumerable<AnimalAutocompleteItemDto>>(animals);
        }

        public async Task<BreedingEventDto> GetByIdAsync(int id)
        {
            var ev = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Evento reprodutivo com id '{id}' não encontrado.");
            return _mapper.Map<BreedingEventDto>(ev);
        }

        public async Task<BreedingEventDto> CreateAsync(int animalId, BreedingEventCreateDto dto)
        {
            if (dto.SyncId.HasValue)
            {
                var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return _mapper.Map<BreedingEventDto>(existing);
            }

            var animal = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            if (!animal.IsActive)
                throw new ConflictException("Não é possível registrar coberturas para um animal inativo.");

            if (animal.Classification != AnimalClassification.Cow &&
                animal.Classification != AnimalClassification.Heifer)
                throw new BusinessRuleException("Apenas vacas e novilhas podem ser submetidas a coberturas.");

            var hasActiveEvent = await _repository.HasActiveByAnimalIdAsync(animalId);

            if (hasActiveEvent)
                return await ExistingBySyncIdAsync(dto.SyncId)
                    ?? throw new BusinessRuleException("O animal já possui uma cobertura aguardando diagnóstico. Registre o diagnóstico do serviço anterior antes de criar uma nova.");

            if (await _pregnancyRepository.HasActiveConfirmedByAnimalIdAsync(animalId))
                return await ExistingBySyncIdAsync(dto.SyncId)
                    ?? throw new BusinessRuleException("O animal possui uma gestação ativa. Não é possível registrar uma nova cobertura.");

            if (dto.ReproductionType == ReproductionType.ArtificialInsemination)
            {
                var semen = await _semenSampleRepository.GetByIdAsync(dto.SemenSampleId!.Value)
                    ?? throw new NotFoundException($"Amostra de sêmen com id '{dto.SemenSampleId}' não encontrada.");

                if (!semen.IsActive)
                    throw new ConflictException("A amostra de sêmen selecionada está inativa.");
            }
            else
            {
                var sire = await _animalRepository.GetAnimalByIdAsync(dto.SireAnimalId!.Value)
                    ?? throw new NotFoundException($"Touro com id '{dto.SireAnimalId}' não encontrado.");

                if (!sire.IsActive)
                    throw new ConflictException("O touro selecionado está inativo.");

                if (sire.Classification != AnimalClassification.Bull)
                    throw new BusinessRuleException("O animal informado como touro pai não possui classificação 'Touro'.");
            }

            var ev = _mapper.Map<BreedingEvent>(dto);
            ev.SyncId = dto.SyncId ?? Guid.NewGuid();
            ev.AnimalId = animalId;
            ev.ServiceNumber = await _repository.CountActiveByAnimalIdAsync(animalId) + 1;

            var semenOutput = ev.ReproductionType == ReproductionType.ArtificialInsemination
                ? new SemenSampleMovement
                {
                    SyncId = Guid.NewGuid(),
                    SemenSampleId = ev.SemenSampleId!.Value,
                    MovementType = SemenMovementType.Output,
                    MovementDate = ev.BreedingDate,
                    Quantity = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
                : null;

            var created = await _repository.CreateAsync(ev, semenOutput);

            created.Animal = animal;
            if (created.SemenSampleId.HasValue)
                created.SemenSample = await _semenSampleRepository.GetByIdAsync(created.SemenSampleId.Value);
            if (created.SireAnimalId.HasValue)
                created.SireAnimal = await _animalRepository.GetAnimalByIdAsync(created.SireAnimalId.Value);

            return _mapper.Map<BreedingEventDto>(created);
        }

        private async Task<BreedingEventDto?> ExistingBySyncIdAsync(Guid? syncId)
        {
            if (!syncId.HasValue)
                return null;

            var existing = await _repository.GetBySyncIdAsync(syncId.Value);
            return existing == null ? null : _mapper.Map<BreedingEventDto>(existing);
        }

        public async Task<BreedingEventDto> UpdateAsync(int id, BreedingEventUpdateDto dto)
        {
            var ev = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Cobertura com id '{id}' não encontrada.");

            if (ev.Status != ReproductiveEventStatus.AwaitingDiagnosis)
                throw new ConflictException("Apenas coberturas com diagnóstico pendente podem ser editadas.");

            var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
            if (SyncTimestampResolver.IsOutdated(editedAt, ev))
                return _mapper.Map<BreedingEventDto>(ev);

            if (dto.BreedingDate.HasValue)
                ev.BreedingDate = dto.BreedingDate.Value;

            if (dto.Notes != null)
                ev.Notes = dto.Notes;

            if (ev.ReproductionType == ReproductionType.ArtificialInsemination && dto.SemenSampleId.HasValue)
            {
                var semen = await _semenSampleRepository.GetByIdAsync(dto.SemenSampleId.Value)
                    ?? throw new NotFoundException($"Amostra de sêmen com id '{dto.SemenSampleId}' não encontrada.");

                if (!semen.IsActive)
                    throw new ConflictException("A amostra de sêmen selecionada está inativa.");

                ev.SemenSampleId = dto.SemenSampleId.Value;
            }

            if (ev.ReproductionType == ReproductionType.NaturalMating && dto.SireAnimalId.HasValue)
            {
                var sire = await _animalRepository.GetAnimalByIdAsync(dto.SireAnimalId.Value)
                    ?? throw new NotFoundException($"Touro com id '{dto.SireAnimalId}' não encontrado.");

                if (!sire.IsActive)
                    throw new ConflictException("O touro selecionado está inativo.");

                if (sire.Classification != AnimalClassification.Bull)
                    throw new BusinessRuleException("O animal informado como touro pai não possui classificação 'Touro'.");

                ev.SireAnimalId = dto.SireAnimalId.Value;
            }

            if (ev.ReproductionType == ReproductionType.ArtificialInsemination)
            {
                var semenOutput = await _repository.GetSemenOutputAsync(ev.Id);
                if (semenOutput != null)
                {
                    semenOutput.SemenSampleId = ev.SemenSampleId!.Value;
                    semenOutput.MovementDate = ev.BreedingDate;
                }
            }

            ev.UpdatedAt = editedAt;
            var updated = await _repository.UpdateAsync(ev);

            updated.Animal = await _animalRepository.GetAnimalByIdAsync(ev.AnimalId);
            if (updated.SemenSampleId.HasValue)
                updated.SemenSample = await _semenSampleRepository.GetByIdAsync(updated.SemenSampleId.Value);
            if (updated.SireAnimalId.HasValue)
                updated.SireAnimal = await _animalRepository.GetAnimalByIdAsync(updated.SireAnimalId.Value);

            return _mapper.Map<BreedingEventDto>(updated);
        }

        public async Task<BreedingEventDto> UpdateStatusAsync(int id, BreedingEventStatusUpdateDto dto)
        {
            var ev = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Cobertura com id '{id}' não encontrada.");

            var status = dto.Status!.Value;
            var diagnosisDate = dto.DiagnosisDate!.Value;

            if (ev.Status != ReproductiveEventStatus.AwaitingDiagnosis)
                return SameDiagnosisOrConflict(ev, status);

            if (diagnosisDate < ev.BreedingDate)
                throw new BusinessRuleException("A data do diagnóstico não pode ser anterior à data da cobertura.");

            ev.Status = status;
            ev.DiagnosisDate = diagnosisDate;
            ev.DiagnosisMethod = dto.DiagnosisMethod;

            var pregnancy = status == ReproductiveEventStatus.Successful
                ? _pregnancyService.BuildForBreedingEvent(ev, diagnosisDate, dto.GestationalAge, dto.PregnancySyncId ?? Guid.NewGuid())
                : null;

            var updated = await _repository.SaveDiagnosisAsync(ev, pregnancy);
            if (updated == null)
            {
                var current = await _repository.GetByIdAsync(id)
                    ?? throw new NotFoundException($"Cobertura com id '{id}' não encontrada.");
                return SameDiagnosisOrConflict(current, status);
            }

            return _mapper.Map<BreedingEventDto>(updated);
        }

        private BreedingEventDto SameDiagnosisOrConflict(BreedingEvent ev, ReproductiveEventStatus status)
        {
            if (ev.Status != status)
                throw new ConflictException("O diagnóstico desta cobertura já foi registrado.");

            return _mapper.Map<BreedingEventDto>(ev);
        }

        public async Task<SyncPageDto<BreedingEventSyncDto>> GetChangesAsync(string? since, int? limit)
        {
            if (!SyncPaging.TryDecodeCursor(since, out var cursor))
                throw new ValidationException("Cursor de sincronização inválido.");

            var take = SyncPaging.ResolveLimit(limit);
            var fetched = await _repository.GetChangesAsync(cursor, take + 1);
            return SyncPaging.BuildPage(fetched, take, cursor, e => _mapper.Map<BreedingEventSyncDto>(e));
        }

        public async Task DeactivateAsync(int id)
        {
            var ev = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Cobertura com id '{id}' não encontrada.");

            if (!ev.IsActive)
                return;

            if (await _pregnancyRepository.ExistsActiveForBreedingEventAsync(ev.Id))
                throw new ConflictException("Esta cobertura possui uma gestação ativa vinculada. Inative a gestação primeiro.");

            ev.IsActive = false;

            if (ev.ReproductionType == ReproductionType.ArtificialInsemination)
            {
                var semenOutput = await _repository.GetSemenOutputAsync(ev.Id);
                if (semenOutput != null)
                    semenOutput.IsActive = false;
            }

            await _repository.UpdateAsync(ev);
        }
    }
}
