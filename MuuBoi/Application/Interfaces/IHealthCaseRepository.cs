using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IHealthCaseRepository
    {
        Task<IEnumerable<HealthCase>> GetAllAsync(HealthCaseFilterDto filter);
        Task<HealthCase?> GetByIdAsync(int id);
        Task<HealthCase?> GetBySyncIdAsync(Guid syncId);
        Task<HealthCase> CreateAsync(HealthCase healthCase);
        Task<HealthCase> UpdateAsync(HealthCase healthCase);

        /// <summary>Active cases that include the given animal, most recent diagnosis first (history).</summary>
        Task<IEnumerable<HealthCase>> GetByAnimalAsync(int animalId);

        Task<AnimalMedication> AddMedicationAsync(AnimalMedication medication);
        Task<AnimalMedication?> GetMedicationAsync(int medicationId, int healthCaseId);
        Task<AnimalMedication?> GetMedicationBySyncIdAsync(Guid syncId);
        Task<AnimalMedication> UpdateMedicationAsync(AnimalMedication medication);

        Task<MastitisTest> AddTestAsync(MastitisTest test);
        Task<MastitisTest?> GetTestAsync(int testId, int healthCaseId);
        Task<MastitisTest?> GetTestBySyncIdAsync(Guid syncId);

        Task<IReadOnlyList<HealthCase>> GetChangesAsync(ulong since, int take);
        Task<List<AnimalMedication>> GetMedicationsByCaseIdsAsync(IReadOnlyCollection<int> caseIds);
        Task<List<MastitisTest>> GetTestsByCaseIdsAsync(IReadOnlyCollection<int> caseIds);
        Task<MastitisTest> UpdateTestAsync(MastitisTest test);

        /// <summary>
        /// Sanitary facts for each of the given animals, in a bounded number of queries (no N+1).
        /// Key = animal id; animals with no facts are absent (caller treats as Healthy).
        /// </summary>
        Task<Dictionary<int, AnimalSanitaryFacts>> GetSanitaryFactsMapAsync(
            IReadOnlyCollection<int> animalIds, DateTime utcNow);
    }
}
