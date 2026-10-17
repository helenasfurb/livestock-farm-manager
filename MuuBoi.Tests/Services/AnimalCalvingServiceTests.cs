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

public class AnimalCalvingServiceTests
{
    private static readonly DateTime CalvingDate = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAnimalCalvingRepository> _repository = new();
    private readonly Mock<IAnimalPregnancyRepository> _pregnancyRepository = new();
    private readonly Mock<IBodyConditionRecordRepository> _bodyConditionRecordRepository = new();
    private readonly Mock<ILactationRepository> _lactationRepository = new();
    private readonly Mock<IAnimalRepository> _animalRepository = new();
    private readonly AnimalCalvingService _service;
    private AnimalCalving? _createdCalving;
    private Lactation? _createdLactation;
    private BodyConditionRecord? _createdBodyCondition;

    public AnimalCalvingServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<AnimalCalvingProfile>()).CreateMapper();
        _service = new AnimalCalvingService(
            _repository.Object, _pregnancyRepository.Object, _bodyConditionRecordRepository.Object,
            _lactationRepository.Object, _animalRepository.Object, mapper);

        _repository.Setup(r => r.CreateAsync(It.IsAny<AnimalCalving>(), It.IsAny<Lactation>(), It.IsAny<BodyConditionRecord?>()))
            .ReturnsAsync((AnimalCalving c, Lactation l, BodyConditionRecord? b) =>
            {
                c.Id = 30;
                l.Id = 40;
                l.CalvingId = 30;
                _createdCalving = c;
                _createdLactation = l;
                _createdBodyCondition = b;
                _lactationRepository.Setup(r => r.GetByCalvingIdAsync(30)).ReturnsAsync(l);
                if (b != null)
                {
                    b.Id = 50;
                    _bodyConditionRecordRepository.Setup(r => r.GetBySyncIdAsync(b.SyncId)).ReturnsAsync(b);
                }
                return c;
            });
        _repository.Setup(r => r.UpdateAsync(It.IsAny<AnimalCalving>())).ReturnsAsync((AnimalCalving c) => c);
        _repository.Setup(r => r.UpdateCalfAsync(It.IsAny<AnimalCalvingCalf>())).ReturnsAsync((AnimalCalvingCalf cf) => cf);
    }

    private static AnimalPregnancy BuildPregnancy(AnimalPregnancyStatus status = AnimalPregnancyStatus.Confirmed, bool motherActive = true, DateTime? updatedAt = null)
    {
        return new AnimalPregnancy
        {
            Id = 20,
            AnimalId = 1,
            ConfirmationDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpectedCalvingDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = status,
            IsActive = true,
            UpdatedAt = updatedAt,
            Animal = new Animal { Id = 1, Gender = AnimalGender.F, Classification = AnimalClassification.Cow, IsActive = motherActive }
        };
    }

    private void SetupPregnancy(AnimalPregnancy pregnancy) =>
        _pregnancyRepository.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(pregnancy);

    private static AnimalCalvingCalfCreateDto LiveCalf(string tag = "123456", decimal? weight = null) =>
        new() { Name = "Bezerra", Sex = AnimalGender.F, Breed = AnimalBreed.Holstein, VitalStatus = CalfVitalStatus.Live, TagNumber = tag, WeightKg = weight };

    private static AnimalCalvingCreateDto BuildCreateDto(Guid? syncId = null, params AnimalCalvingCalfCreateDto[] calves) =>
        new() { SyncId = syncId, CalvingDate = CalvingDate, Calves = calves.Length > 0 ? calves.ToList() : new List<AnimalCalvingCalfCreateDto> { LiveCalf() } };

    private static AnimalCalving BuildCalving(int id = 9, bool isActive = true, DateTime? updatedAt = null, Guid? syncId = null, ulong rowVersion = 1)
    {
        var calving = new AnimalCalving
        {
            Id = id,
            SyncId = syncId ?? Guid.NewGuid(),
            AnimalPregnancyId = 20,
            AnimalId = 1,
            CalvingDate = CalvingDate,
            IsActive = isActive,
            CreatedAt = CalvingDate,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion),
            AnimalPregnancy = BuildPregnancy(AnimalPregnancyStatus.Calved, updatedAt: updatedAt)
        };
        var animal = new Animal
        {
            Id = 100,
            SyncId = Guid.NewGuid(),
            Gender = AnimalGender.F,
            IsActive = isActive,
            UpdatedAt = updatedAt,
            CreatedAt = CalvingDate,
            WeightRecords = new List<WeightRecord>
            {
                new() { Id = 200, SyncId = Guid.NewGuid(), AnimalId = 100, Weight = 30, RecordedAt = CalvingDate, IsActive = true, CreatedAt = CalvingDate, UpdatedAt = updatedAt }
            }
        };
        var calf = new AnimalCalvingCalf
        {
            Id = 60,
            SyncId = Guid.NewGuid(),
            CalvingId = id,
            AnimalId = 100,
            Sex = AnimalGender.F,
            WeightKg = 30,
            VitalStatus = CalfVitalStatus.Live,
            IsActive = isActive,
            CreatedAt = CalvingDate,
            UpdatedAt = updatedAt,
            Animal = animal,
            Calving = calving
        };
        calving.Calves = new List<AnimalCalvingCalf> { calf };
        return calving;
    }

    [Fact]
    public async Task CreateAsync_WithAllSyncIds_BuildsDerivedRecordsWithGivenSyncIds()
    {
        SetupPregnancy(BuildPregnancy());
        Guid calving = Guid.NewGuid(), lactation = Guid.NewGuid(), bodyCondition = Guid.NewGuid(), calf = Guid.NewGuid(), animal = Guid.NewGuid(), weight = Guid.NewGuid();
        var calfDto = LiveCalf(weight: 31.5m);
        calfDto.SyncId = calf;
        calfDto.AnimalSyncId = animal;
        calfDto.WeightSyncId = weight;
        var dto = BuildCreateDto(calving, calfDto);
        dto.LactationSyncId = lactation;
        dto.BodyConditionSyncId = bodyCondition;
        dto.BodyConditionScore = BodyConditionScore.Ideal;
        var result = await _service.CreateAsync(20, dto);
        Assert.Equal(calving, _createdCalving!.SyncId);
        var createdCalf = Assert.Single(_createdCalving.Calves!);
        Assert.Equal(calf, createdCalf.SyncId);
        Assert.Equal(animal, createdCalf.Animal!.SyncId);
        Assert.Equal(weight, Assert.Single(createdCalf.Animal.WeightRecords!).SyncId);
        Assert.Equal(lactation, _createdLactation!.SyncId);
        Assert.Equal(LactationOrigin.Calving, _createdLactation.Origin);
        Assert.Equal(bodyCondition, _createdBodyCondition!.SyncId);
        Assert.Equal(BodyConditionScore.Ideal, _createdBodyCondition.Score);
        Assert.Equal(lactation, result.Lactation!.SyncId);
        Assert.Equal(bodyCondition, result.BodyCondition!.SyncId);
    }

    [Fact]
    public async Task CreateAsync_SetsPregnancyCalvedWithoutChangingUpdatedAt()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var pregnancy = BuildPregnancy(updatedAt: updatedAt);
        SetupPregnancy(pregnancy);
        await _service.CreateAsync(20, BuildCreateDto(Guid.NewGuid()));
        Assert.Equal(AnimalPregnancyStatus.Calved, pregnancy.Status);
        Assert.Equal(updatedAt, pregnancy.UpdatedAt);
    }

    [Fact]
    public async Task CreateAsync_StillbornCalf_HasNoAnimal()
    {
        SetupPregnancy(BuildPregnancy());
        await _service.CreateAsync(20, BuildCreateDto(Guid.NewGuid(), new AnimalCalvingCalfCreateDto { Sex = AnimalGender.M, VitalStatus = CalfVitalStatus.Stillborn }));
        Assert.Null(Assert.Single(_createdCalving!.Calves!).Animal);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(BuildCalving(syncId: syncId));
        var result = await _service.CreateAsync(20, BuildCreateDto(syncId));
        Assert.Equal(9, result.Id);
        _repository.Verify(r => r.CreateAsync(It.IsAny<AnimalCalving>(), It.IsAny<Lactation>(), It.IsAny<BodyConditionRecord?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_DoesNotCheckPregnancy()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(BuildCalving(syncId: syncId));
        await _service.CreateAsync(20, BuildCreateDto(syncId));
        _pregnancyRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        _lactationRepository.Verify(r => r.HasOpenByAnimalIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenPregnancyCalvedBySameSyncId_ReturnsExisting()
    {
        var syncId = Guid.NewGuid();
        SetupPregnancy(BuildPregnancy(AnimalPregnancyStatus.Calved));
        _repository.SetupSequence(r => r.GetBySyncIdAsync(syncId))
            .ReturnsAsync((AnimalCalving?)null)
            .ReturnsAsync(BuildCalving(syncId: syncId));
        var result = await _service.CreateAsync(20, BuildCreateDto(syncId));
        Assert.Equal(9, result.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenOpenLactationBySameSyncId_ReturnsExisting()
    {
        var syncId = Guid.NewGuid();
        SetupPregnancy(BuildPregnancy());
        _lactationRepository.Setup(r => r.HasOpenByAnimalIdAsync(1)).ReturnsAsync(true);
        _repository.SetupSequence(r => r.GetBySyncIdAsync(syncId))
            .ReturnsAsync((AnimalCalving?)null)
            .ReturnsAsync(BuildCalving(syncId: syncId));
        var result = await _service.CreateAsync(20, BuildCreateDto(syncId));
        Assert.Equal(9, result.Id);
        _repository.Verify(r => r.CreateAsync(It.IsAny<AnimalCalving>(), It.IsAny<Lactation>(), It.IsAny<BodyConditionRecord?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenActiveCalvingWithOtherSyncId_ThrowsConflictException()
    {
        SetupPregnancy(BuildPregnancy());
        _repository.Setup(r => r.HasActiveByPregnancyIdAsync(20)).ReturnsAsync(true);
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(20, BuildCreateDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<AnimalCalving>(), It.IsAny<Lactation>(), It.IsAny<BodyConditionRecord?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenRepositoryRejectsAndSyncIdExists_ReturnsExisting()
    {
        var syncId = Guid.NewGuid();
        SetupPregnancy(BuildPregnancy());
        _repository.Setup(r => r.CreateAsync(It.IsAny<AnimalCalving>(), It.IsAny<Lactation>(), It.IsAny<BodyConditionRecord?>())).ReturnsAsync((AnimalCalving?)null);
        _repository.SetupSequence(r => r.GetBySyncIdAsync(syncId))
            .ReturnsAsync((AnimalCalving?)null)
            .ReturnsAsync(BuildCalving(syncId: syncId));
        var result = await _service.CreateAsync(20, BuildCreateDto(syncId));
        Assert.Equal(9, result.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenRepositoryRejectsAndSyncIdMissing_ThrowsConflictException()
    {
        SetupPregnancy(BuildPregnancy());
        _repository.Setup(r => r.CreateAsync(It.IsAny<AnimalCalving>(), It.IsAny<Lactation>(), It.IsAny<BodyConditionRecord?>())).ReturnsAsync((AnimalCalving?)null);
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(20, BuildCreateDto(Guid.NewGuid())));
    }

    [Fact]
    public async Task CreateAsync_WithBodyConditionAndInactiveMother_ThrowsWithoutSaving()
    {
        SetupPregnancy(BuildPregnancy(motherActive: false));
        var dto = BuildCreateDto(Guid.NewGuid());
        dto.BodyConditionScore = BodyConditionScore.Ideal;
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(20, dto));
        _repository.Verify(r => r.CreateAsync(It.IsAny<AnimalCalving>(), It.IsAny<Lactation>(), It.IsAny<BodyConditionRecord?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateTagInBatch_ThrowsConflictException()
    {
        SetupPregnancy(BuildPregnancy());
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(20, BuildCreateDto(Guid.NewGuid(), LiveCalf("111111"), LiveCalf("111111"))));
        _repository.Verify(r => r.CreateAsync(It.IsAny<AnimalCalving>(), It.IsAny<Lactation>(), It.IsAny<BodyConditionRecord?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCalfAsync_WithNewerClientUpdatedAt_AppliesToCalfAnimalAndBirthWeight()
    {
        var now = DateTime.UtcNow;
        var calf = BuildCalving(updatedAt: now.AddHours(-1)).Calves!.Single();
        _repository.Setup(r => r.GetCalfByIdAsync(60)).ReturnsAsync(calf);
        var editedAt = now.AddMinutes(-10);
        await _service.UpdateCalfAsync(9, 60, new AnimalCalvingCalfUpdateDto { Sex = AnimalGender.M, WeightKg = 35, UpdatedAt = editedAt });
        Assert.Equal(AnimalGender.M, calf.Sex);
        Assert.Equal(AnimalGender.M, calf.Animal!.Gender);
        Assert.Equal(35, calf.Animal.WeightRecords!.Single().Weight);
        Assert.Equal(editedAt, calf.UpdatedAt);
        Assert.Equal(editedAt, calf.Animal.UpdatedAt);
        Assert.Equal(editedAt, calf.Animal.WeightRecords!.Single().UpdatedAt);
        _repository.Verify(r => r.UpdateCalfAsync(calf), Times.Once);
    }

    [Fact]
    public async Task UpdateCalfAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var calf = BuildCalving(updatedAt: serverUpdatedAt).Calves!.Single();
        _repository.Setup(r => r.GetCalfByIdAsync(60)).ReturnsAsync(calf);
        await _service.UpdateCalfAsync(9, 60, new AnimalCalvingCalfUpdateDto { Sex = AnimalGender.M, UpdatedAt = now.AddHours(-3) });
        Assert.Equal(AnimalGender.F, calf.Sex);
        Assert.Equal(serverUpdatedAt, calf.UpdatedAt);
        _repository.Verify(r => r.UpdateCalfAsync(It.IsAny<AnimalCalvingCalf>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCalfAsync_WhenAnimalIsNewer_KeepsAnimalSex()
    {
        var now = DateTime.UtcNow;
        var calf = BuildCalving(updatedAt: now.AddHours(-2)).Calves!.Single();
        var animalUpdatedAt = now.AddMinutes(-5);
        calf.Animal!.UpdatedAt = animalUpdatedAt;
        _repository.Setup(r => r.GetCalfByIdAsync(60)).ReturnsAsync(calf);
        await _service.UpdateCalfAsync(9, 60, new AnimalCalvingCalfUpdateDto { Sex = AnimalGender.M, UpdatedAt = now.AddHours(-1) });
        Assert.Equal(AnimalGender.M, calf.Sex);
        Assert.Equal(AnimalGender.F, calf.Animal.Gender);
        Assert.Equal(animalUpdatedAt, calf.Animal.UpdatedAt);
    }

    [Fact]
    public async Task UpdateCalfAsync_WithoutBirthWeight_CreatesWeightWithGivenSyncId()
    {
        var calf = BuildCalving().Calves!.Single();
        calf.Animal!.WeightRecords = new List<WeightRecord>();
        _repository.Setup(r => r.GetCalfByIdAsync(60)).ReturnsAsync(calf);
        var weightSyncId = Guid.NewGuid();
        await _service.UpdateCalfAsync(9, 60, new AnimalCalvingCalfUpdateDto { WeightKg = 33, WeightSyncId = weightSyncId });
        var weight = Assert.Single(calf.Animal.WeightRecords);
        Assert.Equal(weightSyncId, weight.SyncId);
        Assert.Equal(33, weight.Weight);
        Assert.Equal(CalvingDate, weight.RecordedAt);
    }

    [Fact]
    public async Task UpdateCalfAsync_WhenCalvingInactive_ThrowsConflictException()
    {
        var calf = BuildCalving(isActive: false).Calves!.Single();
        _repository.Setup(r => r.GetCalfByIdAsync(60)).ReturnsAsync(calf);
        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateCalfAsync(9, 60, new AnimalCalvingCalfUpdateDto { Notes = "x" }));
        _repository.Verify(r => r.UpdateCalfAsync(It.IsAny<AnimalCalvingCalf>()), Times.Never);
    }

    [Fact]
    public async Task InactivateAsync_CascadesWithoutChangingUpdatedAt()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var calving = BuildCalving(updatedAt: updatedAt);
        var lactation = new Lactation { Id = 40, CalvingId = 9, IsActive = true, UpdatedAt = updatedAt };
        _repository.Setup(r => r.GetByIdAsync(9)).ReturnsAsync(calving);
        _lactationRepository.Setup(r => r.GetByCalvingIdAsync(9)).ReturnsAsync(lactation);
        var result = await _service.InactivateAsync(9);
        var calf = calving.Calves!.Single();
        Assert.True(result);
        Assert.False(calving.IsActive);
        Assert.False(calf.IsActive);
        Assert.False(calf.Animal!.IsActive);
        Assert.False(lactation.IsActive);
        Assert.Equal(AnimalPregnancyStatus.Confirmed, calving.AnimalPregnancy!.Status);
        Assert.Equal(updatedAt, calving.UpdatedAt);
        Assert.Equal(updatedAt, calf.UpdatedAt);
        Assert.Equal(updatedAt, calf.Animal.UpdatedAt);
        Assert.Equal(updatedAt, lactation.UpdatedAt);
        Assert.Equal(updatedAt, calving.AnimalPregnancy.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(calving), Times.Once);
        _lactationRepository.Verify(r => r.UpdateAsync(It.IsAny<Lactation>()), Times.Never);
        _pregnancyRepository.Verify(r => r.UpdateAsync(It.IsAny<AnimalPregnancy>()), Times.Never);
    }

    [Fact]
    public async Task InactivateAsync_WhenAlreadyInactive_ReturnsTrueWithoutSaving()
    {
        _repository.Setup(r => r.GetByIdAsync(9)).ReturnsAsync(BuildCalving(isActive: false));
        var result = await _service.InactivateAsync(9);
        Assert.True(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<AnimalCalving>()), Times.Never);
        _lactationRepository.Verify(r => r.GetByCalvingIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetChangesAsync_WithInvalidCursor_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.GetChangesAsync("abc", null));
        _repository.Verify(r => r.GetChangesAsync(It.IsAny<ulong>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetChangesAsync_FillsCalvesIncludingInactive()
    {
        var fetched = new List<AnimalCalving> { BuildCalving(id: 1, rowVersion: 10), BuildCalving(id: 2, rowVersion: 11) };
        _repository.Setup(r => r.GetChangesAsync(0UL, It.IsAny<int>())).ReturnsAsync(fetched);
        _repository.Setup(r => r.GetCalvesByCalvingIdsAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1, 2 }))))
            .ReturnsAsync(new List<AnimalCalvingCalf>
            {
                new() { Id = 7, CalvingId = 1, Sex = AnimalGender.F, VitalStatus = CalfVitalStatus.Live, IsActive = true },
                new() { Id = 8, CalvingId = 1, Sex = AnimalGender.M, VitalStatus = CalfVitalStatus.Stillborn, IsActive = false }
            });
        var result = (await _service.GetChangesAsync(null, null)).Items.ToList();
        Assert.Equal(new[] { 7, 8 }, result[0].Calves.Select(c => c.Id));
        Assert.False(result[0].Calves.Single(c => c.Id == 8).IsActive);
        Assert.Empty(result[1].Calves);
    }
}
