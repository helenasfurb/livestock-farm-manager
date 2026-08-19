using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class AnimalService : IAnimalService
    {
        private readonly IAnimalRepository _animalRepository;
        private readonly IMapper _mapper;

        public AnimalService(IAnimalRepository animalRepository, IMapper mapper)
        {
            _animalRepository = animalRepository;
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
