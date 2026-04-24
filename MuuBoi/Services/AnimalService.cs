using AutoMapper;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;
using MuuBoi.Models;

namespace MuuBoi.Services
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

        public async Task<IEnumerable<AnimalDto>> GetAllAnimalsAsync(string userId)
        {
            var animals = await _animalRepository.GetAllAnimalsAsync();
            var userAnimals = animals.Where(a => a.UserId == userId);
            return _mapper.Map<IEnumerable<AnimalDto>>(userAnimals);
        }

        public async Task<AnimalDto?> GetAnimalByIdAsync(int id, string userId)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(id);
            if (animal == null || animal.UserId != userId)
                return null;

            return _mapper.Map<AnimalDto>(animal);
        }

        public async Task<AnimalDto> CreateAnimalAsync(AnimalCreateDto animalCreateDto, string userId)
        {
            var animal = _mapper.Map<Animal>(animalCreateDto);
            animal.UserId = userId;
            animal.IsActive = true;

            CreateWeightRecord(animalCreateDto, animal);

            var created = await _animalRepository.CreateAnimalAsync(animal);
            return _mapper.Map<AnimalDto>(created);
        }

        public async Task<AnimalDto?> UpdateAnimalAsync(int id, AnimalUpdateDto animalUpdateDto, string userId)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(id);
            if (animal == null || animal.UserId != userId)
                return null;

            _mapper.Map(animalUpdateDto, animal);
            animal.UpdatedAt = DateTime.UtcNow;

            var updated = await _animalRepository.UpdateAnimalAsync(animal);
            return updated == null ? null : _mapper.Map<AnimalDto>(updated);
        }

        public async Task<AnimalDto?> DeleteAnimalAsync(int id, string userId)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(id);
            if (animal == null || animal.UserId != userId)
                return null;

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
