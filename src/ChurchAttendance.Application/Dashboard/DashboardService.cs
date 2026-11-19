using System.Globalization;
using ChurchAttendance.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.Dashboard;

public class DashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly IChurchClock _clock;

    public DashboardService(IApplicationDbContext db, IChurchClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<DashboardKpisDto> GetKpisAsync(
        DateOnly? from, DateOnly? to, ActorContext actor, CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(actor, ct);
        var today = _clock.Today;
        var (fromDate, toDate) = NormalizeRange(from, to);

        var activeMembers = _db.Members.AsNoTracking().Where(m => m.IsActive);
        if (scope.CellId is not null)
            activeMembers = activeMembers.Where(m => m.CellId == scope.CellId);
        var totalActiveMembers = await activeMembers.CountAsync(ct);

        var totalCells = scope.CellId is null
            ? await _db.Cells.CountAsync(c => c.IsActive, ct)
            : 1;

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var previousMonthStart = monthStart.AddMonths(-1);
        var previousMonthEnd = monthStart.AddDays(-1);

        var eventsThisMonth = await _db.Events.CountAsync(
            e => e.StartDate >= monthStart && e.StartDate <= monthEnd, ct);
        var eventsPreviousMonth = await _db.Events.CountAsync(
            e => e.StartDate >= previousMonthStart && e.StartDate <= previousMonthEnd, ct);

        var lastDay = await _db.EventDays
            .AsNoTracking()
            .Include(d => d.Event)
            .Where(d => d.EventDate <= today)
            .OrderByDescending(d => d.EventDate)
            .ThenByDescending(d => d.Event.CreatedAt)
            .FirstOrDefaultAsync(ct);

        LastEventDto? lastEvent = null;
        var lastEventAttendance = 0;
        if (lastDay is not null)
        {
            var attendanceQuery = _db.Attendance.AsNoTracking()
                .Where(a => a.EventDayId == lastDay.Id);
            if (scope.CellId is not null)
                attendanceQuery = attendanceQuery.Where(a => a.Member.CellId == scope.CellId);
            lastEventAttendance = await attendanceQuery.CountAsync(ct);
            lastEvent = new LastEventDto(lastDay.Event.Name, lastDay.EventDate, lastEventAttendance);
        }

        var effectiveTo = toDate > today ? today : toDate;
        var dayCount = effectiveTo < fromDate
            ? 0
            : await _db.EventDays.CountAsync(d => d.EventDate >= fromDate && d.EventDate <= effectiveTo, ct);

        var attendanceTotalQuery = _db.Attendance.AsNoTracking()
            .Where(a => a.EventDay.EventDate >= fromDate && a.EventDay.EventDate <= effectiveTo);
        if (scope.CellId is not null)
            attendanceTotalQuery = attendanceTotalQuery.Where(a => a.Member.CellId == scope.CellId);
        var attendanceTotal = await attendanceTotalQuery.CountAsync(ct);
        var averageAttendance = dayCount == 0 ? 0 : Math.Round(attendanceTotal / (double)dayCount, 1);

        var rangeDays = toDate.DayNumber - fromDate.DayNumber + 1;
        var previousTo = fromDate.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(rangeDays - 1));
        var previousDayCount = await _db.EventDays.CountAsync(
            d => d.EventDate >= previousFrom && d.EventDate <= previousTo, ct);
        var previousAttendanceQuery = _db.Attendance.AsNoTracking()
            .Where(a => a.EventDay.EventDate >= previousFrom && a.EventDay.EventDate <= previousTo);
        if (scope.CellId is not null)
            previousAttendanceQuery = previousAttendanceQuery.Where(a => a.Member.CellId == scope.CellId);
        var previousAttendanceTotal = await previousAttendanceQuery.CountAsync(ct);
        var previousAverage = previousDayCount == 0 ? 0 : previousAttendanceTotal / (double)previousDayCount;

        var attendanceRate = totalActiveMembers == 0
            ? 0
            : Math.Round(lastEventAttendance * 100.0 / totalActiveMembers, 1);

        return new DashboardKpisDto(
            totalActiveMembers,
            totalCells,
            eventsThisMonth,
            lastEvent,
            averageAttendance,
            attendanceRate,
            PercentDelta(averageAttendance, previousAverage),
            PercentDelta(eventsThisMonth, eventsPreviousMonth),
            fromDate,
            toDate);
    }

    public async Task<WeeklyTrendDto> GetWeeklyTrendAsync(int? weeks, ActorContext actor, CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(actor, ct);
        var weekCount = Math.Clamp(weeks ?? 12, 1, 52);
        var today = _clock.Today;
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var currentWeekStart = today.AddDays(-daysSinceMonday);
        var start = currentWeekStart.AddDays(-7 * (weekCount - 1));

        var rows = await GetDayRowsAsync(start, today, scope, ct);
        var byWeek = rows
            .GroupBy(r => StartOfWeek(r.Date))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        var points = new List<WeeklyTrendPointDto>(weekCount);
        for (var i = 0; i < weekCount; i++)
        {
            var weekStart = start.AddDays(i * 7);
            byWeek.TryGetValue(weekStart, out var count);
            points.Add(new WeeklyTrendPointDto(
                weekStart,
                weekStart.ToString("MMM d", CultureInfo.InvariantCulture),
                count));
        }

        return new WeeklyTrendDto(points);
    }

    public async Task<AttendanceByCellDto> GetAttendanceByCellAsync(
        DateOnly? from, DateOnly? to, ActorContext actor, CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(actor, ct);
        var (fromDate, toDate) = NormalizeRange(from, to);

        var cellRows = await _db.Cells.AsNoTracking()
            .Where(c => c.IsActive && (scope.CellId == null || c.Id == scope.CellId))
            .Select(c => new
            {
                c.Id,
                c.Name,
                MemberCount = c.Members.Count(m => m.IsActive)
            })
            .ToListAsync(ct);

        var attendanceByCell = await GetAttendanceCountsByCellAsync(fromDate, toDate, scope, ct);

        var result = cellRows
            .Select(c =>
            {
                var stats = attendanceByCell.GetValueOrDefault(c.Id);
                return new CellAttendanceDto(c.Id, c.Name, stats?.Count ?? 0, c.MemberCount, stats?.Unique ?? 0);
            })
            .ToList();

        if (scope.CellId is null)
        {
            var unassignedMembers = await _db.Members.CountAsync(m => m.IsActive && m.CellId == null, ct);
            var unassignedStats = attendanceByCell.GetValueOrDefault(Guid.Empty);
            if (unassignedMembers > 0 || (unassignedStats?.Count ?? 0) > 0)
            {
                result.Add(new CellAttendanceDto(
                    null, "Unassigned", unassignedStats?.Count ?? 0, unassignedMembers, unassignedStats?.Unique ?? 0));
            }
        }

        return new AttendanceByCellDto(
            fromDate,
            toDate,
            result.OrderByDescending(c => c.AttendanceCount).ThenBy(c => c.Name).ToList());
    }

    public async Task<MonthlyComparisonDto> GetMonthlyComparisonAsync(
        int? months, int? year, ActorContext actor, CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(actor, ct);
        var today = _clock.Today;

        if (year is not null)
        {
            if (year < 1900 || year > 2200)
                throw new AppValidationException(nameof(year), "Year must be between 1900 and 2200.");

            var start = new DateOnly(year.Value, 1, 1);
            var end = new DateOnly(year.Value, 12, 31);
            var current = await GetMonthlyRowsAsync(start, end, scope, ct);
            var previous = await GetMonthlyRowsAsync(start.AddYears(-1), end.AddYears(-1), scope, ct);

            var labels = Enumerable.Range(1, 12)
                .Select(m => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m))
                .ToList();

            return new MonthlyComparisonDto(
                "year",
                labels,
                BuildSeries(current, year.Value, labels.Count, r => r.Total),
                BuildSeries(current, year.Value, labels.Count, r => r.Unique),
                BuildSeries(previous, year.Value - 1, labels.Count, r => r.Total),
                BuildSeries(previous, year.Value - 1, labels.Count, r => r.Unique));
        }

        var monthCount = Math.Clamp(months ?? 6, 1, 24);
        var endMonth = new DateOnly(today.Year, today.Month, 1);
        var startMonth = endMonth.AddMonths(-(monthCount - 1));
        var rangeStart = startMonth;
        var rangeEnd = endMonth.AddMonths(1).AddDays(-1);

        var rows = await GetMonthlyRowsAsync(rangeStart, rangeEnd, scope, ct);
        var labelsRange = Enumerable.Range(0, monthCount)
            .Select(i => startMonth.AddMonths(i))
            .ToList();
        var byMonth = rows.ToDictionary(r => (r.Year, r.Month));

        var totals = new List<int>(monthCount);
        var uniques = new List<int>(monthCount);
        foreach (var month in labelsRange)
        {
            byMonth.TryGetValue((month.Year, month.Month), out var row);
            totals.Add(row?.Total ?? 0);
            uniques.Add(row?.Unique ?? 0);
        }

        return new MonthlyComparisonDto(
            "months",
            labelsRange.Select(m => m.ToString("MMM yyyy", CultureInfo.InvariantCulture)).ToList(),
            totals,
            uniques,
            null,
            null);
    }

    public async Task<IReadOnlyList<RecentEventDto>> GetRecentEventsAsync(
        int? count, ActorContext actor, CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(actor, ct);
        var take = Math.Clamp(count ?? 6, 1, 12);
        var today = _clock.Today;

        var events = await _db.Events.AsNoTracking()
            .Where(e => e.StartDate <= today)
            .OrderByDescending(e => e.StartDate)
            .ThenByDescending(e => e.CreatedAt)
            .Take(take)
            .Select(e => new
            {
                e.Id,
                e.Name,
                e.StartDate,
                e.EndDate,
                DayCount = e.Days.Count,
                AttendanceCount = scope.CellId == null
                    ? e.Days.Sum(d => d.AttendanceRecords.Count)
                    : e.Days.Sum(d => d.AttendanceRecords.Count(a => a.Member.CellId == scope.CellId))
            })
            .ToListAsync(ct);

        return events.Select(e => new RecentEventDto(
            e.Id,
            e.Name,
            e.StartDate,
            e.EndDate,
            e.DayCount,
            e.AttendanceCount,
            e.DayCount == 0 ? 0 : Math.Round(e.AttendanceCount / (double)e.DayCount, 1))).ToList();
    }

    private async Task<DashboardScope> ResolveScopeAsync(ActorContext actor, CancellationToken ct)
    {
        if (actor.IsAdmin)
            return new DashboardScope(null);

        if (!actor.IsCellLeader)
            throw new ForbiddenException("Only administrators and cell leaders can view the dashboard.");

        var cell = await _db.Cells.AsNoTracking()
            .FirstOrDefaultAsync(c => c.LeaderMemberId == actor.MemberId && c.IsActive, ct)
            ?? throw new ForbiddenException("You do not lead an active cell.");

        return new DashboardScope(cell.Id);
    }

    private (DateOnly From, DateOnly To) NormalizeRange(DateOnly? from, DateOnly? to)
    {
        var toDate = to ?? _clock.Today;
        var fromDate = from ?? toDate.AddDays(-89);
        if (fromDate > toDate)
            throw new AppValidationException(nameof(from), "The from date must be on or before the to date.");
        return (fromDate, toDate);
    }

    private async Task<List<DayRow>> GetDayRowsAsync(
        DateOnly start, DateOnly end, DashboardScope scope, CancellationToken ct)
    {
        var query = _db.EventDays.AsNoTracking()
            .Where(d => d.EventDate >= start && d.EventDate <= end);

        if (scope.CellId is null)
        {
            return await query
                .Select(d => new DayRow(d.EventDate, d.AttendanceRecords.Count))
                .ToListAsync(ct);
        }

        var cellId = scope.CellId.Value;
        return await query
            .Select(d => new DayRow(
                d.EventDate,
                d.AttendanceRecords.Count(a => a.Member.CellId == cellId)))
            .ToListAsync(ct);
    }

    private async Task<Dictionary<Guid, CellStats>> GetAttendanceCountsByCellAsync(
        DateOnly from, DateOnly to, DashboardScope scope, CancellationToken ct)
    {
        var query = _db.Attendance.AsNoTracking()
            .Where(a => a.EventDay.EventDate >= from && a.EventDay.EventDate <= to);

        if (scope.CellId is not null)
            query = query.Where(a => a.Member.CellId == scope.CellId);

        var rows = await query
            .GroupBy(a => a.Member.CellId)
            .Select(g => new CellStats(
                g.Key,
                g.Count(),
                g.Select(a => a.MemberId).Distinct().Count()))
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.CellId ?? Guid.Empty);
    }

    private async Task<List<MonthlyRow>> GetMonthlyRowsAsync(
        DateOnly start, DateOnly end, DashboardScope scope, CancellationToken ct)
    {
        var query = _db.Attendance.AsNoTracking()
            .Where(a => a.EventDay.EventDate >= start && a.EventDay.EventDate <= end);

        if (scope.CellId is not null)
            query = query.Where(a => a.Member.CellId == scope.CellId);

        var rows = await query
            .GroupBy(a => new { a.EventDay.EventDate.Year, a.EventDay.EventDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Count(),
                Unique = g.Select(a => a.MemberId).Distinct().Count()
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new MonthlyRow(r.Year, r.Month, r.Total, r.Unique))
            .OrderBy(r => r.Year)
            .ThenBy(r => r.Month)
            .ToList();
    }

    private static List<int> BuildSeries(
        IReadOnlyCollection<MonthlyRow> rows, int year, int months, Func<MonthlyRow, int> selector)
    {
        var lookup = rows.Where(r => r.Year == year).ToDictionary(r => r.Month);
        var result = new List<int>(months);
        for (var month = 1; month <= months; month++)
        {
            lookup.TryGetValue(month, out var row);
            result.Add(row is null ? 0 : selector(row));
        }
        return result;
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static double? PercentDelta(double current, double previous)
    {
        if (previous <= 0)
            return null;
        return Math.Round((current - previous) * 100.0 / previous, 1);
    }

    private sealed record DashboardScope(Guid? CellId);
    private sealed record DayRow(DateOnly Date, int Count);
    private sealed record CellStats(Guid? CellId, int Count, int Unique);
    private sealed record MonthlyRow(int Year, int Month, int Total, int Unique);
}
