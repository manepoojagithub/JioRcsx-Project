using FluentAssertions;
using JioCxRcsWrapper.Application.Common;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.UnitTests.Shared;

public sealed class StatusLabelTests
{
    [Fact]
    public void ContactSentStatus_IsDisplayedAsSuccessfullySend()
    {
        StatusLabels.For(ContactStatus.Sent).Should().Be("Successfully Send");
    }

    [Fact]
    public void CompletedCampaignStatus_IsDisplayedAsSuccessfullySend()
    {
        StatusLabels.For(CampaignStatus.Completed).Should().Be("Successfully Send");
    }
}
