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

public class BodyConditionRecordServiceTests
{
    private readonly Mock<IBodyConditionRecordRepository> _repository = new();
    private readonly Mock<IAnimalRepository> _animalRepository = new();
    private readonly BodyConditionRecordService _service;

    public BodyConditionRecordServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<BodyConditionRecordProfile>()).CreateMapper();
        _service = new BodyConditionRecordService(_repository.Object, _animalRepository.Object, mapper);
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(new Animal { Id = 1, IsActive = true });
        _repository.Setup(r => r.CreateAsync(It.IsAny<BodyConditionRecord>())).ReturnsAsync((BodyConditionRecord r) => r);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<BodyConditionRecord>())).ReturnsAsync((BodyConditionRecord r) => r);
    }

    private static BodyConditionRecord BuildRecord(DateTime createdAt, DateTime? updatedAt = null, ulong rowVersion = 1)
    {
        return new BodyConditionRecord
        {
            Id = 1,
            SyncId = Guid.NewGuid(),
            AnimalId = 1,
            Score = BodyConditionScore.Ideal,
            RecordedAt = createdAt,
            IsActive = true,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    private static BodyConditionRecordCreateDto BuildCreateDto(Guid? syncId = null)
    {
        return new BodyConditionRecordCreateDto
        {
            SyncId = syncId,
            Score = BodyConditionScore.Ideal,
            RecordedAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    [Fact]
    public async Task CreateAsync_WithNewSyncId_CreatesRecordWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync((BodyConditionRecord?)null);
        var result = await _service.CreateAsync(1, BuildCreateDto(syncId));
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.CreateAsync(It.Is<BodyConditionRecord>(x => x.SyncId == syncId && x.AnimalId == 1)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = new BodyConditionRecord { Id = 7, SyncId = syncId, AnimalId = 1, Score = BodyConditionScore.Thin, CreatedAt = DateTime.UtcNow };
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var dto = BuildCreateDto(syncId);
        dto.Score = BodyConditionScore.SeverelyObese;
        var result = await _service.CreateAsync(1, dto);
        Assert.Equal(7, result.Id);
        Assert.Equal(BodyConditionScore.Thin, result.Score);
        _repository.Verify(r => r.CreateAsync(It.IsAny<BodyConditionRecord>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_DoesNotLoadAnimal()
    {
        var syncId = Guid.NewGuid();
        var existing = new BodyConditionRecord { Id = 7, SyncId = syncId, AnimalId = 1, Score = BodyConditionScore.Thin, CreatedAt = DateTime.UtcNow };
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        await _service.CreateAsync(999, BuildCreateDto(syncId));
        _animalRepository.Verify(r => r.GetAnimalByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithoutSyncId_GeneratesSyncId()
    {
        var result = await _service.CreateAsync(1, BuildCreateDto());
        Assert.NotEqual(Guid.Empty, result.SyncId);
        _repository.Verify(r => r.GetBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenAnimalNotFound_ThrowsNotFoundException()
    {
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(999)).ReturnsAsync((Animal?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(999, BuildCreateDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<BodyConditionRecord>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenAnimalInactive_ThrowsConflictException()
    {
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(2)).ReturnsAsync(new Animal { Id = 2, IsActive = false });
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(2, BuildCreateDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<BodyConditionRecord>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithFutureDate_ThrowsBusinessRuleException()
    {
        var dto = BuildCreateDto();
        dto.RecordedAt = DateTime.UtcNow.AddDays(1);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAsync(1, dto));
        _repository.Verify(r => r.CreateAsync(It.IsAny<BodyConditionRecord>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var record = BuildRecord(DateTime.UtcNow.AddDays(-2));
        _repository.Setup(r => r.GetByIdAsync(1, 1)).ReturnsAsync(record);
        var editedAt = DateTime.UtcNow.AddHours(-1);
        var result = await _service.UpdateAsync(1, 1, new BodyConditionRecordUpdateDto { Score = BodyConditionScore.Fleshy, UpdatedAt = editedAt });
        Assert.Equal(BodyConditionScore.Fleshy, result.Score);
        Assert.Equal(editedAt, record.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(record), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var serverUpdatedAt = DateTime.UtcNow.AddHours(-1);
        var record = BuildRecord(DateTime.UtcNow.AddDays(-2), serverUpdatedAt);
        _repository.Setup(r => r.GetByIdAsync(1, 1)).ReturnsAsync(record);
        var result = await _service.UpdateAsync(1, 1, new BodyConditionRecordUpdateDto { Score = BodyConditionScore.SeverelyThin, UpdatedAt = serverUpdatedAt.AddMinutes(-30) });
        Assert.Equal(BodyConditionScore.Ideal, result.Score);
        Assert.Equal(serverUpdatedAt, record.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<BodyConditionRecord>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithoutClientUpdatedAt_UsesNow()
    {
        var record = BuildRecord(DateTime.UtcNow.AddDays(-2));
        _repository.Setup(r => r.GetByIdAsync(1, 1)).ReturnsAsync(record);
        var before = DateTime.UtcNow;
        await _service.UpdateAsync(1, 1, new BodyConditionRecordUpdateDto { Score = BodyConditionScore.Fleshy });
        Assert.InRange(record.UpdatedAt!.Value, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task UpdateAsync_WhenNeverEdited_ComparesWithCreatedAt()
    {
        var createdAt = DateTime.UtcNow.AddHours(-1);
        var record = BuildRecord(createdAt);
        _repository.Setup(r => r.GetByIdAsync(1, 1)).ReturnsAsync(record);
        await _service.UpdateAsync(1, 1, new BodyConditionRecordUpdateDto { Score = BodyConditionScore.SeverelyThin, UpdatedAt = createdAt.AddMinutes(-5) });
        Assert.Equal(BodyConditionScore.Ideal, record.Score);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<BodyConditionRecord>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithFutureDate_ThrowsBusinessRuleException()
    {
        var record = BuildRecord(DateTime.UtcNow.AddDays(-2));
        _repository.Setup(r => r.GetByIdAsync(1, 1)).ReturnsAsync(record);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateAsync(1, 1, new BodyConditionRecordUpdateDto { RecordedAt = DateTime.UtcNow.AddDays(1) }));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<BodyConditionRecord>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenRecordNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(999, 1)).ReturnsAsync((BodyConditionRecord?)null);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(1, 999, new BodyConditionRecordUpdateDto { Score = BodyConditionScore.Fleshy }));
    }

    [Fact]
    public async Task UpdateAsync_WhenAnimalInactive_AppliesChanges()
    {
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(2)).ReturnsAsync(new Animal { Id = 2, IsActive = false });
        var record = BuildRecord(DateTime.UtcNow.AddDays(-2));
        record.AnimalId = 2;
        _repository.Setup(r => r.GetByIdAsync(1, 2)).ReturnsAsync(record);
        var result = await _service.UpdateAsync(2, 1, new BodyConditionRecordUpdateDto { Score = BodyConditionScore.Fleshy });
        Assert.Equal(BodyConditionScore.Fleshy, result.Score);
        _repository.Verify(r => r.UpdateAsync(record), Times.Once);
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
        var fetched = new List<BodyConditionRecord>
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
