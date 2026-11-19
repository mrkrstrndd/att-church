using ChurchAttendance.Domain.Entities;

namespace ChurchAttendance.Application.Common;

public sealed record ActorContext(
    Guid UserId,
    Guid MemberId,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    Guid? CellId)
{
    public bool IsAdmin => Roles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase);
    public bool IsCellLeader => Roles.Contains(RoleNames.CellLeader, StringComparer.OrdinalIgnoreCase);
}
