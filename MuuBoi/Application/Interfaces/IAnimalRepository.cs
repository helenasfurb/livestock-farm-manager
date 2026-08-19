using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalRepository
    {
        Task<IEnumerable<Animal>> GetAllAnimalsAsync(AnimalFilterDto filter);
        Task<Animal?> GetAnimalByIdAsync(int id);
        Task<Animal> CreateAnimalAsync(Animal animal);
        Task<Animal> UpdateAnimalAsync(Animal animal);
        Task<bool> TagNumberExistsAsync(string tagNumber, int? excludeAnimalId = null);
    }
}
