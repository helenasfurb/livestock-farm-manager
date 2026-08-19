using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IMedicationService
    {
        Task<IEnumerable<MedicationDto>> GetAllMedicationsAsync();
        Task<MedicationDto?> GetMedicationByIdAsync(int id);
        Task<MedicationDto> CreateMedicationAsync(MedicationCreateDto dto);
        Task<MedicationDto?> UpdateMedicationAsync(int id, MedicationUpdateDto dto);
        Task<MedicationDto?> DeleteMedicationAsync(int id);
    }
}
