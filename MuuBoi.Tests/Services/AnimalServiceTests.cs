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

public class AnimalServiceTests
{
    private readonly Mock<IAnimalRepository> _animalRepository = new();
    private readonly Mock<IAnimalExitRecordRepository> _exitRecordRepository = new();
    private readonly Mock<IBreedingEventRepository> _breedingEventRepository = new();
    private readonly Mock<IAnimalPregnancyRepository> _pregnancyRepository = new();
    private readonly Mock<IAnimalCalvingRepository> _calvingRepository = new();
    private readonly Mock<ILactationRepository> _lactationRepository = new();
    private readonly Mock<IHealthCaseService> _healthCaseService = new();
    private readonly AnimalService _service;

    public AnimalServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddMaps(typeof(AnimalProfile).Assembly)).CreateMapper();
        _service = new AnimalService(
            _animalRepository.Object,
            _exitRecordRepository.Object,
            _breedingEventRepository.Object,
            _pregnancyRepository.Object,
            _calvingRepository.Object,
            _lactationRepository.Object,
            _healthCaseService.Object,
            mapper);
        _animalRepository.Setup(r => r.CreateAnimalAsync(It.IsAny<Animal>())).ReturnsAsync((Animal a) => a);
        _animalRepository.Setup(r => r.UpdateAnimalAsync(It.IsAny<Animal>())).ReturnsAsync((Animal a) => a);
        _exitRecordRepository.Setup(r => r.GetByAnimalIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new List<AnimalExitRecord>());
    }

    private static Animal BuildAnimal(DateTime createdAt, DateTime? updatedAt = null, bool isActive = true, ulong rowVersion = 1, int id = 1)
    {
        return new Animal
        {
            Id = id,
            SyncId = Guid.NewGuid(),
            Name = "Mimosa",
            TagNumber = "123456",
            Gender = AnimalGender.F,
            Classification = AnimalClassification.Heifer,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    private static AnimalCreateDto BuildCreateDto(Guid? syncId = null) =>
        new()
        {
            SyncId = syncId,
            TagNumber = "123456",
            Name = "Mimosa",
            Gender = AnimalGender.F,
            Classification = AnimalClassification.Cow
        };

    private static AnimalExitDto BuildExitDto(AnimalExitReason reason = AnimalExitReason.Sale) =>
        new() { ExitReason = reason, ExitDate = DateTime.UtcNow.AddDays(-1), ExitNotes = "Venda" };

    [Fact]
    public async Task CreateAnimalAsync_WithNewSyncId_CreatesAnimalWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        _animalRepository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync((Animal?)null);
        var result = await _service.CreateAnimalAsync(BuildCreateDto(syncId));
        Assert.Equal(syncId, result.SyncId);
        _animalRepository.Verify(r => r.CreateAnimalAsync(It.Is<Animal>(a => a.SyncId == syncId)), Times.Once);
    }

    [Fact]
    public async Task CreateAnimalAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = BuildAnimal(DateTime.UtcNow, id: 7);
        existing.SyncId = syncId;
        _animalRepository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var dto = BuildCreateDto(syncId);
        dto.Name = "Outro nome";
        var result = await _service.CreateAnimalAsync(dto);
        Assert.Equal(7, result.Id);
        Assert.Equal("Mimosa", result.Name);
        _animalRepository.Verify(r => r.CreateAnimalAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task CreateAnimalAsync_WithExistingSyncId_DoesNotCheckTagNumber()
    {
        var syncId = Guid.NewGuid();
        _animalRepository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(BuildAnimal(DateTime.UtcNow, id: 7));
        _animalRepository.Setup(r => r.TagNumberExistsAsync(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(true);
        var result = await _service.CreateAnimalAsync(BuildCreateDto(syncId));
        Assert.Equal(7, result.Id);
        _animalRepository.Verify(r => r.TagNumberExistsAsync(It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAnimalAsync_WithoutSyncId_GeneratesSyncId()
    {
        var result = await _service.CreateAnimalAsync(BuildCreateDto());
        Assert.NotEqual(Guid.Empty, result.SyncId);
        _animalRepository.Verify(r => r.GetBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
        _animalRepository.Verify(r => r.CreateAnimalAsync(It.Is<Animal>(a => a.SyncId != Guid.Empty)), Times.Once);
    }

    [Fact]
    public async Task CreateAnimalAsync_WithTagNumberInUse_ThrowsConflictException()
    {
        _animalRepository.Setup(r => r.TagNumberExistsAsync("123456", null)).ReturnsAsync(true);
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAnimalAsync(BuildCreateDto(Guid.NewGuid())));
        _animalRepository.Verify(r => r.CreateAnimalAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task CreateAnimalAsync_WhenTagInUseBySameSyncId_ReturnsExisting()
    {
        var syncId = Guid.NewGuid();
        var concurrent = BuildAnimal(DateTime.UtcNow, id: 7);
        concurrent.SyncId = syncId;
        _animalRepository.SetupSequence(r => r.GetBySyncIdAsync(syncId))
            .ReturnsAsync((Animal?)null)
            .ReturnsAsync(concurrent);
        _animalRepository.Setup(r => r.TagNumberExistsAsync("123456", null)).ReturnsAsync(true);
        var result = await _service.CreateAnimalAsync(BuildCreateDto(syncId));
        Assert.Equal(7, result.Id);
        _animalRepository.Verify(r => r.GetBySyncIdAsync(syncId), Times.Exactly(2));
        _animalRepository.Verify(r => r.CreateAnimalAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task CreateAnimalAsync_WithInitialLactationForMale_ThrowsBusinessRuleException()
    {
        var dto = BuildCreateDto(Guid.NewGuid());
        dto.Gender = AnimalGender.M;
        dto.Classification = AnimalClassification.Bull;
        dto.InitialLactation = new LactationSeedDto { StartDate = DateTime.UtcNow.AddDays(-30) };
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAnimalAsync(dto));
        _animalRepository.Verify(r => r.CreateAnimalAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task CreateAnimalAsync_WithInitialLactation_AttachesLactationToAnimal()
    {
        var startDate = DateTime.UtcNow.AddDays(-400);
        var endDate = DateTime.UtcNow.AddDays(-100);
        var dto = BuildCreateDto(Guid.NewGuid());
        dto.InitialLactation = new LactationSeedDto { StartDate = startDate, EndDate = endDate };
        await _service.CreateAnimalAsync(dto);
        _animalRepository.Verify(r => r.CreateAnimalAsync(It.Is<Animal>(a =>
            a.Lactations != null && a.Lactations.Count == 1 &&
            a.Lactations.First().StartDate == startDate &&
            a.Lactations.First().EndDate == endDate &&
            a.Lactations.First().CalvingId == null &&
            a.Lactations.First().Origin == LactationOrigin.InitialSeed &&
            a.Lactations.First().IsActive)), Times.Once);
        _lactationRepository.Verify(r => r.CreateAsync(It.IsAny<Lactation>()), Times.Never);
    }

    [Fact]
    public async Task CreateAnimalAsync_WithInitialWeightAndBodyCondition_AttachesRecordsToAnimal()
    {
        var weightDate = DateTime.UtcNow.AddDays(-1);
        var bodyConditionDate = DateTime.UtcNow.AddDays(-2);
        var dto = BuildCreateDto(Guid.NewGuid());
        dto.InitialWeight = 450;
        dto.InitialWeightDate = weightDate;
        dto.InitialBodyConditionScore = BodyConditionScore.Ideal;
        dto.InitialBodyConditionDate = bodyConditionDate;
        await _service.CreateAnimalAsync(dto);
        _animalRepository.Verify(r => r.CreateAnimalAsync(It.Is<Animal>(a =>
            a.WeightRecords!.Single().Weight == 450 &&
            a.WeightRecords!.Single().RecordedAt == weightDate &&
            a.BodyConditionRecords!.Single().Score == BodyConditionScore.Ideal &&
            a.BodyConditionRecords!.Single().RecordedAt == bodyConditionDate &&
            a.Lactations == null)), Times.Once);
    }

    [Fact]
    public async Task CreateAnimalAsync_WithInitialWeightSyncId_AttachesRecordWithGivenSyncId()
    {
        var weightSyncId = Guid.NewGuid();
        var dto = BuildCreateDto(Guid.NewGuid());
        dto.InitialWeight = 450;
        dto.InitialWeightSyncId = weightSyncId;
        await _service.CreateAnimalAsync(dto);
        _animalRepository.Verify(r => r.CreateAnimalAsync(It.Is<Animal>(a =>
            a.WeightRecords!.Single().SyncId == weightSyncId)), Times.Once);
    }

    [Fact]
    public async Task CreateAnimalAsync_WithoutInitialWeightSyncId_GeneratesRecordSyncId()
    {
        var dto = BuildCreateDto(Guid.NewGuid());
        dto.InitialWeight = 450;
        await _service.CreateAnimalAsync(dto);
        _animalRepository.Verify(r => r.CreateAnimalAsync(It.Is<Animal>(a =>
            a.WeightRecords!.Single().SyncId != Guid.Empty)), Times.Once);
    }

    [Fact]
    public async Task UpdateAnimalAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var animal = BuildAnimal(DateTime.UtcNow.AddDays(-2));
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(animal);
        var editedAt = DateTime.UtcNow.AddHours(-1);
        var result = await _service.UpdateAnimalAsync(1, new AnimalUpdateDto { Name = "Estrela", UpdatedAt = editedAt });
        Assert.Equal("Estrela", result.Name);
        Assert.Equal(editedAt, animal.UpdatedAt);
        _animalRepository.Verify(r => r.UpdateAnimalAsync(animal), Times.Once);
    }

    [Fact]
    public async Task UpdateAnimalAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var serverUpdatedAt = DateTime.UtcNow.AddHours(-1);
        var animal = BuildAnimal(DateTime.UtcNow.AddDays(-2), serverUpdatedAt);
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(animal);
        var result = await _service.UpdateAnimalAsync(1, new AnimalUpdateDto { Name = "Estrela", UpdatedAt = DateTime.UtcNow.AddHours(-2) });
        Assert.Equal("Mimosa", result.Name);
        Assert.Equal(serverUpdatedAt, animal.UpdatedAt);
        _animalRepository.Verify(r => r.UpdateAnimalAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAnimalAsync_WithOlderClientUpdatedAt_DoesNotCheckTagNumber()
    {
        var animal = BuildAnimal(DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddHours(-1));
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(animal);
        _animalRepository.Setup(r => r.TagNumberExistsAsync(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(true);
        var result = await _service.UpdateAnimalAsync(1, new AnimalUpdateDto { TagNumber = "654321", UpdatedAt = DateTime.UtcNow.AddHours(-2) });
        Assert.Equal("123456", result.TagNumber);
        _animalRepository.Verify(r => r.TagNumberExistsAsync(It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAnimalAsync_WithNewerClientUpdatedAtAndTagInUse_ThrowsConflictException()
    {
        var animal = BuildAnimal(DateTime.UtcNow.AddDays(-2));
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(animal);
        _animalRepository.Setup(r => r.TagNumberExistsAsync("654321", 1)).ReturnsAsync(true);
        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAnimalAsync(1, new AnimalUpdateDto { TagNumber = "654321", UpdatedAt = DateTime.UtcNow.AddHours(-1) }));
        _animalRepository.Verify(r => r.UpdateAnimalAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAnimalAsync_WhenGenderChanges_UpdatesLinkedCalfSex()
    {
        var animal = BuildAnimal(DateTime.UtcNow.AddDays(-2));
        animal.Gender = AnimalGender.M;
        animal.Classification = AnimalClassification.Calf;
        var calf = new AnimalCalvingCalf { Id = 3, AnimalId = 1, Sex = AnimalGender.M, IsActive = true };
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(animal);
        _calvingRepository.Setup(r => r.GetActiveCalfByAnimalIdAsync(1)).ReturnsAsync(calf);
        var editedAt = DateTime.UtcNow.AddHours(-1);
        await _service.UpdateAnimalAsync(1, new AnimalUpdateDto { Gender = AnimalGender.F, UpdatedAt = editedAt });
        Assert.Equal(AnimalGender.F, animal.Gender);
        Assert.Equal(AnimalGender.F, calf.Sex);
        Assert.Equal(editedAt, calf.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAnimalAsync_WhenAnimalNotFound_ThrowsNotFoundException()
    {
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(99)).ReturnsAsync((Animal?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAnimalAsync(99, new AnimalUpdateDto { Name = "x" }));
    }

    [Fact]
    public async Task ExitAnimalAsync_WhenActive_AddsExitRecordAndDeactivatesInSameCall()
    {
        var animal = BuildAnimal(DateTime.UtcNow.AddDays(-2));
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(animal);
        var result = await _service.ExitAnimalAsync(1, BuildExitDto());
        Assert.False(result.IsActive);
        Assert.Equal((int)AnimalExitReason.Sale, result.LastExitRecord!.ExitReason!.Value);
        _animalRepository.Verify(r => r.UpdateAnimalAsync(It.Is<Animal>(a =>
            !a.IsActive && a.ExitRecords!.Single().ExitReason == AnimalExitReason.Sale)), Times.Once);
    }

    [Fact]
    public async Task ExitAnimalAsync_WhenAlreadyInactive_ReturnsWithoutUpdating()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var animal = BuildAnimal(DateTime.UtcNow.AddDays(-2), updatedAt, isActive: false);
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(animal);
        var result = await _service.ExitAnimalAsync(1, BuildExitDto());
        Assert.False(result.IsActive);
        Assert.Equal(updatedAt, animal.UpdatedAt);
        Assert.Null(animal.ExitRecords);
        _animalRepository.Verify(r => r.UpdateAnimalAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task ExitAnimalAsync_WhenPreviousExitExists_KeepsPreviousRecord()
    {
        var previous = new AnimalExitRecord { Id = 5, AnimalId = 1, ExitReason = AnimalExitReason.Sale, ExitDate = DateTime.UtcNow.AddDays(-30) };
        var animal = BuildAnimal(DateTime.UtcNow.AddDays(-60));
        animal.ExitRecords = new List<AnimalExitRecord> { previous };
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(animal);
        var result = await _service.ExitAnimalAsync(1, BuildExitDto(AnimalExitReason.NaturalDeath));
        Assert.Equal(2, animal.ExitRecords.Count);
        Assert.Contains(previous, animal.ExitRecords);
        Assert.Equal((int)AnimalExitReason.NaturalDeath, result.LastExitRecord!.ExitReason!.Value);
    }

    [Fact]
    public async Task ReactivateAnimalAsync_WhenInactive_Reactivates()
    {
        var animal = BuildAnimal(DateTime.UtcNow.AddDays(-2), isActive: false);
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(animal);
        var result = await _service.ReactivateAnimalAsync(1);
        Assert.True(result.IsActive);
        _animalRepository.Verify(r => r.UpdateAnimalAsync(It.Is<Animal>(a => a.IsActive)), Times.Once);
    }

    [Fact]
    public async Task ReactivateAnimalAsync_WhenAlreadyActive_ReturnsWithoutUpdating()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var animal = BuildAnimal(DateTime.UtcNow.AddDays(-2), updatedAt);
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(animal);
        var result = await _service.ReactivateAnimalAsync(1);
        Assert.True(result.IsActive);
        Assert.Equal(updatedAt, animal.UpdatedAt);
        _animalRepository.Verify(r => r.UpdateAnimalAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task GetChangesAsync_WithInvalidCursor_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.GetChangesAsync("abc", null));
        _animalRepository.Verify(r => r.GetChangesAsync(It.IsAny<ulong>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var fetched = new List<Animal>
        {
            BuildAnimal(createdAt, rowVersion: 10, id: 1),
            BuildAnimal(createdAt, rowVersion: 11, id: 2),
            BuildAnimal(createdAt, rowVersion: 12, id: 3)
        };
        _animalRepository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }

    [Fact]
    public async Task GetChangesAsync_FillsExitRecordsForPageItems()
    {
        var createdAt = DateTime.UtcNow.AddDays(-60);
        var fetched = new List<Animal> { BuildAnimal(createdAt, rowVersion: 10, id: 1), BuildAnimal(createdAt, rowVersion: 11, id: 2) };
        _animalRepository.Setup(r => r.GetChangesAsync(0UL, SyncPaging.DefaultLimit + 1)).ReturnsAsync(fetched);
        _exitRecordRepository.Setup(r => r.GetByAnimalIdsAsync(It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(new List<AnimalExitRecord>
            {
                new() { Id = 5, AnimalId = 1, ExitReason = AnimalExitReason.Sale, ExitDate = DateTime.UtcNow.AddDays(-30) },
                new() { Id = 6, AnimalId = 1, ExitReason = AnimalExitReason.NaturalDeath, ExitDate = DateTime.UtcNow.AddDays(-1) }
            });
        var result = (await _service.GetChangesAsync(null, null)).Items.ToList();
        Assert.Equal(new[] { 6, 5 }, result[0].ExitRecords.Select(e => e.Id));
        Assert.Empty(result[1].ExitRecords);
        _exitRecordRepository.Verify(r => r.GetByAnimalIdsAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1, 2 }))), Times.Once);
    }
}
