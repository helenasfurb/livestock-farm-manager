using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class HealthCaseRepository : IHealthCaseRepository
    {
        private readonly ApplicationDbContext _context;

        public HealthCaseRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<HealthCase>> GetAllAsync(HealthCaseFilterDto filter)
        {
            var query = _context.HealthCases
                .Include(c => c.Animal)
                .Include(c => c.Medications!.Where(m => m.IsActive))
                .AsSplitQuery()
                .AsQueryable();

            if (filter.IsActive.HasValue)
                query = query.Where(c => c.IsActive == filter.IsActive.Value);
            else
                query = query.Where(c => c.IsActive);

            if (filter.DiseaseType.HasValue)
                query = query.Where(c => c.DiseaseType == filter.DiseaseType.Value);

            if (filter.AnimalId.HasValue)
                query = query.Where(c => c.AnimalId == filter.AnimalId.Value);

            if (filter.DateFrom.HasValue)
                query = query.Where(c => c.DiagnosisDate >= filter.DateFrom.Value);

            if (filter.DateTo.HasValue)
                query = query.Where(c => c.DiagnosisDate <= filter.DateTo.Value);

            return await query
                .OrderByDescending(c => c.DiagnosisDate)
                .ToListAsync();
        }

        public async Task<HealthCase?> GetByIdAsync(int id)
        {
            return await _context.HealthCases
                .Include(c => c.Animal)
                .Include(c => c.Tests!.Where(t => t.IsActive))
                .Include(c => c.Medications!.Where(m => m.IsActive))
                    .ThenInclude(m => m.Medication)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<HealthCase> CreateAsync(HealthCase healthCase)
        {
            _context.HealthCases.Add(healthCase);
            await _context.SaveChangesAsync();
            return healthCase;
        }

        public async Task<HealthCase> UpdateAsync(HealthCase healthCase)
        {
            _context.HealthCases.Update(healthCase);
            await _context.SaveChangesAsync();
            return healthCase;
        }

        public async Task<IEnumerable<HealthCase>> GetByAnimalAsync(int animalId)
        {
            return await _context.HealthCases
                .Include(c => c.Animal)
                .Include(c => c.Medications!.Where(m => m.IsActive))
                .AsSplitQuery()
                .Where(c => c.IsActive && c.AnimalId == animalId)
                .OrderByDescending(c => c.DiagnosisDate)
                .ToListAsync();
        }

        public async Task<AnimalMedication> AddMedicationAsync(AnimalMedication medication)
        {
            _context.AnimalMedications.Add(medication);
            await _context.SaveChangesAsync();
            return medication;
        }

        public async Task<AnimalMedication?> GetMedicationAsync(int medicationId, int healthCaseId)
        {
            return await _context.AnimalMedications
                .Include(m => m.Medication)
                .FirstOrDefaultAsync(m => m.Id == medicationId && m.HealthCaseId == healthCaseId);
        }

        public async Task<AnimalMedication> UpdateMedicationAsync(AnimalMedication medication)
        {
            _context.AnimalMedications.Update(medication);
            await _context.SaveChangesAsync();
            return medication;
        }

        public async Task<MastitisTest> AddTestAsync(MastitisTest test)
        {
            _context.MastitisTests.Add(test);
            await _context.SaveChangesAsync();
            return test;
        }

        public async Task<MastitisTest?> GetTestAsync(int testId, int healthCaseId)
        {
            return await _context.MastitisTests
                .FirstOrDefaultAsync(t => t.Id == testId && t.HealthCaseId == healthCaseId);
        }

        public async Task<MastitisTest> UpdateTestAsync(MastitisTest test)
        {
            _context.MastitisTests.Update(test);
            await _context.SaveChangesAsync();
            return test;
        }

        public async Task<Dictionary<int, AnimalSanitaryFacts>> GetSanitaryFactsMapAsync(
            IReadOnlyCollection<int> animalIds, DateTime utcNow)
        {
            var result = new Dictionary<int, AnimalSanitaryFacts>();
            if (animalIds == null || animalIds.Count == 0)
                return result;

            var cases = await _context.HealthCases
                .Where(c => c.IsActive && animalIds.Contains(c.AnimalId))
                .Select(c => new
                {
                    c.AnimalId,
                    c.ResolvedAt,
                    HasMedication = c.Medications!.Any(m => m.IsActive)
                })
                .ToListAsync();

            var meds = await _context.AnimalMedications
                .Where(m => m.IsActive
                            && m.WithdrawalPeriodDays != null
                            && animalIds.Contains(m.AnimalId))
                .Select(m => new { m.AnimalId, m.ApplicationDate, Days = m.WithdrawalPeriodDays!.Value })
                .ToListAsync();

            var today = utcNow.Date;

            foreach (var animalId in animalIds.Distinct())
            {
                var animalCases = cases.Where(c => c.AnimalId == animalId).ToList();
                var liberations = meds
                    .Where(m => m.AnimalId == animalId)
                    .Select(m => m.ApplicationDate.Date.AddDays(m.Days))
                    .Where(liberation => liberation > today)   // still withholding (inclusive boundary)
                    .ToList();

                var facts = new AnimalSanitaryFacts
                {
                    HasCaseUnderTreatment = animalCases.Any(c => c.HasMedication && c.ResolvedAt == null),
                    HasSuspectedCase = animalCases.Any(c => !c.HasMedication && c.ResolvedAt == null),
                    MilkWithheldUntil = liberations.Count > 0 ? liberations.Max() : null
                };

                if (facts.HasCaseUnderTreatment || facts.HasSuspectedCase || facts.MilkWithheldUntil.HasValue)
                    result[animalId] = facts;
            }

            return result;
        }
    }
}
