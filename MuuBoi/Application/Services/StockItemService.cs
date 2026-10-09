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
    public class StockItemService : IStockItemService
    {
        private readonly IStockItemRepository _repository;
        private readonly IStockMovementRepository _movementRepository;
        private readonly IStockReferenceRepository _referenceRepository;
        private readonly IMapper _mapper;

        public StockItemService(
            IStockItemRepository repository,
            IStockMovementRepository movementRepository,
            IStockReferenceRepository referenceRepository,
            IMapper mapper)
        {
            _repository = repository;
            _movementRepository = movementRepository;
            _referenceRepository = referenceRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<StockItemListItemDto>> GetAllAsync(StockItemFilterDto filter)
        {
            var items = (await _repository.GetAllAsync(filter)).ToList();
            if (items.Count == 0)
                return new List<StockItemListItemDto>();

            var ids = items.Select(i => i.Id).ToList();
            var levelsMap = await _movementRepository.GetLevelsBatchAsync(ids);
            var since = DateTime.UtcNow.Date.AddDays(-StockForecastResolver.ConsumptionWindowDays);
            var consumptionMap = await _movementRepository.GetConsumptionSinceBatchAsync(ids, since);
            var today = DateTime.UtcNow;

            var dtos = new List<StockItemListItemDto>(items.Count);
            foreach (var entity in items)
            {
                var levels = levelsMap.GetValueOrDefault(entity.Id);
                var consumed = consumptionMap.GetValueOrDefault(entity.Id, 0m);
                var dailyRate = StockForecastResolver.DailyConsumptionRate(consumed, StockForecastResolver.ConsumptionWindowDays);
                var (_, runOut) = StockForecastResolver.Forecast(levels.Quantity, dailyRate, today);
                var severity = StockForecastResolver.ResolveSeverity(
                    levels.Quantity, entity.ReorderPoint, runOut, entity.ReplenishmentLeadDays, today);

                var dto = _mapper.Map<StockItemListItemDto>(entity);
                dto.CurrentBalance = levels.Quantity;
                dto.StockValue = levels.Value;
                dto.AlertSeverity = severity.ToEnumValue();
                dtos.Add(dto);
            }

            return dtos;
        }

        public async Task<StockItemDto> GetByIdAsync(int id)
        {
            var item = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Insumo com id '{id}' não encontrado.");

            return await ComposeDetailAsync<StockItemDto>(item);
        }

        public async Task<StockItemCreatedDto> CreateAsync(StockItemCreateDto dto)
        {
            if (dto.SyncId.HasValue)
            {
                var existing = await _repository.GetBySyncIdAsync(dto.SyncId.Value);
                if (existing != null)
                    return await ComposeCreatedAsync(existing, dto.InitialMovementSyncId);
            }

            await ValidateReferencesAsync(dto.StockCategoryId, dto.UnitOfMeasureId);

            var item = _mapper.Map<StockItem>(dto);
            item.SyncId = dto.SyncId ?? Guid.NewGuid();

            if (dto.InitialQuantity.HasValue)
                item.Movements = new List<StockMovement>
                {
                    new()
                    {
                        SyncId = dto.InitialMovementSyncId ?? Guid.NewGuid(),
                        MovementType = StockMovementType.Input,
                        MovementReason = StockMovementReason.OpeningBalance,
                        MovementDate = DateTime.UtcNow,
                        Quantity = dto.InitialQuantity.Value,
                        TotalValue = dto.InitialTotalValue,
                        ValueEntryMode = dto.InitialTotalValue.HasValue ? ValueEntryMode.TotalPrice : null,
                        Notes = dto.InitialNotes,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    }
                };

            var created = await _repository.CreateAsync(item);
            var reloaded = await _repository.GetByIdAsync(created.Id) ?? created;
            return await ComposeCreatedAsync(reloaded, item.Movements?.FirstOrDefault()?.SyncId);
        }

        public async Task<StockItemDto> UpdateAsync(int id, StockItemUpdateDto dto)
        {
            var item = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Insumo com id '{id}' não encontrado.");

            if (dto.StockCategoryId.HasValue && !await _referenceRepository.CategoryExistsAsync(dto.StockCategoryId.Value))
                throw new NotFoundException($"Categoria com id '{dto.StockCategoryId.Value}' não encontrada.");

            if (dto.UnitOfMeasureId.HasValue && !await _referenceRepository.UnitExistsAsync(dto.UnitOfMeasureId.Value))
                throw new NotFoundException($"Unidade de medida com id '{dto.UnitOfMeasureId.Value}' não encontrada.");

            var editedAt = SyncTimestampResolver.ResolveEditedAt(dto.UpdatedAt, DateTime.UtcNow);
            if (SyncTimestampResolver.IsOutdated(editedAt, item))
                return await ComposeDetailAsync<StockItemDto>(item);

            _mapper.Map(dto, item);
            item.UpdatedAt = editedAt;
            await _repository.UpdateAsync(item);

            var reloaded = await _repository.GetByIdAsync(id) ?? item;
            return await ComposeDetailAsync<StockItemDto>(reloaded);
        }

        public async Task DeactivateAsync(int id)
        {
            var item = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Insumo com id '{id}' não encontrado.");

            if (!item.IsActive)
                return;

            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(item);
        }

        public async Task<StockDashboardDto> GetDashboardAsync(StockDashboardFilterDto filter)
        {
            var today = DateTime.UtcNow.Date;
            var from = (filter.DateFrom ?? new DateTime(today.Year, today.Month, 1)).Date;
            var to = (filter.DateTo ?? today).Date;

            var items = (await _repository.GetAllAsync(new StockItemFilterDto
            {
                IsActive = true,
                StockCategoryId = filter.StockCategoryId
            })).ToList();

            if (filter.StockItemId.HasValue)
                items = items.Where(i => i.Id == filter.StockItemId.Value).ToList();

            var ids = items.Select(i => i.Id).ToList();
            var hasScope = filter.StockCategoryId.HasValue || filter.StockItemId.HasValue;
            var scopeIds = hasScope ? ids : null;

            var periodSpent = await _movementRepository.GetPeriodPurchaseTotalAsync(from, to, scopeIds);
            var periodConsumedValue = await _movementRepository.GetPeriodConsumedValueAsync(from, to, scopeIds);

            var levelsMap = await _movementRepository.GetLevelsBatchAsync(ids);
            var consumedByItem = await _movementRepository.GetConsumedQuantityByItemAsync(from, to, scopeIds);

            var dashboard = new StockDashboardDto
            {
                DateFrom = from,
                DateTo = to,
                PeriodSpent = periodSpent,
                PeriodConsumedValue = periodConsumedValue,
                StockValue = levelsMap.Values.Sum(v => v.Value),
                Items = items.Select(e => new StockDashboardItemDto
                {
                    StockItemId = e.Id,
                    Name = e.Name,
                    UnitAbbreviation = e.UnitOfMeasure?.Abbreviation,
                    ConsumedQuantity = consumedByItem.GetValueOrDefault(e.Id, 0m),
                    CurrentBalance = levelsMap.GetValueOrDefault(e.Id).Quantity
                }).ToList()
            };

            return dashboard;
        }

        public async Task<IEnumerable<StockAlertDto>> GetAlertsAsync()
        {
            var items = (await _repository.GetAllAsync(new StockItemFilterDto { IsActive = true }))
                .Where(i => i.ReorderPoint.HasValue)
                .ToList();
            if (items.Count == 0)
                return Enumerable.Empty<StockAlertDto>();

            var ids = items.Select(i => i.Id).ToList();
            var levelsMap = await _movementRepository.GetLevelsBatchAsync(ids);
            var since = DateTime.UtcNow.Date.AddDays(-StockForecastResolver.ConsumptionWindowDays);
            var consumptionMap = await _movementRepository.GetConsumptionSinceBatchAsync(ids, since);
            var today = DateTime.UtcNow;

            var alerts = new List<(StockAlertDto Alert, StockAlertSeverity Severity)>();
            foreach (var entity in items)
            {
                var levels = levelsMap.GetValueOrDefault(entity.Id);
                if (levels.Quantity > entity.ReorderPoint!.Value)
                    continue;

                var consumed = consumptionMap.GetValueOrDefault(entity.Id, 0m);
                var dailyRate = StockForecastResolver.DailyConsumptionRate(consumed, StockForecastResolver.ConsumptionWindowDays);
                var (_, runOut) = StockForecastResolver.Forecast(levels.Quantity, dailyRate, today);
                var severity = StockForecastResolver.ResolveSeverity(
                    levels.Quantity, entity.ReorderPoint, runOut, entity.ReplenishmentLeadDays, today);

                alerts.Add((new StockAlertDto
                {
                    StockItemId = entity.Id,
                    Name = entity.Name,
                    UnitAbbreviation = entity.UnitOfMeasure?.Abbreviation,
                    CurrentBalance = levels.Quantity,
                    ReorderPoint = entity.ReorderPoint,
                    EstimatedRunOutDate = runOut,
                    Severity = severity.ToEnumValue()
                }, severity));
            }

            return alerts
                .OrderByDescending(a => a.Severity)
                .ThenBy(a => a.Alert.Name)
                .Select(a => a.Alert)
                .ToList();
        }

        public async Task<SyncPageDto<StockItemDto>> GetChangesAsync(string? since, int? limit)
        {
            if (!SyncPaging.TryDecodeCursor(since, out var cursor))
                throw new ValidationException("Cursor de sincronização inválido.");

            var take = SyncPaging.ResolveLimit(limit);
            var fetched = await _repository.GetChangesAsync(cursor, take + 1);

            var categories = (await _referenceRepository.GetCategoriesAsync()).ToDictionary(c => c.Id);
            var units = (await _referenceRepository.GetUnitsAsync()).ToDictionary(u => u.Id);
            foreach (var item in fetched)
            {
                item.StockCategory = categories.GetValueOrDefault(item.StockCategoryId);
                item.UnitOfMeasure = units.GetValueOrDefault(item.UnitOfMeasureId);
            }

            var ids = fetched.Select(i => i.Id).ToList();
            var levelsMap = await _movementRepository.GetLevelsBatchAsync(ids);
            var consumptionMap = await _movementRepository.GetConsumptionSinceBatchAsync(
                ids, DateTime.UtcNow.Date.AddDays(-StockForecastResolver.ConsumptionWindowDays));
            var today = DateTime.UtcNow;

            return SyncPaging.BuildPage(fetched, take, cursor, item =>
            {
                var dto = _mapper.Map<StockItemDto>(item);
                FillDerived(dto, item, levelsMap.GetValueOrDefault(item.Id), consumptionMap.GetValueOrDefault(item.Id, 0m), today);
                return dto;
            });
        }

        private async Task<StockItemCreatedDto> ComposeCreatedAsync(StockItem item, Guid? initialMovementSyncId)
        {
            var dto = await ComposeDetailAsync<StockItemCreatedDto>(item);

            if (initialMovementSyncId.HasValue)
            {
                var movement = await _movementRepository.GetBySyncIdAsync(initialMovementSyncId.Value);
                if (movement != null && movement.StockItemId == item.Id)
                    dto.InitialMovement = _mapper.Map<StockMovementRefDto>(movement);
            }

            return dto;
        }

        private async Task<TDto> ComposeDetailAsync<TDto>(StockItem item) where TDto : StockItemDto
        {
            var levels = await _movementRepository.GetLevelsAsync(item.Id);
            var since = DateTime.UtcNow.Date.AddDays(-StockForecastResolver.ConsumptionWindowDays);
            var consumptionMap = await _movementRepository.GetConsumptionSinceBatchAsync(new[] { item.Id }, since);

            var dto = _mapper.Map<TDto>(item);
            FillDerived(dto, item, levels, consumptionMap.GetValueOrDefault(item.Id, 0m), DateTime.UtcNow);
            return dto;
        }

        private static void FillDerived(StockItemDto dto, StockItem item, StockItemLevels levels, decimal consumed, DateTime today)
        {
            var dailyRate = StockForecastResolver.DailyConsumptionRate(consumed, StockForecastResolver.ConsumptionWindowDays);
            var (days, runOut) = StockForecastResolver.Forecast(levels.Quantity, dailyRate, today);
            var severity = StockForecastResolver.ResolveSeverity(
                levels.Quantity, item.ReorderPoint, runOut, item.ReplenishmentLeadDays, today);

            dto.CurrentBalance = levels.Quantity;
            dto.StockValue = levels.Value;
            dto.AverageUnitCost = StockForecastResolver.CurrentAverageUnitCost(levels.Quantity, levels.Value);
            dto.DaysOfCoverage = days;
            dto.EstimatedRunOutDate = runOut;
            dto.AlertSeverity = severity.ToEnumValue();
        }

        private async Task ValidateReferencesAsync(int categoryId, int unitId)
        {
            if (!await _referenceRepository.CategoryExistsAsync(categoryId))
                throw new NotFoundException($"Categoria com id '{categoryId}' não encontrada.");

            if (!await _referenceRepository.UnitExistsAsync(unitId))
                throw new NotFoundException($"Unidade de medida com id '{unitId}' não encontrada.");
        }
    }
}
