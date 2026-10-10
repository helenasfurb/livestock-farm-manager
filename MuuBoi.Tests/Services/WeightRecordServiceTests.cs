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

public class WeightRecordServiceTests
{
    private readonly Mock<IWeightRecordRepository> _repository = new();
    private readonly Mock<IAnimalRepository> _animalRepository = new();
    private readonly WeightRecordService _service;

    public WeightRecordServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<WeightRecordProfile>()).CreateMapper();
        _service = new WeightRecordService(_repository.Object, _animalRepository.Object, mapper);
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(new Animal { Id = 1 });
        _repository.Setup(r => r.CreateWeightRecordAsync(It.IsAny<WeightRecord>())).ReturnsAsync((WeightRecord w) => w);
        _repository.Setup(r => r.UpdateWeightRecordAsync(It.IsAny<WeightRecord>())).ReturnsAsync((WeightRecord w) => w);
    }

    private static WeightRecord BuildRecord(DateTime createdAt, DateTime? updatedAt = null, bool isActive = true, ulong rowVersion = 1)
    {
        return new WeightRecord
        {
            Id = 1,
            SyncId = Guid.NewGuid(),
            AnimalId = 1,
            Weight = 350,
            RecordedAt = createdAt,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    [Fact]
    public async Task CreateWeightRecordAsync_WithNewSyncId_CreatesRecordWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetWeightRecordBySyncIdAsync(syncId)).ReturnsAsync((WeightRecord?)null);
        var result = await _service.CreateWeightRecordAsync(new WeightRecordCreateDto { SyncId = syncId, Weight = 360 }, 1);
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.CreateWeightRecordAsync(It.Is<WeightRecord>(w => w.SyncId == syncId && w.AnimalId == 1)), Times.Once);
    }

    [Fact]
    public async Task CreateWeightRecordAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = new WeightRecord { Id = 7, SyncId = syncId, AnimalId = 1, Weight = 360, CreatedAt = DateTime.UtcNow };
        _repository.Setup(r => r.GetWeightRecordBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var result = await _service.CreateWeightRecordAsync(new WeightRecordCreateDto { SyncId = syncId, Weight = 999 }, 1);
        Assert.Equal(7, result.Id);
        Assert.Equal(360, result.Weight);
        _repository.Verify(r => r.CreateWeightRecordAsync(It.IsAny<WeightRecord>()), Times.Never);
    }

    [Fact]
    public async Task CreateWeightRecordAsync_WithExistingSyncId_DoesNotLoadAnimal()
    {
        var syncId = Guid.NewGuid();
        var existing = new WeightRecord { Id = 7, SyncId = syncId, AnimalId = 1, Weight = 360, CreatedAt = DateTime.UtcNow };
        _repository.Setup(r => r.GetWeightRecordBySyncIdAsync(syncId)).ReturnsAsync(existing);
        await _service.CreateWeightRecordAsync(new WeightRecordCreateDto { SyncId = syncId, Weight = 360 }, 999);
        _animalRepository.Verify(r => r.GetAnimalByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateWeightRecordAsync_WithoutSyncId_GeneratesSyncId()
    {
        var result = await _service.CreateWeightRecordAsync(new WeightRecordCreateDto { Weight = 360 }, 1);
        Assert.NotEqual(Guid.Empty, result.SyncId);
        _repository.Verify(r => r.GetWeightRecordBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CreateWeightRecordAsync_WithoutWeightDate_UsesNow()
    {
        var before = DateTime.UtcNow;
        var result = await _service.CreateWeightRecordAsync(new WeightRecordCreateDto { Weight = 360 }, 1);
        Assert.InRange(result.RecordedAt, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateWeightRecordAsync_WhenAnimalNotFound_ThrowsNotFoundException()
    {
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(999)).ReturnsAsync((Animal?)null);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateWeightRecordAsync(new WeightRecordCreateDto { SyncId = Guid.NewGuid(), Weight = 360 }, 999));
        _repository.Verify(r => r.CreateWeightRecordAsync(It.IsAny<WeightRecord>()), Times.Never);
    }

    [Fact]
    public async Task UpdateWeightRecordAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var record = BuildRecord(DateTime.UtcNow.AddDays(-2));
        _repository.Setup(r => r.GetWeightRecordByIdAsync(1, 1)).ReturnsAsync(record);
        var editedAt = DateTime.UtcNow.AddHours(-1);
        var result = await _service.UpdateWeightRecordAsync(1, 1, new WeightRecordUpdateDto { Weight = 365, UpdatedAt = editedAt });
        Assert.Equal(365, result.Weight);
        Assert.Equal(editedAt, record.UpdatedAt);
        _repository.Verify(r => r.UpdateWeightRecordAsync(record), Times.Once);
    }

    [Fact]
    public async Task UpdateWeightRecordAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var serverUpdatedAt = DateTime.UtcNow.AddHours(-1);
        var record = BuildRecord(DateTime.UtcNow.AddDays(-2), serverUpdatedAt);
        _repository.Setup(r => r.GetWeightRecordByIdAsync(1, 1)).ReturnsAsync(record);
        var result = await _service.UpdateWeightRecordAsync(1, 1, new WeightRecordUpdateDto { Weight = 999, UpdatedAt = serverUpdatedAt.AddMinutes(-30) });
        Assert.Equal(350, result.Weight);
        Assert.Equal(serverUpdatedAt, record.UpdatedAt);
        _repository.Verify(r => r.UpdateWeightRecordAsync(It.IsAny<WeightRecord>()), Times.Never);
    }

    [Fact]
    public async Task UpdateWeightRecordAsync_WithoutClientUpdatedAt_UsesNow()
    {
        var record = BuildRecord(DateTime.UtcNow.AddDays(-2));
        _repository.Setup(r => r.GetWeightRecordByIdAsync(1, 1)).ReturnsAsync(record);
        var before = DateTime.UtcNow;
        await _service.UpdateWeightRecordAsync(1, 1, new WeightRecordUpdateDto { Weight = 365 });
        Assert.InRange(record.UpdatedAt!.Value, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task UpdateWeightRecordAsync_WhenNeverEdited_ComparesWithCreatedAt()
    {
        var createdAt = DateTime.UtcNow.AddHours(-1);
        var record = BuildRecord(createdAt);
        _repository.Setup(r => r.GetWeightRecordByIdAsync(1, 1)).ReturnsAsync(record);
        await _service.UpdateWeightRecordAsync(1, 1, new WeightRecordUpdateDto { Weight = 999, UpdatedAt = createdAt.AddMinutes(-5) });
        Assert.Equal(350, record.Weight);
        _repository.Verify(r => r.UpdateWeightRecordAsync(It.IsAny<WeightRecord>()), Times.Never);
    }

    [Fact]
    public async Task UpdateWeightRecordAsync_WhenRecordNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetWeightRecordByIdAsync(999, 1)).ReturnsAsync((WeightRecord?)null);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateWeightRecordAsync(999, 1, new WeightRecordUpdateDto { Weight = 365 }));
    }

    [Fact]
    public async Task DeleteWeightRecordAsync_WhenActive_DeactivatesAndReturnsTrue()
    {
        var record = BuildRecord(DateTime.UtcNow.AddDays(-1));
        _repository.Setup(r => r.GetWeightRecordByIdAsync(1, 1)).ReturnsAsync(record);
        var result = await _service.DeleteWeightRecordAsync(1, 1);
        Assert.True(result);
        _repository.Verify(r => r.DeleteWeightRecordAsync(1, 1), Times.Once);
    }

    [Fact]
    public async Task DeleteWeightRecordAsync_WhenAlreadyInactive_ReturnsTrueWithoutDeleting()
    {
        var record = BuildRecord(DateTime.UtcNow.AddDays(-1), isActive: false);
        _repository.Setup(r => r.GetWeightRecordByIdAsync(1, 1)).ReturnsAsync(record);
        var result = await _service.DeleteWeightRecordAsync(1, 1);
        Assert.True(result);
        _repository.Verify(r => r.DeleteWeightRecordAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteWeightRecordAsync_WhenRecordNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetWeightRecordByIdAsync(999, 1)).ReturnsAsync((WeightRecord?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteWeightRecordAsync(999, 1));
        _repository.Verify(r => r.DeleteWeightRecordAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
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
        var fetched = new List<WeightRecord>
        {
            BuildRecord(createdAt, rowVersion: 10),
            BuildRecord(createdAt, rowVersion: 11),
            BuildRecord(createdAt, rowVersion: 12)
        };
        _repository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }
}
