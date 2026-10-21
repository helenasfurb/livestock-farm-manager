using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalExitRecordRepository
    {
        Task<IEnumerable<AnimalExitRecord>> GetByAnimalIdAsync(int animalId);
        Task<IReadOnlyList<AnimalExitRecord>> GetByAnimalIdsAsync(IReadOnlyCollection<int> animalIds);
    }
}
