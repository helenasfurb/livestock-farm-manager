using AutoMapper;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class StockMovementService : IStockMovementService
    {
        private readonly IStockMovementRepository _repository;
        private readonly IStockItemRepository _itemRepository;
        private readonly IMapper _mapper;

        public StockMovementService(
            IStockMovementRepository repository,
            IStockItemRepository itemRepository,
            IMapper mapper)
        {
            _repository = repository;
            _itemRepository = itemRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<StockMovementListItemDto>> GetByStockItemIdAsync(int stockItemId, StockMovementFilterDto filter)
        {
            var item = await _itemRepository.GetByIdAsync(stockItemId)
                ?? throw new NotFoundException($"Insumo com id '{stockItemId}' não encontrado.");

            var movements = (await _repository.GetByStockItemIdAsync(stockItemId, filter)).ToList();
            foreach (var movement in movements)
                movement.StockItem = item;

            return _mapper.Map<IEnumerable<StockMovementListItemDto>>(movements);
        }

        public async Task<StockMovementDto> GetByIdAsync(int stockItemId, int movementId)
        {
            var item = await _itemRepository.GetByIdAsync(stockItemId)
                ?? throw new NotFoundException($"Insumo com id '{stockItemId}' não encontrado.");

            var movement = await _repository.GetByIdAsync(movementId)
                ?? throw new NotFoundException($"Movimentação com id '{movementId}' não encontrada.");
            movement.StockItem = item;

            return _mapper.Map<StockMovementDto>(movement);
        }

        public async Task<StockMovementDto> CreateAsync(int stockItemId, StockMovementCreateDto dto)
        {
            var item = await _itemRepository.GetByIdAsync(stockItemId)
                ?? throw new NotFoundException($"Insumo com id '{stockItemId}' não encontrado.");

            if (!item.IsActive)
                throw new ConflictException("Não é possível registrar movimentação para um insumo inativo.");

            var movement = _mapper.Map<StockMovement>(dto);
            movement.StockItemId = stockItemId;

            await ApplyValuationAsync(movement, dto);

            var created = await _repository.CreateAsync(movement);
            created.StockItem = item;

            return _mapper.Map<StockMovementDto>(created);
        }

        public async Task<StockMovementDto> UpdateAsync(int stockItemId, int movementId, StockMovementUpdateDto dto)
        {
            var item = await _itemRepository.GetByIdAsync(stockItemId)
                ?? throw new NotFoundException($"Insumo com id '{stockItemId}' não encontrado.");

            var movement = await _repository.GetByIdAsync(movementId)
                ?? throw new NotFoundException($"Movimentação com id '{movementId}' não encontrada.");
            movement.StockItem = item;

            if (dto.MovementDate.HasValue)
            {
                if (dto.MovementDate.Value.Date > DateTime.UtcNow.Date)
                    throw new BusinessRuleException("A data da movimentação não pode ser futura.");
                movement.MovementDate = dto.MovementDate.Value;
            }

            if (dto.Quantity.HasValue)
                movement.Quantity = dto.Quantity.Value;

            if (dto.TotalValue.HasValue)
                movement.TotalValue = dto.TotalValue.Value;

            if (dto.Notes != null)
                movement.Notes = dto.Notes;

            movement.UpdatedAt = DateTime.UtcNow;
            var updated = await _repository.UpdateAsync(movement);

            return _mapper.Map<StockMovementDto>(updated);
        }

        public async Task DeactivateAsync(int stockItemId, int movementId)
        {
            _ = await _itemRepository.GetByIdAsync(stockItemId)
                ?? throw new NotFoundException($"Insumo com id '{stockItemId}' não encontrado.");

            var movement = await _repository.GetByIdAsync(movementId)
                ?? throw new NotFoundException($"Movimentação com id '{movementId}' não encontrada.");

            if (!movement.IsActive)
                throw new ConflictException("A movimentação já está inativa.");

            movement.IsActive = false;
            movement.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(movement);
        }

        public async Task CreateOpeningBalanceAsync(int stockItemId, decimal quantity, decimal? totalValue, string? notes)
        {
            var movement = new StockMovement
            {
                StockItemId = stockItemId,
                MovementType = StockMovementType.Input,
                MovementReason = StockMovementReason.OpeningBalance,
                MovementDate = DateTime.UtcNow,
                Quantity = quantity,
                TotalValue = totalValue,
                ValueEntryMode = totalValue.HasValue ? ValueEntryMode.TotalPrice : null,
                Notes = notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.CreateAsync(movement);
        }

        private async Task ApplyValuationAsync(StockMovement movement, StockMovementCreateDto dto)
        {
            if (movement.MovementType == StockMovementType.Input &&
                (movement.MovementReason == StockMovementReason.Purchase ||
                 movement.MovementReason == StockMovementReason.OpeningBalance))
            {
                movement.ValueEntryMode = dto.ValueEntryMode;
                movement.TotalValue = dto.ValueEntryMode == ValueEntryMode.UnitPrice
                    ? (dto.UnitPrice ?? 0m) * movement.Quantity
                    : dto.TotalValue;
                return;
            }

            var levels = await _repository.GetLevelsAsync(movement.StockItemId);
            var averageUnitCost = StockForecastResolver.CurrentAverageUnitCost(levels.Quantity, levels.Value);

            if (movement.MovementType == StockMovementType.Output)
                movement.UnitCostSnapshot = averageUnitCost;
            else
                movement.TotalValue = averageUnitCost * movement.Quantity;
        }
    }
}
