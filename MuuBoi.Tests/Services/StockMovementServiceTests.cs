using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Moq;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Mappings;
using MuuBoi.Application.Services;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Tests.Services;

public class StockMovementServiceTests
{
    private readonly Mock<IStockMovementRepository> _repository = new();
    private readonly Mock<IStockItemRepository> _itemRepository = new();
    private readonly StockMovementService _service;

    public StockMovementServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<StockProfile>()).CreateMapper();
        _service = new StockMovementService(_repository.Object, _itemRepository.Object, mapper);
        _repository.Setup(r => r.CreateAsync(It.IsAny<StockMovement>())).ReturnsAsync((StockMovement m) => m);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<StockMovement>())).ReturnsAsync((StockMovement m) => m);
        _itemRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(BuildItem());
        _itemRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, StockItem>());
    }

    private static StockItem BuildItem(bool isActive = true) =>
        new() { Id = 1, Name = "Ração", IsActive = isActive, UnitOfMeasure = new UnitOfMeasure { Id = 1, Name = "Quilograma", Abbreviation = "kg" } };

    private static StockMovement BuildMovement(DateTime createdAt, DateTime? updatedAt = null, bool isActive = true, ulong rowVersion = 1, int id = 1)
    {
        return new StockMovement
        {
            Id = id,
            SyncId = Guid.NewGuid(),
            StockItemId = 1,
            MovementType = StockMovementType.Output,
            MovementReason = StockMovementReason.Consumption,
            MovementDate = createdAt,
            Quantity = 10,
            UnitCostSnapshot = 2,
            Notes = "original",
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    private static StockMovementCreateDto BuildOutputDto(Guid? syncId = null) =>
        new() { SyncId = syncId, MovementType = StockMovementType.Output, MovementReason = StockMovementReason.Consumption, MovementDate = DateTime.UtcNow, Quantity = 5 };

    [Fact]
    public async Task CreateAsync_WithNewSyncId_CreatesMovementWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync((StockMovement?)null);
        var result = await _service.CreateAsync(1, BuildOutputDto(syncId));
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.CreateAsync(It.Is<StockMovement>(m => m.SyncId == syncId && m.StockItemId == 1)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = BuildMovement(DateTime.UtcNow, id: 7);
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var result = await _service.CreateAsync(1, BuildOutputDto(syncId));
        Assert.Equal(7, result.Id);
        Assert.Equal(10, result.Quantity);
        _repository.Verify(r => r.CreateAsync(It.IsAny<StockMovement>()), Times.Never);
        _itemRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_DoesNotRecalculateValuation()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(BuildMovement(DateTime.UtcNow, id: 7));
        _repository.Setup(r => r.GetLevelsAsync(1)).ReturnsAsync(new StockItemLevels(10, 680));
        var result = await _service.CreateAsync(1, BuildOutputDto(syncId));
        Assert.Equal(2, result.UnitCost);
        _repository.Verify(r => r.GetLevelsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncIdAndInactiveItem_ReturnsExisting()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(BuildMovement(DateTime.UtcNow, id: 7));
        _itemRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(BuildItem(isActive: false));
        var result = await _service.CreateAsync(1, BuildOutputDto(syncId));
        Assert.Equal(7, result.Id);
        _repository.Verify(r => r.CreateAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithoutSyncId_GeneratesSyncId()
    {
        var result = await _service.CreateAsync(1, BuildOutputDto());
        Assert.NotEqual(Guid.Empty, result.SyncId);
        _repository.Verify(r => r.GetBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
        _repository.Verify(r => r.CreateAsync(It.Is<StockMovement>(m => m.SyncId != Guid.Empty)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenItemNotFound_ThrowsNotFoundException()
    {
        _itemRepository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((StockItem?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(99, BuildOutputDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenItemInactive_ThrowsConflictException()
    {
        _itemRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(BuildItem(isActive: false));
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(1, BuildOutputDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithOutput_FreezesCurrentAverageUnitCost()
    {
        _repository.Setup(r => r.GetLevelsAsync(1)).ReturnsAsync(new StockItemLevels(100, 250));
        var result = await _service.CreateAsync(1, BuildOutputDto());
        Assert.Equal(2.5m, result.UnitCost);
        _repository.Verify(r => r.CreateAsync(It.Is<StockMovement>(m => m.UnitCostSnapshot == 2.5m)), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var movement = BuildMovement(now.AddDays(-2), now.AddHours(-1));
        var clientUpdatedAt = now.AddMinutes(-10);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(movement);
        var result = await _service.UpdateAsync(1, 1, new StockMovementUpdateDto { Quantity = 20, UpdatedAt = clientUpdatedAt });
        Assert.Equal(20, result.Quantity);
        Assert.Equal(clientUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(movement), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var movement = BuildMovement(now.AddDays(-2), serverUpdatedAt);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(movement);
        var result = await _service.UpdateAsync(1, 1, new StockMovementUpdateDto { Quantity = 20, UpdatedAt = now.AddHours(-3) });
        Assert.Equal(10, result.Quantity);
        Assert.Equal(serverUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithoutClientUpdatedAt_UsesNow()
    {
        var before = DateTime.UtcNow;
        var movement = BuildMovement(before.AddDays(-2), before.AddHours(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(movement);
        var result = await _service.UpdateAsync(1, 1, new StockMovementUpdateDto { Notes = "corrigida" });
        var after = DateTime.UtcNow;
        Assert.Equal("corrigida", result.Notes);
        Assert.InRange(result.UpdatedAt!.Value, before, after);
    }

    [Fact]
    public async Task UpdateAsync_WithFutureDate_ThrowsBusinessRuleException()
    {
        var now = DateTime.UtcNow;
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(BuildMovement(now.AddDays(-2)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateAsync(1, 1, new StockMovementUpdateDto { MovementDate = now.AddDays(2), UpdatedAt = now }));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenMovementNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((StockMovement?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(1, 99, new StockMovementUpdateDto { Quantity = 20 }));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_WhenActive_Deactivates()
    {
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(BuildMovement(DateTime.UtcNow.AddDays(-1)));
        await _service.DeactivateAsync(1, 1);
        _repository.Verify(r => r.UpdateAsync(It.Is<StockMovement>(m => !m.IsActive)), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_WhenAlreadyInactive_ReturnsWithoutUpdating()
    {
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(BuildMovement(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddHours(-1), isActive: false));
        await _service.DeactivateAsync(1, 1);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<StockMovement>()), Times.Never);
    }

    [Fact]
    public async Task GetChangesAsync_WithInvalidCursor_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.GetChangesAsync("abc", null));
        _repository.Verify(r => r.GetChangesAsync(It.IsAny<ulong>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var fetched = new List<StockMovement>
        {
            BuildMovement(createdAt, rowVersion: 10, id: 1),
            BuildMovement(createdAt, rowVersion: 11, id: 2),
            BuildMovement(createdAt, rowVersion: 12, id: 3)
        };
        _repository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }

    [Fact]
    public async Task GetChangesAsync_FillsStockItemNameAndUnitForPageItems()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var other = BuildMovement(createdAt, rowVersion: 11, id: 2);
        other.StockItemId = 2;
        var fetched = new List<StockMovement> { BuildMovement(createdAt, rowVersion: 10, id: 1), other };
        _repository.Setup(r => r.GetChangesAsync(0UL, SyncPaging.DefaultLimit + 1)).ReturnsAsync(fetched);
        _itemRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, StockItem> { [1] = BuildItem() });
        var result = (await _service.GetChangesAsync(null, null)).Items.ToList();
        Assert.Equal("Ração", result[0].StockItemName);
        Assert.Equal("kg", result[0].UnitAbbreviation);
        Assert.Equal(string.Empty, result[1].StockItemName);
    }
}
