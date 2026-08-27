namespace WhatsAppAI.Domain.Identity;

using System.Globalization;
using System.Text.RegularExpressions;
using WhatsAppAI.Domain.Integrations;

public sealed class TenantMembership
{
    private static readonly Regex LineAssignmentPattern = new(
        "\\\"ConnectionType\\\"\\s*:\\s*\\\"(?<connectionType>[^\\\"]+)\\\"\\s*,\\s*\\\"LineNumber\\\"\\s*:\\s*(?<lineNumber>-?\\d+)",
        RegexOptions.CultureInvariant);

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public MembershipRole Role { get; private set; }
    public MembershipStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DeactivatedAt { get; private set; }
    public DateTime? ReactivatedAt { get; private set; }
    public uint Version { get; private set; }
    public WhatsAppConnectionType? AssignedConnectionType { get; private set; }
    public int? AssignedLineNumber { get; private set; }
    public string? AssignedLinesJson { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public User User { get; private set; } = null!;

    private readonly List<LineAssignment> _assignedLines = [];
    public IReadOnlyList<LineAssignment> AssignedLines => _assignedLines.AsReadOnly();

    private TenantMembership() { }

    public static TenantMembership Create(Guid tenantId, User user, MembershipRole role)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsPlatformAdmin)
            throw new InvalidOperationException("Platform administrators cannot belong to a tenant.");

        return new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            User = user,
            Role = role,
            Status = MembershipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Activate()
    {
        if (Status == MembershipStatus.Active)
            throw new InvalidOperationException("Membership is already active.");

        Status = MembershipStatus.Active;
        Version++;
    }

    public void Deactivate()
    {
        if (Status == MembershipStatus.Inactive)
            throw new InvalidOperationException("Membership is already inactive.");

        Status = MembershipStatus.Inactive;
        DeactivatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Reactivate()
    {
        if (Status != MembershipStatus.Inactive)
            throw new InvalidOperationException("Only inactive memberships can be reactivated.");

        Status = MembershipStatus.Active;
        ReactivatedAt = DateTime.UtcNow;
        Version++;
    }

    public void AssignLine(WhatsAppConnectionType connectionType, int lineNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lineNumber, 1);
        AssignedConnectionType = connectionType;
        AssignedLineNumber = lineNumber;
        Version++;
    }

    public void ClearLineAssignment()
    {
        AssignedConnectionType = null;
        AssignedLineNumber = null;
        Version++;
    }

    public void SetAssignedLines(IEnumerable<LineAssignment> lines)
    {
        _assignedLines.Clear();
        _assignedLines.AddRange(lines);
        SyncAssignedLinesJson();
        Version++;
    }

    public void AddAssignedLine(WhatsAppConnectionType connectionType, int lineNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lineNumber, 1);
        if (_assignedLines.Exists(l => l.ConnectionType == connectionType && l.LineNumber == lineNumber))
            return;
        _assignedLines.Add(new LineAssignment(connectionType, lineNumber));
        SyncAssignedLinesJson();
        Version++;
    }

    public void RemoveAssignedLine(WhatsAppConnectionType connectionType, int lineNumber)
    {
        _assignedLines.RemoveAll(l => l.ConnectionType == connectionType && l.LineNumber == lineNumber);
        SyncAssignedLinesJson();
        Version++;
    }

    public void ClearAssignedLines()
    {
        _assignedLines.Clear();
        SyncAssignedLinesJson();
        Version++;
    }

    public void LoadAssignedLinesFromJson()
    {
        if (string.IsNullOrWhiteSpace(AssignedLinesJson))
        {
            _assignedLines.Clear();
            return;
        }
        _assignedLines.Clear();
        foreach (Match match in LineAssignmentPattern.Matches(AssignedLinesJson))
        {
            if (Enum.TryParse<WhatsAppConnectionType>(match.Groups["connectionType"].Value, true, out var connectionType) &&
                int.TryParse(match.Groups["lineNumber"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lineNumber))
                _assignedLines.Add(new LineAssignment(connectionType, lineNumber));
        }
    }

    private void SyncAssignedLinesJson()
    {
        if (_assignedLines.Count == 0)
        {
            AssignedLinesJson = null;
            return;
        }
        AssignedLinesJson = "[" + string.Join(",", _assignedLines.Select(line =>
            $"{{\"ConnectionType\":\"{line.ConnectionType}\",\"LineNumber\":{line.LineNumber.ToString(CultureInfo.InvariantCulture)}}}")) + "]";
    }
}

public sealed record LineAssignment(WhatsAppConnectionType ConnectionType, int LineNumber);

public enum MembershipRole
{
    TenantOwner = 0,
    Operator = 1
}

public enum MembershipStatus
{
    Pending = 0,
    Active = 1,
    Inactive = 2
}
