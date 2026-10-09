using System.ComponentModel.DataAnnotations;
using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class SemenSampleService : ISemenSampleService
    {
        private readonly ISemenSampleRepository _repository;
        private readonly ISemenSampleMovementRepository _movementRepository;
        private readonly IMapper _mapper;

        public SemenSampleService(
            ISemenSampleRepository repository,
            ISemenSampleMovementRepository movementRepository,
            IMapper mapper)
        {
            _repository = repository;
            _movementRepository = movementRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<SemenSampleListItemDto>> GetAllAsync(SemenSampleFilterDto filter)
        {
            var samples = await _repository.GetAllAsync(filter);
            var sampleList = samples.ToList();

            var dtos = _mapper.Map<List<SemenSampleListItemDto>>(sampleList);

            var doses = await _repository.GetAvailableDosesBatchAsync(sampleList.Select(s => s.Id));
            foreach (var dto in dtos)
                dto.AvailableDoses = doses.GetValueOrDefault(dto.Id, 0);

            return dtos;
        }

        public async Task<IEnumerable<SemenSampleAutocompleteItemDto>> GetAutocompleteAsync(string? name)
        {
            var samples = await _repository.GetAutocompleteAsync(name);
            return _mapper.Map<IEnumerable<SemenSampleAutocompleteItemDto>>(samples);
        }

        public async Task<SemenSampleDto> GetByIdAsync(int id)
        {
            var sample = await FindAsync(id);
            return await ComposeDetailAsync(sample);
        }

        public async Task<SemenSampleCreatedDto> CreateAsync(SemenSampleCreateDto dto)
        {
            if (dto.SyncId.HasValue)
            {
                var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return await ComposeCreatedAsync(existing, dto.InitialMovementSyncId);
            }

            var sample = _mapper.Map<SemenSample>(dto);
            sample.SyncId = dto.SyncId ?? Guid.NewGuid();

            if (dto.InitialQuantity.HasValue)
                sample.Movements = new List<SemenSampleMovement>
                {
                    new()
                    {
                        SyncId = dto.InitialMovementSyncId ?? Guid.NewGuid(),
                        MovementType = SemenMovementType.Input,
                        MovementDate = DateTime.UtcNow,
                        Quantity = dto.InitialQuantity.Value,
                        Notes = dto.InitialNotes,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    }
                };

            var created = await _repository.CreateAsync(sample);
            return await ComposeCreatedAsync(created, sample.Movements?.FirstOrDefault()?.SyncId);
        }

        public async Task<SemenSampleDto> UpdateAsync(int id, SemenSampleUpdateDto dto)
        {
            var sample = await FindAsync(id);

            var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
            if (SyncTimestampResolver.IsOutdated(editedAt, sample))
                return await ComposeDetailAsync(sample);

            _mapper.Map(dto, sample);
            sample.UpdatedAt = editedAt;
            var updated = await _repository.UpdateAsync(sample);
            return await ComposeDetailAsync(updated);
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var sample = await FindAsync(id);
            if (!sample.IsActive)
                return false;

            sample.IsActive = false;
            sample.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(sample);
            return false;
        }

        public async Task<bool> ReactivateAsync(int id)
        {
            var sample = await FindAsync(id);
            if (sample.IsActive)
                return true;

            sample.IsActive = true;
            sample.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(sample);
            return true;
        }

        public async Task<SyncPageDto<SemenSampleDto>> GetChangesAsync(string? since, int? limit)
        {
            if (!SyncPaging.TryDecodeCursor(since, out var cursor))
                throw new ValidationException("Cursor de sincronização inválido.");

            var take = SyncPaging.ResolveLimit(limit);
            var fetched = await _repository.GetChangesAsync(cursor, take + 1);

            var page = SyncPaging.BuildPage(fetched, take, cursor, s => _mapper.Map<SemenSampleDto>(s));

            var doses = await _repository.GetAvailableDosesBatchAsync(page.Items.Select(i => i.Id));
            foreach (var item in page.Items)
                item.AvailableDoses = doses.GetValueOrDefault(item.Id, 0);

            return page;
        }

        private async Task<SemenSample> FindAsync(int id)
        {
            return await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Amostra de sêmen com id '{id}' não encontrada.");
        }

        private async Task<SemenSampleCreatedDto> ComposeCreatedAsync(SemenSample sample, Guid? initialMovementSyncId)
        {
            var dto = _mapper.Map<SemenSampleCreatedDto>(sample);
            dto.AvailableDoses = await _repository.GetAvailableDosesAsync(sample.Id);

            if (initialMovementSyncId.HasValue)
            {
                var movement = await _movementRepository.GetBySyncIdAsync(initialMovementSyncId.Value);
                if (movement != null && movement.SemenSampleId == sample.Id)
                    dto.InitialMovement = _mapper.Map<SemenSampleMovementRefDto>(movement);
            }

            return dto;
        }

        private async Task<SemenSampleDto> ComposeDetailAsync(SemenSample sample)
        {
            var dto = _mapper.Map<SemenSampleDto>(sample);
            dto.AvailableDoses = await _repository.GetAvailableDosesAsync(sample.Id);
            return dto;
        }
    }
}
