using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly ApplicationDbContext _context;

        public AccountRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<string>> GetUserIdsByPropertyAsync(Guid propertyId)
        {
            return await _context.Users
                .Where(u => u.PropertyId == propertyId)
                .Select(u => u.Id)
                .ToListAsync();
        }

        public async Task<bool> DeletePropertyAsync(Guid propertyId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var animalIds = _context.Animals
                .IgnoreQueryFilters()
                .Where(a => a.PropertyId == propertyId)
                .Select(a => a.Id);

            await _context.MastitisTests.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.AnimalMedications.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.HealthCases.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();

            await _context.VaccinationEventAnimals.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.VaccinationEvents.IgnoreQueryFilters()
                .Where(x => x.PropertyId == propertyId && x.ParentEventId != null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ParentEventId, (int?)null));
            await _context.VaccinationEvents.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();

            await _context.Lactations.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.AnimalCalvingCalves.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.AnimalCalvings.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.AnimalPregnancies.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.SemenSampleMovements.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.BreedingEvents.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.SemenSamples.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();

            await _context.MilkProductions.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.WeightRecords.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.BodyConditionRecords.Where(x => animalIds.Contains(x.AnimalId)).ExecuteDeleteAsync();
            await _context.AnimalExitRecords.Where(x => animalIds.Contains(x.AnimalId)).ExecuteDeleteAsync();

            await _context.StockMovements.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.StockItems.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();

            await _context.Animals.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.Vaccines.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.Medications.IgnoreQueryFilters().Where(x => x.PropertyId == propertyId).ExecuteDeleteAsync();

            await _context.Users.Where(u => u.PropertyId == propertyId).ExecuteDeleteAsync();
            await _context.Properties.Where(p => p.Id == propertyId).ExecuteDeleteAsync();

            await transaction.CommitAsync();

            return true;
        }
    }
}
