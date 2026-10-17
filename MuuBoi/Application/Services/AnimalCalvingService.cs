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
    public class AnimalCalvingService : IAnimalCalvingService
    {
        private readonly IAnimalCalvingRepository _repository;
        private readonly IAnimalPregnancyRepository _pregnancyRepository;
        private readonly IBodyConditionRecordRepository _bodyConditionRecordRepository;
        private readonly ILactationRepository _lactationRepository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IMapper _mapper;

        public AnimalCalvingService(
            IAnimalCalvingRepository repository,
            IAnimalPregnancyRepository pregnancyRepository,
            IBodyConditionRecordRepository bodyConditionRecordRepository,
            ILactationRepository lactationRepository,
            IAnimalRepository animalRepository,
            IMapper mapper)
        {
            _repository = repository;
            _pregnancyRepository = pregnancyRepository;
            _bodyConditionRecordRepository = bodyConditionRecordRepository;
            _lactationRepository = lactationRepository;
            _animalRepository = animalRepository;
            _mapper = mapper;
        }

        public async Task<AnimalCalvingCreatedDto> CreateAsync(int pregnancyId, AnimalCalvingCreateDto dto)
        {
            if (dto.SyncId.HasValue)
            {
                var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return await ComposeCreatedAsync(existing, dto.BodyConditionSyncId);
            }

            var pregnancy = await _pregnancyRepository.GetByIdAsync(pregnancyId)
                ?? throw new NotFoundException($"Gestação com id '{pregnancyId}' não encontrada.");

            if (pregnancy.Status != AnimalPregnancyStatus.Confirmed)
                return await ExistingBySyncIdAsync(dto)
                    ?? throw new ConflictException("O parto só pode ser registrado para uma gestação confirmada.");

            if (await _repository.HasActiveByPregnancyIdAsync(pregnancy.Id))
                return await ExistingBySyncIdAsync(dto)
                    ?? throw new ConflictException("Esta gestação já possui um parto ativo registrado.");

            var calvingDate = dto.CalvingDate!.Value;

            if (calvingDate < pregnancy.ConfirmationDate)
                throw new BusinessRuleException("A data do parto não pode ser anterior à data de confirmação da gestação.");

            if (await _lactationRepository.HasOpenByAnimalIdAsync(pregnancy.AnimalId))
                return await ExistingBySyncIdAsync(dto)
                    ?? throw new BusinessRuleException("O animal possui uma lactação em aberto. Registre a secagem antes de lançar um novo parto.");

            var tagConflict = await FindCalfTagConflictAsync(dto.Calves);
            if (tagConflict != null)
                return await ExistingBySyncIdAsync(dto)
                    ?? throw new ConflictException(tagConflict);

            if (dto.BodyConditionScore.HasValue && pregnancy.Animal is { IsActive: false })
                throw new ConflictException("Não é possível registrar ECC de um animal inativo.");

            var calving = new AnimalCalving
            {
                SyncId = dto.SyncId ?? Guid.NewGuid(),
                AnimalPregnancyId = pregnancy.Id,
                AnimalId = pregnancy.AnimalId,
                CalvingDate = calvingDate,
                Notes = dto.Notes,
                PropertyId = pregnancy.PropertyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Calves = dto.Calves.Select(calfDto =>
                {
                    var calf = _mapper.Map<AnimalCalvingCalf>(calfDto);
                    calf.SyncId = calfDto.SyncId ?? Guid.NewGuid();
                    calf.PropertyId = pregnancy.PropertyId;
                    if (calfDto.VitalStatus == CalfVitalStatus.Live)
                        calf.Animal = BuildCalfAnimal(calfDto, calvingDate, pregnancy.PropertyId);
                    return calf;
                }).ToList()
            };

            pregnancy.Status = AnimalPregnancyStatus.Calved;

            var lactation = new Lactation
            {
                SyncId = dto.LactationSyncId ?? Guid.NewGuid(),
                AnimalId = pregnancy.AnimalId,
                StartDate = calvingDate,
                EndDate = null,
                Origin = LactationOrigin.Calving,
                PropertyId = pregnancy.PropertyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var bodyCondition = dto.BodyConditionScore.HasValue
                ? new BodyConditionRecord
                {
                    SyncId = dto.BodyConditionSyncId ?? Guid.NewGuid(),
                    AnimalId = pregnancy.AnimalId,
                    Score = dto.BodyConditionScore.Value,
                    RecordedAt = calvingDate,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
                : null;

            var created = await _repository.CreateAsync(calving, lactation, bodyCondition);
            if (created == null)
                return await ExistingBySyncIdAsync(dto)
                    ?? throw new ConflictException("Esta gestação já possui um parto ativo registrado.");

            return await ComposeCreatedAsync(created, bodyCondition?.SyncId);
        }

        private async Task<AnimalCalvingCreatedDto?> ExistingBySyncIdAsync(AnimalCalvingCreateDto dto)
        {
            if (!dto.SyncId.HasValue)
                return null;

            var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
            return existing == null ? null : await ComposeCreatedAsync(existing, dto.BodyConditionSyncId);
        }

        private async Task<AnimalCalvingCreatedDto> ComposeCreatedAsync(AnimalCalving calving, Guid? bodyConditionSyncId)
        {
            var dto = _mapper.Map<AnimalCalvingCreatedDto>(calving);

            var lactation = await _lactationRepository.GetByCalvingIdAsync(calving.Id);
            if (lactation != null)
                dto.Lactation = new LactationRefDto { Id = lactation.Id, SyncId = lactation.SyncId };

            if (bodyConditionSyncId.HasValue)
            {
                var bodyCondition = await _bodyConditionRecordRepository.GetBySyncIdAsync(bodyConditionSyncId.Value);
                if (bodyCondition != null && bodyCondition.AnimalId == calving.AnimalId)
                    dto.BodyCondition = new SyncRefDto { Id = bodyCondition.Id, SyncId = bodyCondition.SyncId };
            }

            return dto;
        }

        public async Task<AnimalCalvingCalfDto> UpdateCalfAsync(int calvingId, int calfId, AnimalCalvingCalfUpdateDto dto)
        {
            var calf = await _repository.GetCalfByIdAsync(calfId);

            if (calf == null || calf.CalvingId != calvingId)
                throw new NotFoundException($"Cria com id '{calfId}' não encontrada no parto '{calvingId}'.");

            if (!calf.IsActive || calf.Calving == null || !calf.Calving.IsActive)
                throw new ConflictException("Não é possível editar uma cria de um parto inativo.");

            var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
            if (SyncTimestampResolver.IsOutdated(editedAt, calf))
                return _mapper.Map<AnimalCalvingCalfDto>(calf);

            if (dto.Notes != null)
                calf.Notes = dto.Notes;

            if (dto.Sex.HasValue)
            {
                calf.Sex = dto.Sex.Value;
                if (calf.Animal != null && !SyncTimestampResolver.IsOutdated(editedAt, calf.Animal))
                {
                    calf.Animal.Gender = dto.Sex.Value;
                    calf.Animal.UpdatedAt = editedAt;
                }
            }

            if (dto.WeightKg.HasValue)
            {
                calf.WeightKg = dto.WeightKg.Value;
                if (calf.Animal != null)
                    ApplyBirthWeight(calf.Animal, dto.WeightKg.Value, calf.Calving.CalvingDate, calf.Notes, editedAt, dto.WeightSyncId);
            }

            calf.UpdatedAt = editedAt;

            await _repository.UpdateCalfAsync(calf);
            return _mapper.Map<AnimalCalvingCalfDto>(calf);
        }

        public async Task<SyncPageDto<AnimalCalvingSyncDto>> GetChangesAsync(string? since, int? limit)
        {
            if (!SyncPaging.TryDecodeCursor(since, out var cursor))
                throw new ValidationException("Cursor de sincronização inválido.");

            var take = SyncPaging.ResolveLimit(limit);
            var fetched = await _repository.GetChangesAsync(cursor, take + 1);
            var calves = (await _repository.GetCalvesByCalvingIdsAsync(fetched.Select(c => c.Id).ToList()))
                .ToLookup(cf => cf.CalvingId);

            return SyncPaging.BuildPage(fetched, take, cursor, c =>
            {
                var dto = _mapper.Map<AnimalCalvingSyncDto>(c);
                dto.Calves = _mapper.Map<List<AnimalCalvingCalfSyncDto>>(calves[c.Id]);
                return dto;
            });
        }

        public async Task<bool> InactivateAsync(int id)
        {
            var calving = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Parto com id '{id}' não encontrado.");

            if (!calving.IsActive)
                return true;

            calving.IsActive = false;

            if (calving.Calves != null)
                foreach (var calf in calving.Calves.Where(c => c.IsActive))
                {
                    calf.IsActive = false;

                    if (calf.Animal != null && calf.Animal.IsActive)
                        calf.Animal.IsActive = false;
                }

            var lactation = await _lactationRepository.GetByCalvingIdAsync(calving.Id);
            if (lactation != null && lactation.IsActive)
                lactation.IsActive = false;

            if (calving.AnimalPregnancy != null)
                calving.AnimalPregnancy.Status = AnimalPregnancyStatus.Confirmed;

            await _repository.UpdateAsync(calving);

            return true;
        }

        // RN-06: brinco de cria viva deve ser único na base e entre as crias do próprio parto.
        private async Task<string?> FindCalfTagConflictAsync(IEnumerable<AnimalCalvingCalfCreateDto> calves)
        {
            var tags = calves
                .Where(c => c.VitalStatus == CalfVitalStatus.Live && !string.IsNullOrWhiteSpace(c.TagNumber))
                .Select(c => c.TagNumber!.Trim())
                .ToList();

            var duplicateInBatch = tags
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicateInBatch != null)
                return $"O brinco '{duplicateInBatch.Key}' está repetido entre as crias deste parto.";

            foreach (var tag in tags)
                if (await _animalRepository.TagNumberExistsAsync(tag))
                    return $"Já existe um animal com o brinco '{tag}'.";

            return null;
        }

        private static Animal BuildCalfAnimal(AnimalCalvingCalfCreateDto calfDto, DateTime calvingDate, Guid propertyId)
        {
            var animal = new Animal
            {
                SyncId = calfDto.AnimalSyncId ?? Guid.NewGuid(),
                Name = calfDto.Name,
                Gender = calfDto.Sex,
                Breed = calfDto.Breed,
                Classification = AnimalClassification.Calf,
                Origin = AnimalOrigin.BornOnFarm,
                BirthDate = calvingDate,
                TagNumber = string.IsNullOrWhiteSpace(calfDto.TagNumber) ? null : calfDto.TagNumber,
                PropertyTagNumber = string.IsNullOrWhiteSpace(calfDto.PropertyTagNumber) ? null : calfDto.PropertyTagNumber,
                PropertyId = propertyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            if (calfDto.WeightKg.HasValue)
                animal.WeightRecords = new List<WeightRecord>
                {
                    new()
                    {
                        SyncId = calfDto.WeightSyncId ?? Guid.NewGuid(),
                        Weight = calfDto.WeightKg.Value,
                        RecordedAt = calvingDate,
                        Observations = calfDto.Notes,
                        PropertyId = propertyId
                    }
                };

            return animal;
        }

        private static void ApplyBirthWeight(Animal animal, decimal weight, DateTime calvingDate, string? observations, DateTime editedAt, Guid? weightSyncId)
        {
            animal.WeightRecords ??= new List<WeightRecord>();

            var birthRecord = animal.WeightRecords.FirstOrDefault(w => w.RecordedAt == calvingDate);
            if (birthRecord != null)
            {
                if (SyncTimestampResolver.IsOutdated(editedAt, birthRecord))
                    return;

                birthRecord.Weight = weight;
                birthRecord.UpdatedAt = editedAt;
            }
            else
            {
                animal.WeightRecords.Add(new WeightRecord
                {
                    SyncId = weightSyncId ?? Guid.NewGuid(),
                    Weight = weight,
                    RecordedAt = calvingDate,
                    Observations = observations,
                    AnimalId = animal.Id,
                    PropertyId = animal.PropertyId
                });
            }
        }
    }
}
