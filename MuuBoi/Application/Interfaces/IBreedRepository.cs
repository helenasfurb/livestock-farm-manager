using MuuBoi.Models;

namespace MuuBoi.Interfaces
{
    public interface IBreedRepository
    {
        Task<IEnumerable<Breed>> GetAllBreedsAsync(string userId);
        Task<Breed?> GetBreedByIdAsync(int id);
        Task<Breed> CreateBreedAsync(Breed breed);
        Task<Breed?> UpdateBreedAsync(Breed breed);
        Task<Breed?> DeleteBreedAsync(int id);
    }
}
