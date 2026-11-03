using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Repositories
{
    public class PortalRepository : IPortalRepository
    {
        private static readonly int ActivePropertyDays = 30;

        private readonly ApplicationDbContext _context;

        public PortalRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PortalTotalsFact> GetTotalsAsync(DateTime nowUtc, DateTime newSinceUtc)
        {
            var users = _context.Users.AsNoTracking();
            var animals = _context.Animals.IgnoreQueryFilters().AsNoTracking();
            var activeSince = nowUtc.AddDays(-ActivePropertyDays);

            return new PortalTotalsFact
            {
                Properties = await _context.Properties.CountAsync(),
                NewProperties = await _context.Properties.CountAsync(p => p.CreatedAt >= newSinceUtc),
                ActiveProperties = await users.Where(u => u.LastSeenAt >= activeSince)
                    .Select(u => u.PropertyId).Distinct().CountAsync(),
                Users = await users.CountAsync(),
                ActiveUsers = await users.CountAsync(u => u.IsActive),
                Admins = await users.CountAsync(u => u.Role == UserRole.Admin),
                SeenLastDay = await users.CountAsync(u => u.LastSeenAt >= nowUtc.AddDays(-1)),
                SeenLastWeek = await users.CountAsync(u => u.LastSeenAt >= nowUtc.AddDays(-7)),
                SeenLastMonth = await users.CountAsync(u => u.LastSeenAt >= activeSince),
                ActiveAnimals = await animals.CountAsync(a => a.IsActive),
                TotalAnimals = await animals.CountAsync()
            };
        }

        public async Task<IReadOnlyList<PortalDailyUsageDto>> GetDailyUsageAsync(DateTime fromDate)
        {
            return await _context.UsageDaily
                .AsNoTracking()
                .Where(u => u.Date >= fromDate)
                .GroupBy(u => u.Date)
                .Select(g => new PortalDailyUsageDto
                {
                    Date = g.Key,
                    Logins = g.Sum(u => u.Logins),
                    Pushes = g.Sum(u => u.Pushes),
                    Pulls = g.Sum(u => u.Pulls)
                })
                .ToListAsync();
        }

        public async Task<IReadOnlyList<PortalMonthFact>> GetNewPropertiesByMonthAsync(DateTime fromUtc)
        {
            return await _context.Properties
                .AsNoTracking()
                .Where(p => p.CreatedAt >= fromUtc)
                .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
                .Select(g => new PortalMonthFact { Year = g.Key.Year, Month = g.Key.Month, Count = g.Count() })
                .ToListAsync();
        }

        public async Task<PortalPropertyPageDto> GetPropertiesAsync(PortalPropertyFilterDto filter, DateTime usageFromDate)
        {
            var properties = _context.Properties.IgnoreQueryFilters().AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.Search))
                properties = properties.Where(p => p.Name.Contains(filter.Search.Trim()));

            var rows = properties.Select(p => new PortalPropertyRowDto
            {
                Id = p.Id,
                Name = p.Name,
                CreatedAt = p.CreatedAt,
                ActiveUsers = _context.Users.Count(u => u.PropertyId == p.Id && u.IsActive),
                TotalUsers = _context.Users.Count(u => u.PropertyId == p.Id),
                ActiveAnimals = _context.Animals.Count(a => a.PropertyId == p.Id && a.IsActive),
                LastSeenAt = _context.Users.Where(u => u.PropertyId == p.Id).Max(u => u.LastSeenAt),
                Logins = _context.UsageDaily.Where(u => u.PropertyId == p.Id && u.Date >= usageFromDate).Sum(u => (int?)u.Logins) ?? 0,
                Pushes = _context.UsageDaily.Where(u => u.PropertyId == p.Id && u.Date >= usageFromDate).Sum(u => (int?)u.Pushes) ?? 0,
                Pulls = _context.UsageDaily.Where(u => u.PropertyId == p.Id && u.Date >= usageFromDate).Sum(u => (int?)u.Pulls) ?? 0
            });

            rows = filter.Sort switch
            {
                "name" => rows.OrderBy(r => r.Name),
                "animals" => rows.OrderByDescending(r => r.ActiveAnimals).ThenBy(r => r.Name),
                _ => rows.OrderByDescending(r => r.LastSeenAt).ThenBy(r => r.Name)
            };

            return new PortalPropertyPageDto
            {
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = await properties.CountAsync(),
                Items = await rows.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync()
            };
        }
    }
}
