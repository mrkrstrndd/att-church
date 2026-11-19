namespace ChurchAttendance.Domain.Entities;

public class EventDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public DateOnly EventDate { get; set; }
    public ICollection<Attendance> AttendanceRecords { get; set; } = new List<Attendance>();
}
