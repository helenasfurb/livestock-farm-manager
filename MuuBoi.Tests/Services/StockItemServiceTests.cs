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

public class StockItemServiceTests
{
    private readonly Mock<IStockItemRepository> _repository = new();
    private readonly Mock<IStockMovementRepository> _movementRepository = new();
    private readonly Mock<IStockReferenceRepository> _referenceRepository = new();
    private readonly StockItemService _service;

    public StockItemServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<StockProfile>()).CreateMapper();
        _service = new StockItemService(_repository.Object, _movementRepository.Object, _referenceRepository.Object, mapper);
        _repository.Setup(r => r.CreateAsync(It.IsAny<StockItem>())).ReturnsAsync((StockItem i) => i);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<StockItem>())).ReturnsAsync((StockItem i) => i);
        _referenceRepository.Setup(r => r.CategoryExistsAsync(It.IsAny<int>())).ReturnsAsync(true);
        _referenceRepository.Setup(r => r.UnitExistsAsync(It.IsAny<int>())).ReturnsAsync(true);
        _referenceRepository.Setup(r => r.GetCategoriesAsync()).ReturnsAsync(new List<StockCategory>());
        _referenceRepository.Setup(r => r.GetUnitsAsync()).ReturnsAsync(new List<UnitOfMeasure>());
        _movementRepository.Setup(r => r.GetLevelsBatchAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, StockItemLevels>());
        _movementRepository.Setup(r => r.GetConsumptionSinceBatchAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>())).ReturnsAsync(new Dictionary<int, decimal>());
    }

    private static StockItem BuildItem(DateTime createdAt, DateTime? updatedAt = null, bool isActive = true, ulong rowVersion = 1, int id = 1)
    {
        return new StockItem
        {
            Id = id,
            SyncId = Guid.NewGuid(),
            Name = "Ração",
            StockCategoryId = 1,
            UnitOfMeasureId = 1,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    private static StockItemCreateDto BuildCreateDto(Guid? syncId = null) =>
        new() { SyncId = syncId, Name = "Ração", StockCategoryId = 1, UnitOfMeasureId = 1 };

    [Fact]
    public async Task CreateAsync_WithNewSyncId_CreatesItemWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync((StockItem?)null);
        var result = await _service.CreateAsync(BuildCreateDto(syncId));
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.CreateAsync(It.Is<StockItem>(i => i.SyncId == syncId)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = BuildItem(DateTime.UtcNow, id: 7);
        existing.SyncId = syncId;
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var dto = BuildCreateDto(syncId);
        dto.Name = "Outro nome";
        dto.InitialQuantity = 5;
        var result = await _service.CreateAsync(dto);
        Assert.Equal(7, result.Id);
        Assert.Equal("Ração", result.Name);
        _repository.Verify(r => r.CreateAsync(It.IsAny<StockItem>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_DoesNotValidateReferences()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(BuildItem(DateTime.UtcNow, id: 7));
        _referenceRepository.Setup(r => r.CategoryExistsAsync(It.IsAny<int>())).ReturnsAsync(false);
        var result = await _service.CreateAsync(BuildCreateDto(syncId));
        Assert.Equal(7, result.Id);
        _referenceRepository.Verify(r => r.CategoryExistsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithoutSyncId_GeneratesSyncId()
    {
        var result = await _service.CreateAsync(BuildCreateDto());
        Assert.NotEqual(Guid.Empty, result.SyncId);
        _repository.Verify(r => r.GetBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
        _repository.Verify(r => r.CreateAsync(It.Is<StockItem>(i => i.SyncId != Guid.Empty)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithInitialQuantity_CreatesItemWithOpeningBalanceMovementInSameCall()
    {
        var dto = BuildCreateDto();
        dto.InitialQuantity = 50;
        dto.InitialTotalValue = 100;
        dto.InitialNotes = "inicial";
        await _service.CreateAsync(dto);
        _repository.Verify(r => r.CreateAsync(It.Is<StockItem>(i =>
            i.Movements != null && i.Movements.Count == 1 &&
            i.Movements.First().MovementType == StockMovementType.Input &&
            i.Movements.First().MovementReason == StockMovementReason.OpeningBalance &&
            i.Movements.First().Quantity == 50 &&
            i.Movements.First().TotalValue == 100 &&
            i.Movements.First().ValueEntryMode == ValueEntryMode.TotalPrice &&
            i.Movements.First().Notes == "inicial")), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithInitialMovementSyncId_CreatesMovementWithGivenSyncIdAndReturnsIt()
    {
        var movementSyncId = Guid.NewGuid();
        _movementRepository.Setup(r => r.GetBySyncIdAsync(movementSyncId))
            .ReturnsAsync(new StockMovement { Id = 21, SyncId = movementSyncId, StockItemId = 0 });
        var dto = BuildCreateDto();
        dto.InitialQuantity = 10;
        dto.InitialMovementSyncId = movementSyncId;
        var result = await _service.CreateAsync(dto);
        _repository.Verify(r => r.CreateAsync(It.Is<StockItem>(i => i.Movements!.Single().SyncId == movementSyncId)), Times.Once);
        Assert.Equal(21, result.InitialMovement!.Id);
        Assert.Equal(movementSyncId, result.InitialMovement.SyncId);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncIdAndInitialMovementSyncId_ReturnsSameInitialMovement()
    {
        var syncId = Guid.NewGuid();
        var movementSyncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(BuildItem(DateTime.UtcNow, id: 7));
        _movementRepository.Setup(r => r.GetBySyncIdAsync(movementSyncId))
            .ReturnsAsync(new StockMovement { Id = 21, SyncId = movementSyncId, StockItemId = 7 });
        var dto = BuildCreateDto(syncId);
        dto.InitialQuantity = 10;
        dto.InitialMovementSyncId = movementSyncId;
        var result = await _service.CreateAsync(dto);
        Assert.Equal(7, result.Id);
        Assert.Equal(21, result.InitialMovement!.Id);
        _repository.Verify(r => r.CreateAsync(It.IsAny<StockItem>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithoutInitialQuantity_ReturnsNullInitialMovement()
    {
        var dto = BuildCreateDto();
        dto.InitialMovementSyncId = Guid.NewGuid();
        var result = await _service.CreateAsync(dto);
        Assert.Null(result.InitialMovement);
        _repository.Verify(r => r.CreateAsync(It.Is<StockItem>(i => i.Movements == null || i.Movements.Count == 0)), Times.Once);
        _movementRepository.Verify(r => r.GetBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenCategoryNotFound_ThrowsNotFoundException()
    {
        _referenceRepository.Setup(r => r.CategoryExistsAsync(99)).ReturnsAsync(false);
        var dto = BuildCreateDto(Guid.NewGuid());
        dto.StockCategoryId = 99;
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(dto));
        _repository.Verify(r => r.CreateAsync(It.IsAny<StockItem>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var item = BuildItem(now.AddDays(-2), now.AddHours(-1));
        var clientUpdatedAt = now.AddMinutes(-10);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(item);
        var result = await _service.UpdateAsync(1, new StockItemUpdateDto { Name = "Ração B", UpdatedAt = clientUpdatedAt });
        Assert.Equal("Ração B", result.Name);
        Assert.Equal(clientUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(item), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var item = BuildItem(now.AddDays(-2), serverUpdatedAt);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(item);
        var result = await _service.UpdateAsync(1, new StockItemUpdateDto { Name = "Ração B", UpdatedAt = now.AddHours(-3) });
        Assert.Equal("Ração", result.Name);
        Assert.Equal(serverUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<StockItem>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithoutClientUpdatedAt_UsesNow()
    {
        var before = DateTime.UtcNow;
        var item = BuildItem(before.AddDays(-2), before.AddHours(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(item);
        var result = await _service.UpdateAsync(1, new StockItemUpdateDto { Name = "Ração B" });
        var after = DateTime.UtcNow;
        Assert.Equal("Ração B", result.Name);
        Assert.InRange(result.UpdatedAt!.Value, before, after);
    }

    [Fact]
    public async Task UpdateAsync_WhenNeverEdited_ComparesWithCreatedAt()
    {
        var now = DateTime.UtcNow;
        var item = BuildItem(now.AddHours(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(item);
        var result = await _service.UpdateAsync(1, new StockItemUpdateDto { Name = "Ração B", UpdatedAt = now.AddHours(-2) });
        Assert.Equal("Ração", result.Name);
        Assert.Null(result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<StockItem>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenItemNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((StockItem?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(99, new StockItemUpdateDto { Name = "Ração B" }));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<StockItem>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_WhenActive_Deactivates()
    {
        var item = BuildItem(DateTime.UtcNow.AddDays(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(item);
        await _service.DeactivateAsync(1);
        _repository.Verify(r => r.UpdateAsync(It.Is<StockItem>(i => !i.IsActive)), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_WhenAlreadyInactive_ReturnsWithoutUpdating()
    {
        var item = BuildItem(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddHours(-1), isActive: false);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(item);
        await _service.DeactivateAsync(1);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<StockItem>()), Times.Never);
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
        var fetched = new List<StockItem>
        {
            BuildItem(createdAt, rowVersion: 10, id: 1),
            BuildItem(createdAt, rowVersion: 11, id: 2),
            BuildItem(createdAt, rowVersion: 12, id: 3)
        };
        _repository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }

    [Fact]
    public async Task GetChangesAsync_FillsDerivedFieldsAndReferencesForPageItems()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var fetched = new List<StockItem> { BuildItem(createdAt, rowVersion: 10, id: 1), BuildItem(createdAt, rowVersion: 11, id: 2) };
        _repository.Setup(r => r.GetChangesAsync(0UL, SyncPaging.DefaultLimit + 1)).ReturnsAsync(fetched);
        _referenceRepository.Setup(r => r.GetCategoriesAsync()).ReturnsAsync(new List<StockCategory> { new() { Id = 1, Name = "Concentrado" } });
        _referenceRepository.Setup(r => r.GetUnitsAsync()).ReturnsAsync(new List<UnitOfMeasure> { new() { Id = 1, Name = "Quilograma", Abbreviation = "kg" } });
        _movementRepository.Setup(r => r.GetLevelsBatchAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, StockItemLevels> { [1] = new StockItemLevels(70, 210) });
        var result = (await _service.GetChangesAsync(null, null)).Items.ToList();
        Assert.Equal(70, result[0].CurrentBalance);
        Assert.Equal(210, result[0].StockValue);
        Assert.Equal(3, result[0].AverageUnitCost);
        Assert.Equal(0, result[1].CurrentBalance);
        Assert.Equal("Concentrado", result[0].StockCategory.Name);
        Assert.Equal("kg", result[1].UnitOfMeasure.Abbreviation);
    }
}
