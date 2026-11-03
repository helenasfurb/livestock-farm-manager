using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;

namespace MuuBoi.Application.Services
{
    public class PortalMetricsService : IPortalMetricsService
    {
        private static readonly int ListUsageDays = 30;
        private static readonly int MonthsInSeries = 12;

        private readonly IPortalRepository _portalRepository;
        private readonly TimeProvider _timeProvider;

        public PortalMetricsService(IPortalRepository portalRepository, TimeProvider timeProvider)
        {
            _portalRepository = portalRepository;
            _timeProvider = timeProvider;
        }

        public async Task<PortalSummaryDto> GetSummaryAsync(PortalSummaryFilterDto filter)
        {
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var today = BrazilTime.ToLocalDate(nowUtc);
            var fromDate = today.AddDays(-(filter.Days - 1));
            var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-(MonthsInSeries - 1));

            var totals = await _portalRepository.GetTotalsAsync(nowUtc, nowUtc.AddDays(-filter.Days));
            var usage = await _portalRepository.GetDailyUsageAsync(fromDate);
            var months = await _portalRepository.GetNewPropertiesByMonthAsync(firstMonth);

            var usageByDate = usage.ToDictionary(u => u.Date.Date);
            var dailyUsage = Enumerable.Range(0, filter.Days)
                .Select(i => fromDate.AddDays(i))
                .Select(d => usageByDate.TryGetValue(d, out var u)
                    ? u
                    : new PortalDailyUsageDto { Date = d })
                .ToList();

            var newByMonth = Enumerable.Range(0, MonthsInSeries)
                .Select(i => firstMonth.AddMonths(i))
                .Select(m => new PortalMonthlyCountDto
                {
                    Year = m.Year,
                    Month = m.Month,
                    Count = months.FirstOrDefault(f => f.Year == m.Year && f.Month == m.Month)?.Count ?? 0
                })
                .ToList();

            return new PortalSummaryDto
            {
                Days = filter.Days,
                Properties = new PortalPropertyTotalsDto
                {
                    Total = totals.Properties,
                    New = totals.NewProperties,
                    Active = totals.ActiveProperties
                },
                Users = new PortalUserTotalsDto
                {
                    Total = totals.Users,
                    Active = totals.ActiveUsers,
                    Revoked = totals.Users - totals.ActiveUsers,
                    Admins = totals.Admins,
                    Members = totals.Users - totals.Admins,
                    ActiveLastDay = totals.SeenLastDay,
                    ActiveLastWeek = totals.SeenLastWeek,
                    ActiveLastMonth = totals.SeenLastMonth
                },
                Animals = new PortalAnimalTotalsDto
                {
                    Active = totals.ActiveAnimals,
                    Total = totals.TotalAnimals
                },
                Usage = new PortalUsageTotalsDto
                {
                    Logins = dailyUsage.Sum(d => d.Logins),
                    Pushes = dailyUsage.Sum(d => d.Pushes),
                    Pulls = dailyUsage.Sum(d => d.Pulls)
                },
                DailyUsage = dailyUsage,
                NewPropertiesByMonth = newByMonth
            };
        }

        public async Task<PortalPropertyPageDto> GetPropertiesAsync(PortalPropertyFilterDto filter)
        {
            var today = BrazilTime.ToLocalDate(_timeProvider.GetUtcNow().UtcDateTime);

            return await _portalRepository.GetPropertiesAsync(filter, today.AddDays(-(ListUsageDays - 1)));
        }
    }
}
