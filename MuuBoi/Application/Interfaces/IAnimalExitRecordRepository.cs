using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalExitRecordRepository
    {
        Task<IEnumerable<AnimalExitRecord>> GetByAnimalIdAsync(int animalId);
        Task<AnimalExitRecord> CreateAsync(AnimalExitRecord record);
    }
}
