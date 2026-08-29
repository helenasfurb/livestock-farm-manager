using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalRepository
    {
        Task<IEnumerable<Animal>> GetAllAnimalsAsync(AnimalFilterDto filter);
        Task<Dictionary<int, ReproductiveStatus>> GetReproductiveStatusMapAsync(IReadOnlyCollection<int> animalIds);
        Task<Animal?> GetAnimalByIdAsync(int id);
        Task<IEnumerable<Animal>> GetBreedingEligibleAnimalsAsync(string? search);
        Task<Animal> CreateAnimalAsync(Animal animal);
        Task<Animal> UpdateAnimalAsync(Animal animal);
        Task<bool> TagNumberExistsAsync(string tagNumber, int? excludeAnimalId = null);
    }
}
