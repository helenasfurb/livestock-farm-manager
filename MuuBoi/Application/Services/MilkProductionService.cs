using System.ComponentModel.DataAnnotations;
using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class MilkProductionService : IMilkProductionService
    {
        private readonly IMilkProductionRepository _repository;
        private readonly IMapper _mapper;

        public MilkProductionService(IMilkProductionRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<MilkProductionDayDto>> GetAllAsync(MilkProductionFilterDto filter)
        {
            var productions = await _repository.GetAllAsync(filter);

            return productions
                .GroupBy(p => p.Date.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new MilkProductionDayDto
                {
                    Date = g.Key,
                    TotalVolume = g.Sum(p => p.Volume),
                    RecordCount = g.Count()
                })
                .ToList();
        }

        public async Task<IEnumerable<MilkProductionListItemDto>> GetByDateAsync(DateTime date)
        {
            var filter = new MilkProductionFilterDto
            {
                DateFrom = date.Date,
                DateTo = date.Date.AddDays(1).AddTicks(-1)
            };

            var productions = (await _repository.GetAllAsync(filter))
                .OrderBy(p => p.Milking)
                .ThenBy(p => p.CreatedAt)
                .ToList();

            return _mapper.Map<List<MilkProductionListItemDto>>(productions);
        }

        public async Task<MilkProductionDto> GetByIdAsync(int id)
        {
            var production = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Lançamento de produção de leite com id '{id}' não encontrado.");
            return _mapper.Map<MilkProductionDto>(production);
        }

        public async Task<MilkProductionDto> CreateAsync(MilkProductionCreateDto dto)
        {
            if (dto.SyncId.HasValue)
            {
                var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return _mapper.Map<MilkProductionDto>(existing);
            }

            var production = _mapper.Map<MilkProduction>(dto);
            production.SyncId = dto.SyncId ?? Guid.NewGuid();
            var created = await _repository.CreateAsync(production);
            return _mapper.Map<MilkProductionDto>(created);
        }

        public async Task<MilkProductionDto> UpdateAsync(int id, MilkProductionUpdateDto dto)
        {
            var production = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Lançamento de produção de leite com id '{id}' não encontrado.");

            var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
            if (SyncTimestampResolver.IsOutdated(editedAt, production))
                return _mapper.Map<MilkProductionDto>(production);

            if (dto.Date.HasValue)
                production.Date = dto.Date.Value;

            if (dto.Milking.HasValue)
                production.Milking = dto.Milking;

            if (dto.Volume.HasValue)
                production.Volume = dto.Volume.Value;

            if (dto.Notes != null)
                production.Notes = dto.Notes;

            production.UpdatedAt = editedAt;
            var updated = await _repository.UpdateAsync(production);
            return _mapper.Map<MilkProductionDto>(updated);
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var production = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Lançamento de produção de leite com id '{id}' não encontrado.");

            if (!production.IsActive)
                return true;

            production.IsActive = false;
            production.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(production);
            return true;
        }

        public async Task<SyncPageDto<MilkProductionDto>> GetChangesAsync(string? since, int? limit)
        {
            if (!SyncPaging.TryDecodeCursor(since, out var cursor))
                throw new ValidationException("Cursor de sincronização inválido.");

            var take = SyncPaging.ResolveLimit(limit);
            var fetched = await _repository.GetChangesAsync(cursor, take + 1);

            return SyncPaging.BuildPage(fetched, take, cursor, p => _mapper.Map<MilkProductionDto>(p));
        }
    }
}
