using MuuBoi.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IAnimalMedicationRepository
    {
        Task<IEnumerable<AnimalMedication>> GetAllAnimalMedicationsAsync(int animalId);
        Task<AnimalMedication?> GetAnimalMedicationByIdAsync(int id, int animalId);
        Task<AnimalMedication> CreateAnimalMedicationAsync(AnimalMedication medication);
        Task<AnimalMedication?> UpdateAnimalMedicationAsync(AnimalMedication medication);
        Task<AnimalMedication?> DeleteAnimalMedicationAsync(int id, int animalId);
    }
}
