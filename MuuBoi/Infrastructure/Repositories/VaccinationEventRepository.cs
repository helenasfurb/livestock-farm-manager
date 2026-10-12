using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class VaccinationEventRepository : IVaccinationEventRepository
    {
        private readonly ApplicationDbContext _context;

        public VaccinationEventRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<VaccinationEvent>> GetAllAsync(VaccinationEventFilterDto filter)
        {
            var query = _context.VaccinationEvents
                .Include(e => e.Vaccine)
                .Include(e => e.EventAnimals!)
                .AsSplitQuery()
                .AsQueryable();

            if (filter.IsActive.HasValue)
                query = query.Where(e => e.IsActive == filter.IsActive.Value);

            if (filter.VaccineId.HasValue)
                query = query.Where(e => e.VaccineId == filter.VaccineId.Value);

            if (filter.AnimalId.HasValue)
                query = query.Where(e => e.EventAnimals!.Any(a => a.AnimalId == filter.AnimalId.Value));

            if (filter.DateFrom.HasValue)
                query = query.Where(e => (e.ApplicationDate ?? e.PredictedDate) >= filter.DateFrom.Value);

            if (filter.DateTo.HasValue)
                query = query.Where(e => (e.ApplicationDate ?? e.PredictedDate) <= filter.DateTo.Value);

            return await query
                .OrderByDescending(e => e.ApplicationDate ?? e.PredictedDate)
                .ToListAsync();
        }

        public async Task<VaccinationEvent?> GetByIdAsync(int id)
        {
            return await _context.VaccinationEvents
                .Include(e => e.Vaccine)
                .Include(e => e.EventAnimals!)
                    .ThenInclude(a => a.Animal)
                .Include(e => e.ParentEvent)
                .AsSplitQuery()
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<VaccinationEvent?> GetBySyncIdAsync(Guid syncId)
        {
            return await _context.VaccinationEvents
                .Include(e => e.Vaccine)
                .Include(e => e.EventAnimals!)
                    .ThenInclude(a => a.Animal)
                .Include(e => e.ParentEvent)
                .AsSplitQuery()
                .FirstOrDefaultAsync(e => e.SyncId == syncId);
        }

        public async Task<VaccinationEvent> CreateAsync(VaccinationEvent vaccinationEvent)
        {
            return await _context.AddSyncableAsync(vaccinationEvent);
        }

        public async Task<VaccinationEvent?> CreateBoosterAsync(VaccinationEvent booster)
        {
            try
            {
                return await _context.AddSyncableAsync(booster);
            }
            catch (DbUpdateException ex) when (IsActiveBoosterUniqueViolation(ex))
            {
                foreach (var link in booster.EventAnimals?.ToList() ?? new List<VaccinationEventAnimal>())
                    _context.Entry(link).State = EntityState.Detached;
                _context.Entry(booster).State = EntityState.Detached;
                return null;
            }
        }

        public async Task<IReadOnlyList<VaccinationEvent>> GetChangesAsync(ulong since, int take)
        {
            return await _context.GetChangesSinceAsync<VaccinationEvent>(since, take);
        }

        public async Task<Dictionary<int, List<int>>> GetAnimalIdsByEventIdsAsync(IReadOnlyCollection<int> eventIds)
        {
            if (eventIds.Count == 0)
                return new Dictionary<int, List<int>>();

            var links = await _context.VaccinationEventAnimals
                .Where(a => eventIds.Contains(a.VaccinationEventId))
                .Select(a => new { a.VaccinationEventId, a.AnimalId })
                .ToListAsync();

            return links
                .GroupBy(l => l.VaccinationEventId)
                .ToDictionary(g => g.Key, g => g.Select(l => l.AnimalId).OrderBy(id => id).ToList());
        }

        private static bool IsActiveBoosterUniqueViolation(DbUpdateException ex)
        {
            return ex.InnerException is SqlException sql
                && (sql.Number == 2601 || sql.Number == 2627)
                && sql.Message.Contains("UX_VaccinationEvents_ParentEventId_Active");
        }

        public async Task<VaccinationEvent> UpdateAsync(VaccinationEvent vaccinationEvent)
        {
            _context.VaccinationEvents.Update(vaccinationEvent);
            await _context.SaveChangesAsync();
            return vaccinationEvent;
        }

        public async Task<VaccinationEvent?> GetActiveChildByParentIdAsync(int parentEventId)
        {
            return await _context.VaccinationEvents
                .Include(e => e.EventAnimals!)
                .FirstOrDefaultAsync(e => e.ParentEventId == parentEventId && e.IsActive);
        }

        public async Task<IEnumerable<VaccinationEvent>> GetAppliedByAnimalAsync(int animalId)
        {
            return await _context.VaccinationEvents
                .Include(e => e.Vaccine)
                .Where(e => e.IsActive
                            && e.ApplicationDate != null
                            && e.EventAnimals!.Any(a => a.AnimalId == animalId))
                .OrderByDescending(e => e.ApplicationDate)
                .ToListAsync();
        }

        public async Task<Dictionary<int, DateTime?>> GetChildPredictedDatesAsync(IReadOnlyCollection<int> parentEventIds)
        {
            if (parentEventIds == null || parentEventIds.Count == 0)
                return new Dictionary<int, DateTime?>();

            return await _context.VaccinationEvents
                .Where(e => e.IsActive
                            && e.ParentEventId != null
                            && parentEventIds.Contains(e.ParentEventId.Value))
                .Select(e => new { ParentId = e.ParentEventId!.Value, e.PredictedDate })
                .ToDictionaryAsync(x => x.ParentId, x => x.PredictedDate);
        }
    }
}
