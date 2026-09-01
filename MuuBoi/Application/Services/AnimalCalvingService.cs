using AutoMapper;
using MuuBoi.Application.DTOs;
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
        private readonly IBodyConditionRecordService _bodyConditionRecordService;
        private readonly ILactationRepository _lactationRepository;
        private readonly IMapper _mapper;

        public AnimalCalvingService(
            IAnimalCalvingRepository repository,
            IAnimalPregnancyRepository pregnancyRepository,
            IBodyConditionRecordService bodyConditionRecordService,
            ILactationRepository lactationRepository,
            IMapper mapper)
        {
            _repository = repository;
            _pregnancyRepository = pregnancyRepository;
            _bodyConditionRecordService = bodyConditionRecordService;
            _lactationRepository = lactationRepository;
            _mapper = mapper;
        }

        public async Task<AnimalCalvingDto> CreateAsync(int pregnancyId, AnimalCalvingCreateDto dto)
        {
            var pregnancy = await _pregnancyRepository.GetByIdAsync(pregnancyId)
                ?? throw new NotFoundException($"Gestação com id '{pregnancyId}' não encontrada.");

            if (pregnancy.Status != AnimalPregnancyStatus.Confirmed)
                throw new ConflictException("O parto só pode ser registrado para uma gestação confirmada.");

            if (await _repository.HasActiveByPregnancyIdAsync(pregnancy.Id))
                throw new ConflictException("Esta gestação já possui um parto ativo registrado.");

            if (dto.CalvingDate < pregnancy.ConfirmationDate)
                throw new BusinessRuleException("A data do parto não pode ser anterior à data de confirmação da gestação.");

            if (await _lactationRepository.HasOpenByAnimalIdAsync(pregnancy.AnimalId))
                throw new BusinessRuleException("O animal possui uma lactação em aberto. Registre a secagem antes de lançar um novo parto.");

            var calving = new AnimalCalving
            {
                AnimalPregnancyId = pregnancy.Id,
                AnimalId = pregnancy.AnimalId,
                CalvingDate = dto.CalvingDate,
                Notes = dto.Notes,
                PropertyId = pregnancy.PropertyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Calves = dto.Calves.Select(calfDto =>
                {
                    var calf = _mapper.Map<AnimalCalvingCalf>(calfDto);
                    calf.PropertyId = pregnancy.PropertyId;
                    if (calfDto.VitalStatus == CalfVitalStatus.Live)
                        calf.Animal = BuildCalfAnimal(calfDto, dto.CalvingDate, pregnancy.PropertyId);
                    return calf;
                }).ToList()
            };

            var created = await _repository.CreateAsync(calving);

            pregnancy.Status = AnimalPregnancyStatus.Calved;
            pregnancy.UpdatedAt = DateTime.UtcNow;
            await _pregnancyRepository.UpdateAsync(pregnancy);

            await _lactationRepository.CreateAsync(new Lactation
            {
                AnimalId = pregnancy.AnimalId,
                StartDate = dto.CalvingDate,
                EndDate = null,
                CalvingId = created.Id,
                Origin = LactationOrigin.Calving,
                PropertyId = pregnancy.PropertyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            if (dto.BodyConditionScore.HasValue)
                await _bodyConditionRecordService.CreateAsync(pregnancy.AnimalId, new BodyConditionRecordCreateDto
                {
                    Score = dto.BodyConditionScore.Value,
                    RecordedAt = dto.CalvingDate
                });

            return _mapper.Map<AnimalCalvingDto>(created);
        }

        public async Task<AnimalCalvingCalfDto> UpdateCalfAsync(int calvingId, int calfId, AnimalCalvingCalfUpdateDto dto)
        {
            var calf = await _repository.GetCalfByIdAsync(calfId);

            if (calf == null || calf.CalvingId != calvingId)
                throw new NotFoundException($"Cria com id '{calfId}' não encontrada no parto '{calvingId}'.");

            if (!calf.IsActive || calf.Calving == null || !calf.Calving.IsActive)
                throw new ConflictException("Não é possível editar uma cria de um parto inativo.");

            if (dto.Notes != null)
                calf.Notes = dto.Notes;

            if (dto.Sex.HasValue)
            {
                calf.Sex = dto.Sex.Value;
                if (calf.Animal != null)
                    calf.Animal.Gender = dto.Sex.Value;
            }

            if (dto.WeightKg.HasValue)
            {
                calf.WeightKg = dto.WeightKg.Value;
                if (calf.Animal != null)
                    ApplyBirthWeight(calf.Animal, dto.WeightKg.Value, calf.Calving.CalvingDate, calf.Notes);
            }

            calf.UpdatedAt = DateTime.UtcNow;
            if (calf.Animal != null)
                calf.Animal.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateCalfAsync(calf);
            return _mapper.Map<AnimalCalvingCalfDto>(calf);
        }

        public async Task<bool> InactivateAsync(int id)
        {
            var calving = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Parto com id '{id}' não encontrado.");

            if (!calving.IsActive)
                throw new ConflictException("O parto já está inativo.");

            calving.IsActive = false;
            calving.UpdatedAt = DateTime.UtcNow;

            if (calving.Calves != null)
                foreach (var calf in calving.Calves.Where(c => c.IsActive))
                {
                    calf.IsActive = false;
                    calf.UpdatedAt = DateTime.UtcNow;

                    if (calf.Animal != null && calf.Animal.IsActive)
                    {
                        calf.Animal.IsActive = false;
                        calf.Animal.UpdatedAt = DateTime.UtcNow;
                    }
                }

            await _repository.UpdateAsync(calving);

            var lactation = await _lactationRepository.GetByCalvingIdAsync(calving.Id);
            if (lactation != null && lactation.IsActive)
            {
                lactation.IsActive = false;
                lactation.UpdatedAt = DateTime.UtcNow;
                await _lactationRepository.UpdateAsync(lactation);
            }

            if (calving.AnimalPregnancy != null)
            {
                calving.AnimalPregnancy.Status = AnimalPregnancyStatus.Confirmed;
                calving.AnimalPregnancy.UpdatedAt = DateTime.UtcNow;
                await _pregnancyRepository.UpdateAsync(calving.AnimalPregnancy);
            }

            return true;
        }

        private static Animal BuildCalfAnimal(AnimalCalvingCalfCreateDto calfDto, DateTime calvingDate, Guid propertyId)
        {
            var animal = new Animal
            {
                Name = calfDto.Name,
                Gender = calfDto.Sex,
                Breed = calfDto.Breed,
                Classification = AnimalClassification.Calf,
                Origin = AnimalOrigin.BornOnFarm,
                BirthDate = calvingDate,
                TagNumber = null,
                PropertyId = propertyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            if (calfDto.WeightKg.HasValue)
                animal.WeightRecords = new List<WeightRecord>
                {
                    new()
                    {
                        Weight = calfDto.WeightKg.Value,
                        RecordedAt = calvingDate,
                        Observations = calfDto.Notes,
                        PropertyId = propertyId
                    }
                };

            return animal;
        }

        private static void ApplyBirthWeight(Animal animal, decimal weight, DateTime calvingDate, string? observations)
        {
            animal.WeightRecords ??= new List<WeightRecord>();

            var birthRecord = animal.WeightRecords.FirstOrDefault(w => w.RecordedAt == calvingDate);
            if (birthRecord != null)
            {
                birthRecord.Weight = weight;
                birthRecord.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                animal.WeightRecords.Add(new WeightRecord
                {
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
