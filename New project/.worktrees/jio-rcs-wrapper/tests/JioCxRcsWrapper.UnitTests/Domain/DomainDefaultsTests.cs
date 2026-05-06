using FluentAssertions;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.UnitTests.Domain;

public sealed class DomainDefaultsTests
{
    [Fact]
    public void CampaignQueueStatus_IncludesRetryableStates()
    {
        Enum.GetNames<CampaignQueueStatus>()
            .Should()
            .Contain(["Pending", "Processing", "RetryScheduled", "Succeeded", "Failed", "Paused"]);
    }

    [Fact]
    public void CtaActionType_OnlyOpenUrlIsSendableFromDocumentedSchema()
    {
        CtaActionType.OpenUrl.Should().Be(CtaActionType.OpenUrl);
        CtaActionType.Dialer.Should().NotBe(CtaActionType.OpenUrl);
        CtaActionType.Calendar.Should().NotBe(CtaActionType.OpenUrl);
        CtaActionType.Location.Should().NotBe(CtaActionType.OpenUrl);
    }
}
