using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MuuBoi.Application.Interfaces;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Services
{
    public class SessionValidator : ISessionValidator
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan LastSeenThreshold = TimeSpan.FromMinutes(15);

        private readonly IMemoryCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;

        public SessionValidator(IMemoryCache cache, IServiceScopeFactory scopeFactory)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
        }

        public async Task<bool> IsValidAsync(string userId, string securityStamp)
        {
            var state = await _cache.GetOrCreateAsync(CacheKey(userId), async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var user = await context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.IsActive, u.SecurityStamp, u.LastSeenAt })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return null;

                var now = DateTime.UtcNow;
                if (user.IsActive && user.SecurityStamp == securityStamp
                    && (user.LastSeenAt == null || now - user.LastSeenAt >= LastSeenThreshold))
                {
                    await context.Users
                        .Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenAt, now));
                }

                return new SessionState(user.IsActive, user.SecurityStamp);
            });

            return state is { IsActive: true } && state.SecurityStamp == securityStamp;
        }

        public void Invalidate(string userId) => _cache.Remove(CacheKey(userId));

        private static string CacheKey(string userId) => $"session:{userId}";

        private sealed record SessionState(bool IsActive, string? SecurityStamp);
    }
}
