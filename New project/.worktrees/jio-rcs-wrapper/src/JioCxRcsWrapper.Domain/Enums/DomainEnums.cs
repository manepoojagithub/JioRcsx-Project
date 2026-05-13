namespace JioCxRcsWrapper.Domain.Enums;

public enum CampaignType
{
    Schedule = 1,
    Recurring = 2,
    OneTime = 3
}

public enum CampaignStatus
{
    Draft = 1,
    Scheduled = 2,
    Queued = 3,
    Processing = 4,
    Completed = 5,
    Failed = 6,
    Paused = 7
}

public enum MessageType
{
    PlainText = 1,
    StandaloneCard = 2,
    Carousel = 3
}

public enum ContactStatus
{
    Pending = 1,
    Sent = 2,
    Delivered = 3,
    Failed = 4,
    Opened = 5,
    Clicked = 6
}

public enum CampaignQueueStatus
{
    Pending = 1,
    Processing = 2,
    RetryScheduled = 3,
    Succeeded = 4,
    Failed = 5,
    Paused = 6
}

public enum CtaActionType
{
    OpenUrl = 1,
    Dialer = 2,
    Calendar = 3,
    Location = 4,
    SuggestedReply = 5
}

public enum MediaType
{
    Image = 1,
    Video = 2,
    Gif = 3
}
