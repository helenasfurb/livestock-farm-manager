using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

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

        public async Task<IEnumerable<WeightRecordDto>> GetAllWeightRecordsAsync(int animalId)
        {
            await FindAnimalAsync(animalId);

            var records = await _weightRecordRepository.GetAllWeightRecordsAsync(animalId);
            return _mapper.Map<IEnumerable<WeightRecordDto>>(records);
        }

        public async Task<WeightRecordDto> GetWeightRecordByIdAsync(int id, int animalId)
        {
            await FindAnimalAsync(animalId);

            var record = await FindWeightRecordAsync(id, animalId);
            return _mapper.Map<WeightRecordDto>(record);
        }

        public async Task<WeightRecordDto> CreateWeightRecordAsync(WeightRecordCreateDto weightRecordCreateDto, int animalId)
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

        public async Task<bool> DeleteWeightRecordAsync(int id, int animalId)
        {
            await FindAnimalAsync(animalId);
            await FindWeightRecordAsync(id, animalId);

            await _weightRecordRepository.DeleteWeightRecordAsync(id, animalId);
            return true;
        }

        private async Task<Animal> FindAnimalAsync(int animalId)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(animalId);

            if (animal == null)
                throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            return animal;
        }

        private async Task<WeightRecord> FindWeightRecordAsync(int id, int animalId)
        {
            return await _weightRecordRepository.GetWeightRecordByIdAsync(id, animalId)
                ?? throw new NotFoundException($"Pesagem com id '{id}' não encontrada.");
        }

        public async Task<WeightRecordDto> UpdateWeightRecordAsync(int id, int animalId, WeightRecordUpdateDto weightRecordUpdateDto)
        {
            await FindAnimalAsync(animalId);

            var existing = await FindWeightRecordAsync(id, animalId);

            _mapper.Map(weightRecordUpdateDto, existing);
            var updated = await _weightRecordRepository.UpdateWeightRecordAsync(existing);
            return _mapper.Map<WeightRecordDto>(updated);
        }
    }
}
