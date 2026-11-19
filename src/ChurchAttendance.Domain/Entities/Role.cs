namespace ChurchAttendance.Domain.Entities;

/// <summary>
/// Seeded roles. A member can hold more than one role.
/// </summary>
public static class RoleNames
{
    public const string Member = "Member";
    public const string CellLeader = "CellLeader";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = new[] { Member, CellLeader, Admin };
}

public class Role
{
    public short Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<MemberRole> Members { get; set; } = new List<MemberRole>();
}
