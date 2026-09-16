using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly ApplicationDbContext _context;

        public DashboardRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AnimalCompositionFact>> GetActiveAnimalCompositionFactsAsync()
        {
            return await _context.Animals
                .Where(a => a.IsActive)
                .Select(a => new AnimalCompositionFact
                {
                    Classification = a.Classification,
                    Gender = a.Gender,
                    Breed = a.Breed
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<VaccinationEventFact>> GetVaccinationEventFactsAsync(DateTime cutoffUtc)
        {
            // Raw facts, not aggregated: applied events within the window (per-month series) plus every
            // not-yet-applied event (overdue is derived on read). AnimalCount counts active animals only,
            // preserving the previous "one dose per animal" metric.
            return await _context.VaccinationEvents
                .Where(e => e.IsActive)
                .Where(e => e.ApplicationDate >= cutoffUtc || e.ApplicationDate == null)
                .Select(e => new VaccinationEventFact
                {
                    VaccinationEventId = e.Id,
                    VaccineName = e.Vaccine!.Name,
                    ApplicationDate = e.ApplicationDate,
                    PredictedDate = e.PredictedDate,
                    AnimalCount = e.EventAnimals!.Count(ea => ea.Animal!.IsActive)
                })
                .ToListAsync();
        }
    }
}
