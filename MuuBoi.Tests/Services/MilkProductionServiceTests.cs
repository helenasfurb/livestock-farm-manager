using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Moq;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Mappings;
using MuuBoi.Application.Services;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Tests.Services;

public class MilkProductionServiceTests
{
    private readonly Mock<IMilkProductionRepository> _repository = new();
    private readonly MilkProductionService _service;

    public MilkProductionServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MilkProductionProfile>()).CreateMapper();
        _service = new MilkProductionService(_repository.Object, mapper);
        _repository.Setup(r => r.CreateAsync(It.IsAny<MilkProduction>())).ReturnsAsync((MilkProduction p) => p);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<MilkProduction>())).ReturnsAsync((MilkProduction p) => p);
    }

    private static MilkProduction BuildProduction(DateTime createdAt, DateTime? updatedAt = null, bool isActive = true, ulong rowVersion = 1)
    {
        return new MilkProduction
        {
            Id = 1,
            SyncId = Guid.NewGuid(),
            Date = createdAt.Date,
            Volume = 100m,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    [Fact]
    public async Task CreateAsync_WithNewSyncId_CreatesProductionWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync((MilkProduction?)null);
        var result = await _service.CreateAsync(new MilkProductionCreateDto { SyncId = syncId, Date = DateTime.UtcNow.Date, Volume = 120m });
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.CreateAsync(It.Is<MilkProduction>(p => p.SyncId == syncId)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = new MilkProduction { Id = 7, SyncId = syncId, Date = DateTime.UtcNow.Date, Volume = 120m, CreatedAt = DateTime.UtcNow };
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var result = await _service.CreateAsync(new MilkProductionCreateDto { SyncId = syncId, Date = DateTime.UtcNow.Date, Volume = 999m });
        Assert.Equal(7, result.Id);
        Assert.Equal(120m, result.Volume);
        _repository.Verify(r => r.CreateAsync(It.IsAny<MilkProduction>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithoutSyncId_GeneratesSyncId()
    {
        var result = await _service.CreateAsync(new MilkProductionCreateDto { Date = DateTime.UtcNow.Date, Volume = 95m });
        Assert.NotEqual(Guid.Empty, result.SyncId);
        _repository.Verify(r => r.GetBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
        _repository.Verify(r => r.CreateAsync(It.Is<MilkProduction>(p => p.SyncId != Guid.Empty)), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var production = BuildProduction(now.AddDays(-2), now.AddHours(-1));
        var clientUpdatedAt = now.AddMinutes(-10);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(production);
        var result = await _service.UpdateAsync(1, new MilkProductionUpdateDto { Volume = 200m, UpdatedAt = clientUpdatedAt });
        Assert.Equal(200m, result.Volume);
        Assert.Equal(clientUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(production), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var production = BuildProduction(now.AddDays(-2), serverUpdatedAt);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(production);
        var result = await _service.UpdateAsync(1, new MilkProductionUpdateDto { Volume = 200m, UpdatedAt = now.AddHours(-3) });
        Assert.Equal(100m, result.Volume);
        Assert.Equal(serverUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<MilkProduction>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithSameClientUpdatedAt_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var production = BuildProduction(now.AddDays(-2), serverUpdatedAt);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(production);
        var result = await _service.UpdateAsync(1, new MilkProductionUpdateDto { Volume = 200m, UpdatedAt = serverUpdatedAt });
        Assert.Equal(200m, result.Volume);
        Assert.Equal(serverUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(production), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithFutureClientUpdatedAt_ClampsToNow()
    {
        var before = DateTime.UtcNow;
        var production = BuildProduction(before.AddDays(-2), before.AddHours(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(production);
        var result = await _service.UpdateAsync(1, new MilkProductionUpdateDto { Volume = 200m, UpdatedAt = before.AddDays(1) });
        var after = DateTime.UtcNow;
        Assert.Equal(200m, result.Volume);
        Assert.InRange(result.UpdatedAt!.Value, before, after);
    }

    [Fact]
    public async Task UpdateAsync_WithOffsetClientUpdatedAt_ConvertsToUtc()
    {
        var now = DateTime.UtcNow;
        var production = BuildProduction(now.AddDays(-2), now.AddHours(-1));
        var clientUtc = now.AddMinutes(-10);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(production);
        var result = await _service.UpdateAsync(1, new MilkProductionUpdateDto { Volume = 200m, UpdatedAt = clientUtc.ToLocalTime() });
        Assert.Equal(200m, result.Volume);
        Assert.Equal(clientUtc, result.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, result.UpdatedAt!.Value.Kind);
    }

    [Fact]
    public async Task UpdateAsync_WithoutClientUpdatedAt_UsesNow()
    {
        var before = DateTime.UtcNow;
        var production = BuildProduction(before.AddDays(-2), before.AddHours(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(production);
        var result = await _service.UpdateAsync(1, new MilkProductionUpdateDto { Volume = 200m });
        var after = DateTime.UtcNow;
        Assert.Equal(200m, result.Volume);
        Assert.InRange(result.UpdatedAt!.Value, before, after);
    }

    [Fact]
    public async Task UpdateAsync_WhenNeverEdited_ComparesWithCreatedAt()
    {
        var now = DateTime.UtcNow;
        var production = BuildProduction(now.AddHours(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(production);
        var result = await _service.UpdateAsync(1, new MilkProductionUpdateDto { Volume = 200m, UpdatedAt = now.AddHours(-2) });
        Assert.Equal(100m, result.Volume);
        Assert.Null(result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<MilkProduction>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenProductionNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((MilkProduction?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(99, new MilkProductionUpdateDto { Volume = 200m }));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<MilkProduction>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_WhenActive_DeactivatesAndReturnsTrue()
    {
        var production = BuildProduction(DateTime.UtcNow.AddDays(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(production);
        var result = await _service.DeactivateAsync(1);
        Assert.True(result);
        Assert.False(production.IsActive);
        _repository.Verify(r => r.UpdateAsync(production), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var production = BuildProduction(DateTime.UtcNow.AddDays(-1), updatedAt, isActive: false);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(production);
        var result = await _service.DeactivateAsync(1);
        Assert.True(result);
        Assert.Equal(updatedAt, production.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<MilkProduction>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_WhenProductionNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((MilkProduction?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeactivateAsync(99));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<MilkProduction>()), Times.Never);
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
        _repository.Setup(r => r.GetChangesAsync(It.IsAny<ulong>(), It.IsAny<int>())).ReturnsAsync(new List<MilkProduction>());
        await _service.GetChangesAsync(null, null);
        _repository.Verify(r => r.GetChangesAsync(0UL, SyncPaging.DefaultLimit + 1), Times.Once);
    }

    [Fact]
    public async Task GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var fetched = new List<MilkProduction>
        {
            BuildProduction(createdAt, rowVersion: 10),
            BuildProduction(createdAt, rowVersion: 11),
            BuildProduction(createdAt, rowVersion: 12)
        };
        _repository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }

    [Fact]
    public async Task GetChangesAsync_WhenNoChanges_KeepsReceivedCursor()
    {
        _repository.Setup(r => r.GetChangesAsync(38027UL, It.IsAny<int>())).ReturnsAsync(new List<MilkProduction>());
        var result = await _service.GetChangesAsync("38027", null);
        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
        Assert.Equal("38027", result.NextCursor);
    }

    [Fact]
    public async Task GetChangesAsync_WithLimitAboveMax_RequestsMaxPlusOne()
    {
        _repository.Setup(r => r.GetChangesAsync(It.IsAny<ulong>(), It.IsAny<int>())).ReturnsAsync(new List<MilkProduction>());
        await _service.GetChangesAsync(null, 100000);
        _repository.Verify(r => r.GetChangesAsync(0UL, SyncPaging.MaxLimit + 1), Times.Once);
    }
}
