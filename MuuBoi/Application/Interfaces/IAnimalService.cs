using MuuBoi.DTOs;

namespace MuuBoi.Interfaces
{
    public interface IAnimalService
    {
        Task<IEnumerable<AnimalDto>> GetAllAnimalsAsync();
        Task<AnimalDto?> GetAnimalByIdAsync(int id);
        Task<AnimalDto> CreateAnimalAsync(AnimalCreateDto animalCreateDto);
        Task<AnimalDto?> UpdateAnimalAsync(int id, AnimalUpdateDto animalUpdateDto);
        Task<AnimalDto?> DeleteAnimalAsync(int id);
    }
}
