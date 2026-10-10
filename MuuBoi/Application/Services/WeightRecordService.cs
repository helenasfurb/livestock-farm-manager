using System.ComponentModel.DataAnnotations;
using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
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
            if (weightRecordCreateDto.SyncId.HasValue)
            {
                var existing = await _weightRecordRepository.GetWeightRecordBySyncIdAsync(weightRecordCreateDto.SyncId.Value);
                if (existing != null)
                    return _mapper.Map<WeightRecordDto>(existing);
            }

            var animal = await FindAnimalAsync(animalId);

            var weightRecord = new WeightRecord
            {
                SyncId = weightRecordCreateDto.SyncId ?? Guid.NewGuid(),
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
            var record = await FindWeightRecordAsync(id, animalId);
            if (!record.IsActive)
                return true;

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

            var editedAt = SyncTimestampResolver.ResolveEditedAt(weightRecordUpdateDto.UpdatedAt, DateTime.UtcNow);
            if (SyncTimestampResolver.IsOutdated(editedAt, existing))
                return _mapper.Map<WeightRecordDto>(existing);

            _mapper.Map(weightRecordUpdateDto, existing);
            existing.UpdatedAt = editedAt;
            var updated = await _weightRecordRepository.UpdateWeightRecordAsync(existing);
            return _mapper.Map<WeightRecordDto>(updated);
        }

        public async Task<SyncPageDto<WeightRecordDto>> GetChangesAsync(string? since, int? limit)
        {
            if (!SyncPaging.TryDecodeCursor(since, out var cursor))
                throw new ValidationException("Cursor de sincronização inválido.");

            var take = SyncPaging.ResolveLimit(limit);
            var fetched = await _weightRecordRepository.GetChangesAsync(cursor, take + 1);

            return SyncPaging.BuildPage(fetched, take, cursor, w => _mapper.Map<WeightRecordDto>(w));
        }
    }
}
