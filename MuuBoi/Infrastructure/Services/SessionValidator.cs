using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MuuBoi.Application.Interfaces;
using MuuBoi.Infrastructure.Data;

namespace MuuBoi.Infrastructure.Services
{
    public class SessionValidator : ISessionValidator
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

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

                return await context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new SessionState(u.IsActive, u.SecurityStamp))
                    .FirstOrDefaultAsync();
            });

            return state is { IsActive: true } && state.SecurityStamp == securityStamp;
        }

        public void Invalidate(string userId) => _cache.Remove(CacheKey(userId));

        private static string CacheKey(string userId) => $"session:{userId}";

        private sealed record SessionState(bool IsActive, string? SecurityStamp);
    }
}
