using Moq;
using MuuBoi.Application.Services;

namespace MuuBoi.Tests.Services;

public class UsageTrackerTests
{
    private readonly Mock<TimeProvider> _clock = new();
    private readonly UsageTracker _tracker;

    public UsageTrackerTests()
    {
        _clock.Setup(c => c.GetUtcNow()).Returns(new DateTimeOffset(2026, 10, 17, 15, 0, 0, TimeSpan.Zero));
        _tracker = new UsageTracker(_clock.Object);
    }

    [Fact]
    public void Drain_WithRecordsForSamePropertyAndDay_ReturnsSingleAccumulatedCount()
    {
        var propertyId = Guid.NewGuid();
        _tracker.RecordLogin(propertyId);
        _tracker.RecordPush(propertyId);
        _tracker.RecordPush(propertyId);
        _tracker.RecordPull(propertyId);
        var result = _tracker.Drain();
        var count = Assert.Single(result);
        Assert.Equal((1, 2, 1), (count.Logins, count.Pushes, count.Pulls));
    }

    [Fact]
    public void Drain_WithDifferentProperties_ReturnsOneCountPerProperty()
    {
        _tracker.RecordPush(Guid.NewGuid());
        _tracker.RecordPush(Guid.NewGuid());
        var result = _tracker.Drain();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Drain_CalledTwice_ReturnsEmptyOnSecondCall()
    {
        _tracker.RecordPull(Guid.NewGuid());
        _tracker.Drain();
        var result = _tracker.Drain();
        Assert.Empty(result);
    }

    [Fact]
    public void RecordPush_At22hInBrasilia_UsesBrazilianDate()
    {
        _clock.Setup(c => c.GetUtcNow()).Returns(new DateTimeOffset(2026, 10, 18, 1, 0, 0, TimeSpan.Zero));
        _tracker.RecordPush(Guid.NewGuid());
        var result = _tracker.Drain();
        Assert.Equal(new DateTime(2026, 10, 17), Assert.Single(result).Date);
    }

    [Fact]
    public void Restore_AfterDrain_MergesCountsWithNewRecords()
    {
        var propertyId = Guid.NewGuid();
        _tracker.RecordPush(propertyId);
        var drained = _tracker.Drain();
        _tracker.RecordPush(propertyId);
        _tracker.Restore(drained);
        var result = _tracker.Drain();
        Assert.Equal(2, Assert.Single(result).Pushes);
    }
}
