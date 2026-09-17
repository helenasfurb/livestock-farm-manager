using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface ILactationRepository
    {
        Task<IEnumerable<Lactation>> GetByAnimalIdAsync(int animalId);
        Task<Lactation?> GetByIdAsync(int id);
        Task<Lactation?> GetOpenByAnimalIdAsync(int animalId);
        Task<bool> HasOpenByAnimalIdAsync(int animalId);
        Task<Lactation?> GetByCalvingIdAsync(int calvingId);
        Task<IEnumerable<Lactation>> GetActiveByAnimalIdsAsync(IEnumerable<int> animalIds);
        Task<Lactation> CreateAsync(Lactation lactation);
        Task<Lactation> UpdateAsync(Lactation lactation);
    }
}
