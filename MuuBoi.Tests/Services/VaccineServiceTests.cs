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

public class VaccineServiceTests
{
    private readonly Mock<IVaccineRepository> _repository = new();
    private readonly VaccineService _service;

    public VaccineServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<VaccineProfile>()).CreateMapper();
        _service = new VaccineService(_repository.Object, mapper);
        _repository.Setup(r => r.CreateVaccineAsync(It.IsAny<Vaccine>())).ReturnsAsync((Vaccine v) => v);
        _repository.Setup(r => r.UpdateVaccineAsync(It.IsAny<Vaccine>())).ReturnsAsync((Vaccine v) => v);
    }

    private static Vaccine BuildVaccine(DateTime createdAt, DateTime? updatedAt = null, bool isActive = true, ulong rowVersion = 1)
    {
        return new Vaccine
        {
            Id = 1,
            SyncId = Guid.NewGuid(),
            Name = "Raiva",
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            RowVersion = SyncPaging.ToRowVersionBytes(rowVersion)
        };
    }

    [Fact]
    public async Task CreateVaccineAsync_WithNewSyncId_CreatesVaccineWithGivenSyncId()
    {
        var syncId = Guid.NewGuid();
        _repository.Setup(r => r.GetVaccineBySyncIdAsync(syncId)).ReturnsAsync((Vaccine?)null);
        var result = await _service.CreateVaccineAsync(new VaccineCreateDto { SyncId = syncId, Name = "Raiva" });
        Assert.Equal(syncId, result.SyncId);
        _repository.Verify(r => r.CreateVaccineAsync(It.Is<Vaccine>(v => v.SyncId == syncId)), Times.Once);
    }

    [Fact]
    public async Task CreateVaccineAsync_WithExistingSyncId_ReturnsExistingWithoutCreating()
    {
        var syncId = Guid.NewGuid();
        var existing = new Vaccine { Id = 7, SyncId = syncId, Name = "Raiva", CreatedAt = DateTime.UtcNow };
        _repository.Setup(r => r.GetVaccineBySyncIdAsync(syncId)).ReturnsAsync(existing);
        var result = await _service.CreateVaccineAsync(new VaccineCreateDto { SyncId = syncId, Name = "Outro nome" });
        Assert.Equal(7, result.Id);
        Assert.Equal("Raiva", result.Name);
        _repository.Verify(r => r.CreateVaccineAsync(It.IsAny<Vaccine>()), Times.Never);
    }

    [Fact]
    public async Task CreateVaccineAsync_WithoutSyncId_GeneratesSyncId()
    {
        var result = await _service.CreateVaccineAsync(new VaccineCreateDto { Name = "Raiva" });
        Assert.NotEqual(Guid.Empty, result.SyncId);
        _repository.Verify(r => r.GetVaccineBySyncIdAsync(It.IsAny<Guid>()), Times.Never);
        _repository.Verify(r => r.CreateVaccineAsync(It.Is<Vaccine>(v => v.SyncId != Guid.Empty)), Times.Once);
    }

    [Fact]
    public async Task UpdateVaccineAsync_WithNewerClientUpdatedAt_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var vaccine = BuildVaccine(now.AddDays(-2), now.AddHours(-1));
        var clientUpdatedAt = now.AddMinutes(-10);
        _repository.Setup(r => r.GetVaccineByIdAsync(1)).ReturnsAsync(vaccine);
        var result = await _service.UpdateVaccineAsync(1, new VaccineUpdateDto { Name = "Brucelose", UpdatedAt = clientUpdatedAt });
        Assert.Equal("Brucelose", result.Name);
        Assert.Equal(clientUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateVaccineAsync(vaccine), Times.Once);
    }

    [Fact]
    public async Task UpdateVaccineAsync_WithOlderClientUpdatedAt_KeepsServerVersion()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var vaccine = BuildVaccine(now.AddDays(-2), serverUpdatedAt);
        _repository.Setup(r => r.GetVaccineByIdAsync(1)).ReturnsAsync(vaccine);
        var result = await _service.UpdateVaccineAsync(1, new VaccineUpdateDto { Name = "Brucelose", UpdatedAt = now.AddHours(-3) });
        Assert.Equal("Raiva", result.Name);
        Assert.Equal(serverUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateVaccineAsync(It.IsAny<Vaccine>()), Times.Never);
    }

    [Fact]
    public async Task UpdateVaccineAsync_WithSameClientUpdatedAt_AppliesChanges()
    {
        var now = DateTime.UtcNow;
        var serverUpdatedAt = now.AddHours(-1);
        var vaccine = BuildVaccine(now.AddDays(-2), serverUpdatedAt);
        _repository.Setup(r => r.GetVaccineByIdAsync(1)).ReturnsAsync(vaccine);
        var result = await _service.UpdateVaccineAsync(1, new VaccineUpdateDto { Name = "Brucelose", UpdatedAt = serverUpdatedAt });
        Assert.Equal("Brucelose", result.Name);
        Assert.Equal(serverUpdatedAt, result.UpdatedAt);
        _repository.Verify(r => r.UpdateVaccineAsync(vaccine), Times.Once);
    }

    [Fact]
    public async Task UpdateVaccineAsync_WithoutClientUpdatedAt_UsesNow()
    {
        var before = DateTime.UtcNow;
        var vaccine = BuildVaccine(before.AddDays(-2), before.AddHours(-1));
        _repository.Setup(r => r.GetVaccineByIdAsync(1)).ReturnsAsync(vaccine);
        var result = await _service.UpdateVaccineAsync(1, new VaccineUpdateDto { Name = "Brucelose" });
        var after = DateTime.UtcNow;
        Assert.Equal("Brucelose", result.Name);
        Assert.InRange(result.UpdatedAt!.Value, before, after);
    }

    [Fact]
    public async Task UpdateVaccineAsync_WhenNeverEdited_ComparesWithCreatedAt()
    {
        var now = DateTime.UtcNow;
        var vaccine = BuildVaccine(now.AddHours(-1));
        _repository.Setup(r => r.GetVaccineByIdAsync(1)).ReturnsAsync(vaccine);
        var result = await _service.UpdateVaccineAsync(1, new VaccineUpdateDto { Name = "Brucelose", UpdatedAt = now.AddHours(-2) });
        Assert.Equal("Raiva", result.Name);
        Assert.Null(result.UpdatedAt);
        _repository.Verify(r => r.UpdateVaccineAsync(It.IsAny<Vaccine>()), Times.Never);
    }

    [Fact]
    public async Task UpdateVaccineAsync_WhenVaccineNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetVaccineByIdAsync(99)).ReturnsAsync((Vaccine?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateVaccineAsync(99, new VaccineUpdateDto { Name = "Brucelose" }));
        _repository.Verify(r => r.UpdateVaccineAsync(It.IsAny<Vaccine>()), Times.Never);
    }

    [Fact]
    public async Task DeleteVaccineAsync_WhenActive_DeactivatesAndReturnsTrue()
    {
        var vaccine = BuildVaccine(DateTime.UtcNow.AddDays(-1));
        _repository.Setup(r => r.GetVaccineByIdAsync(1)).ReturnsAsync(vaccine);
        var result = await _service.DeleteVaccineAsync(1);
        Assert.True(result);
        _repository.Verify(r => r.DeleteVaccineAsync(1), Times.Once);
    }

    [Fact]
    public async Task DeleteVaccineAsync_WhenAlreadyInactive_ReturnsTrueWithoutDeleting()
    {
        var vaccine = BuildVaccine(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddHours(-1), isActive: false);
        _repository.Setup(r => r.GetVaccineByIdAsync(1)).ReturnsAsync(vaccine);
        var result = await _service.DeleteVaccineAsync(1);
        Assert.True(result);
        _repository.Verify(r => r.DeleteVaccineAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteVaccineAsync_WhenVaccineNotFound_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetVaccineByIdAsync(99)).ReturnsAsync((Vaccine?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteVaccineAsync(99));
        _repository.Verify(r => r.DeleteVaccineAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetChangesAsync_WithInvalidCursor_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.GetChangesAsync("abc", null));
        _repository.Verify(r => r.GetChangesAsync(It.IsAny<ulong>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetChangesAsync_WithoutCursor_RequestsChangesFromZero()
    {
        _repository.Setup(r => r.GetChangesAsync(It.IsAny<ulong>(), It.IsAny<int>())).ReturnsAsync(new List<Vaccine>());
        await _service.GetChangesAsync(null, null);
        _repository.Verify(r => r.GetChangesAsync(0UL, SyncPaging.DefaultLimit + 1), Times.Once);
    }

    [Fact]
    public async Task GetChangesAsync_WhenMoreThanLimit_ReturnsHasMoreAndLastItemCursor()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var fetched = new List<Vaccine>
        {
            BuildVaccine(createdAt, rowVersion: 10),
            BuildVaccine(createdAt, rowVersion: 11),
            BuildVaccine(createdAt, rowVersion: 12)
        };
        _repository.Setup(r => r.GetChangesAsync(5UL, 3)).ReturnsAsync(fetched);
        var result = await _service.GetChangesAsync("5", 2);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasMore);
        Assert.Equal("11", result.NextCursor);
    }
}
