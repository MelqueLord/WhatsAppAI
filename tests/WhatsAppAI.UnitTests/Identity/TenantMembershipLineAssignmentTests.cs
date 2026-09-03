using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.UnitTests.Identity;

public sealed class TenantMembershipLineAssignmentTests
{
    [Fact]
    public void LoadAssignedLinesFromJson_FallsBackToLegacyAssignment()
    {
        var user = User.Create("operator@example.com");
        var membership = TenantMembership.Create(Guid.NewGuid(), user, MembershipRole.Operator);
        membership.Activate();
        membership.AssignLine(WhatsAppConnectionType.QrCode, 2);

        membership.LoadAssignedLinesFromJson();

        var assignment = Assert.Single(membership.AssignedLines);
        Assert.Equal(WhatsAppConnectionType.QrCode, assignment.ConnectionType);
        Assert.Equal(2, assignment.LineNumber);
    }
}
