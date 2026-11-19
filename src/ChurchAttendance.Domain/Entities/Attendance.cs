namespace ChurchAttendance.Domain.Entities;

public class Attendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventDayId { get; set; }
    public EventDay EventDay { get; set; } = null!;
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public DateTimeOffset CheckedInAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RecordedBy { get; set; }
}
