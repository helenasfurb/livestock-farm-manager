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

public class AnimalPregnancyServiceTests
{
    private readonly Mock<IAnimalPregnancyRepository> _repository = new();
    private readonly Mock<IAnimalCalvingRepository> _calvingRepository = new();
    private readonly Mock<IAnimalRepository> _animalRepository = new();
    private readonly Mock<ISemenSampleRepository> _semenSampleRepository = new();
    private readonly AnimalPregnancyService _service;

    public AnimalPregnancyServiceTests()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AnimalPregnancyProfile>();
            cfg.AddProfile<AnimalCalvingProfile>();
        }).CreateMapper();
        _service = new AnimalPregnancyService(
            _repository.Object, _calvingRepository.Object, _animalRepository.Object, _semenSampleRepository.Object, mapper);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<AnimalPregnancy>())).ReturnsAsync((AnimalPregnancy p) => p);
        _animalRepository.Setup(r => r.GetAnimalByIdAsync(1)).ReturnsAsync(new Animal
        {
            Id = 1, TagNumber = "111111", Gender = AnimalGender.F, Classification = AnimalClassification.Cow, IsActive = true
        });
    }

    private static AnimalPregnancy BuildPregnancy(
        int id = 20,
        AnimalPregnancyStatus status = AnimalPregnancyStatus.Confirmed,
        bool isActive = true,
        DateTime? updatedAt = null,
        Guid? syncId = null,
        ulong rowVersion = 1)
    {
        return new AnimalPregnancy
        {
            Id = id,
            SyncId = syncId ?? Guid.NewGuid(),
            AnimalId = 1,
            ConfirmationDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpectedCalvingDate = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = status,
            Notes = "original",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    private static AnimalPregnancyRetroactiveCreateDto BuildRetroactiveDto(Guid? syncId = null) =>
        new()
        {
            SyncId = syncId,
            ConfirmationDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpectedCalvingDate = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    private static AnimalPregnancyStatusUpdateDto BuildLossDto() =>
        new() { Status = AnimalPregnancyStatus.LostPregnancy, LossDate = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc) };

    [Fact]
    public async Task CreateRetroactiveAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetBySyncIdAsync(syncId)).ReturnsAsync(BuildPregnancy(id: 9, syncId: syncId));
        var result = await _service.CreateRetroactiveAsync(1, BuildRetroactiveDto(syncId));
        Assert.Equal(9, result.Id);
        _animalRepository.Verify(r => r.GetAnimalByIdAsync(It.IsAny<int>()), Times.Never);
        _repository.Verify(r => r.CreateAsync(It.IsAny<AnimalPregnancy>()), Times.Never);
    }

    [Fact]
    public async Task CreateRetroactiveAsync_WhenActivePregnancyIsSameSyncId_ReturnsExisting()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.HasActiveConfirmedByAnimalIdAsync(1)).ReturnsAsync(true);
        _repository.SetupSequence(r => r.GetBySyncIdAsync(syncId))
            .ReturnsAsync((AnimalPregnancy?)null)
            .ReturnsAsync(BuildPregnancy(id: 9, syncId: syncId));
        var result = await _service.CreateRetroactiveAsync(1, BuildRetroactiveDto(syncId));
        Assert.Equal(9, result.Id);
        _repository.Verify(r => r.CreateAsync(It.IsAny<AnimalPregnancy>()), Times.Never);
    }

    [Fact]
    public async Task CreateRetroactiveAsync_WhenAnotherActivePregnancy_ThrowsConflictException()
    {
        _repository.Setup(r => r.HasActiveConfirmedByAnimalIdAsync(1)).ReturnsAsync(true);
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateRetroactiveAsync(1, BuildRetroactiveDto(Guid.NewGuid())));
        _repository.Verify(r => r.CreateAsync(It.IsAny<AnimalPregnancy>()), Times.Never);
    }

    [Fact]
    public async Task RegisterLossAsync_WhenAlreadyLost_ReturnsWithoutSaving()
    {
        _repository.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(BuildPregnancy(status: AnimalPregnancyStatus.LostPregnancy));
        var result = await _service.RegisterLossAsync(20, BuildLossDto());
        Assert.Equal((int)AnimalPregnancyStatus.LostPregnancy, result.Status!.Value);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<AnimalPregnancy>()), Times.Never);
    }

    [Fact]
    public async Task RegisterLossAsync_WhenCalved_ThrowsConflictException()
    {
        _repository.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(BuildPregnancy(status: AnimalPregnancyStatus.Calved));
        await Assert.ThrowsAsync<ConflictException>(() => _service.RegisterLossAsync(20, BuildLossDto()));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<AnimalPregnancy>()), Times.Never);
    }

    [Fact]
    public async Task RegisterLossAsync_DoesNotChangeUpdatedAt()
    {
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var pregnancy = BuildPregnancy(updatedAt: updatedAt);
        _repository.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(pregnancy);
        await _service.RegisterLossAsync(20, BuildLossDto());
        Assert.Equal(AnimalPregnancyStatus.LostPregnancy, pregnancy.Status);
        Assert.Equal(updatedAt, pregnancy.UpdatedAt);
        _repository.Verify(r => r.UpdateAsync(pregnancy), Times.Once);
    }

    [Fact]
    public async Task InactivateAsync_WhenAlreadyInactive_ReturnsTrueWithoutSaving()
    {
        _repository.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(BuildPregnancy(isActive: false));
        var result = await _service.InactivateAsync(20);
        Assert.True(result);
        _calvingRepository.Verify(r => r.HasActiveByPregnancyIdAsync(It.IsAny<int>()), Times.Never);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<AnimalPregnancy>()), Times.Never);
    }

    [Fact]
    public async Task GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor()
    {
        var fetched = new List<AnimalPregnancy>
        {
            BuildPregnancy(id: 1, rowVersion: 10),
            BuildPregnancy(id: 2, rowVersion: 11),
            BuildPregnancy(id: 3, rowVersion: 12)
        };
        _repository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }
}
