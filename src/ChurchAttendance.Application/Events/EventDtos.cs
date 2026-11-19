namespace ChurchAttendance.Application.Events;

public record EventDayDto(Guid Id, DateOnly EventDate, int AttendanceCount);

public record EventListItemDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsMultiDay,
    DateOnly StartDate,
    DateOnly EndDate,
    int DayCount,
    int AttendanceCount);

public record EventDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsMultiDay,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<EventDayDto> Days,
    DateTimeOffset CreatedAt);

public record CreateEventRequest(
    string? Name,
    string? Description,
    bool IsMultiDay,
    DateOnly? StartDate,
    DateOnly? EndDate);

public record UpdateEventRequest(
    string? Name,
    string? Description,
    bool IsMultiDay,
    DateOnly? StartDate,
    DateOnly? EndDate);

public record EventQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 20);
