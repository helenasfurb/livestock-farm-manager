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

public class BreedingEventServiceTests
{
    private static readonly DateTime BreedingDate = new(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DiagnosisDate = new(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IBreedingEventRepository> _repository = new();
    private readonly Mock<IAnimalRepository> _animalRepository = new();
    private readonly Mock<ISemenSampleRepository> _semenSampleRepository = new();
    private readonly Mock<IAnimalPregnancyRepository> _pregnancyRepository = new();
    private readonly Dictionary<int, BreedingEvent> _store = new();
    private readonly BreedingEventService _service;

    public BreedingEventServiceTests()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<BreedingEventProfile>();
            cfg.AddProfile<AnimalPregnancyProfile>();
            cfg.AddProfile<AnimalCalvingProfile>();
        }).CreateMapper();
        var pregnancyService = new AnimalPregnancyService(
            _pregnancyRepository.Object, new Mock<IAnimalCalvingRepository>().Object,
            _animalRepository.Object, _semenSampleRepository.Object, mapper);
        _service = new BreedingEventService(
            _repository.Object, _animalRepository.Object, _semenSampleRepository.Object,
            pregnancyService, _pregnancyRepository.Object, mapper);

        _repository.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => _store.GetValueOrDefault(id));
        _repository.Setup(r => r.CreateAsync(It.IsAny<BreedingEvent>(), It.IsAny<SemenSampleMovement?>()))
            .ReturnsAsync((BreedingEvent e, SemenSampleMovement? _) => Store(e));
        _repository.Setup(r => r.UpdateAsync(It.IsAny<BreedingEvent>())).ReturnsAsync((BreedingEvent e) => e);
        _repository.Setup(r => r.SaveDiagnosisAsync(It.IsAny<BreedingEvent>(), It.IsAny<AnimalPregnancy?>()))
            .ReturnsAsync((BreedingEvent e, AnimalPregnancy? p) =>
            {
                e.Pregnancy = p;
                return e;
            });
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(new Animal
        {
            Id = 1, TagNumber = "111111", Gender = AnimalGender.F, Classification = AnimalClassification.Cow, IsActive = true
        });
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(2)).ReturnsAsync(new Animal
        {
            Id = 2, TagNumber = "222222", Gender = AnimalGender.M, Classification = AnimalClassification.Bull, IsActive = true
        });
        _semenSampleRepository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(new SemenSample { Id = 5, Name = "Touro A", IsActive = true });
        _semenSampleRepository.Setup(r => r.GetByIdAsync(6)).ReturnsAsync(new SemenSample { Id = 6, Name = "Touro B", IsActive = true });
    }

    private BreedingEvent Store(BreedingEvent breedingEvent)
    {
        if (breedingEvent.Id == 0)
            breedingEvent.Id = 100 + _store.Count;
        _store[breedingEvent.Id] = breedingEvent;
        return breedingEvent;
    }

    private static BreedingEvent BuildEvent(
        int id = 10,
        ReproductionType type = ReproductionType.NaturalMating,
        ReproductiveEventStatus status = ReproductiveEventStatus.AwaitingDiagnosis,
        bool isActive = true,
        DateTime? createdAt = null,
        DateTime? updatedAt = null,
        Guid? syncId = null,
        ulong rowVersion = 1)
    {
        return new BreedingEvent
        {
            Id = id,
            SyncId = syncId ?? Guid.NewGuid(),
            AnimalId = 1,
            ReproductionType = type,
            BreedingDate = BreedingDate,
            SemenSampleId = type == ReproductionType.ArtificialInsemination ? 5 : null,
            SireAnimalId = type == ReproductionType.NaturalMating ? 2 : null,
            Status = status,
            ServiceNumber = 1,
            Notes = "original",
            IsActive = isActive,
            CreatedAt = createdAt ?? DateTime.UtcNow.AddDays(-2),
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    private static BreedingEventCreateDto BuildCreateDto(Guid? syncId = null, ReproductionType type = ReproductionType.NaturalMating) =>
        new()
        {
            SyncId = syncId,
            ReproductionType = type,
            BreedingDate = BreedingDate,
            SemenSampleId = type == ReproductionType.ArtificialInsemination ? 5 : null,
            SireAnimalId = type == ReproductionType.NaturalMating ? 2 : null
        };

    private static BreedingEventStatusUpdateDto BuildDiagnosisDto(ReproductiveEventStatus status, Guid? pregnancySyncId = null, int? gestationalAge = null) =>
        new()
        {
            Status = status,
            DiagnosisDate = DiagnosisDate,
            DiagnosisMethod = DiagnosisMethod.Ultrasound,
            PregnancySyncId = pregnancySyncId,
            GestationalAge = gestationalAge
        };

    [Fact]
    public async Task CreateAsync_WithNewSyncId_CreatesEventWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        var result = await _service.CreateAsync(1, BuildCreateDto(syncId));
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.CreateAsync(It.Is<BreedingEvent>(e => e.SyncId == syncId && e.AnimalId == 1), null), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(BuildEvent(id: 9, syncId: syncId));
        var dto = BuildCreateDto(syncId);
        dto.Notes = "outro";
        var result = await _service.CreateAsync(1, dto);
        Assert.Equal(9, result.Id);
        Assert.Equal("original", result.Notes);
        _repository.Verify(r => r.CreateAsync(It.IsAny<BreedingEvent>(), It.IsAny<SemenSampleMovement?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_DoesNotCheckAnimalOrSemen()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(BuildEvent(id: 9, type: ReproductionType.ArtificialInsemination, syncId: syncId));
        await _service.CreateAsync(1, BuildCreateDto(syncId, ReproductionType.ArtificialInsemination));
        _animalRepository.Verify(r => r.GetAnimalByIdAsync(It.IsAny<int>()), Times.Never);
        _semenSampleRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenAwaitingDiagnosisIsSameSyncId_ReturnsExisting()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.HasActiveByAnimalIdAsync(1)).ReturnsAsync(true);
        _repository.SetupSequence(r => r.GetBySyncIdAsync(syncId))
            .ReturnsAsync((BreedingEvent?)null)
            .ReturnsAsync(BuildEvent(id: 9, syncId: syncId));
        var result = await _service.CreateAsync(1, BuildCreateDto(syncId));
        Assert.Equal(9, result.Id);
        _repository.Verify(r => r.CreateAsync(It.IsAny<BreedingEvent>(), It.IsAny<SemenSampleMovement?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenAnotherEventAwaitsDiagnosis_ThrowsBusinessRuleException()
    {
        _repository.Setup(r => r.HasActiveByAnimalIdAsync(1)).ReturnsAsync(true);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAsync(1, BuildCreateDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<BreedingEvent>(), It.IsAny<SemenSampleMovement?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ArtificialInsemination_CreatesSemenOutputWithEvent()
    {
        await _service.CreateAsync(1, BuildCreateDto(Guid.NewGuid(), ReproductionType.ArtificialInsemination));
        _repository.Verify(r => r.CreateAsync(It.IsAny<BreedingEvent>(), It.Is<SemenSampleMovement>(m =>
            m.SemenSampleId == 5
            && m.MovementType == SemenMovementType.Output
            && m.Quantity == 1
            && m.MovementDate == BreedingDate
            && m.SyncId != Guid.Empty)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NaturalMating_CreatesNoSemenOutput()
    {
        await _service.CreateAsync(1, BuildCreateDto(Guid.NewGuid()));
        _repository.Verify(r => r.CreateAsync(It.IsAny<BreedingEvent>(), null), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNoAvailableDoses_CreatesEvent()
    {
        _semenSampleRepository.Setup(r => r.GetAvailableDosesAsync(5)).ReturnsAsync(0);
        await _service.CreateAsync(1, BuildCreateDto(Guid.NewGuid(), ReproductionType.ArtificialInsemination));
        _repository.Verify(r => r.CreateAsync(It.IsAny<BreedingEvent>(), It.IsAny<SemenSampleMovement?>()), Times.Once);
        _semenSampleRepository.Verify(r => r.GetAvailableDosesAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_Successful_CreatesPregnancyWithGivenSyncId()
    {
        var pregnancySyncId = Guid.NewGuid();
        Store(BuildEvent());
        var result = await _service.UpdateStatusAsync(10, BuildDiagnosisDto(ReproductiveEventStatus.Successful, pregnancySyncId));
        Assert.Equal(pregnancySyncId, result.PregnancySyncId);
        _repository.Verify(r => r.SaveDiagnosisAsync(
            It.Is<BreedingEvent>(e => e.Status == ReproductiveEventStatus.Successful && e.DiagnosisDate == DiagnosisDate),
            It.Is<AnimalPregnancy>(p => p.SyncId == pregnancySyncId
                && p.BreedingEventId == 10
                && p.Status == AnimalPregnancyStatus.Confirmed
                && p.ExpectedCalvingDate == BreedingDate.AddDays(280))), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_Successful_ComputesExpectedCalvingDateFromGestationalAge()
    {
        Store(BuildEvent());
        await _service.UpdateStatusAsync(10, BuildDiagnosisDto(ReproductiveEventStatus.Successful, gestationalAge: 60));
        _repository.Verify(r => r.SaveDiagnosisAsync(It.IsAny<BreedingEvent>(),
            It.Is<AnimalPregnancy>(p => p.ExpectedCalvingDate == DiagnosisDate.AddDays(220) && p.GestationalAge == 60)), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenAlreadySameStatus_ReturnsWithoutSaving()
    {
        Store(BuildEvent(status: ReproductiveEventStatus.Successful));
        var result = await _service.UpdateStatusAsync(10, BuildDiagnosisDto(ReproductiveEventStatus.Successful, Guid.NewGuid()));
        Assert.Equal((int)ReproductiveEventStatus.Successful, result.Status!.Value);
        _repository.Verify(r => r.SaveDiagnosisAsync(It.IsAny<BreedingEvent>(), It.IsAny<AnimalPregnancy?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenAlreadyDifferentStatus_ThrowsConflictException()
    {
        Store(BuildEvent(status: ReproductiveEventStatus.Successful));
        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateStatusAsync(10, BuildDiagnosisDto(ReproductiveEventStatus.Unsuccessful)));
        _repository.Verify(r => r.SaveDiagnosisAsync(It.IsAny<BreedingEvent>(), It.IsAny<AnimalPregnancy?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenSaveRejectedAndNowSameStatus_ReturnsCurrent()
    {
        _repository.SetupSequence(r => r.GetByIdAsync(10))
            .ReturnsAsync(BuildEvent())
            .ReturnsAsync(BuildEvent(status: ReproductiveEventStatus.Successful));
        _repository.Setup(r => r.SaveDiagnosisAsync(It.IsAny<BreedingEvent>(), It.IsAny<AnimalPregnancy?>())).ReturnsAsync((BreedingEvent?)null);
        var result = await _service.UpdateStatusAsync(10, BuildDiagnosisDto(ReproductiveEventStatus.Successful, Guid.NewGuid()));
        Assert.Equal((int)ReproductiveEventStatus.Successful, result.Status!.Value);
    }

    [Fact]
    public async Task UpdateStatusAsync_DoesNotChangeUpdatedAt()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var ev = Store(BuildEvent(updatedAt: updatedAt));
        await _service.UpdateStatusAsync(10, BuildDiagnosisDto(ReproductiveEventStatus.Unsuccessful));
        Assert.Equal(updatedAt, ev.UpdatedAt);
        _repository.Verify(r => r.SaveDiagnosisAsync(ev, null), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNewerClientUpdatedAt_AppliesAndMovesSemenOutput()
    {
        var now = DateTime.UtcNow;
        var ev = Store(BuildEvent(type: ReproductionType.ArtificialInsemination, updatedAt: now.AddHours(-1)));
        var semenOutput = new SemenSampleMovement { Id = 40, SemenSampleId = 5, MovementDate = BreedingDate, BreedingEventId = 10, IsActive = true };
        _repository.Setup(r => r.GetSemenOutputAsync(10)).ReturnsAsync(semenOutput);
        var newDate = BreedingDate.AddDays(1);
        var clientUpdatedAt = now.AddMinutes(-10);
        await _service.UpdateAsync(10, new BreedingEventUpdateDto { SemenSampleId = 6, BreedingDate = newDate, UpdatedAt = clientUpdatedAt });
        Assert.Equal(6, ev.SemenSampleId);
        Assert.Equal(6, semenOutput.SemenSampleId);
        Assert.Equal(newDate, semenOutput.MovementDate);
        Assert.Equal(clientUpdatedAt, ev.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(ev), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var ev = Store(BuildEvent(type: ReproductionType.ArtificialInsemination, updatedAt: serverUpdatedAt));
        var result = await _service.UpdateAsync(10, new BreedingEventUpdateDto { Notes = "velho", SemenSampleId = 6, UpdatedAt = now.AddHours(-3) });
        Assert.Equal("original", result.Notes);
        Assert.Equal(5, ev.SemenSampleId);
        Assert.Equal(serverUpdatedAt, ev.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<BreedingEvent>()), Times.Never);
        _repository.Verify(r => r.GetSemenOutputAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_ArtificialInsemination_DeactivatesSemenOutputWithoutChangingUpdatedAt()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var ev = Store(BuildEvent(type: ReproductionType.ArtificialInsemination, updatedAt: updatedAt));
        var semenOutput = new SemenSampleMovement { Id = 40, SemenSampleId = 5, BreedingEventId = 10, IsActive = true };
        _repository.Setup(r => r.GetSemenOutputAsync(10)).ReturnsAsync(semenOutput);
        await _service.DeactivateAsync(10);
        Assert.False(ev.IsActive);
        Assert.False(semenOutput.IsActive);
        Assert.Equal(updatedAt, ev.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(ev), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_WhenAlreadyInactive_DoesNotSave()
    {
        Store(BuildEvent(isActive: false));
        await _service.DeactivateAsync(10);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<BreedingEvent>()), Times.Never);
        _pregnancyRepository.Verify(r => r.ExistsActiveForBreedingEventAsync(It.IsAny<int>()), Times.Never);
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
        var fetched = new List<BreedingEvent>
        {
            BuildEvent(id: 1, rowVersion: 10),
            BuildEvent(id: 2, rowVersion: 11),
            BuildEvent(id: 3, rowVersion: 12)
        };
        _repository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }
}
