using ChurchAttendance.Application.Attendance;
using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Vip;
using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Tests;

public class VipAttendanceTests
{
    [Fact]
    public async Task CreateOrCheckInVipAsync_CreatesVipWithInviterAndChecksIn()
    {
        await using var db = TestDb.Create();
        var inviter = TestDb.Member("inviter@example.com", "Mia", "Member");
        db.Members.Add(inviter);
        db.MemberRoles.Add(new MemberRole
        {
            MemberId = inviter.Id,
            RoleId = TestDb.Role(db, RoleNames.Member).Id
        });
        var (eventId, dayId) = await AddEventAsync(db);

        var service = CreateService(db);
        var result = await service.CreateOrCheckInVipAsync(
            eventId,
            dayId,
            new CreateVipCheckInRequest(
                null,
                "Visitor",
                "Vera",
                "vera.visitor@example.com",
                "+639171234567",
                inviter.Id),
            TestDb.AdminActor());

        Assert.True(result.Created);
        Assert.False(result.AlreadyCheckedIn);
        Assert.Equal(1, result.CheckedInCount);

        var vip = await db.Members
            .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
            .SingleAsync(m => m.Id == result.MemberId);
        Assert.Contains(RoleNames.Vip, vip.MemberRoles.Select(mr => mr.Role.Name));
        Assert.Null(vip.CellId);
        Assert.Equal(inviter.Id, vip.InvitedByMemberId);
        Assert.Null(vip.User);
        Assert.True(await db.Attendance.AnyAsync(a => a.EventDayId == dayId && a.MemberId == vip.Id));
    }

    [Fact]
    public async Task CreateOrCheckInVipAsync_ReusesExistingVipAndIsIdempotent()
    {
        await using var db = TestDb.Create();
        var vip = TestDb.Member("existing.vip@example.com", "Vera", "Visitor");
        db.Members.Add(vip);
        db.MemberRoles.Add(new MemberRole
        {
            MemberId = vip.Id,
            RoleId = TestDb.Role(db, RoleNames.Vip).Id
        });
        var (eventId, dayId) = await AddEventAsync(db);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var first = await service.CreateOrCheckInVipAsync(
            eventId,
            dayId,
            new CreateVipCheckInRequest(vip.Id, null, null, null, null, null),
            TestDb.AdminActor());
        var second = await service.CreateOrCheckInVipAsync(
            eventId,
            dayId,
            new CreateVipCheckInRequest(vip.Id, null, null, null, null, null),
            TestDb.AdminActor());

        Assert.False(first.Created);
        Assert.False(first.AlreadyCheckedIn);
        Assert.True(second.AlreadyCheckedIn);
        Assert.Equal(1, second.CheckedInCount);
        Assert.Equal(1, await db.Members.CountAsync(m => m.Email == "existing.vip@example.com"));
        Assert.Equal(1, await db.Attendance.CountAsync(a => a.EventDayId == dayId));
    }

    [Fact]
    public async Task CreateOrCheckInVipAsync_MatchesExactNameWithoutExistingId()
    {
        await using var db = TestDb.Create();
        var vip = TestDb.Member("named.vip@example.com", "Vera", "Visitor");
        db.Members.Add(vip);
        db.MemberRoles.Add(new MemberRole
        {
            MemberId = vip.Id,
            RoleId = TestDb.Role(db, RoleNames.Vip).Id
        });
        var (eventId, dayId) = await AddEventAsync(db);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CreateOrCheckInVipAsync(
            eventId,
            dayId,
            new CreateVipCheckInRequest(
                null,
                "Visitor",
                "Vera",
                "another.email@example.com",
                null,
                null),
            TestDb.AdminActor());

        Assert.False(result.Created);
        Assert.Equal(vip.Id, result.MemberId);
        Assert.Equal(1, await db.Members.CountAsync(m => m.LastName == "Visitor"));
    }

    [Fact]
    public async Task CreateOrCheckInVipAsync_RejectsNonAdmin()
    {
        await using var db = TestDb.Create();
        var (eventId, dayId) = await AddEventAsync(db);
        var cellLeader = new ActorContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Cell Leader",
            new[] { RoleNames.CellLeader },
            Guid.NewGuid());

        var service = CreateService(db);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateOrCheckInVipAsync(
                eventId,
                dayId,
                new CreateVipCheckInRequest(
                    null,
                    "Visitor",
                    "Vera",
                    "forbidden.vip@example.com",
                    null,
                    null),
                cellLeader));
    }

    private static AttendanceService CreateService(Infrastructure.Persistence.AppDbContext db)
    {
        var clock = new FixedChurchClock(new DateOnly(2026, 11, 20));
        return new AttendanceService(db, clock, new VipService(db, clock));
    }

    private static async Task<(Guid EventId, Guid DayId)> AddEventAsync(Infrastructure.Persistence.AppDbContext db)
    {
        var date = new DateOnly(2026, 11, 20);
        var churchEvent = new Event
        {
            Name = "VIP Test Service",
            IsMultiDay = false,
            StartDate = date,
            EndDate = date
        };
        var day = new EventDay
        {
            EventId = churchEvent.Id,
            EventDate = date
        };
        db.Events.Add(churchEvent);
        db.EventDays.Add(day);
        await db.SaveChangesAsync();
        return (churchEvent.Id, day.Id);
    }
}
