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

public class SemenSampleMovementServiceTests
{
    private readonly Mock<ISemenSampleMovementRepository> _repository = new();
    private readonly Mock<ISemenSampleRepository> _semenSampleRepository = new();
    private readonly SemenSampleMovementService _service;

    public SemenSampleMovementServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<SemenSampleMovementProfile>()).CreateMapper();
        _service = new SemenSampleMovementService(_repository.Object, _semenSampleRepository.Object, mapper);
        _repository.Setup(r => r.CreateAsync(It.IsAny<SemenSampleMovement>())).ReturnsAsync((SemenSampleMovement m) => m);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<SemenSampleMovement>())).ReturnsAsync((SemenSampleMovement m) => m);
        _semenSampleRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new SemenSample { Id = 1, Name = "Touro A", IsActive = true });
        _semenSampleRepository.Setup(r => r.GetNamesByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, string>());
    }

    private static SemenSampleMovement BuildMovement(DateTime createdAt, DateTime? updatedAt = null, bool isActive = true, int? breedingEventId = null, ulong rowVersion = 1, int id = 1)
    {
        return new SemenSampleMovement
        {
            Id = id,
            SyncId = Guid.NewGuid(),
            SemenSampleId = 1,
            MovementType = SemenMovementType.Input,
            MovementDate = createdAt.Date,
            Quantity = 5,
            BreedingEventId = breedingEventId,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    private static SemenSampleMovementCreateDto BuildCreateDto(Guid? syncId = null)
    {
        return new SemenSampleMovementCreateDto
        {
            SyncId = syncId,
            MovementType = SemenMovementType.Input,
            MovementDate = DateTime.UtcNow.Date,
            Quantity = 5
        };
    }

    [Fact]
    public async Task CreateAsync_WithNewSyncId_CreatesMovementWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync((SemenSampleMovement?)null);
        var result = await _service.CreateAsync(1, BuildCreateDto(syncId));
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.CreateAsync(It.Is<SemenSampleMovement>(m => m.SyncId == syncId && m.SemenSampleId == 1)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = BuildMovement(DateTime.UtcNow, id: 9);
        existing.SyncId = syncId;
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var dto = BuildCreateDto(syncId);
        dto.Quantity = 99;
        var result = await _service.CreateAsync(1, dto);
        Assert.Equal(9, result.Id);
        Assert.Equal(5, result.Quantity);
        _repository.Verify(r => r.CreateAsync(It.IsAny<SemenSampleMovement>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncIdAndInactiveSample_ReturnsExisting()
    {
        var syncId = Guid.NewGuid();
        var existing = BuildMovement(DateTime.UtcNow, id: 9);
        existing.SyncId = syncId;
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        _semenSampleRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new SemenSample { Id = 1, Name = "Touro A", IsActive = false });
        var result = await _service.CreateAsync(1, BuildCreateDto(syncId));
        Assert.Equal(9, result.Id);
        _repository.Verify(r => r.CreateAsync(It.IsAny<SemenSampleMovement>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithoutSyncId_GeneratesSyncId()
    {
        var result = await _service.CreateAsync(1, BuildCreateDto());
        Assert.NotEqual(Guid.Empty, result.SyncId);
        _repository.Verify(r => r.GetBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
        _repository.Verify(r => r.CreateAsync(It.Is<SemenSampleMovement>(m => m.SyncId != Guid.Empty)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenSampleNotFound_ThrowsNotFoundException()
    {
        _semenSampleRepository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((SemenSample?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(99, BuildCreateDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<SemenSampleMovement>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenSampleInactive_ThrowsConflictException()
    {
        _semenSampleRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new SemenSample { Id = 1, Name = "Touro A", IsActive = false });
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(1, BuildCreateDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<SemenSampleMovement>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var movement = BuildMovement(now.AddDays(-2), now.AddHours(-1));
        var clientUpdatedAt = now.AddMinutes(-10);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(movement);
        var result = await _service.UpdateAsync(1, 1, new SemenSampleMovementUpdateDto { Quantity = 8, UpdatedAt = clientUpdatedAt });
        Assert.Equal(8, result.Quantity);
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
        var result = await _service.UpdateAsync(1, 1, new SemenSampleMovementUpdateDto { Quantity = 8, UpdatedAt = now.AddHours(-3) });
        Assert.Equal(5, result.Quantity);
        Assert.Equal(serverUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSampleMovement>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithoutClientUpdatedAt_UsesNow()
    {
        var before = DateTime.UtcNow;
        var movement = BuildMovement(before.AddDays(-2), before.AddHours(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(movement);
        var result = await _service.UpdateAsync(1, 1, new SemenSampleMovementUpdateDto { Quantity = 8 });
        var after = DateTime.UtcNow;
        Assert.Equal(8, result.Quantity);
        Assert.InRange(result.UpdatedAt!.Value, before, after);
    }

    [Fact]
    public async Task UpdateAsync_WhenNeverEdited_ComparesWithCreatedAt()
    {
        var now = DateTime.UtcNow;
        var movement = BuildMovement(now.AddHours(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(movement);
        var result = await _service.UpdateAsync(1, 1, new SemenSampleMovementUpdateDto { Quantity = 8, UpdatedAt = now.AddHours(-2) });
        Assert.Equal(5, result.Quantity);
        Assert.Null(result.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSampleMovement>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenSystemGenerated_ThrowsConflictException()
    {
        var movement = BuildMovement(DateTime.UtcNow.AddDays(-1), breedingEventId: 3);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(movement);
        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(1, 1, new SemenSampleMovementUpdateDto { Quantity = 8, UpdatedAt = DateTime.UtcNow }));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSampleMovement>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenMovementNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((SemenSampleMovement?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(1, 99, new SemenSampleMovementUpdateDto { Quantity = 8 }));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSampleMovement>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_WhenActive_Deactivates()
    {
        var movement = BuildMovement(DateTime.UtcNow.AddDays(-1));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(movement);
        await _service.DeactivateAsync(1, 1);
        _repository.Verify(r => r.UpdateAsync(It.Is<SemenSampleMovement>(m => !m.IsActive)), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_WhenAlreadyInactive_ReturnsWithoutUpdating()
    {
        var movement = BuildMovement(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddHours(-1), isActive: false);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(movement);
        await _service.DeactivateAsync(1, 1);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSampleMovement>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_WhenSystemGenerated_ThrowsConflictException()
    {
        var movement = BuildMovement(DateTime.UtcNow.AddDays(-1), breedingEventId: 3);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(movement);
        await Assert.ThrowsAsync<ConflictException>(() => _service.DeactivateAsync(1, 1));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSampleMovement>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_WhenSystemGeneratedAndInactive_ThrowsConflictException()
    {
        var movement = BuildMovement(DateTime.UtcNow.AddDays(-1), isActive: false, breedingEventId: 3);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(movement);
        await Assert.ThrowsAsync<ConflictException>(() => _service.DeactivateAsync(1, 1));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SemenSampleMovement>()), Times.Never);
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
        var fetched = new List<SemenSampleMovement>
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
    public async Task GetChangesAsync_FillsSemenSampleNameForPageItems()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var other = BuildMovement(createdAt, rowVersion: 11, id: 2);
        other.SemenSampleId = 2;
        var fetched = new List<SemenSampleMovement> { BuildMovement(createdAt, rowVersion: 10, id: 1), other };
        _repository.Setup(r => r.GetChangesAsync(0UL, SyncPaging.DefaultLimit + 1)).ReturnsAsync(fetched);
        _semenSampleRepository.Setup(r => r.GetNamesByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, string> { [1] = "Touro A" });
        var result = (await _service.GetChangesAsync(null, null)).Items.ToList();
        Assert.Equal("Touro A", result[0].SemenSampleName);
        Assert.Equal(string.Empty, result[1].SemenSampleName);
    }
}
