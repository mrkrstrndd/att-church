namespace ChurchAttendance.Domain.Entities;

public class MemberRole
{
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public short RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
