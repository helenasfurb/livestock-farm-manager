using MuuBoi.Application.Interfaces;

namespace MuuBoi.Infrastructure.Services
{
    public class UsageFlushService : BackgroundService
    {
        private static readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan PurgeInterval = TimeSpan.FromDays(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<UsageFlushService> _logger;

        public UsageFlushService(IServiceScopeFactory scopeFactory, ILogger<UsageFlushService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(FlushInterval);
            var lastPurge = DateTime.MinValue;

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RunAsync(s => s.FlushAsync());

                    if (DateTime.UtcNow - lastPurge >= PurgeInterval)
                    {
                        await RunAsync(s => s.PurgeLoginEventsAsync());
                        lastPurge = DateTime.UtcNow;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }

            await RunAsync(s => s.FlushAsync());
        }

        private async Task RunAsync(Func<IUsageService, Task<bool>> action)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await action(scope.ServiceProvider.GetRequiredService<IUsageService>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist usage counters.");
            }
        }
    }
}
