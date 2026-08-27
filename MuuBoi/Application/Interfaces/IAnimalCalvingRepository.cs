using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalCalvingRepository
    {
        Task<AnimalCalving?> GetByIdAsync(int id);
        Task<AnimalCalving> CreateAsync(AnimalCalving calving);
        Task<AnimalCalving> UpdateAsync(AnimalCalving calving);
        Task<bool> HasActiveByPregnancyIdAsync(int pregnancyId);
        Task<AnimalCalving?> GetLastActiveByAnimalIdAsync(int animalId);
    }
}
