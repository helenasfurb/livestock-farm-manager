using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class BodyConditionRecordService : IBodyConditionRecordService
    {
        private readonly IBodyConditionRecordRepository _repository;
        private readonly IAnimalRepository _animalRepository;
        private readonly IMapper _mapper;

        public BodyConditionRecordService(
            IBodyConditionRecordRepository repository,
            IAnimalRepository animalRepository,
            IMapper mapper)
        {
            _repository = repository;
            _animalRepository = animalRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<BodyConditionRecordDto>> GetByAnimalIdAsync(int animalId)
        {
            await FindActiveAnimalAsync(animalId, requireActive: false);

            var records = await _repository.GetByAnimalIdAsync(animalId);
            return _mapper.Map<IEnumerable<BodyConditionRecordDto>>(records);
        }

        public async Task<BodyConditionRecordDto> CreateAsync(int animalId, BodyConditionRecordCreateDto dto)
        {
            var animal = await FindActiveAnimalAsync(animalId, requireActive: true);

            if (dto.RecordedAt > DateTime.UtcNow)
                throw new BusinessRuleException("A data da avaliação não pode ser futura.");

            var record = new BodyConditionRecord
            {
                AnimalId = animal.Id,
                Score = dto.Score,
                RecordedAt = dto.RecordedAt,
                Notes = dto.Notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _repository.CreateAsync(record);
            return _mapper.Map<BodyConditionRecordDto>(created);
        }

        public async Task<BodyConditionRecordDto> UpdateAsync(int animalId, int recordId, BodyConditionRecordUpdateDto dto)
        {
            await FindActiveAnimalAsync(animalId, requireActive: false);

            var record = await _repository.GetByIdAsync(recordId, animalId)
                ?? throw new NotFoundException($"Registro de ECC com id '{recordId}' não encontrado.");

            if (dto.RecordedAt.HasValue && dto.RecordedAt.Value > DateTime.UtcNow)
                throw new BusinessRuleException("A data da avaliação não pode ser futura.");

            if (dto.Score.HasValue)
                record.Score = dto.Score.Value;

            if (dto.RecordedAt.HasValue)
                record.RecordedAt = dto.RecordedAt.Value;

            record.UpdatedAt = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(record);
            return _mapper.Map<BodyConditionRecordDto>(updated);
        }

        private async Task<Animal> FindActiveAnimalAsync(int animalId, bool requireActive)
        {
            var animal = await _animalRepository.GetAnimalByIdAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            if (requireActive && !animal.IsActive)
                throw new ConflictException("Não é possível registrar ECC de um animal inativo.");

            return animal;
        }
    }
}
