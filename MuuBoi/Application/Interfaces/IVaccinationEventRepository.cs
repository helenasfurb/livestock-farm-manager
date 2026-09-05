using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;

namespace MuuBoi.Application.Interfaces
{
    public interface IVaccinationEventRepository
    {
        Task<IEnumerable<VaccinationEvent>> GetAllAsync(VaccinationEventFilterDto filter);
        Task<VaccinationEvent?> GetByIdAsync(int id);
        Task<VaccinationEvent> CreateAsync(VaccinationEvent vaccinationEvent);
        Task<VaccinationEvent> UpdateAsync(VaccinationEvent vaccinationEvent);

        /// <summary>The active booster child of a parent event, if any (one child per parent).</summary>
        Task<VaccinationEvent?> GetActiveChildByParentIdAsync(int parentEventId);

        /// <summary>Active applied events that include the given animal, most recent first (history).</summary>
        Task<IEnumerable<VaccinationEvent>> GetAppliedByAnimalAsync(int animalId);

        /// <summary>
        /// Predicted date of the active booster child for each of the given parent events, in a
        /// single query. Key = parent event id, value = child's predicted date (next dose).
        /// </summary>
        Task<Dictionary<int, DateTime?>> GetChildPredictedDatesAsync(IReadOnlyCollection<int> parentEventIds);
    }
}
