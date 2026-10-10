using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalRepository
    {
        Task<IEnumerable<Animal>> GetAllAnimalsAsync(AnimalFilterDto filter);
        Task<Dictionary<int, ReproductiveStatus>> GetReproductiveStatusMapAsync(IReadOnlyCollection<int> animalIds);
        Task<List<int>> GetAdultFemaleIdsAsync();
        Task<List<AnimalReproductiveFactsDto>> GetReproductiveFactsAsync();
        Task<Animal?> GetAnimalByIdAsync(int id);
        Task<Animal?> GetBySyncIdAsync(Guid syncId);
        Task<IReadOnlyList<Animal>> GetChangesAsync(ulong since, int take);
        Task<List<int>> GetExistingAnimalIdsAsync(IReadOnlyCollection<int> ids);
        Task<IEnumerable<Animal>> GetBreedingEligibleAnimalsAsync(string? search);
        Task<Animal> CreateAnimalAsync(Animal animal);
        Task<Animal> UpdateAnimalAsync(Animal animal);
        Task<bool> TagNumberExistsAsync(string tagNumber, int? excludeAnimalId = null);
    }
}
