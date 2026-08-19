using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IBreedService
    {
        Task<IEnumerable<BreedDto>> GetAllBreedsAsync();
        Task<BreedDto?> GetBreedByIdAsync(int id);
        Task<BreedDto> CreateBreedAsync(BreedCreateDto breedCreateDto);
        Task<BreedDto?> UpdateBreedAsync(int id, BreedUpdateDto breedUpdateDto);
        Task<BreedDto?> DeleteBreedAsync(int id);
    }
}
