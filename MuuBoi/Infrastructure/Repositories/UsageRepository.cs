using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Models;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class UsageRepository : IUsageRepository
    {
        private readonly ApplicationDbContext _context;

        public UsageRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> AddLoginEventAsync(LoginEvent loginEvent)
        {
            _context.LoginEvents.Add(loginEvent);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AddUsageAsync(UsageCountDto count)
        {
            if (await IncrementAsync(count) > 0)
                return true;

            _context.UsageDaily.Add(new UsageDaily
            {
                PropertyId = count.PropertyId,
                Date = count.Date,
                Logins = count.Logins,
                Pushes = count.Pushes,
                Pulls = count.Pulls
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                await IncrementAsync(count);
            }

            return true;
        }

        public async Task<int> DeleteLoginEventsBeforeAsync(DateTime cutoffUtc)
        {
            return await _context.LoginEvents
                .Where(e => e.OccurredAt < cutoffUtc)
                .ExecuteDeleteAsync();
        }

        private Task<int> IncrementAsync(UsageCountDto count)
        {
            return _context.UsageDaily
                .Where(u => u.PropertyId == count.PropertyId && u.Date == count.Date)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.Logins, u => u.Logins + count.Logins)
                    .SetProperty(u => u.Pushes, u => u.Pushes + count.Pushes)
                    .SetProperty(u => u.Pulls, u => u.Pulls + count.Pulls)
                    .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
        }
    }
}
