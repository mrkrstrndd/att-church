using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Configuration;
using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChurchAttendance.Application.Events;

public class EventService
{
    private const int MaxEventDays = 3660;
    private readonly IApplicationDbContext _db;
    private readonly IChurchClock _clock;
    private readonly ChurchSettings _settings;

    public EventService(IApplicationDbContext db, IChurchClock clock, IOptions<ChurchSettings> settings)
    {
        _db = db;
        _clock = clock;
        _settings = settings.Value;
    }

    public async Task<PagedResult<EventListItemDto>> GetPagedAsync(EventQuery query, CancellationToken ct = default)
    {
        if (query.From is not null && query.To is not null && query.From > query.To)
            throw new AppValidationException(nameof(query.From), "The from date must be on or before the to date.");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? _settings.DefaultPageSize : query.PageSize, 1, _settings.MaxPageSize);

        var events = _db.Events.AsNoTracking().AsQueryable();
        if (query.From is not null)
            events = events.Where(e => e.EndDate >= query.From);
        if (query.To is not null)
            events = events.Where(e => e.StartDate <= query.To);

        var total = await events.CountAsync(ct);
        var items = await events
            .OrderByDescending(e => e.StartDate)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventListItemDto(
                e.Id,
                e.Name,
                e.Description,
                e.IsMultiDay,
                e.StartDate,
                e.EndDate,
                e.Days.Count,
                e.Days.Sum(d => d.AttendanceRecords.Count)))
            .ToListAsync(ct);

        return PagedResult<EventListItemDto>.Create(items, page, pageSize, total);
    }

    public async Task<EventDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct)
                 ?? throw new NotFoundException("Event not found.");

        var days = await _db.EventDays
            .AsNoTracking()
            .Where(d => d.EventId == id)
            .OrderBy(d => d.EventDate)
            .Select(d => new EventDayDto(d.Id, d.EventDate, d.AttendanceRecords.Count))
            .ToListAsync(ct);

        return new EventDto(ev.Id, ev.Name, ev.Description, ev.IsMultiDay, ev.StartDate, ev.EndDate, days, ev.CreatedAt);
    }

    public async Task<EventDto> CreateAsync(CreateEventRequest request, ActorContext actor, CancellationToken ct = default)
    {
        var start = request.StartDate ?? throw new AppValidationException(nameof(request.StartDate), "A start date is required.");
        var end = NormalizeEnd(request.IsMultiDay, start, request.EndDate);
        EnsureRange(start, end);

        var now = _clock.UtcNow;
        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Name = Clean(request.Name),
            Description = CleanOptional(request.Description),
            IsMultiDay = request.IsMultiDay,
            StartDate = start,
            EndDate = end,
            CreatedBy = actor.UserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        for (var date = start; date <= end; date = date.AddDays(1))
            ev.Days.Add(new EventDay { Id = Guid.NewGuid(), EventId = ev.Id, EventDate = date });

        _db.Events.Add(ev);
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(ev.Id, ct);
    }

    public async Task<EventDto> UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken ct = default)
    {
        var ev = await _db.Events
            .Include(e => e.Days).ThenInclude(d => d.AttendanceRecords)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Event not found.");

        var start = request.StartDate ?? throw new AppValidationException(nameof(request.StartDate), "A start date is required.");
        var end = NormalizeEnd(request.IsMultiDay, start, request.EndDate);
        EnsureRange(start, end);

        var targetDates = DatesBetween(start, end).ToHashSet();
        var removingDaysWithAttendance = ev.Days
            .Where(d => d.AttendanceRecords.Count > 0 && !targetDates.Contains(d.EventDate))
            .Select(d => d.EventDate)
            .OrderBy(d => d)
            .ToList();

        if (removingDaysWithAttendance.Count > 0)
            throw new ConflictException(
                "Attendance already exists on " +
                string.Join(", ", removingDaysWithAttendance.Select(d => d.ToString("yyyy-MM-dd"))) +
                ". Remove those attendance records before changing the event dates.");

        var byDate = ev.Days.ToDictionary(d => d.EventDate);
        foreach (var day in ev.Days.Where(d => !targetDates.Contains(d.EventDate)).ToList())
        {
            _db.EventDays.Remove(day);
            byDate.Remove(day.EventDate);
        }

        foreach (var date in targetDates.OrderBy(d => d))
        {
            if (!byDate.ContainsKey(date))
            {
                var newDay = new EventDay { Id = Guid.NewGuid(), EventId = ev.Id, EventDate = date };
                ev.Days.Add(newDay);
                byDate[date] = newDay;
            }
        }

        ev.Name = Clean(request.Name);
        ev.Description = CleanOptional(request.Description);
        ev.IsMultiDay = request.IsMultiDay;
        ev.StartDate = start;
        ev.EndDate = end;
        ev.UpdatedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(ev.Id, ct);
    }

    public async Task DeleteAsync(Guid id, bool force, CancellationToken ct = default)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id, ct)
                 ?? throw new NotFoundException("Event not found.");

        var attendanceCount = await _db.Attendance
            .CountAsync(a => a.EventDay.EventId == id, ct);

        if (attendanceCount > 0 && !force)
        {
            throw new ConflictException(
                $"This event has {attendanceCount} attendance record(s). Confirm deletion to remove the event and its attendance history.");
        }

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync(ct);
    }

    private static DateOnly NormalizeEnd(bool isMultiDay, DateOnly start, DateOnly? end)
    {
        if (!isMultiDay)
            return start;

        var normalized = end ?? throw new AppValidationException("endDate", "An end date is required for a multi-day event.");
        if (normalized < start)
            throw new AppValidationException("endDate", "The end date must be on or after the start date.");
        return normalized;
    }

    private static void EnsureRange(DateOnly start, DateOnly end)
    {
        var days = end.DayNumber - start.DayNumber + 1;
        if (days > MaxEventDays)
            throw new AppValidationException("endDate", $"An event cannot span more than {MaxEventDays} days.");
    }

    private static IEnumerable<DateOnly> DatesBetween(DateOnly start, DateOnly end)
    {
        for (var date = start; date <= end; date = date.AddDays(1))
            yield return date;
    }

    private static string Clean(string? value) => (value ?? string.Empty).Trim();
    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
