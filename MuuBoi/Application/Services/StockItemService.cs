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
        private readonly IStockMovementService _movementService;
        private readonly IStockReferenceRepository _referenceRepository;
        private readonly IMapper _mapper;

        public StockItemService(
            IStockItemRepository repository,
            IStockMovementRepository movementRepository,
            IStockMovementService movementService,
            IStockReferenceRepository referenceRepository,
            IMapper mapper)
        {
            _repository = repository;
            _movementRepository = movementRepository;
            _movementService = movementService;
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

            return await ComposeDetailAsync(item);
        }

        public async Task<StockItemDto> CreateAsync(StockItemCreateDto dto)
        {
            await ValidateReferencesAsync(dto.StockCategoryId, dto.UnitOfMeasureId);

            var item = _mapper.Map<StockItem>(dto);
            var created = await _repository.CreateAsync(item);

            if (dto.InitialQuantity.HasValue)
                await _movementService.CreateOpeningBalanceAsync(
                    created.Id, dto.InitialQuantity.Value, dto.InitialTotalValue, dto.InitialNotes);

            var reloaded = await _repository.GetByIdAsync(created.Id) ?? created;
            return await ComposeDetailAsync(reloaded);
        }

        public async Task<StockItemDto> UpdateAsync(int id, StockItemUpdateDto dto)
        {
            var item = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Insumo com id '{id}' não encontrado.");

            if (dto.StockCategoryId.HasValue && !await _referenceRepository.CategoryExistsAsync(dto.StockCategoryId.Value))
                throw new NotFoundException($"Categoria com id '{dto.StockCategoryId.Value}' não encontrada.");

            if (dto.UnitOfMeasureId.HasValue && !await _referenceRepository.UnitExistsAsync(dto.UnitOfMeasureId.Value))
                throw new NotFoundException($"Unidade de medida com id '{dto.UnitOfMeasureId.Value}' não encontrada.");

            _mapper.Map(dto, item);
            item.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(item);

            var reloaded = await _repository.GetByIdAsync(id) ?? item;
            return await ComposeDetailAsync(reloaded);
        }

        public async Task DeactivateAsync(int id)
        {
            var item = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Insumo com id '{id}' não encontrado.");

            if (!item.IsActive)
                throw new ConflictException("O insumo já está inativo.");

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

        private async Task<StockItemDto> ComposeDetailAsync(StockItem item)
        {
            var levels = await _movementRepository.GetLevelsAsync(item.Id);
            var since = DateTime.UtcNow.Date.AddDays(-StockForecastResolver.ConsumptionWindowDays);
            var consumptionMap = await _movementRepository.GetConsumptionSinceBatchAsync(new[] { item.Id }, since);
            var consumed = consumptionMap.GetValueOrDefault(item.Id, 0m);
            var dailyRate = StockForecastResolver.DailyConsumptionRate(consumed, StockForecastResolver.ConsumptionWindowDays);
            var today = DateTime.UtcNow;
            var (days, runOut) = StockForecastResolver.Forecast(levels.Quantity, dailyRate, today);
            var severity = StockForecastResolver.ResolveSeverity(
                levels.Quantity, item.ReorderPoint, runOut, item.ReplenishmentLeadDays, today);

            var dto = _mapper.Map<StockItemDto>(item);
            dto.CurrentBalance = levels.Quantity;
            dto.StockValue = levels.Value;
            dto.AverageUnitCost = StockForecastResolver.CurrentAverageUnitCost(levels.Quantity, levels.Value);
            dto.DaysOfCoverage = days;
            dto.EstimatedRunOutDate = runOut;
            dto.AlertSeverity = severity.ToEnumValue();
            return dto;
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
