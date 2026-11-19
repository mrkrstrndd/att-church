using ChurchAttendance.Application.Configuration;
using Microsoft.Extensions.Options;

namespace ChurchAttendance.Application.Common;

public interface IChurchClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today { get; }
    TimeZoneInfo TimeZone { get; }
}

public sealed class ChurchClock : IChurchClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public ChurchClock(TimeProvider timeProvider, IOptions<ChurchSettings> settings)
    {
        _timeProvider = timeProvider;
        _timeZone = ResolveTimeZone(settings.Value.TimeZoneId);
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();
    public TimeZoneInfo TimeZone => _timeZone;

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, _timeZone).DateTime);

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
