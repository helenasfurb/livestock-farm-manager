using Moq;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Services;
using MuuBoi.Domain.Models;

namespace MuuBoi.Tests.Services;

public class UsageServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);

    private readonly Mock<IUsageRepository> _repository = new();
    private readonly Mock<IUsageTracker> _tracker = new();
    private readonly UsageService _service;

    public UsageServiceTests()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(Now);
        _service = new UsageService(_repository.Object, _tracker.Object, clock.Object);
    }

    [Fact]
    public async Task RecordLoginAsync_WithUser_AddsLoginEventAndRecordsLogin()
    {
        var propertyId = Guid.NewGuid();
        await _service.RecordLoginAsync("user-1", propertyId);
        _repository.Verify(r => r.AddLoginEventAsync(It.Is<LoginEvent>(e =>
            e.UserId == "user-1" && e.PropertyId == propertyId && e.OccurredAt == Now.UtcDateTime)), Times.Once);
        _tracker.Verify(t => t.RecordLogin(propertyId), Times.Once);
    }

    [Fact]
    public async Task FlushAsync_WithCounts_PersistsEachCount()
    {
        var counts = new List<UsageCountDto> { new() { PropertyId = Guid.NewGuid() }, new() { PropertyId = Guid.NewGuid() } };
        _tracker.Setup(t => t.Drain()).Returns(counts);
        await _service.FlushAsync();
        _repository.Verify(r => r.AddUsageAsync(It.IsAny<UsageCountDto>()), Times.Exactly(2));
        _tracker.Verify(t => t.Restore(It.IsAny<IEnumerable<UsageCountDto>>()), Times.Never);
    }

    [Fact]
    public async Task FlushAsync_WhenPersistFails_RestoresUnsavedCountsAndRethrows()
    {
        var first = new UsageCountDto { PropertyId = Guid.NewGuid() };
        var second = new UsageCountDto { PropertyId = Guid.NewGuid() };
        var third = new UsageCountDto { PropertyId = Guid.NewGuid() };
        List<UsageCountDto>? restored = null;
        _tracker.Setup(t => t.Drain()).Returns(new List<UsageCountDto> { first, second, third });
        _tracker.Setup(t => t.Restore(It.IsAny<IEnumerable<UsageCountDto>>()))
            .Callback<IEnumerable<UsageCountDto>>(c => restored = c.ToList());
        _repository.Setup(r => r.AddUsageAsync(second)).ThrowsAsync(new InvalidOperationException());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.FlushAsync());
        Assert.Equal(new[] { second, third }, restored);
    }

    [Fact]
    public async Task FlushAsync_WithNoCounts_DoesNotCallRepository()
    {
        _tracker.Setup(t => t.Drain()).Returns(new List<UsageCountDto>());
        await _service.FlushAsync();
        _repository.Verify(r => r.AddUsageAsync(It.IsAny<UsageCountDto>()), Times.Never);
    }

    [Fact]
    public async Task PurgeLoginEventsAsync_Always_DeletesEventsOlderThanTwelveMonths()
    {
        await _service.PurgeLoginEventsAsync();
        _repository.Verify(r => r.DeleteLoginEventsBeforeAsync(Now.UtcDateTime.AddMonths(-12)), Times.Once);
    }
}
