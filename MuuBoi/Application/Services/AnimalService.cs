using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
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

        public async Task<IEnumerable<AnimalDto>> GetAllAnimalsAsync()
        {
            var animals = await _animalRepository.GetAllAnimalsAsync();
            return _mapper.Map<IEnumerable<AnimalDto>>(animals);
        }

        public async Task<AnimalDto?> GetAnimalByIdAsync(int id)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(id);
            return animal == null ? null : _mapper.Map<AnimalDto>(animal);
        }

        public async Task<AnimalDto> CreateAnimalAsync(AnimalCreateDto animalCreateDto)
        {
            var animal = _mapper.Map<Animal>(animalCreateDto);
            animal.IsActive = true;

            CreateWeightRecord(animalCreateDto, animal);

            var created = await _animalRepository.CreateAnimalAsync(animal);
            return _mapper.Map<AnimalDto>(created);
        }

        public async Task<AnimalDto?> UpdateAnimalAsync(int id, AnimalUpdateDto animalUpdateDto)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(id);
            if (animal == null) return null;

            _mapper.Map(animalUpdateDto, animal);
            animal.UpdatedAt = DateTime.UtcNow;

            var updated = await _animalRepository.UpdateAnimalAsync(animal);
            return updated == null ? null : _mapper.Map<AnimalDto>(updated);
        }

        public async Task<AnimalDto?> DeleteAnimalAsync(int id)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(id);
            if (animal == null) return null;

            var deleted = await _animalRepository.DeleteAnimalAsync(id);
            return deleted == null ? null : _mapper.Map<AnimalDto>(deleted);
        }

        private void CreateWeightRecord(AnimalCreateDto dto, Animal animal)
        {
            if (dto.InitialWeight.HasValue)
            {
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
}
