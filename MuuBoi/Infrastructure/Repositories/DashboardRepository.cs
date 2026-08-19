using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Infrastructure.Data;
using MuuBoi.Application.DTOs;

namespace MuuBoi.Infrastructure.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly ApplicationDbContext _context;

        public DashboardRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardCardsDto> GetCardsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var baseQuery = _context.Animals.Where(a => a.IsActive);

            var total = await baseQuery.CountAsync();
            var pregnant = await baseQuery.CountAsync(a => a.IsPregnant);
            var treatments = await _context.AnimalMedications
                .Where(am => am.Animal!.IsActive)
                .Where(am => am.EndDate == null || am.EndDate.Value.Date >= today)
                .CountAsync();

            return new DashboardCardsDto
            {
                TotalAnimals = total,
                PregnantAnimals = pregnant,
                ActiveTreatments = treatments
            };
        }

        public async Task<IEnumerable<GenderDistributionDto>> GetGenderDistributionAsync()
        {
            var raw = await _context.Animals
                .Where(a => a.IsActive && a.Gender != null)
                .GroupBy(a => a.Gender!)
                .Select(g => new { Gender = g.Key, Count = g.Count() })
                .OrderBy(x => x.Gender)
                .ToListAsync();

            return raw.Select(x => new GenderDistributionDto
            {
                Gender = x.Gender!.Value.ToString(),
                Label = x.Gender!.Value.GetDescription(),
                Count = x.Count
            });
        }

        public async Task<IEnumerable<BreedDistributionDto>> GetBreedDistributionAsync()
        {
            var raw = await _context.Animals
                .Where(a => a.IsActive && a.BreedId != null)
                .GroupBy(a => new { a.BreedId, a.Breed!.Name })
                .Select(g => new { BreedId = g.Key.BreedId!.Value, BreedName = g.Key.Name, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return raw.Select(x => new BreedDistributionDto
            {
                BreedId = x.BreedId,
                BreedName = x.BreedName,
                Count = x.Count
            });
        }

        public async Task<IEnumerable<VaccinePerMonthDto>> GetVaccinesPerMonthAsync(int months = 12)
        {
            var cutoff = DateTime.UtcNow.AddMonths(-months);

            var raw = await _context.AnimalVaccinations
                .Where(av => av.Animal!.IsActive)
                .Where(av => av.ApplicationDate >= cutoff)
                .GroupBy(av => new { av.ApplicationDate.Year, av.ApplicationDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            return raw.Select(x => new VaccinePerMonthDto
            {
                Year = x.Year,
                Month = x.Month,
                MonthLabel = $"{new DateTime(x.Year, x.Month, 1):MMM/yyyy}",
                Count = x.Count
            });
        }

        public async Task<IEnumerable<BirthForecastDto>> GetBirthForecastAsync()
        {
            return await _context.Animals
                .Where(a => a.IsActive && a.IsPregnant && a.ExpectedBirthDate != null)
                .OrderBy(a => a.ExpectedBirthDate)
                .Select(a => new BirthForecastDto
                {
                    AnimalId = a.Id,
                    AnimalName = a.Name,
                    TagNumber = a.TagNumber,
                    ExpectedBirthDate = a.ExpectedBirthDate!.Value
                })
                .ToListAsync();
        }
    }
}
