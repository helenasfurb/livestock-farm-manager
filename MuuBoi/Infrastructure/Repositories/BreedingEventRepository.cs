using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class BreedingEventRepository : IBreedingEventRepository
    {
        private readonly ApplicationDbContext _context;

        public BreedingEventRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<BreedingEvent>> GetByAnimalIdAsync(int animalId)
        {
            return await _context.BreedingEvents
                .Include(e => e.Animal)
                .Include(e => e.SemenSample)
                .Include(e => e.SireAnimal)
                .Where(e => e.AnimalId == animalId)
                .OrderByDescending(e => e.BreedingDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<BreedingEvent>> GetAllAsync(BreedingEventFilterDto filter)
        {
            var query = _context.BreedingEvents
                .Include(e => e.Animal)
                .AsQueryable();

            if (filter.AnimalId.HasValue)
                query = query.Where(e => e.AnimalId == filter.AnimalId);

            if (filter.ReproductionType.HasValue)
                query = query.Where(e => e.ReproductionType == filter.ReproductionType);

            if (filter.Status.HasValue)
                query = query.Where(e => e.Status == filter.Status);

            if (filter.BreedingDateFrom.HasValue)
                query = query.Where(e => e.BreedingDate >= filter.BreedingDateFrom);

            if (filter.BreedingDateTo.HasValue)
                query = query.Where(e => e.BreedingDate <= filter.BreedingDateTo);

            if (filter.IsActive.HasValue)
                query = query.Where(e => e.IsActive == filter.IsActive);

            return await query.OrderByDescending(e => e.BreedingDate).ToListAsync();
        }

        public async Task<BreedingEvent?> GetByIdAsync(int id)
        {
            return await _context.BreedingEvents
                .Include(e => e.Animal)
                .Include(e => e.SemenSample)
                .Include(e => e.SireAnimal)
                .Include(e => e.Pregnancy)
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<int> CountActiveByAnimalIdAsync(int animalId)
        {
            return await _context.BreedingEvents
                .CountAsync(e => e.AnimalId == animalId && e.IsActive);
        }

        public async Task<bool> HasActiveByAnimalIdAsync(int animalId)
        {
            return await _context.BreedingEvents
                .AnyAsync(e => e.AnimalId == animalId
                    && e.IsActive
                    && e.Status == ReproductiveEventStatus.AwaitingDiagnosis);
        }

        public async Task<DateTime?> GetLastActiveAwaitingDiagnosisDateAsync(int animalId)
        {
            return await _context.BreedingEvents
                .Where(e => e.AnimalId == animalId
                    && e.IsActive
                    && e.Status == ReproductiveEventStatus.AwaitingDiagnosis)
                .MaxAsync(e => (DateTime?)e.BreedingDate);
        }

        public async Task<(int Successful, int Unsuccessful, int Awaiting)> GetStatusCountsAsync(DateTime from, DateTime to)
        {
            var upperExclusive = to.Date.AddDays(1);
            var rows = await _context.BreedingEvents
                .Where(e => e.IsActive && e.BreedingDate >= from.Date && e.BreedingDate < upperExclusive)
                .GroupBy(e => e.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int CountFor(ReproductiveEventStatus status)
                => rows.FirstOrDefault(r => r.Status == status)?.Count ?? 0;

            return (
                CountFor(ReproductiveEventStatus.Successful),
                CountFor(ReproductiveEventStatus.Unsuccessful),
                CountFor(ReproductiveEventStatus.AwaitingDiagnosis));
        }

        public async Task<(int Successful, int Unsuccessful, int Awaiting)> GetFirstServiceStatusCountsAsync(DateTime from, DateTime to)
        {
            var upperExclusive = to.Date.AddDays(1);
            var rows = await _context.BreedingEvents
                .Where(e => e.IsActive
                    && e.ServiceNumber == 1
                    && e.BreedingDate >= from.Date && e.BreedingDate < upperExclusive)
                .GroupBy(e => e.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int CountFor(ReproductiveEventStatus status)
                => rows.FirstOrDefault(r => r.Status == status)?.Count ?? 0;

            return (
                CountFor(ReproductiveEventStatus.Successful),
                CountFor(ReproductiveEventStatus.Unsuccessful),
                CountFor(ReproductiveEventStatus.AwaitingDiagnosis));
        }

        public async Task<List<(int AnimalId, DateTime BreedingDate)>> GetSuccessfulBreedingsAsync(DateTime from, DateTime to)
        {
            var upperExclusive = to.Date.AddDays(1);
            var rows = await _context.BreedingEvents
                .Where(e => e.IsActive
                    && e.Status == ReproductiveEventStatus.Successful
                    && e.BreedingDate >= from.Date && e.BreedingDate < upperExclusive)
                .Select(e => new { e.AnimalId, e.BreedingDate })
                .ToListAsync();

            return rows.Select(r => (r.AnimalId, r.BreedingDate)).ToList();
        }

        public async Task<BreedingEvent?> GetBySyncIdAsync(Guid syncId)
        {
            return await _context.BreedingEvents
                .Include(e => e.Animal)
                .Include(e => e.SemenSample)
                .Include(e => e.SireAnimal)
                .Include(e => e.Pregnancy)
                .FirstOrDefaultAsync(e => e.SyncId == syncId);
        }

        public async Task<BreedingEvent> CreateAsync(BreedingEvent breedingEvent, SemenSampleMovement? semenOutput)
        {
            if (semenOutput != null)
            {
                semenOutput.BreedingEvent = breedingEvent;
                _context.SemenSampleMovements.Add(semenOutput);
            }

            return await _context.AddSyncableAsync(breedingEvent);
        }

        public async Task<BreedingEvent> UpdateAsync(BreedingEvent breedingEvent)
        {
            _context.BreedingEvents.Update(breedingEvent);
            await _context.SaveChangesAsync();
            return breedingEvent;
        }

        public async Task<BreedingEvent?> SaveDiagnosisAsync(BreedingEvent breedingEvent, AnimalPregnancy? pregnancy)
        {
            if (pregnancy != null)
            {
                pregnancy.BreedingEvent = breedingEvent;
                _context.AnimalPregnancies.Add(pregnancy);
            }

            try
            {
                await _context.SaveChangesAsync();
                return breedingEvent;
            }
            catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || IsPregnancyUniqueViolation(ex))
            {
                if (pregnancy != null)
                    _context.Entry(pregnancy).State = EntityState.Detached;
                _context.Entry(breedingEvent).State = EntityState.Detached;
                return null;
            }
        }

        public async Task<SemenSampleMovement?> GetSemenOutputAsync(int breedingEventId)
        {
            return await _context.SemenSampleMovements
                .FirstOrDefaultAsync(m => m.BreedingEventId == breedingEventId);
        }

        public async Task<IReadOnlyList<BreedingEvent>> GetChangesAsync(ulong since, int take)
        {
            return await _context.GetChangesSinceAsync<BreedingEvent>(since, take);
        }

        private static bool IsPregnancyUniqueViolation(DbUpdateException ex)
        {
            return ex.InnerException is SqlException sql
                && (sql.Number == 2601 || sql.Number == 2627)
                && (sql.Message.Contains("UX_AnimalPregnancies_SyncId") || sql.Message.Contains("IX_AnimalPregnancies_BreedingEventId"));
        }
    }
}
