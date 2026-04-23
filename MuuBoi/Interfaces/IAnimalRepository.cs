using MuuBoi.Models;

namespace MuuBoi.Interfaces
{
    public interface IAnimalRepository
    {
        Task<IEnumerable<Animal>> GetAllAnimalsAsync();
        Task<Animal?> GetAnimalByIdAsync(int id);
        Task<Animal> CreateAnimalAsync(Animal animal);
        Task<Animal?> UpdateAnimalAsync(Animal animal);
        Task<Animal?> DeleteAnimalAsync(int id);
    }
}
