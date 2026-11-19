namespace ChurchAttendance.Domain.Entities;

public class Member
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNo { get; set; }
    public string QrToken { get; set; } = MemberQrCode.NewToken();

    public Guid? CellId { get; set; }
    public Cell? Cell { get; set; }

    public Guid? InvitedByMemberId { get; set; }
    public Member? InvitedBy { get; set; }
    public ICollection<Member> Invitees { get; set; } = new List<Member>();

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MemberRole> MemberRoles { get; set; } = new List<MemberRole>();
    public ICollection<Attendance> AttendanceRecords { get; set; } = new List<Attendance>();
    public Cell? LedCell { get; set; }
    public User? User { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
