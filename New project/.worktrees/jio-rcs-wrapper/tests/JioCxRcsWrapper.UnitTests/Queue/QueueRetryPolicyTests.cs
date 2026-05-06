using FluentAssertions;
using JioCxRcsWrapper.Application.Queue;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.UnitTests.Queue;

public sealed class QueueRetryPolicyTests
{
    private readonly QueueRetryPolicy _policy = new();

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    public void RetryableStatuses_AreRetried(int statusCode)
    {
        _policy.ShouldRetry(statusCode, attemptCount: 1, maxAttempts: 4).Should().BeTrue();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    public void NonRetryableStatuses_AreNotRetried(int statusCode)
    {
        _policy.ShouldRetry(statusCode, attemptCount: 1, maxAttempts: 4).Should().BeFalse();
    }

    [Fact]
    public void MaxAttemptsExceeded_FailsQueueItem()
    {
        _policy.GetFailureStatus(500, attemptCount: 4, maxAttempts: 4).Should().Be(CampaignQueueStatus.Failed);
    }

    [Fact]
    public void NextAttemptAt_UsesDocumentedBackoffSteps()
    {
        var now = DateTimeOffset.Parse("2026-05-03T10:00:00Z");

        _policy.NextAttemptAt(now, 1).Should().Be(now.AddMinutes(1));
        _policy.NextAttemptAt(now, 2).Should().Be(now.AddMinutes(5));
        _policy.NextAttemptAt(now, 3).Should().Be(now.AddMinutes(15));
        _policy.NextAttemptAt(now, 4).Should().Be(now.AddMinutes(60));
    }
}
