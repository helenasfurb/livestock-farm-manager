using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.DTOs;
using MuuBoi.Interfaces;
using MuuBoi.Models;

namespace MuuBoi.Application.Services
{
    public class WeightRecordService : IWeightRecordService
    {
        private readonly IWeightRecordRepository _weightRecordRepository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IMapper _mapper;

        public WeightRecordService(IWeightRecordRepository weightRecordRepository, IAnimalRepository animalRepository, IMapper mapper)
        {
            _weightRecordRepository = weightRecordRepository;
            _animalRepository = animalRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<WeightRecordDto>> GetAllWeightRecordsAsync(string animalId)
        {
            await FindAnimalAsync(animalId);

            var records = await _weightRecordRepository.GetAllWeightRecordsAsync(animalId);
            return _mapper.Map<IEnumerable<WeightRecordDto>>(records);
        }

        public async Task<WeightRecordDto?> GetWeightRecordByIdAsync(int id, string animalId)
        {
            await FindAnimalAsync(animalId);

            var record = await _weightRecordRepository.GetWeightRecordByIdAsync(id, animalId);
            return record == null ? null : _mapper.Map<WeightRecordDto>(record);
        }

        public async Task<WeightRecordDto> CreateWeightRecordAsync(WeightRecordCreateDto weightRecordCreateDto, string animalId)
        {
            var animal = await FindAnimalAsync(animalId);

            var weightRecord = new WeightRecord
            {
                AnimalId = animal.Id,
                Weight = weightRecordCreateDto.Weight!.Value,
                RecordedAt = weightRecordCreateDto.WeightDate ?? DateTime.UtcNow,
                Observations = weightRecordCreateDto.WeightObservations
            };

            var created = await _weightRecordRepository.CreateWeightRecordAsync(weightRecord);
            return _mapper.Map<WeightRecordDto>(created);
        }

        public async Task<WeightRecordDto?> DeleteWeightRecordAsync(int id, string animalId)
        {
            await FindAnimalAsync(animalId);

            var deleted = await _weightRecordRepository.DeleteWeightRecordAsync(id, animalId);
            return deleted == null ? null : _mapper.Map<WeightRecordDto>(deleted);
        }

        private async Task<Animal> FindAnimalAsync(string animalId)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(int.Parse(animalId));

            if (animal == null)
                throw new NotFoundException("Animal not found");

            return animal;
        }

        public async Task<WeightRecordDto?> UpdateWeightRecordAsync(int id, string animalId, WeightRecordUpdateDto weightRecordUpdateDto)
        {
            await FindAnimalAsync(animalId);

            var existing = await _weightRecordRepository.GetWeightRecordByIdAsync(id, animalId);
            if (existing == null) return null;

            _mapper.Map(weightRecordUpdateDto, existing);
            var updated = await _weightRecordRepository.UpdateWeightRecordAsync(existing);
            return _mapper.Map<WeightRecordDto>(updated);
        }
    }
}
