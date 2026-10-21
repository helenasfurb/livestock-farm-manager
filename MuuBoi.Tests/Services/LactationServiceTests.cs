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

public class LactationServiceTests
{
    private readonly Mock<ILactationRepository> _repository = new();
    private readonly Mock<IAnimalRepository> _animalRepository = new();
    private readonly LactationService _service;

    public LactationServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<LactationProfile>()).CreateMapper();
        _service = new LactationService(_repository.Object, _animalRepository.Object, mapper);
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(BuildAnimal());
        _repository.Setup(r => r.CreateAsync(It.IsAny<Lactation>())).ReturnsAsync((Lactation l) => l);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Lactation>())).ReturnsAsync((Lactation l) => l);
    }

    private static Animal BuildAnimal(int id = 1, bool isActive = true, AnimalClassification classification = AnimalClassification.Cow)
    {
        return new Animal
        {
            Id = id,
            TagNumber = "123456",
            Gender = classification == AnimalClassification.Steer ? AnimalGender.M : AnimalGender.F,
            Classification = classification,
            IsActive = isActive
        };
    }

    private static Lactation BuildLactation(DateTime createdAt, DateTime? updatedAt = null, bool isActive = true, DateTime? endDate = null, ulong rowVersion = 1, int id = 1)
    {
        return new Lactation
        {
            Id = id,
            SyncId = Guid.NewGuid(),
            AnimalId = 1,
            StartDate = createdAt.AddDays(-30),
            EndDate = endDate,
            Origin = LactationOrigin.InitialSeed,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    private static LactationCreateDto BuildCreateDto(Guid? syncId = null) =>
        new() { SyncId = syncId, StartDate = DateTime.UtcNow.AddDays(-10) };

    [Fact]
    public async Task CreateAsync_WithNewSyncId_CreatesLactationWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync((Lactation?)null);
        var result = await _service.CreateAsync(1, BuildCreateDto(syncId));
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.CreateAsync(It.Is<Lactation>(l => l.SyncId == syncId && l.AnimalId == 1)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = BuildLactation(DateTime.UtcNow, id: 7);
        existing.SyncId = syncId;
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var result = await _service.CreateAsync(1, BuildCreateDto(syncId));
        Assert.Equal(7, result.Id);
        _repository.Verify(r => r.CreateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_DoesNotLoadAnimal()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(BuildLactation(DateTime.UtcNow, id: 7));
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
    public async Task CreateAsync_WhenOpenLactationIsSameSyncId_ReturnsExisting()
    {
        var syncId = Guid.NewGuid();
        var concurrent = BuildLactation(DateTime.UtcNow, id: 7);
        concurrent.SyncId = syncId;
        _repository.SetupSequence(r => r.GetBySyncIdAsync(syncId))
            .ReturnsAsync((Lactation?)null)
            .ReturnsAsync(concurrent);
        _repository.Setup(r => r.HasOpenByAnimalIdAsync(1)).ReturnsAsync(true);
        var result = await _service.CreateAsync(1, BuildCreateDto(syncId));
        Assert.Equal(7, result.Id);
        _repository.Verify(r => r.CreateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenAnotherLactationIsOpen_ThrowsConflictException()
    {
        _repository.Setup(r => r.HasOpenByAnimalIdAsync(1)).ReturnsAsync(true);
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(1, BuildCreateDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenAnimalInactive_ThrowsConflictException()
    {
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(2)).ReturnsAsync(BuildAnimal(2, isActive: false));
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(2, BuildCreateDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenAnimalIsMale_ThrowsBusinessRuleException()
    {
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(3)).ReturnsAsync(BuildAnimal(3, classification: AnimalClassification.Steer));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAsync(3, BuildCreateDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var lactation = BuildLactation(DateTime.UtcNow.AddDays(-2));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lactation);
        var editedAt = DateTime.UtcNow.AddHours(-1);
        var newStart = DateTime.UtcNow.AddDays(-40);
        var result = await _service.UpdateAsync(1, new LactationUpdateDto { StartDate = newStart, UpdatedAt = editedAt });
        Assert.Equal(newStart, result.StartDate);
        Assert.Equal(editedAt, lactation.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(lactation), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var serverUpdatedAt = DateTime.UtcNow.AddHours(-1);
        var lactation = BuildLactation(DateTime.UtcNow.AddDays(-2), serverUpdatedAt);
        var originalStart = lactation.StartDate;
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lactation);
        var result = await _service.UpdateAsync(1, new LactationUpdateDto { StartDate = DateTime.UtcNow.AddDays(-90), UpdatedAt = serverUpdatedAt.AddMinutes(-30) });
        Assert.Equal(originalStart, result.StartDate);
        Assert.Equal(serverUpdatedAt, lactation.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenInactive_ReturnsWithoutUpdating()
    {
        var lactation = BuildLactation(DateTime.UtcNow.AddDays(-2), isActive: false);
        var originalStart = lactation.StartDate;
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lactation);
        var result = await _service.UpdateAsync(1, new LactationUpdateDto { StartDate = DateTime.UtcNow.AddDays(-90) });
        Assert.False(result.IsActive);
        Assert.Equal(originalStart, lactation.StartDate);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithStartDateAfterEndDate_ThrowsBusinessRuleException()
    {
        var lactation = BuildLactation(DateTime.UtcNow.AddDays(-2), endDate: DateTime.UtcNow.AddDays(-5));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lactation);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateAsync(1, new LactationUpdateDto { StartDate = DateTime.UtcNow.AddDays(-1) }));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task DryOffAsync_WhenOpen_ClosesWithoutChangingUpdatedAt()
    {
        var updatedAt = DateTime.UtcNow.AddDays(-1);
        var lactation = BuildLactation(DateTime.UtcNow.AddDays(-2), updatedAt);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lactation);
        var endDate = DateTime.UtcNow.AddHours(-2);
        var result = await _service.DryOffAsync(1, new LactationDryOffDto { EndDate = endDate, DryOffNotes = "Secagem" });
        Assert.Equal(endDate, result.EndDate);
        Assert.Equal("Secagem", lactation.DryOffNotes);
        Assert.Equal(updatedAt, lactation.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(lactation), Times.Once);
    }

    [Fact]
    public async Task DryOffAsync_WhenAlreadyDry_ReturnsWithoutUpdating()
    {
        var endDate = DateTime.UtcNow.AddDays(-5);
        var lactation = BuildLactation(DateTime.UtcNow.AddDays(-2), endDate: endDate);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lactation);
        var result = await _service.DryOffAsync(1, new LactationDryOffDto { EndDate = DateTime.UtcNow.AddDays(-1) });
        Assert.Equal(endDate, result.EndDate);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task DryOffAsync_WhenInactive_ReturnsWithoutUpdating()
    {
        var lactation = BuildLactation(DateTime.UtcNow.AddDays(-2), isActive: false);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lactation);
        var result = await _service.DryOffAsync(1, new LactationDryOffDto { EndDate = DateTime.UtcNow.AddDays(-1) });
        Assert.False(result.IsActive);
        Assert.Null(lactation.EndDate);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task DryOffAsync_WithEndDateBeforeStartDate_ThrowsBusinessRuleException()
    {
        var lactation = BuildLactation(DateTime.UtcNow.AddDays(-2));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lactation);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.DryOffAsync(1, new LactationDryOffDto { EndDate = lactation.StartDate.AddDays(-1) }));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task UndoDryOffAsync_WhenAlreadyOpen_ReturnsWithoutUpdating()
    {
        var lactation = BuildLactation(DateTime.UtcNow.AddDays(-2));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lactation);
        var result = await _service.UndoDryOffAsync(1);
        Assert.Null(result.EndDate);
        _repository.Verify(r => r.HasOpenByAnimalIdAsync(It.IsAny<int>()), Times.Never);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task UndoDryOffAsync_WhenAnotherLactationIsOpen_ThrowsConflictException()
    {
        var lactation = BuildLactation(DateTime.UtcNow.AddDays(-2), endDate: DateTime.UtcNow.AddDays(-5));
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lactation);
        _repository.Setup(r => r.HasOpenByAnimalIdAsync(1)).ReturnsAsync(true);
        await Assert.ThrowsAsync<ConflictException>(() => _service.UndoDryOffAsync(1));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating()
    {
        var lactation = BuildLactation(DateTime.UtcNow.AddDays(-2), isActive: false);
        _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lactation);
        var result = await _service.DeactivateAsync(1);
        Assert.True(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Lactation>()), Times.Never);
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
        var fetched = new List<Lactation>
        {
            BuildLactation(createdAt, rowVersion: 10),
            BuildLactation(createdAt, rowVersion: 11),
            BuildLactation(createdAt, rowVersion: 12)
        };
        _repository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        _animalRepository.Setup(r => r.GetTagNumbersByIdsAsync(It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync(new Dictionary<int, string?>());
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }

    [Fact]
    public async Task GetChangesAsync_FillsAnimalTagNumberForPageItems()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var other = BuildLactation(createdAt, rowVersion: 11, id: 2);
        other.AnimalId = 2;
        var fetched = new List<Lactation> { BuildLactation(createdAt, rowVersion: 10), other };
        _repository.Setup(r => r.GetChangesAsync(0UL, SyncPaging.DefaultLimit + 1)).ReturnsAsync(fetched);
        _animalRepository.Setup(r => r.GetTagNumbersByIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new Dictionary<int, string?> { [1] = "123456" });
        var result = (await _service.GetChangesAsync(null, null)).Items.ToList();
        Assert.Equal("123456", result[0].AnimalTagNumber);
        Assert.Null(result[1].AnimalTagNumber);
        Assert.True(result[0].IsLactating);
    }
}
