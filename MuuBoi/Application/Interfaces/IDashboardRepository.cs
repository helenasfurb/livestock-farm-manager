using MuuBoi.Application.DTOs;

namespace MuuBoi.Application.Interfaces
{
    public interface IDashboardRepository
    {
        Task<IEnumerable<AnimalCompositionFact>> GetActiveAnimalCompositionFactsAsync();
        Task<IEnumerable<VaccinationEventFact>> GetVaccinationEventFactsAsync(DateTime cutoffUtc);
    }
}
