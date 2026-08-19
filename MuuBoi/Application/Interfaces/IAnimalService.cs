using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalService
    {
        Task<IEnumerable<AnimalListItemDto>> GetAllAnimalsAsync(AnimalFilterDto filter);
        Task<AnimalDto> GetAnimalByIdAsync(int id);
        Task<AnimalDto> CreateAnimalAsync(AnimalCreateDto animalCreateDto);
        Task<AnimalDto> UpdateAnimalAsync(int id, AnimalUpdateDto animalUpdateDto);
    }
}
