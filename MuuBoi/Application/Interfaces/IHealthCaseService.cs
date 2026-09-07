using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IHealthCaseService
    {
        Task<IEnumerable<HealthCaseListItemDto>> GetAllAsync(HealthCaseFilterDto filter);
        Task<HealthCaseDto> GetByIdAsync(int id);
        Task<HealthCaseDto> CreateAsync(HealthCaseCreateDto dto);
        Task<HealthCaseDto> UpdateAsync(int id, HealthCaseUpdateDto dto);
        Task<bool> DeactivateAsync(int id);

        Task<MedicationUseDto> AddMedicationAsync(int healthCaseId, MedicationUseCreateDto dto);
        Task<bool> DeactivateMedicationAsync(int healthCaseId, int medicationId);

        Task<MastitisTestDto> AddTestAsync(int healthCaseId, MastitisTestCreateDto dto);
        Task<bool> DeactivateTestAsync(int healthCaseId, int testId);

        Task<IEnumerable<HealthCaseListItemDto>> GetAnimalHistoryAsync(int animalId);

        /// <summary>Resolved sanitary status per animal (bulk, no N+1). Animals with no facts are absent (Healthy).</summary>
        Task<Dictionary<int, AnimalSanitaryStatusDto>> GetSanitaryStatusMapAsync(IReadOnlyCollection<int> animalIds);
    }
}
