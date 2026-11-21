using ChurchAttendance.Application.Auth;
using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Configuration;
using ChurchAttendance.Application.Events;
using ChurchAttendance.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChurchAttendance.Tests;

public class EventServiceTests
{
    [Fact]
    public async Task CreateAsync_GeneratesOneDayPerDateInclusive()
    {
        await using var db = TestDb.Create();
        var clock = new FixedChurchClock(new DateOnly(2026, 12, 10));
        var service = new EventService(db, clock, TestDb.ChurchSettings());
        var actor = TestDb.AdminActor();

        var created = await service.CreateAsync(
            new CreateEventRequest("Youth Conference", "Annual", true, new DateOnly(2026, 12, 10), new DateOnly(2026, 12, 12)),
            actor);

        Assert.Equal(3, created.Days.Count);
        Assert.Equal(new DateOnly(2026, 12, 10), created.Days[0].EventDate);
        Assert.Equal(new DateOnly(2026, 12, 12), created.Days[2].EventDate);
    }

    [Fact]
    public async Task CreateAsync_ForcesSingleDayEndDate()
    {
        await using var db = TestDb.Create();
        var clock = new FixedChurchClock(new DateOnly(2026, 12, 10));
        var service = new EventService(db, clock, TestDb.ChurchSettings());

        var created = await service.CreateAsync(
            new CreateEventRequest("Sunday Service", null, false, new DateOnly(2026, 12, 13), new DateOnly(2026, 12, 20)),
            TestDb.AdminActor());

        Assert.Equal(created.StartDate, created.EndDate);
        Assert.Single(created.Days);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByMonthAndListsMonthOptions()
    {
        await using var db = TestDb.Create();
        var clock = new FixedChurchClock(new DateOnly(2026, 12, 10));
        var service = new EventService(db, clock, TestDb.ChurchSettings());
        var actor = TestDb.AdminActor();

        await service.CreateAsync(
            new CreateEventRequest("November Gathering", null, false, new DateOnly(2026, 11, 1), null),
            actor);
        await service.CreateAsync(
            new CreateEventRequest("November Midweek", null, false, new DateOnly(2026, 11, 15), null),
            actor);
        await service.CreateAsync(
            new CreateEventRequest("December Gathering", null, false, new DateOnly(2026, 12, 6), null),
            actor);

        var november = await service.GetPagedAsync(new EventQuery(Month: "2026-11"));
        var months = await service.GetMonthsAsync();

        Assert.Equal(2, november.TotalCount);
        Assert.All(november.Items, item => Assert.Equal(11, item.StartDate.Month));
        Assert.Equal(
            new[] { "2026-12", "2026-11" },
            months.Select(month => month.Month));
        Assert.Equal(1, months[0].EventCount);
        Assert.Equal(2, months[1].EventCount);
    }

    [Fact]
    public async Task GetPagedAsync_RejectsInvalidMonth()
    {
        await using var db = TestDb.Create();
        var clock = new FixedChurchClock(new DateOnly(2026, 12, 10));
        var service = new EventService(db, clock, TestDb.ChurchSettings());

        await Assert.ThrowsAsync<AppValidationException>(() =>
            service.GetPagedAsync(new EventQuery(Month: "November")));
    }

    [Fact]
    public async Task UpdateAsync_RejectsRemovingDayWithAttendance()
    {
        await using var db = TestDb.Create();
        var clock = new FixedChurchClock(new DateOnly(2026, 12, 10));
        var service = new EventService(db, clock, TestDb.ChurchSettings());
        var created = await service.CreateAsync(
            new CreateEventRequest("Camp", null, true, new DateOnly(2026, 12, 10), new DateOnly(2026, 12, 11)),
            TestDb.AdminActor());

        var member = TestDb.Member();
        db.Members.Add(member);
        await db.SaveChangesAsync();
        db.Attendance.Add(new Domain.Entities.Attendance
        {
            EventDayId = created.Days[1].Id,
            MemberId = member.Id
        });
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(
            created.Id,
            new UpdateEventRequest("Camp", null, false, new DateOnly(2026, 12, 10), null)));

        Assert.Contains("Attendance already exists", exception.Message);
    }
}
