using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Mappings;
using MuuBoi.Application.Services;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Tests.Services;

public class AnimalPhotoServiceTests
{
    private static readonly byte[] PhotoBytes = { 0xFF, 0xD8, 0xFF, 0x01, 0x02, 0x03 };
    private static readonly byte[] ThumbnailBytes = { 0xFF, 0xD8, 0xFF, 0x04, 0x05 };
    private static readonly byte[] OtherPhotoBytes = { 0xFF, 0xD8, 0xFF, 0x06, 0x07, 0x08 };
    private static readonly DateTime TenAm = new(2026, 10, 21, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAnimalRepository> _animalRepository = new();
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly AnimalPhotoService _service;

    public AnimalPhotoServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<AnimalProfile>()).CreateMapper();
        _service = new AnimalPhotoService(
            _animalRepository.Object,
            _fileStorage.Object,
            mapper,
            NullLogger<AnimalPhotoService>.Instance);
        _animalRepository.Setup(r => r.UpdatePhotoAsync(It.IsAny<Animal>())).ReturnsAsync(true);
        _fileStorage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync(true);
        _fileStorage.Setup(s => s.DeleteAsync(It.IsAny<string>())).ReturnsAsync(true);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static IFormFile BuildFile(byte[] bytes, string name) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, name, $"{name}.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

    private static AnimalPhotoUploadDto BuildUploadDto(
        byte[] photo, DateTime? takenAt = null, string? photoHash = null, string? thumbnailHash = null) =>
        new()
        {
            Photo = BuildFile(photo, "photo"),
            Thumbnail = BuildFile(ThumbnailBytes, "thumbnail"),
            PhotoHash = photoHash ?? Hash(photo),
            ThumbnailHash = thumbnailHash ?? Hash(ThumbnailBytes),
            TakenAt = takenAt
        };

    private Animal SetupAnimal(byte[]? currentPhoto = null, DateTime? photoUpdatedAt = null)
    {
        var animal = new Animal
        {
            Id = 1,
            SyncId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            TagNumber = "123456",
            CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            PhotoHash = currentPhoto == null ? null : Hash(currentPhoto),
            PhotoUpdatedAt = photoUpdatedAt
        };
        _animalRepository.Setup(r => r.GetAnimalForPhotoAsync(1)).ReturnsAsync(animal);
        return animal;
    }

    [Fact]
    public async Task UploadPhotoAsync_WithMismatchedPhotoHash_ThrowsValidationException()
    {
        var dto = BuildUploadDto(PhotoBytes, photoHash: Hash(OtherPhotoBytes));
        await Assert.ThrowsAsync<ValidationException>(() => _service.UploadPhotoAsync(1, dto));
        _animalRepository.Verify(r => r.GetAnimalForPhotoAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithMismatchedThumbnailHash_ThrowsValidationException()
    {
        var dto = BuildUploadDto(PhotoBytes, thumbnailHash: Hash(OtherPhotoBytes));
        await Assert.ThrowsAsync<ValidationException>(() => _service.UploadPhotoAsync(1, dto));
        _fileStorage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithUppercaseHash_SavesLowercaseHash()
    {
        SetupAnimal();
        var result = await _service.UploadPhotoAsync(1, BuildUploadDto(PhotoBytes, TenAm, Hash(PhotoBytes).ToUpperInvariant()));
        Assert.Equal(Hash(PhotoBytes), result.PhotoHash);
    }

    [Fact]
    public async Task UploadPhotoAsync_WhenAnimalNotFound_ThrowsNotFoundException()
    {
        _animalRepository.Setup(r => r.GetAnimalForPhotoAsync(1)).ReturnsAsync((Animal?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UploadPhotoAsync(1, BuildUploadDto(PhotoBytes)));
        _fileStorage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithFirstPhoto_UploadsBothFilesAndSavesHash()
    {
        var animal = SetupAnimal();
        var result = await _service.UploadPhotoAsync(1, BuildUploadDto(PhotoBytes, TenAm));
        Assert.Equal(Hash(PhotoBytes), result.PhotoHash);
        Assert.Equal(TenAm, animal.PhotoUpdatedAt);
        _fileStorage.Verify(s => s.UploadAsync(AnimalPhotoPaths.PhotoKey(animal, Hash(PhotoBytes)), It.IsAny<Stream>(), "image/jpeg"), Times.Once);
        _fileStorage.Verify(s => s.UploadAsync(AnimalPhotoPaths.ThumbnailKey(animal, Hash(PhotoBytes)), It.IsAny<Stream>(), "image/jpeg"), Times.Once);
        _animalRepository.Verify(r => r.UpdatePhotoAsync(animal), Times.Once);
        _fileStorage.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithFirstPhoto_DoesNotChangeAnimalUpdatedAt()
    {
        var animal = SetupAnimal();
        await _service.UploadPhotoAsync(1, BuildUploadDto(PhotoBytes, TenAm));
        Assert.Null(animal.UpdatedAt);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithSameHashAsCurrent_ReturnsCurrentWithoutSaving()
    {
        var animal = SetupAnimal(PhotoBytes, TenAm);
        var result = await _service.UploadPhotoAsync(1, BuildUploadDto(PhotoBytes, TenAm.AddDays(-30)));
        Assert.Equal(Hash(PhotoBytes), result.PhotoHash);
        Assert.Equal(TenAm, animal.PhotoUpdatedAt);
        _fileStorage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
        _animalRepository.Verify(r => r.UpdatePhotoAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithOlderTakenAt_ReturnsCurrentWithoutSaving()
    {
        SetupAnimal(OtherPhotoBytes, TenAm);
        var result = await _service.UploadPhotoAsync(1, BuildUploadDto(PhotoBytes, TenAm.AddHours(-1)));
        Assert.Equal(Hash(OtherPhotoBytes), result.PhotoHash);
        _fileStorage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
        _animalRepository.Verify(r => r.UpdatePhotoAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithSameTakenAt_ReplacesPhoto()
    {
        SetupAnimal(OtherPhotoBytes, TenAm);
        var result = await _service.UploadPhotoAsync(1, BuildUploadDto(PhotoBytes, TenAm));
        Assert.Equal(Hash(PhotoBytes), result.PhotoHash);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithNewerPhoto_DeletesPreviousFilesAfterSaving()
    {
        var animal = SetupAnimal(OtherPhotoBytes, TenAm);
        var previousPhotoKey = AnimalPhotoPaths.PhotoKey(animal, Hash(OtherPhotoBytes));
        var previousThumbnailKey = AnimalPhotoPaths.ThumbnailKey(animal, Hash(OtherPhotoBytes));
        var calls = new List<string>();
        _animalRepository.Setup(r => r.UpdatePhotoAsync(animal)).Callback(() => calls.Add("save")).ReturnsAsync(true);
        _fileStorage.Setup(s => s.DeleteAsync(It.IsAny<string>())).Callback((string key) => calls.Add(key)).ReturnsAsync(true);
        await _service.UploadPhotoAsync(1, BuildUploadDto(PhotoBytes, TenAm.AddHours(1)));
        Assert.Equal(new[] { "save", previousPhotoKey, previousThumbnailKey }, calls);
    }

    [Fact]
    public async Task UploadPhotoAsync_WhenDeletingPreviousFilesFails_ReturnsNewPhoto()
    {
        SetupAnimal(OtherPhotoBytes, TenAm);
        _fileStorage.Setup(s => s.DeleteAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException());
        var result = await _service.UploadPhotoAsync(1, BuildUploadDto(PhotoBytes, TenAm.AddHours(1)));
        Assert.Equal(Hash(PhotoBytes), result.PhotoHash);
    }

    [Fact]
    public async Task UploadPhotoAsync_WhenUploadFails_DoesNotSaveAnimal()
    {
        SetupAnimal();
        _fileStorage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UploadPhotoAsync(1, BuildUploadDto(PhotoBytes, TenAm)));
        _animalRepository.Verify(r => r.UpdatePhotoAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task DeletePhotoAsync_WhenAnimalNotFound_ThrowsNotFoundException()
    {
        _animalRepository.Setup(r => r.GetAnimalForPhotoAsync(1)).ReturnsAsync((Animal?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeletePhotoAsync(1, TenAm));
    }

    [Fact]
    public async Task DeletePhotoAsync_WithoutPhoto_ReturnsFalseWithoutSaving()
    {
        SetupAnimal();
        var result = await _service.DeletePhotoAsync(1, TenAm);
        Assert.False(result);
        _animalRepository.Verify(r => r.UpdatePhotoAsync(It.IsAny<Animal>()), Times.Never);
        _fileStorage.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeletePhotoAsync_WithOlderUpdatedAt_KeepsPhoto()
    {
        var animal = SetupAnimal(PhotoBytes, TenAm);
        var result = await _service.DeletePhotoAsync(1, TenAm.AddHours(-1));
        Assert.False(result);
        Assert.Equal(Hash(PhotoBytes), animal.PhotoHash);
        _fileStorage.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeletePhotoAsync_WithNewerUpdatedAt_ClearsHashThenDeletesFiles()
    {
        var animal = SetupAnimal(PhotoBytes, TenAm);
        var photoKey = AnimalPhotoPaths.PhotoKey(animal, Hash(PhotoBytes));
        var thumbnailKey = AnimalPhotoPaths.ThumbnailKey(animal, Hash(PhotoBytes));
        var calls = new List<string>();
        _animalRepository.Setup(r => r.UpdatePhotoAsync(animal)).Callback(() => calls.Add("save")).ReturnsAsync(true);
        _fileStorage.Setup(s => s.DeleteAsync(It.IsAny<string>())).Callback((string key) => calls.Add(key)).ReturnsAsync(true);
        var result = await _service.DeletePhotoAsync(1, TenAm.AddHours(1));
        Assert.True(result);
        Assert.Null(animal.PhotoHash);
        Assert.Equal(TenAm.AddHours(1), animal.PhotoUpdatedAt);
        Assert.Equal(new[] { "save", photoKey, thumbnailKey }, calls);
    }

    [Fact]
    public async Task GetPhotoReadUrlAsync_WhenAnimalNotFound_ThrowsNotFoundException()
    {
        _animalRepository.Setup(r => r.GetAnimalForPhotoAsync(1)).ReturnsAsync((Animal?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetPhotoReadUrlAsync(1, thumbnail: false));
    }

    [Fact]
    public async Task GetPhotoReadUrlAsync_WithoutPhoto_ThrowsNotFoundException()
    {
        SetupAnimal();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetPhotoReadUrlAsync(1, thumbnail: false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetPhotoReadUrlAsync_WithPhoto_ReturnsTenMinuteUrlOfRequestedFile(bool thumbnail)
    {
        var animal = SetupAnimal(PhotoBytes, TenAm);
        var key = thumbnail
            ? AnimalPhotoPaths.ThumbnailKey(animal, Hash(PhotoBytes))
            : AnimalPhotoPaths.PhotoKey(animal, Hash(PhotoBytes));
        var expected = new Uri("https://storage.test/file?sig=x");
        _fileStorage.Setup(s => s.GetReadUrl(key, TimeSpan.FromMinutes(10))).Returns(expected);
        var result = await _service.GetPhotoReadUrlAsync(1, thumbnail);
        Assert.Equal(expected, result);
    }
}
