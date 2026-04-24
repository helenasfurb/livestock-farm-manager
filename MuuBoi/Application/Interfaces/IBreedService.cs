using MuuBoi.DTOs;

namespace MuuBoi.Interfaces
{
    public interface IBreedService
    {
        Task<IEnumerable<BreedDto>> GetAllBreedsAsync(string userId);
        Task<BreedDto?> GetBreedByIdAsync(int id, string userId);
        Task<BreedDto> CreateBreedAsync(BreedCreateDto breedCreateDto, string userId);
        Task<BreedDto?> UpdateBreedAsync(int id, BreedUpdateDto breedUpdateDto, string userId);
        Task<BreedDto?> DeleteBreedAsync(int id, string userId);
    }
}
