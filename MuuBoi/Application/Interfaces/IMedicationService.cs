using MuuBoi.Application.DTOs;
using MuuBoi.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IMedicationService
    {
        Task<IEnumerable<MedicationDto>> GetAllMedicationsAsync(string userId);
        Task<MedicationDto?> GetMedicationByIdAsync(int id, string userId);
        Task<MedicationDto> CreateMedicationAsync(MedicationCreateDto dto, string userId);
        Task<MedicationDto?> UpdateMedicationAsync(int id, MedicationUpdateDto dto, string userId);
        Task<MedicationDto?> DeleteMedicationAsync(int id, string userId);
    }
}
