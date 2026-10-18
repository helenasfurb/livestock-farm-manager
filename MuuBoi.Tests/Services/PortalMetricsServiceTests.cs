using Moq;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Services;

namespace MuuBoi.Tests.Services;

public class PortalMetricsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);

    private readonly Mock<IPortalRepository> _repository = new();
    private readonly PortalMetricsService _service;

    public PortalMetricsServiceTests()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(Now);
        _service = new PortalMetricsService(_repository.Object, clock.Object);
        _repository.Setup(r => r.GetTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new PortalTotalsFact());
        _repository.Setup(r => r.GetDailyUsageAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<PortalDailyUsageDto>());
        _repository.Setup(r => r.GetNewPropertiesByMonthAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<PortalMonthFact>());
    }

    [Fact]
    public async Task GetSummaryAsync_WithGapsInUsage_ReturnsOneEntryPerDayFilledWithZero()
    {
        _repository.Setup(r => r.GetDailyUsageAsync(new DateTime(2026, 10, 11)))
            .ReturnsAsync(new List<PortalDailyUsageDto> { new() { Date = new DateTime(2026, 10, 15), Logins = 3 } });
        var result = await _service.GetSummaryAsync(new PortalSummaryFilterDto { Days = 7 });
        Assert.Equal(7, result.DailyUsage.Count);
        Assert.Equal(new DateTime(2026, 10, 11), result.DailyUsage[0].Date);
        Assert.Equal(new DateTime(2026, 10, 17), result.DailyUsage[^1].Date);
        Assert.Equal(3, result.DailyUsage.Sum(d => d.Logins));
        Assert.Equal(3, result.DailyUsage.Single(d => d.Date == new DateTime(2026, 10, 15)).Logins);
    }

    [Fact]
    public async Task GetSummaryAsync_Always_ReturnsTwelveMonthsEndingInCurrentMonth()
    {
        _repository.Setup(r => r.GetNewPropertiesByMonthAsync(new DateTime(2025, 11, 1)))
            .ReturnsAsync(new List<PortalMonthFact> { new() { Year = 2026, Month = 8, Count = 3 } });
        var result = await _service.GetSummaryAsync(new PortalSummaryFilterDto());
        Assert.Equal(12, result.NewPropertiesByMonth.Count);
        Assert.Equal((2025, 11), (result.NewPropertiesByMonth[0].Year, result.NewPropertiesByMonth[0].Month));
        Assert.Equal((2026, 10), (result.NewPropertiesByMonth[^1].Year, result.NewPropertiesByMonth[^1].Month));
        Assert.Equal(3, result.NewPropertiesByMonth.Single(m => m.Year == 2026 && m.Month == 8).Count);
    }

    [Fact]
    public async Task GetSummaryAsync_WithTotals_DerivesRevokedUsersAndMembers()
    {
        _repository.Setup(r => r.GetTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new PortalTotalsFact { Users = 10, ActiveUsers = 8, Admins = 3 });
        var result = await _service.GetSummaryAsync(new PortalSummaryFilterDto());
        Assert.Equal(2, result.Users.Revoked);
        Assert.Equal(7, result.Users.Members);
    }

    [Fact]
    public async Task GetSummaryAsync_WithDailyUsage_SumsUsageTotals()
    {
        _repository.Setup(r => r.GetDailyUsageAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<PortalDailyUsageDto>
        {
            new() { Date = new DateTime(2026, 10, 16), Logins = 1, Pushes = 4, Pulls = 10 },
            new() { Date = new DateTime(2026, 10, 17), Logins = 2, Pushes = 1, Pulls = 5 }
        });
        var result = await _service.GetSummaryAsync(new PortalSummaryFilterDto { Days = 7 });
        Assert.Equal((3, 5, 15), (result.Usage.Logins, result.Usage.Pushes, result.Usage.Pulls));
    }

    [Fact]
    public async Task GetSummaryAsync_WithDays_QueriesNewPropertiesSinceThatManyDays()
    {
        await _service.GetSummaryAsync(new PortalSummaryFilterDto { Days = 30 });
        _repository.Verify(r => r.GetTotalsAsync(Now.UtcDateTime, Now.UtcDateTime.AddDays(-30)), Times.Once);
    }

    [Fact]
    public async Task GetPropertiesAsync_Always_UsesThirtyDayUsageWindow()
    {
        var filter = new PortalPropertyFilterDto();
        await _service.GetPropertiesAsync(filter);
        _repository.Verify(r => r.GetPropertiesAsync(filter, new DateTime(2026, 9, 18)), Times.Once);
    }
}
