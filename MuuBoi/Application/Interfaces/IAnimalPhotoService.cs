using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalPhotoService
    {
        Task<AnimalPhotoDto> UploadPhotoAsync(int animalId, AnimalPhotoUploadDto dto);
        Task<bool> DeletePhotoAsync(int animalId, DateTime? updatedAt);
        Task<Uri> GetPhotoReadUrlAsync(int animalId, bool thumbnail);
    }
}
