using MuuBoi.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IMedicationRepository
    {
        Task<IEnumerable<Medication>> GetAllMedicationsAsync(string userId);
        Task<Medication?> GetMedicationByIdAsync(int id);
        Task<Medication> CreateMedicationAsync(Medication medication);
        Task<Medication?> UpdateMedicationAsync(Medication medication);
        Task<Medication?> DeleteMedicationAsync(int id);
    }
}
