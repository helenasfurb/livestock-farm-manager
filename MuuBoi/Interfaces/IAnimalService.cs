using MuuBoi.DTOs;

namespace MuuBoi.Interfaces
{
    public interface IAnimalService
    {
        Task<IEnumerable<AnimalDto>> GetAllAnimalsAsync(string userId);
        Task<AnimalDto?> GetAnimalByIdAsync(int id, string userId);
        Task<AnimalDto> CreateAnimalAsync(AnimalCreateDto animalCreateDto, string userId);
        Task<AnimalDto?> UpdateAnimalAsync(int id, AnimalUpdateDto animalUpdateDto, string userId);
        Task<AnimalDto?> DeleteAnimalAsync(int id, string userId);
    }
}
