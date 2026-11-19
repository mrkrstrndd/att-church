namespace ChurchAttendance.Application.Attendance;

public record AttendanceMemberDto(
    Guid Id,
    string FullName,
    string Email,
    string? CellName,
    IReadOnlyList<string> Roles,
    DateTimeOffset? CheckedInAt);

public record AttendanceDayDto(
    Guid EventId,
    string EventName,
    Guid DayId,
    DateOnly EventDate,
    IReadOnlyList<AttendanceMemberDto> Attendees,
    IReadOnlyList<AttendanceMemberDto> NonAttendees,
    int ActiveMemberCount,
    int CheckedInCount,
    double AttendanceRatePercent);

public record CheckInRequest(IReadOnlyList<Guid>? MemberIds);

public record CheckInResultDto(int Added, int AlreadyCheckedIn, int CheckedInCount);

public record ScanQrCodeRequest(string? QrCode);

public record ScanCheckInResultDto(
    Guid MemberId,
    string MemberName,
    bool AlreadyCheckedIn,
    int CheckedInCount);

public record CreateVipCheckInRequest(
    Guid? ExistingMemberId,
    string? LastName,
    string? FirstName,
    string? Email,
    string? PhoneNo,
    Guid? InvitedByMemberId);

public record VipCheckInResultDto(
    Guid MemberId,
    string MemberName,
    bool Created,
    bool AlreadyCheckedIn,
    int CheckedInCount);
