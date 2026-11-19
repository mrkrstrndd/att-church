using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Configuration;
using ChurchAttendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ChurchAttendance.Domain.Entities;

namespace ChurchAttendance.Tests;

public sealed class FixedChurchClock : IChurchClock
{
    public FixedChurchClock(DateOnly today)
    {
        Today = today;
        UtcNow = new DateTimeOffset(today.ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero);
    }

    public DateTimeOffset UtcNow { get; }
    public DateOnly Today { get; }
    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}

public static class TestDb
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"church-test-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public static Role Role(AppDbContext db, string name) => db.Roles.Single(r => r.Name == name);

    public static IOptions<ChurchSettings> ChurchSettings() => Options.Create(new ChurchSettings
    {
        TimeZoneId = "Asia/Manila",
        DefaultPageSize = 20,
        MaxPageSize = 100
    });

    public static IOptions<JwtOptions> JwtOptions() => Options.Create(new JwtOptions
    {
        Issuer = "test",
        Audience = "test",
        Key = "TEST_ONLY_KEY_THAT_IS_LONG_ENOUGH_FOR_HMAC_SHA256_1234567890",
        AccessTokenMinutes = 15,
        RefreshTokenHours = 12,
        RememberMeDays = 30
    });

    public static ActorContext AdminActor(Guid? memberId = null) => new(
        Guid.NewGuid(),
        memberId ?? Guid.NewGuid(),
        "Test Admin",
        new[] { RoleNames.Admin },
        null);

    public static Member Member(string email = "member@example.com", string first = "Test", string last = "Member") => new()
    {
        FirstName = first,
        LastName = last,
        Email = email,
        IsActive = true
    };
}
