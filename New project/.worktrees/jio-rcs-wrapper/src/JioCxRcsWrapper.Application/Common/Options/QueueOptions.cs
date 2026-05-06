namespace JioCxRcsWrapper.Application.Common.Options;

public sealed class QueueOptions
{
    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 4;
    public int PollSeconds { get; set; } = 10;
}
