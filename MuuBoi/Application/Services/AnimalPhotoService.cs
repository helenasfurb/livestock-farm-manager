using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Services
{
    public class AnimalPhotoService : IAnimalPhotoService
    {
        private static readonly TimeSpan ReadUrlValidity = TimeSpan.FromMinutes(10);

        private readonly IAnimalRepository _animalRepository;
        private readonly IFileStorageService _fileStorage;
        private readonly IMapper _mapper;
        private readonly ILogger<AnimalPhotoService> _logger;

        public AnimalPhotoService(
            IAnimalRepository animalRepository,
            IFileStorageService fileStorage,
            IMapper mapper,
            ILogger<AnimalPhotoService> logger)
        {
            _animalRepository = animalRepository;
            _fileStorage = fileStorage;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<AnimalPhotoDto> UploadPhotoAsync(int animalId, AnimalPhotoUploadDto dto)
        {
            var photoHash = await ComputeHashAsync(dto.Photo!);
            if (photoHash != dto.PhotoHash!.ToLowerInvariant())
                throw new ValidationException("O hash da foto não confere com o arquivo enviado.");

            var thumbnailHash = await ComputeHashAsync(dto.Thumbnail!);
            if (thumbnailHash != dto.ThumbnailHash!.ToLowerInvariant())
                throw new ValidationException("O hash da miniatura não confere com o arquivo enviado.");

            var animal = await _animalRepository.GetAnimalForPhotoAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            if (animal.PhotoHash == photoHash)
                return _mapper.Map<AnimalPhotoDto>(animal);

            var takenAt = SyncTimestampResolver.ResolveEditedAt(dto.TakenAt, DateTime.UtcNow);
            if (animal.PhotoUpdatedAt.HasValue && takenAt < animal.PhotoUpdatedAt.Value)
                return _mapper.Map<AnimalPhotoDto>(animal);

            await UploadFileAsync(AnimalPhotoPaths.PhotoKey(animal, photoHash), dto.Photo!);
            await UploadFileAsync(AnimalPhotoPaths.ThumbnailKey(animal, photoHash), dto.Thumbnail!);

            var previousHash = animal.PhotoHash;
            animal.PhotoHash = photoHash;
            animal.PhotoUpdatedAt = takenAt;
            await _animalRepository.UpdatePhotoAsync(animal);

            if (previousHash != null)
                await DeleteFilesAsync(animal, previousHash);

            return _mapper.Map<AnimalPhotoDto>(animal);
        }

        public async Task<bool> DeletePhotoAsync(int animalId, DateTime? updatedAt)
        {
            var animal = await _animalRepository.GetAnimalForPhotoAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            if (animal.PhotoHash == null)
                return false;

            var deletedAt = SyncTimestampResolver.ResolveEditedAt(updatedAt, DateTime.UtcNow);
            if (animal.PhotoUpdatedAt.HasValue && deletedAt < animal.PhotoUpdatedAt.Value)
                return false;

            var previousHash = animal.PhotoHash;
            animal.PhotoHash = null;
            animal.PhotoUpdatedAt = deletedAt;
            await _animalRepository.UpdatePhotoAsync(animal);

            await DeleteFilesAsync(animal, previousHash);
            return true;
        }

        public async Task<Uri> GetPhotoReadUrlAsync(int animalId, bool thumbnail)
        {
            var animal = await _animalRepository.GetAnimalForPhotoAsync(animalId)
                ?? throw new NotFoundException($"Animal com id '{animalId}' não encontrado.");

            if (animal.PhotoHash == null)
                throw new NotFoundException($"O animal com id '{animalId}' não possui foto.");

            var key = thumbnail
                ? AnimalPhotoPaths.ThumbnailKey(animal, animal.PhotoHash)
                : AnimalPhotoPaths.PhotoKey(animal, animal.PhotoHash);

            return _fileStorage.GetReadUrl(key, ReadUrlValidity);
        }

        private async Task<bool> UploadFileAsync(string key, IFormFile file)
        {
            await using var stream = file.OpenReadStream();
            return await _fileStorage.UploadAsync(key, stream, file.ContentType);
        }

        private async Task<bool> DeleteFilesAsync(Animal animal, string photoHash)
        {
            try
            {
                await _fileStorage.DeleteAsync(AnimalPhotoPaths.PhotoKey(animal, photoHash));
                await _fileStorage.DeleteAsync(AnimalPhotoPaths.ThumbnailKey(animal, photoHash));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete previous photo files {PhotoHash} of animal {AnimalId}", photoHash, animal.Id);
                return false;
            }
        }

        private static async Task<string> ComputeHashAsync(IFormFile file)
        {
            await using var stream = file.OpenReadStream();
            var hash = await SHA256.HashDataAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
