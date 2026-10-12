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

public class VaccinationEventServiceTests
{
    private readonly Mock<IVaccinationEventRepository> _repository = new();
    private readonly Mock<IVaccineRepository> _vaccineRepository = new();
    private readonly Mock<IAnimalRepository> _animalRepository = new();
    private readonly Dictionary<int, VaccinationEvent> _store = new();
    private readonly VaccinationEventService _service;
    private int _nextId = 100;

    public VaccinationEventServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<VaccinationEventProfile>()).CreateMapper();
        _service = new VaccinationEventService(_repository.Object, _vaccineRepository.Object, _animalRepository.Object, mapper);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => _store.GetValueOrDefault(id));
        _repository.Setup(r => r.CreateAsync(It.IsAny<VaccinationEvent>())).ReturnsAsync((VaccinationEvent e) => Store(e));
        _repository.Setup(r => r.CreateBoosterAsync(It.IsAny<VaccinationEvent>())).ReturnsAsync((VaccinationEvent e) => Store(e));
        _repository.Setup(r => r.UpdateAsync(It.IsAny<VaccinationEvent>())).ReturnsAsync((VaccinationEvent e) => e);
        _vaccineRepository.Setup(r => r.GetVaccineByIdAsync(51)).ReturnsAsync(new Vaccine { Id = 51, Name = "Brucelose" });
        _animalRepository.Setup(r => r.GetExistingAnimalIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids) => ids.Where(id => id != 999).ToList());
    }

    private VaccinationEvent Store(VaccinationEvent vaccinationEvent)
    {
        if (vaccinationEvent.Id == 0)
            vaccinationEvent.Id = _nextId++;
        _store[vaccinationEvent.Id] = vaccinationEvent;
        return vaccinationEvent;
    }

    private static VaccinationEvent BuildEvent(DateTime createdAt, DateTime? updatedAt = null, bool isActive = true, int? parentEventId = null, ulong rowVersion = 1, int id = 1, Guid? syncId = null)
    {
        return new VaccinationEvent
        {
            Id = id,
            SyncId = syncId ?? Guid.NewGuid(),
            VaccineId = 51,
            DoseType = parentEventId.HasValue ? DoseType.Booster : DoseType.FirstDose,
            ApplicationDate = parentEventId.HasValue ? null : createdAt.Date,
            PredictedDate = parentEventId.HasValue ? createdAt.Date.AddDays(30) : null,
            ParentEventId = parentEventId,
            Notes = "original",
            EventAnimals = new List<VaccinationEventAnimal>
            {
                new() { VaccinationEventId = id, AnimalId = 1 },
                new() { VaccinationEventId = id, AnimalId = 2 }
            },
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    private static VaccinationEventCreateDto BuildCreateDto(Guid? syncId = null, List<int>? animalIds = null, int vaccineId = 51) =>
        new() { SyncId = syncId, VaccineId = vaccineId, AnimalIds = animalIds ?? new List<int> { 1, 2 }, ApplicationDate = DateTime.UtcNow.AddDays(-1) };

    private static VaccinationBoosterCreateDto BuildBoosterDto(Guid? syncId = null, DateTime? predictedDate = null) =>
        new() { SyncId = syncId, PredictedDate = predictedDate ?? DateTime.UtcNow.AddDays(30) };

    [Fact]
    public async Task CreateAsync_WithNewSyncId_CreatesEventWithGivenSyncIdAndAnimals()
    {
        var syncId = Guid.NewGuid();
        var result = await _service.CreateAsync(BuildCreateDto(syncId));
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.CreateAsync(It.Is<VaccinationEvent>(e =>
            e.SyncId == syncId && e.EventAnimals!.Select(a => a.AnimalId).SequenceEqual(new[] { 1, 2 }))), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = Store(BuildEvent(DateTime.UtcNow.AddDays(-1), id: 9, syncId: syncId));
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var result = await _service.CreateAsync(BuildCreateDto(syncId, new List<int> { 3 }));
        Assert.Equal(9, result.Id);
        Assert.Equal(new[] { 1, 2 }, result.Animals.Select(a => a.AnimalId));
        _repository.Verify(r => r.CreateAsync(It.IsAny<VaccinationEvent>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_DoesNotCheckVaccineOrAnimals()
    {
        var syncId = Guid.NewGuid();
        var existing = Store(BuildEvent(DateTime.UtcNow.AddDays(-1), id: 9, syncId: syncId));
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        await _service.CreateAsync(BuildCreateDto(syncId, vaccineId: 99));
        _vaccineRepository.Verify(r => r.GetVaccineByIdAsync(It.IsAny<int>()), Times.Never);
        _animalRepository.Verify(r => r.GetExistingAnimalIdsAsync(It.IsAny<IReadOnlyCollection<int>>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithoutSyncId_GeneratesSyncId()
    {
        var result = await _service.CreateAsync(BuildCreateDto());
        Assert.NotEqual(Guid.Empty, result.SyncId);
        _repository.Verify(r => r.GetBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
        _repository.Verify(r => r.CreateAsync(It.Is<VaccinationEvent>(e => e.SyncId != Guid.Empty)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenVaccineNotFound_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(BuildCreateDto(Guid.NewGuid(), vaccineId: 99)));
        _repository.Verify(r => r.CreateAsync(It.IsAny<VaccinationEvent>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenAnimalNotFound_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(BuildCreateDto(Guid.NewGuid(), new List<int> { 1, 999 })));
        _repository.Verify(r => r.CreateAsync(It.IsAny<VaccinationEvent>()), Times.Never);
    }

    [Fact]
    public async Task CreateBoosterAsync_WithNewSyncId_CreatesBoosterInheritingVaccineAndAnimals()
    {
        var syncId = Guid.NewGuid();
        Store(BuildEvent(DateTime.UtcNow.AddDays(-1), id: 1));
        var result = await _service.CreateBoosterAsync(1, BuildBoosterDto(syncId));
        Assert.Equal(syncId, result.SyncId);
        Assert.Equal(1, result.ParentEventId);
        _repository.Verify(r => r.CreateBoosterAsync(It.Is<VaccinationEvent>(b =>
            b.SyncId == syncId
            && b.VaccineId == 51
            && b.ParentEventId == 1
            && b.DoseType == DoseType.Booster
            && b.EventAnimals!.Select(a => a.AnimalId).SequenceEqual(new[] { 1, 2 }))), Times.Once);
    }

    [Fact]
    public async Task CreateBoosterAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = Store(BuildEvent(DateTime.UtcNow, parentEventId: 1, id: 2, syncId: syncId));
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var result = await _service.CreateBoosterAsync(1, BuildBoosterDto(syncId));
        Assert.Equal(2, result.Id);
        _repository.Verify(r => r.GetByIdAsync(1), Times.Never);
        _repository.Verify(r => r.CreateBoosterAsync(It.IsAny<VaccinationEvent>()), Times.Never);
    }

    [Fact]
    public async Task CreateBoosterAsync_WhenActiveChildIsSameSyncId_ReturnsExisting()
    {
        var syncId = Guid.NewGuid();
        Store(BuildEvent(DateTime.UtcNow.AddDays(-1), id: 1));
        var child = Store(BuildEvent(DateTime.UtcNow, parentEventId: 1, id: 2, syncId: syncId));
        _repository.SetupSequence(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync((VaccinationEvent?)null).ReturnsAsync(child);
        _repository.Setup(r => r.GetActiveChildByParentIdAsync(1)).ReturnsAsync(child);
        var result = await _service.CreateBoosterAsync(1, BuildBoosterDto(syncId));
        Assert.Equal(2, result.Id);
        _repository.Verify(r => r.CreateBoosterAsync(It.IsAny<VaccinationEvent>()), Times.Never);
    }

    [Fact]
    public async Task CreateBoosterAsync_WhenAnotherBoosterExists_ThrowsConflictException()
    {
        Store(BuildEvent(DateTime.UtcNow.AddDays(-1), id: 1));
        var child = Store(BuildEvent(DateTime.UtcNow, parentEventId: 1, id: 2));
        _repository.Setup(r => r.GetActiveChildByParentIdAsync(1)).ReturnsAsync(child);
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateBoosterAsync(1, BuildBoosterDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateBoosterAsync(It.IsAny<VaccinationEvent>()), Times.Never);
    }

    [Fact]
    public async Task CreateBoosterAsync_WhenIndexRejectsAndSyncIdExists_ReturnsExisting()
    {
        var syncId = Guid.NewGuid();
        Store(BuildEvent(DateTime.UtcNow.AddDays(-1), id: 1));
        var concurrent = Store(BuildEvent(DateTime.UtcNow, parentEventId: 1, id: 3, syncId: syncId));
        _repository.Setup(r => r.CreateBoosterAsync(It.IsAny<VaccinationEvent>())).ReturnsAsync((VaccinationEvent?)null);
        _repository.SetupSequence(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync((VaccinationEvent?)null).ReturnsAsync(concurrent);
        var result = await _service.CreateBoosterAsync(1, BuildBoosterDto(syncId));
        Assert.Equal(3, result.Id);
    }

    [Fact]
    public async Task CreateBoosterAsync_WhenIndexRejectsAndSyncIdMissing_ThrowsConflictException()
    {
        Store(BuildEvent(DateTime.UtcNow.AddDays(-1), id: 1));
        _repository.Setup(r => r.CreateBoosterAsync(It.IsAny<VaccinationEvent>())).ReturnsAsync((VaccinationEvent?)null);
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateBoosterAsync(1, BuildBoosterDto(Guid.NewGuid())));
    }

    [Fact]
    public async Task CreateBoosterAsync_WithPredictedDateBeforeParent_ThrowsBusinessRuleException()
    {
        Store(BuildEvent(DateTime.UtcNow.AddDays(-1), id: 1));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateBoosterAsync(1, BuildBoosterDto(Guid.NewGuid(), DateTime.UtcNow.AddDays(-10))));
        _repository.Verify(r => r.CreateBoosterAsync(It.IsAny<VaccinationEvent>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var vaccinationEvent = Store(BuildEvent(now.AddDays(-2), now.AddHours(-1), id: 1));
        var clientUpdatedAt = now.AddMinutes(-10);
        var result = await _service.UpdateAsync(1, new VaccinationEventUpdateDto { Notes = "novo", UpdatedAt = clientUpdatedAt });
        Assert.Equal("novo", result.Notes);
        Assert.Equal(clientUpdatedAt, vaccinationEvent.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(vaccinationEvent), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var vaccinationEvent = Store(BuildEvent(now.AddDays(-2), serverUpdatedAt, id: 1));
        var result = await _service.UpdateAsync(1, new VaccinationEventUpdateDto { Notes = "velho", UpdatedAt = now.AddHours(-3) });
        Assert.Equal("original", result.Notes);
        Assert.Equal(serverUpdatedAt, vaccinationEvent.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<VaccinationEvent>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenInactive_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var vaccinationEvent = Store(BuildEvent(now.AddDays(-2), now.AddHours(-1), isActive: false, id: 1));
        var result = await _service.UpdateAsync(1, new VaccinationEventUpdateDto { Notes = "novo", UpdatedAt = now.AddMinutes(-10) });
        Assert.Equal("novo", result.Notes);
        Assert.False(result.IsActive);
        _repository.Verify(r => r.UpdateAsync(vaccinationEvent), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_WhenActive_DeactivatesWithoutChangingUpdatedAt()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var vaccinationEvent = Store(BuildEvent(DateTime.UtcNow.AddDays(-1), updatedAt, id: 1));
        var result = await _service.DeactivateAsync(1);
        Assert.True(result);
        Assert.False(vaccinationEvent.IsActive);
        Assert.Equal(updatedAt, vaccinationEvent.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(vaccinationEvent), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var vaccinationEvent = Store(BuildEvent(DateTime.UtcNow.AddDays(-1), updatedAt, isActive: false, id: 1));
        var result = await _service.DeactivateAsync(1);
        Assert.True(result);
        Assert.Equal(updatedAt, vaccinationEvent.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<VaccinationEvent>()), Times.Never);
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
        var fetched = new List<VaccinationEvent>
        {
            BuildEvent(createdAt, rowVersion: 10, id: 1),
            BuildEvent(createdAt, rowVersion: 11, id: 2),
            BuildEvent(createdAt, rowVersion: 12, id: 3)
        };
        _repository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        _repository.Setup(r => r.GetAnimalIdsByEventIdsAsync(It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync(new Dictionary<int, List<int>>());
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }

    [Fact]
    public async Task GetChangesAsync_FillsAnimalIdsForPageItems()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var fetched = new List<VaccinationEvent>
        {
            BuildEvent(createdAt, rowVersion: 10, id: 1),
            BuildEvent(createdAt, rowVersion: 11, id: 2)
        };
        _repository.Setup(r => r.GetChangesAsync(0UL, It.IsAny<int>())).ReturnsAsync(fetched);
        _repository.Setup(r => r.GetAnimalIdsByEventIdsAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1, 2 }))))
            .ReturnsAsync(new Dictionary<int, List<int>> { [1] = new() { 5, 6 } });
        var result = (await _service.GetChangesAsync(null, null)).Items.ToList();
        Assert.Equal(new[] { 5, 6 }, result[0].AnimalIds);
        Assert.Empty(result[1].AnimalIds);
    }
}
