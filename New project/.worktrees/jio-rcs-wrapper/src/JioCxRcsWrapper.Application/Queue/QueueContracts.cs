using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Queue;

public interface IQueueRetryPolicy
{
    bool ShouldRetry(int? httpStatusCode, int attemptCount, int maxAttempts);

    DateTimeOffset NextAttemptAt(DateTimeOffset now, int attemptCount);

    CampaignQueueStatus GetFailureStatus(int? httpStatusCode, int attemptCount, int maxAttempts);
}

public interface IRealtimeNotifier
{
    Task CampaignUpdatedAsync(int campaignId, int clientId, object payload, CancellationToken cancellationToken);

    Task DashboardUpdatedAsync(int clientId, object payload, CancellationToken cancellationToken);
}

public sealed class QueueRetryPolicy : IQueueRetryPolicy
{
    private static readonly TimeSpan[] BackoffSteps =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(60)
    ];

    public bool ShouldRetry(int? httpStatusCode, int attemptCount, int maxAttempts)
    {
        if (attemptCount >= maxAttempts)
        {
            return false;
        }

        return httpStatusCode is 429 or >= 500;
    }

    public DateTimeOffset NextAttemptAt(DateTimeOffset now, int attemptCount)
    {
        var index = Math.Clamp(attemptCount - 1, 0, BackoffSteps.Length - 1);
        return now.Add(BackoffSteps[index]);
    }

    public CampaignQueueStatus GetFailureStatus(int? httpStatusCode, int attemptCount, int maxAttempts)
    {
        return ShouldRetry(httpStatusCode, attemptCount, maxAttempts)
            ? CampaignQueueStatus.RetryScheduled
            : CampaignQueueStatus.Failed;
    }
}
