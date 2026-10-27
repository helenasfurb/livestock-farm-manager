using Microsoft.Extensions.Caching.Memory;

namespace MuuBoi.Application.Helpers
{
    public static class CodeAttemptLimiter
    {
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        public static bool IsLocked(IMemoryCache cache, string key)
            => cache.TryGetValue(key, out int failures) && failures >= MaxFailedAttempts;

        public static void RegisterFailure(IMemoryCache cache, string key)
        {
            cache.TryGetValue(key, out int failures);
            cache.Set(key, failures + 1, Window);
        }

        public static void Reset(IMemoryCache cache, string key) => cache.Remove(key);

        public static string KeyFor(string purpose, string email) => $"{purpose}-attempts:{email.Trim().ToUpperInvariant()}";
    }
}
