using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class AnimalService : IAnimalService
    {
        private readonly IAnimalRepository _animalRepository;
        private readonly IAnimalExitRecordRepository _exitRecordRepository;
        private readonly IBreedingEventRepository _breedingEventRepository;
        private readonly IAnimalPregnancyRepository _pregnancyRepository;
        private readonly IAnimalCalvingRepository _calvingRepository;
        private readonly IMapper _mapper;

        public AnimalService(
            IAnimalRepository animalRepository,
            IAnimalExitRecordRepository exitRecordRepository,
            IBreedingEventRepository breedingEventRepository,
            IAnimalPregnancyRepository pregnancyRepository,
            IAnimalCalvingRepository calvingRepository,
            IMapper mapper)
        {
            _animalRepository = animalRepository;
            _exitRecordRepository = exitRecordRepository;
            _breedingEventRepository = breedingEventRepository;
            _pregnancyRepository = pregnancyRepository;
            _calvingRepository = calvingRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<AnimalListItemDto>> GetAllAnimalsAsync(AnimalFilterDto filter)
        {
            var animals = await _animalRepository.GetAllAnimalsAsync(filter);

            var femaleIds = animals
                .Where(a => a.Classification == AnimalClassification.Cow
                    || a.Classification == AnimalClassification.Heifer)
                .Select(a => a.Id)
                .ToList();

            var statusMap = femaleIds.Count > 0
                ? await _animalRepository.GetReproductiveStatusMapAsync(femaleIds)
                : new Dictionary<int, ReproductiveStatus>();

            var items = animals.Select(animal =>
            {
                var dto = _mapper.Map<AnimalListItemDto>(animal);
                if (statusMap.TryGetValue(animal.Id, out var status))
                    dto.ReproductiveStatus = new EnumValueDto { Value = (int)status, Label = status.GetDescription() };
                return dto;
            });

            if (filter.ReproductiveStatus.HasValue)
                items = items.Where(dto => dto.ReproductiveStatus != null
                    && dto.ReproductiveStatus.Value == (int)filter.ReproductiveStatus.Value);

            return items.ToList();
        }

        public async Task<AnimalDto> GetAnimalByIdAsync(int id)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(id)
                ?? throw new NotFoundException($"Animal com id '{id}' não encontrado.");

            var dto = _mapper.Map<AnimalDto>(animal);
            dto.ReproductiveStatus = await DeriveReproductiveStatusAsync(animal);
            return dto;
        }

        public async Task<AnimalDto> CreateAnimalAsync(AnimalCreateDto dto)
        {
            if (await _animalRepository.TagNumberExistsAsync(dto.TagNumber))
                throw new ConflictException($"Já existe um animal com o brinco '{dto.TagNumber}' nesta propriedade.");

            var animal = _mapper.Map<Animal>(dto);
            CreateWeightRecord(dto, animal);
            CreateBodyConditionRecord(dto, animal);

            var created = await _animalRepository.CreateAnimalAsync(animal);
            return _mapper.Map<AnimalDto>(created);
        }

        public async Task<AnimalDto> UpdateAnimalAsync(int id, AnimalUpdateDto dto)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(id)
                ?? throw new NotFoundException($"Animal com id '{id}' não encontrado.");

            if (dto.TagNumber != null && await _animalRepository.TagNumberExistsAsync(dto.TagNumber, excludeAnimalId: id))
                throw new ConflictException($"Já existe um animal com o brinco '{dto.TagNumber}' nesta propriedade.");

            _mapper.Map(dto, animal);
            animal.UpdatedAt = DateTime.UtcNow;

            var updated = await _animalRepository.UpdateAnimalAsync(animal);
            return _mapper.Map<AnimalDto>(updated);
        }

        public async Task<AnimalDto> ExitAnimalAsync(int id, AnimalExitDto dto)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(id)
                ?? throw new NotFoundException($"Animal com id '{id}' não encontrado.");

            if (!animal.IsActive)
                throw new ConflictException("Não é possível registrar saída de um animal já inativo.");

            var exitRecord = new AnimalExitRecord
            {
                AnimalId = id,
                ExitReason = dto.ExitReason,
                ExitDate = dto.ExitDate,
                ExitNotes = dto.ExitNotes,
                CreatedAt = DateTime.UtcNow
            };

            await _exitRecordRepository.CreateAsync(exitRecord);

            animal.IsActive = false;
            animal.UpdatedAt = DateTime.UtcNow;
            animal.ExitRecords = new List<AnimalExitRecord> { exitRecord };

            var updated = await _animalRepository.UpdateAnimalAsync(animal);
            return _mapper.Map<AnimalDto>(updated);
        }

        public async Task<AnimalDto> ReactivateAnimalAsync(int id)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(id)
                ?? throw new NotFoundException($"Animal com id '{id}' não encontrado.");

            if (animal.IsActive)
                throw new ConflictException("Não é possível reativar um animal que já está ativo.");

            animal.IsActive = true;
            animal.UpdatedAt = DateTime.UtcNow;

            var updated = await _animalRepository.UpdateAnimalAsync(animal);
            return _mapper.Map<AnimalDto>(updated);
        }

        public async Task<IEnumerable<AnimalExitRecordDto>> GetExitRecordsAsync(int animalId)
        {
            _ = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            var records = await _exitRecordRepository.GetByAnimalIdAsync(animalId);
            return _mapper.Map<IEnumerable<AnimalExitRecordDto>>(records);
        }

        private async Task<EnumValueDto?> DeriveReproductiveStatusAsync(Animal animal)
        {
            if (animal.Classification != AnimalClassification.Cow &&
                animal.Classification != AnimalClassification.Heifer)
                return null;

            var status = await ResolveReproductiveStatusAsync(animal.Id);
            return new EnumValueDto { Value = (int)status, Label = status.GetDescription() };
        }

        private async Task<ReproductiveStatus> ResolveReproductiveStatusAsync(int animalId)
        {
            var hasConfirmedPregnancy = await _pregnancyRepository.HasActiveConfirmedByAnimalIdAsync(animalId);
            var lastCalving = await _calvingRepository.GetLastActiveByAnimalIdAsync(animalId);
            var lastAwaitingBreedingDate = await _breedingEventRepository.GetLastActiveAwaitingDiagnosisDateAsync(animalId);

            return ReproductiveStatusResolver.Resolve(
                hasConfirmedPregnancy,
                lastCalving?.CalvingDate,
                lastAwaitingBreedingDate,
                DateTime.UtcNow);
        }

        private static void CreateWeightRecord(AnimalCreateDto dto, Animal animal)
        {
            if (!dto.InitialWeight.HasValue) return;

            animal.WeightRecords = new List<WeightRecord>
            {
                new()
                {
                    Weight = dto.InitialWeight.Value,
                    RecordedAt = dto.InitialWeightDate ?? DateTime.UtcNow,
                    Observations = dto.InitialWeightObservations
                }
            };
        }

        private static void CreateBodyConditionRecord(AnimalCreateDto dto, Animal animal)
        {
            if (!dto.InitialBodyConditionScore.HasValue) return;

            animal.BodyConditionRecords = new List<BodyConditionRecord>
            {
                new()
                {
                    Score = dto.InitialBodyConditionScore.Value,
                    RecordedAt = dto.InitialBodyConditionDate ?? DateTime.UtcNow,
                    Notes = dto.InitialBodyConditionNotes
                }
            };
        }
    }
}
