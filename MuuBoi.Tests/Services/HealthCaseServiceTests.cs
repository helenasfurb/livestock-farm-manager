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

public class HealthCaseServiceTests
{
    private readonly Mock<IHealthCaseRepository> _repository = new();
    private readonly Mock<IAnimalRepository> _animalRepository = new();
    private readonly HealthCaseService _service;

    public HealthCaseServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<HealthCaseProfile>()).CreateMapper();
        _service = new HealthCaseService(_repository.Object, _animalRepository.Object, mapper);
        _repository.Setup(r => r.CreateAsync(It.IsAny<HealthCase>())).ReturnsAsync((HealthCase c) =>
        {
            c.Id = 10;
            return c;
        });
        _repository.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(() => new HealthCase { Id = 10, AnimalId = 1 });
        _repository.Setup(r => r.UpdateAsync(It.IsAny<HealthCase>())).ReturnsAsync((HealthCase c) => c);
        _repository.Setup(r => r.AddMedicationAsync(It.IsAny<AnimalMedication>())).ReturnsAsync((AnimalMedication m) =>
        {
            m.Id = 20;
            _repository.Setup(r => r.GetMedicationAsync(20, m.HealthCaseId!.Value)).ReturnsAsync(m);
            return m;
        });
        _repository.Setup(r => r.AddTestAsync(It.IsAny<MastitisTest>())).ReturnsAsync((MastitisTest t) =>
        {
            t.Id = 30;
            return t;
        });
    }

    private static HealthCase BuildCase(DateTime createdAt, DateTime? updatedAt = null, bool isActive = true, int id = 11, Guid? syncId = null, ulong rowVersion = 1)
    {
        return new HealthCase
        {
            Id = id,
            SyncId = syncId ?? Guid.NewGuid(),
            AnimalId = 1,
            DiseaseType = DiseaseType.Other,
            DiseaseName = "Pneumonia",
            DiagnosisDate = createdAt.Date,
            Notes = "original",
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    private static AnimalMedication BuildMedication(int id, bool isActive = true, Guid? syncId = null, int healthCaseId = 10)
    {
        return new AnimalMedication
        {
            Id = id,
            SyncId = syncId ?? Guid.NewGuid(),
            AnimalId = 1,
            HealthCaseId = healthCaseId,
            MedicationName = "Mastizone",
            ApplicationDate = DateTime.UtcNow.Date,
            WithdrawalPeriodDays = 4,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static MastitisTest BuildTest(int id, bool isActive = true, Guid? syncId = null, int healthCaseId = 10)
    {
        return new MastitisTest
        {
            Id = id,
            SyncId = syncId ?? Guid.NewGuid(),
            HealthCaseId = healthCaseId,
            TestType = MastitisTestType.CMT,
            Result = "++",
            TestDate = DateTime.UtcNow.Date,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static MedicationUseCreateDto BuildMedicationDto(Guid? syncId = null) =>
        new() { SyncId = syncId, MedicationName = "Mastizone", ApplicationDate = DateTime.UtcNow.Date, WithdrawalPeriodDays = 4 };

    private static MastitisTestCreateDto BuildTestDto(Guid? syncId = null) =>
        new() { SyncId = syncId, TestType = MastitisTestType.CMT, Result = "++", TestDate = DateTime.UtcNow.Date };

    private void SetupAnimal(AnimalGender? gender, AnimalClassification? classification)
    {
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1))
            .ReturnsAsync(new Animal { Id = 1, Gender = gender, Classification = classification });
    }

    private static HealthCaseCreateDto BuildDto(DiseaseType diseaseType)
    {
        return new HealthCaseCreateDto
        {
            AnimalId = 1,
            DiseaseType = diseaseType,
            DiseaseName = diseaseType == DiseaseType.Other ? "Pneumonia" : null,
            DiagnosisDate = DateTime.UtcNow.Date
        };
    }

    [Fact]
    public async Task CreateAsync_MastitisForMaleAnimal_ThrowsBusinessRuleException()
    {
        SetupAnimal(AnimalGender.M, AnimalClassification.Calf);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAsync(BuildDto(DiseaseType.Mastitis)));
        _repository.Verify(r => r.CreateAsync(It.IsAny<HealthCase>()), Times.Never);
    }

    [Theory]
    [InlineData(AnimalClassification.Bull)]
    [InlineData(AnimalClassification.Steer)]
    public async Task CreateAsync_MastitisForBullOrSteer_ThrowsBusinessRuleException(AnimalClassification classification)
    {
        SetupAnimal(AnimalGender.F, classification);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAsync(BuildDto(DiseaseType.Mastitis)));
        _repository.Verify(r => r.CreateAsync(It.IsAny<HealthCase>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_MastitisForCow_CreatesHealthCase()
    {
        SetupAnimal(AnimalGender.F, AnimalClassification.Cow);
        var result = await _service.CreateAsync(BuildDto(DiseaseType.Mastitis));
        Assert.Equal(10, result.Id);
        _repository.Verify(r => r.CreateAsync(It.Is<HealthCase>(c => c.DiseaseType == DiseaseType.Mastitis)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NonMastitisForMaleAnimal_CreatesHealthCase()
    {
        SetupAnimal(AnimalGender.M, AnimalClassification.Bull);
        var result = await _service.CreateAsync(BuildDto(DiseaseType.Other));
        Assert.Equal(10, result.Id);
        _repository.Verify(r => r.CreateAsync(It.Is<HealthCase>(c => c.DiseaseType == DiseaseType.Other)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNonexistentAnimal_ThrowsNotFoundException()
    {
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync((Animal?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(BuildDto(DiseaseType.Mastitis)));
    }

    [Fact]
    public async Task CreateAsync_WithNewSyncId_CreatesCaseWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        SetupAnimal(AnimalGender.F, AnimalClassification.Cow);
        var dto = BuildDto(DiseaseType.Other);
        dto.SyncId = syncId;
        await _service.CreateAsync(dto);
        _repository.Verify(r => r.CreateAsync(It.Is<HealthCase>(c => c.SyncId == syncId)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = BuildCase(DateTime.UtcNow.AddDays(-1), id: 5, syncId: syncId);
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        _repository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(existing);
        var dto = BuildDto(DiseaseType.Other);
        dto.SyncId = syncId;
        dto.DiseaseName = "Outra doença";
        var result = await _service.CreateAsync(dto);
        Assert.Equal(5, result.Id);
        Assert.Equal("Pneumonia", result.DiseaseName);
        _repository.Verify(r => r.CreateAsync(It.IsAny<HealthCase>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSyncId_DoesNotCheckAnimalOrMastitisRule()
    {
        var syncId = Guid.NewGuid();
        var existing = BuildCase(DateTime.UtcNow.AddDays(-1), id: 5, syncId: syncId);
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(existing);
        _repository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(existing);
        SetupAnimal(AnimalGender.M, AnimalClassification.Bull);
        var dto = BuildDto(DiseaseType.Mastitis);
        dto.SyncId = syncId;
        var result = await _service.CreateAsync(dto);
        Assert.Equal(5, result.Id);
        _animalRepository.Verify(r => r.GetAnimalByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithoutSyncId_GeneratesSyncId()
    {
        SetupAnimal(AnimalGender.F, AnimalClassification.Cow);
        await _service.CreateAsync(BuildDto(DiseaseType.Other));
        _repository.Verify(r => r.GetBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
        _repository.Verify(r => r.CreateAsync(It.Is<HealthCase>(c => c.SyncId != Guid.Empty)), Times.Once);
    }

    [Fact]
    public async Task AddMedicationAsync_WithNewSyncId_CreatesMedicationWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        var result = await _service.AddMedicationAsync(10, BuildMedicationDto(syncId));
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.AddMedicationAsync(It.Is<AnimalMedication>(m =>
            m.SyncId == syncId && m.HealthCaseId == 10 && m.AnimalId == 1)), Times.Once);
    }

    [Fact]
    public async Task AddMedicationAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetMedicationBySyncIdAsync(syncId)).ReturnsAsync(BuildMedication(7, syncId: syncId));
        var dto = BuildMedicationDto(syncId);
        dto.WithdrawalPeriodDays = 9;
        var result = await _service.AddMedicationAsync(10, dto);
        Assert.Equal(7, result.Id);
        Assert.Equal(4, result.WithdrawalPeriodDays);
        _repository.Verify(r => r.AddMedicationAsync(It.IsAny<AnimalMedication>()), Times.Never);
    }

    [Fact]
    public async Task AddMedicationAsync_WithExistingSyncId_DoesNotCheckCase()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetMedicationBySyncIdAsync(syncId)).ReturnsAsync(BuildMedication(7, syncId: syncId));
        var result = await _service.AddMedicationAsync(99, BuildMedicationDto(syncId));
        Assert.Equal(7, result.Id);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        _repository.Verify(r => r.AddMedicationAsync(It.IsAny<AnimalMedication>()), Times.Never);
    }

    [Fact]
    public async Task AddTestAsync_WithNewSyncId_CreatesTestWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        var result = await _service.AddTestAsync(10, BuildTestDto(syncId));
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.AddTestAsync(It.Is<MastitisTest>(t => t.SyncId == syncId && t.HealthCaseId == 10)), Times.Once);
    }

    [Fact]
    public async Task AddTestAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetTestBySyncIdAsync(syncId)).ReturnsAsync(BuildTest(8, syncId: syncId));
        var dto = BuildTestDto(syncId);
        dto.Result = "---";
        var result = await _service.AddTestAsync(10, dto);
        Assert.Equal(8, result.Id);
        Assert.Equal("++", result.Result);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        _repository.Verify(r => r.AddTestAsync(It.IsAny<MastitisTest>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var healthCase = BuildCase(now.AddDays(-2), now.AddHours(-1));
        _repository.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(healthCase);
        var clientUpdatedAt = now.AddMinutes(-10);
        var result = await _service.UpdateAsync(11, new HealthCaseUpdateDto { Notes = "novo", UpdatedAt = clientUpdatedAt });
        Assert.Equal("novo", result.Notes);
        Assert.Equal(clientUpdatedAt, healthCase.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(healthCase), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var healthCase = BuildCase(now.AddDays(-2), serverUpdatedAt);
        _repository.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(healthCase);
        var result = await _service.UpdateAsync(11, new HealthCaseUpdateDto { Notes = "velho", ResolvedAt = now.Date, UpdatedAt = now.AddHours(-3) });
        Assert.Equal("original", result.Notes);
        Assert.Null(healthCase.ResolvedAt);
        Assert.Equal(serverUpdatedAt, healthCase.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<HealthCase>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenInactive_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var healthCase = BuildCase(now.AddDays(-2), now.AddHours(-1), isActive: false);
        _repository.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(healthCase);
        var result = await _service.UpdateAsync(11, new HealthCaseUpdateDto { Notes = "novo", UpdatedAt = now.AddMinutes(-10) });
        Assert.Equal("novo", result.Notes);
        Assert.False(result.IsActive);
        _repository.Verify(r => r.UpdateAsync(healthCase), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_WhenActive_DeactivatesWithoutChangingUpdatedAt()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var healthCase = BuildCase(DateTime.UtcNow.AddDays(-1), updatedAt);
        _repository.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(healthCase);
        var result = await _service.DeactivateAsync(11);
        Assert.True(result);
        Assert.False(healthCase.IsActive);
        Assert.Equal(updatedAt, healthCase.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(healthCase), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var healthCase = BuildCase(DateTime.UtcNow.AddDays(-1), updatedAt, isActive: false);
        _repository.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(healthCase);
        var result = await _service.DeactivateAsync(11);
        Assert.True(result);
        Assert.Equal(updatedAt, healthCase.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<HealthCase>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateMedicationAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating()
    {
        _repository.Setup(r => r.GetMedicationAsync(7, 10)).ReturnsAsync(BuildMedication(7, isActive: false));
        var result = await _service.DeactivateMedicationAsync(10, 7);
        Assert.True(result);
        _repository.Verify(r => r.UpdateMedicationAsync(It.IsAny<AnimalMedication>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateTestAsync_WhenAlreadyInactive_ReturnsTrueWithoutUpdating()
    {
        _repository.Setup(r => r.GetTestAsync(8, 10)).ReturnsAsync(BuildTest(8, isActive: false));
        var result = await _service.DeactivateTestAsync(10, 8);
        Assert.True(result);
        _repository.Verify(r => r.UpdateTestAsync(It.IsAny<MastitisTest>()), Times.Never);
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
        var fetched = new List<HealthCase>
        {
            BuildCase(createdAt, id: 1, rowVersion: 10),
            BuildCase(createdAt, id: 2, rowVersion: 11),
            BuildCase(createdAt, id: 3, rowVersion: 12)
        };
        _repository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        _repository.Setup(r => r.GetMedicationsByCaseIdsAsync(It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync(new List<AnimalMedication>());
        _repository.Setup(r => r.GetTestsByCaseIdsAsync(It.IsAny<IReadOnlyCollection<int>>())).ReturnsAsync(new List<MastitisTest>());
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }

    [Fact]
    public async Task GetChangesAsync_FillsMedicationsAndTestsIncludingInactive()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var fetched = new List<HealthCase>
        {
            BuildCase(createdAt, id: 1, rowVersion: 10),
            BuildCase(createdAt, id: 2, rowVersion: 11)
        };
        _repository.Setup(r => r.GetChangesAsync(0UL, It.IsAny<int>())).ReturnsAsync(fetched);
        _repository.Setup(r => r.GetMedicationsByCaseIdsAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1, 2 }))))
            .ReturnsAsync(new List<AnimalMedication> { BuildMedication(7, healthCaseId: 1), BuildMedication(8, isActive: false, healthCaseId: 1) });
        _repository.Setup(r => r.GetTestsByCaseIdsAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1, 2 }))))
            .ReturnsAsync(new List<MastitisTest> { BuildTest(9, isActive: false, healthCaseId: 2) });
        var result = (await _service.GetChangesAsync(null, null)).Items.ToList();
        Assert.Equal(new[] { 7, 8 }, result[0].Medications.Select(m => m.Id));
        Assert.False(result[0].Medications.Single(m => m.Id == 8).IsActive);
        Assert.Empty(result[0].Tests);
        Assert.Empty(result[1].Medications);
        Assert.False(Assert.Single(result[1].Tests).IsActive);
    }
}
