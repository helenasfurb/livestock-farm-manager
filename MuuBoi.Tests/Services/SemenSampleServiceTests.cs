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

public class SemenSampleServiceTests
{
    private readonly Mock<ISemenSampleRepository> _repository = new();
    private readonly Mock<ISemenSampleMovementRepository> _movementRepository = new();
    private readonly SemenSampleService _service;

    public SemenSampleServiceTests()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SemenSampleProfile>();
            cfg.AddProfile<SemenSampleMovementProfile>();
        }).CreateMapper();
        _service = new SemenSampleService(_repository.Object, _movementRepository.Object, mapper);
        _repository.Setup(r => r.CreateAsync(It.IsAny<SemenSample>())).ReturnsAsync((SemenSample s) => s);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<SemenSample>())).ReturnsAsync((SemenSample s) => s);
        _repository.Setup(r => r.GetAvailableDosesBatchAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, int>());
    }

    private static SemenSample BuildSample(DateTime createdAt, DateTime? updatedAt = null, bool isActive = true, ulong rowVersion = 1, int id = 1)
    {
        return new SemenSample
        {
            Id = id,
            SyncId = Guid.NewGuid(),
            Name = "Touro A",
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    [Fact]
    public async Task CreateAsync_WithNewSyncId_CreatesSampleWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync((SemenSample?)null);
        var result = await _service.CreateAsync(new SemenSampleCreateDto { SyncId = syncId, Name = "Touro A" });
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.CreateAsync(It.Is<SemenSample>(s => s.SyncId == syncId)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = new SemenSample { Id = 7, SyncId = syncId, Name = "Touro A", CreatedAt = DateTime.UtcNow };
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var result = await _service.CreateAsync(new SemenSampleCreateDto { SyncId = syncId, Name = "Outro nome", InitialQuantity = 5 });
        Assert.Equal(7, result.Id);
        Assert.Equal("Touro A", result.Name);
        _repository.Verify(r => r.CreateAsync(It.IsAny<SemenSample>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithoutSyncId_GeneratesSyncId()
    {
        var result = await _service.CreateAsync(new SemenSampleCreateDto { Name = "Touro A" });
        Assert.NotEqual(Guid.Empty, result.SyncId);
        _repository.Verify(r => r.GetBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
        _repository.Verify(r => r.CreateAsync(It.Is<SemenSample>(s => s.SyncId != Guid.Empty)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithInitialQuantity_CreatesSampleWithInputMovementInSameCall()
    {
        await _service.CreateAsync(new SemenSampleCreateDto { Name = "Touro A", InitialQuantity = 10, InitialNotes = "compra" });
        _repository.Verify(r => r.CreateAsync(It.Is<SemenSample>(s =>
            s.Movements != null && s.Movements.Count == 1 &&
            s.Movements.First().MovementType == SemenMovementType.Input &&
            s.Movements.First().Quantity == 10 &&
            s.Movements.First().Notes == "compra")), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithoutInitialQuantity_CreatesSampleWithoutMovements()
    {
        await _service.CreateAsync(new SemenSampleCreateDto { Name = "Touro A" });
        _repository.Verify(r => r.CreateAsync(It.Is<SemenSample>(s => s.Movements == null || s.Movements.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithInitialMovementSyncId_CreatesMovementWithGivenSyncIdAndReturnsIt()
    {
        var movementSyncId = Guid.NewGuid();
        _movementRepository.Setup(r => r.GetBySyncIdAsync(movementSyncId))
            .ReturnsAsync(new SemenSampleMovement { Id = 21, SyncId = movementSyncId, SemenSampleId = 0 });
        var result = await _service.CreateAsync(new SemenSampleCreateDto { Name = "Touro A", InitialQuantity = 10, InitialMovementSyncId = movementSyncId });
        _repository.Verify(r => r.CreateAsync(It.Is<SemenSample>(s => s.Movements!.Single().SyncId == movementSyncId)), Times.Once);
        Assert.Equal(21, result.InitialMovement!.Id);
        Assert.Equal(movementSyncId, result.InitialMovement.SyncId);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncIdAndInitialMovementSyncId_ReturnsSameInitialMovement()
    {
        var syncId = Guid.NewGuid();
        var movementSyncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(new SemenSample { Id = 7, SyncId = syncId, Name = "Touro A", CreatedAt = DateTime.UtcNow });
        _movementRepository.Setup(r => r.GetBySyncIdAsync(movementSyncId))
            .ReturnsAsync(new SemenSampleMovement { Id = 21, SyncId = movementSyncId, SemenSampleId = 7 });
        var result = await _service.CreateAsync(new SemenSampleCreateDto { SyncId = syncId, Name = "Touro A", InitialQuantity = 10, InitialMovementSyncId = movementSyncId });
        Assert.Equal(7, result.Id);
        Assert.Equal(21, result.InitialMovement!.Id);
        _repository.Verify(r => r.CreateAsync(It.IsAny<SemenSample>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithoutInitialQuantity_ReturnsNullInitialMovement()
    {
        var result = await _service.CreateAsync(new SemenSampleCreateDto { Name = "Touro A", InitialMovementSyncId = Guid.NewGuid() });
        Assert.Null(result.InitialMovement);
        _movementRepository.Verify(r => r.GetBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var sample = BuildSample(now.AddDays(-2), now.AddHours(-1));
        var clientUpdatedAt = now.AddMinutes(-10);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(sample);
        var result = await _service.UpdateAsync(1, new SemenSampleUpdateDto { Name = "Touro B", UpdatedAt = clientUpdatedAt });
        Assert.Equal("Touro B", result.Name);
        Assert.Equal(clientUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(sample), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var sample = BuildSample(now.AddDays(-2), serverUpdatedAt);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(sample);
        var result = await _service.UpdateAsync(1, new SemenSampleUpdateDto { Name = "Touro B", UpdatedAt = now.AddHours(-3) });
        Assert.Equal("Touro A", result.Name);
        Assert.Equal(serverUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSample>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithSameClientUpdatedAt_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var sample = BuildSample(now.AddDays(-2), serverUpdatedAt);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(sample);
        var result = await _service.UpdateAsync(1, new SemenSampleUpdateDto { Name = "Touro B", UpdatedAt = serverUpdatedAt });
        Assert.Equal("Touro B", result.Name);
        Assert.Equal(serverUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(sample), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithoutClientUpdatedAt_UsesNow()
    {
        var before = DateTime.UtcNow;
        var sample = BuildSample(before.AddDays(-2), before.AddHours(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(sample);
        var result = await _service.UpdateAsync(1, new SemenSampleUpdateDto { Name = "Touro B" });
        var after = DateTime.UtcNow;
        Assert.Equal("Touro B", result.Name);
        Assert.InRange(result.UpdatedAt!.Value, before, after);
    }

    [Fact]
    public async Task UpdateAsync_WhenNeverEdited_ComparesWithCreatedAt()
    {
        var now = DateTime.UtcNow;
        var sample = BuildSample(now.AddHours(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(sample);
        var result = await _service.UpdateAsync(1, new SemenSampleUpdateDto { Name = "Touro B", UpdatedAt = now.AddHours(-2) });
        Assert.Equal("Touro A", result.Name);
        Assert.Null(result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSample>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenSampleNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((SemenSample?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(99, new SemenSampleUpdateDto { Name = "Touro B" }));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSample>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_WhenActive_DeactivatesAndReturnsFalse()
    {
        var sample = BuildSample(DateTime.UtcNow.AddDays(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(sample);
        var result = await _service.DeactivateAsync(1);
        Assert.False(result);
        _repository.Verify(r => r.UpdateAsync(It.Is<SemenSample>(s => !s.IsActive)), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_WhenAlreadyInactive_ReturnsFalseWithoutUpdating()
    {
        var sample = BuildSample(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddHours(-1), isActive: false);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(sample);
        var result = await _service.DeactivateAsync(1);
        Assert.False(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSample>()), Times.Never);
    }

    [Fact]
    public async Task ReactivateAsync_WhenInactive_ReactivatesAndReturnsTrue()
    {
        var sample = BuildSample(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddHours(-1), isActive: false);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(sample);
        var result = await _service.ReactivateAsync(1);
        Assert.True(result);
        _repository.Verify(r => r.UpdateAsync(It.Is<SemenSample>(s => s.IsActive)), Times.Once);
    }

    [Fact]
    public async Task ReactivateAsync_WhenAlreadyActive_ReturnsTrueWithoutUpdating()
    {
        var sample = BuildSample(DateTime.UtcNow.AddDays(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(sample);
        var result = await _service.ReactivateAsync(1);
        Assert.True(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSample>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_WhenSampleNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((SemenSample?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeactivateAsync(99));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSample>()), Times.Never);
    }

    [Fact]
    public async Task GetChangesAsync_WithInvalidCursor_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.GetChangesAsync("abc", null));
        _repository.Verify(r => r.GetChangesAsync(It.IsAny<ulong>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetChangesAsync_WithoutCursor_RequestsChangesFromZero()
    {
        _repository.Setup(r => r.GetChangesAsync(It.IsAny<ulong>(), It.IsAny<int>())).ReturnsAsync(new List<SemenSample>());
        await _service.GetChangesAsync(null, null);
        _repository.Verify(r => r.GetChangesAsync(0UL, SyncPaging.DefaultLimit + 1), Times.Once);
    }

    [Fact]
    public async Task GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var fetched = new List<SemenSample>
        {
            BuildSample(createdAt, rowVersion: 10, id: 1),
            BuildSample(createdAt, rowVersion: 11, id: 2),
            BuildSample(createdAt, rowVersion: 12, id: 3)
        };
        _repository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }

    [Fact]
    public async Task GetChangesAsync_FillsAvailableDosesForPageItems()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var fetched = new List<SemenSample>
        {
            BuildSample(createdAt, rowVersion: 10, id: 1),
            BuildSample(createdAt, rowVersion: 11, id: 2)
        };
        _repository.Setup(r => r.GetChangesAsync(0UL, SyncPaging.DefaultLimit + 1)).ReturnsAsync(fetched);
        _repository.Setup(r => r.GetAvailableDosesBatchAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, int> { [1] = 8 });
        var result = (await _service.GetChangesAsync(null, null)).Items.ToList();
        Assert.Equal(8, result[0].AvailableDoses);
        Assert.Equal(0, result[1].AvailableDoses);
    }
}
