using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IBreedRepository
    {
        Task<IEnumerable<Breed>> GetAllBreedsAsync();
        Task<Breed?> GetBreedByIdAsync(int id);
        Task<Breed> CreateBreedAsync(Breed breed);
        Task<Breed?> UpdateBreedAsync(Breed breed);
        Task<Breed?> DeleteBreedAsync(int id);
    }
}
