using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;
using MuuBoi.Domain.Enums;

namespace MuuBoi.Application.Services
{
    public class AnimalService : IAnimalService
    {
        private readonly IAnimalRepository _animalRepository;
        private readonly IAnimalExitRecordRepository _exitRecordRepository;
        private readonly IMapper _mapper;

        public AnimalService(IAnimalRepository animalRepository, IAnimalExitRecordRepository exitRecordRepository, IMapper mapper)
        {
            _animalRepository = animalRepository;
            _exitRecordRepository = exitRecordRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<AnimalListItemDto>> GetAllAnimalsAsync(AnimalFilterDto filter)
        {
            var animals = await _animalRepository.GetAllAnimalsAsync(filter);
            return _mapper.Map<IEnumerable<AnimalListItemDto>>(animals);
        }

        public async Task<AnimalDto> GetAnimalByIdAsync(int id)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(id)
                ?? throw new NotFoundException($"Animal com id '{id}' não encontrado.");
            return _mapper.Map<AnimalDto>(animal);
        }

        public async Task<AnimalDto> CreateAnimalAsync(AnimalCreateDto dto)
        {
            if (await _animalRepository.TagNumberExistsAsync(dto.TagNumber))
                throw new ConflictException($"Já existe um animal com o brinco '{dto.TagNumber}' nesta propriedade.");

            var animal = _mapper.Map<Animal>(dto);
            CreateWeightRecord(dto, animal);

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
    }
}
