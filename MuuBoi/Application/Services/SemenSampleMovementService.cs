using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class SemenSampleMovementService : ISemenSampleMovementService
    {
        private readonly ISemenSampleMovementRepository _repository;
        private readonly ISemenSampleRepository _semenSampleRepository;
        private readonly IMapper _mapper;

        public SemenSampleMovementService(
            ISemenSampleMovementRepository repository,
            ISemenSampleRepository semenSampleRepository,
            IMapper mapper)
        {
            _repository = repository;
            _semenSampleRepository = semenSampleRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<SemenSampleMovementListItemDto>> GetBySemenSampleIdAsync(int semenSampleId, SemenSampleMovementFilterDto filter)
        {
            _ = await _semenSampleRepository.GetByIdAsync(semenSampleId)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{semenSampleId}' não encontrada.");

            var movements = await _repository.GetBySemenSampleIdAsync(semenSampleId, filter);
            return _mapper.Map<IEnumerable<SemenSampleMovementListItemDto>>(movements);
        }

        public async Task<SemenSampleMovementDto> GetByIdAsync(int semenSampleId, int movementId)
        {
            _ = await _semenSampleRepository.GetByIdAsync(semenSampleId)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{semenSampleId}' não encontrada.");

            var movement = await _repository.GetByIdAsync(movementId)
                ?? throw new NotFoundException($"Movimentação com id '{movementId}' não encontrada.");

            return _mapper.Map<SemenSampleMovementDto>(movement);
        }

        public async Task<SemenSampleMovementDto> CreateAsync(int semenSampleId, SemenSampleMovementCreateDto dto)
        {
            var semenSample = await _semenSampleRepository.GetByIdAsync(semenSampleId)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{semenSampleId}' não encontrada.");

            if (!semenSample.IsActive)
                throw new ConflictException("Não é possível registrar movimentação para uma amostra de sêmen inativa.");

            var movement = _mapper.Map<SemenSampleMovement>(dto);
            movement.SemenSampleId = semenSampleId;

            var created = await _repository.CreateAsync(movement);
            created.SemenSample = await _semenSampleRepository.GetByIdAsync(semenSampleId);

            return _mapper.Map<SemenSampleMovementDto>(created);
        }

        public async Task<SemenSampleMovementDto> UpdateAsync(int semenSampleId, int movementId, SemenSampleMovementUpdateDto dto)
        {
            _ = await _semenSampleRepository.GetByIdAsync(semenSampleId)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{semenSampleId}' não encontrada.");

            var movement = await _repository.GetByIdAsync(movementId)
                ?? throw new NotFoundException($"Movimentação com id '{movementId}' não encontrada.");

            if (movement.BreedingEventId.HasValue)
                throw new ConflictException("Movimentações geradas pelo sistema não podem ser editadas diretamente.");

            if (dto.MovementDate.HasValue)
                movement.MovementDate = dto.MovementDate.Value;

            if (dto.Quantity.HasValue)
                movement.Quantity = dto.Quantity.Value;

            if (dto.Notes != null)
                movement.Notes = dto.Notes;

            movement.UpdatedAt = DateTime.UtcNow;
            var updated = await _repository.UpdateAsync(movement);
            updated.SemenSample = await _semenSampleRepository.GetByIdAsync(semenSampleId);

            return _mapper.Map<SemenSampleMovementDto>(updated);
        }

        public async Task DeactivateAsync(int semenSampleId, int movementId)
        {
            _ = await _semenSampleRepository.GetByIdAsync(semenSampleId)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{semenSampleId}' não encontrada.");

            var movement = await _repository.GetByIdAsync(movementId)
                ?? throw new NotFoundException($"Movimentação com id '{movementId}' não encontrada.");

            if (movement.BreedingEventId.HasValue)
                throw new ConflictException("Movimentações geradas pelo sistema não podem ser inativadas diretamente.");

            if (!movement.IsActive)
                throw new ConflictException("A movimentação já está inativa.");

            movement.IsActive = false;
            movement.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(movement);
        }

        public async Task CreateForSemenSampleAsync(int semenSampleId, int quantity, string? notes)
        {
            var movement = new SemenSampleMovement
            {
                SemenSampleId = semenSampleId,
                MovementType = SemenMovementType.Input,
                MovementDate = DateTime.UtcNow,
                Quantity = quantity,
                Notes = notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.CreateAsync(movement);
        }

        public async Task CreateForBreedingEventAsync(BreedingEvent breedingEvent)
        {
            var movement = new SemenSampleMovement
            {
                SemenSampleId = breedingEvent.SemenSampleId!.Value,
                MovementType = SemenMovementType.Output,
                MovementDate = breedingEvent.BreedingDate,
                Quantity = 1,
                BreedingEventId = breedingEvent.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.CreateAsync(movement);
        }

        public async Task InactivateForBreedingEventAsync(int breedingEventId)
        {
            var movement = await _repository.GetByBreedingEventIdAsync(breedingEventId);
            if (movement == null)
                return;

            movement.IsActive = false;
            movement.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(movement);
        }
    }
}
