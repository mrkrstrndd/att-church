namespace ChurchAttendance.Application.Dashboard;

public record LastEventDto(string Name, DateOnly Date, int Attendance);

public record DashboardKpisDto(
    int TotalActiveMembers,
    int TotalCells,
    int EventsThisMonth,
    LastEventDto? LastEvent,
    double AverageAttendance,
    double AttendanceRatePercent,
    double? AverageAttendanceDeltaPercent,
    double? EventsThisMonthDeltaPercent,
    DateOnly From,
    DateOnly To);

public record WeeklyTrendPointDto(DateOnly WeekStart, string Label, int TotalAttendance);

public record WeeklyTrendDto(IReadOnlyList<WeeklyTrendPointDto> Points);

public record CellAttendanceDto(
    Guid? CellId,
    string Name,
    int AttendanceCount,
    int MemberCount,
    int UniqueAttendees);

public record AttendanceByCellDto(DateOnly From, DateOnly To, IReadOnlyList<CellAttendanceDto> Cells);

public record MonthlyComparisonDto(
    string Mode,
    IReadOnlyList<string> Labels,
    IReadOnlyList<int> TotalAttendance,
    IReadOnlyList<int> UniqueAttendees,
    IReadOnlyList<int>? PreviousYearAttendance,
    IReadOnlyList<int>? PreviousYearUniqueAttendees);

public record RecentEventDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    int DayCount,
    int AttendanceCount,
    double AveragePerDay);
