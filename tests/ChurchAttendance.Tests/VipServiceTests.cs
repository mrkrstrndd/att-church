using ChurchAttendance.Application.Vip;
using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Tests;

public class VipServiceTests
{
    [Fact]
    public async Task Report_PromotesVipAfterFourthAttendance()
    {
        await using var db = TestDb.Create();
        var vip = await AddVipWithAttendanceAsync(db, 4);
        var clock = new FixedChurchClock(new DateOnly(2026, 11, 20));
        var service = new VipService(db, clock);

        var report = await service.GetReportAsync();

        Assert.Equal(0, report.TotalVips);
        var reloaded = await db.Members
            .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
            .SingleAsync(m => m.Id == vip.Id);
        Assert.Contains(RoleNames.Member, reloaded.MemberRoles.Select(mr => mr.Role.Name));
        Assert.DoesNotContain(RoleNames.Vip, reloaded.MemberRoles.Select(mr => mr.Role.Name));
    }

    [Fact]
    public async Task Report_KeepsVipUntilFourthAttendance()
    {
        await using var db = TestDb.Create();
        var vip = await AddVipWithAttendanceAsync(db, 3);
        var clock = new FixedChurchClock(new DateOnly(2026, 11, 20));
        var service = new VipService(db, clock);

        var report = await service.GetReportAsync();

        var item = Assert.Single(report.Members);
        Assert.Equal(vip.Id, item.Id);
        Assert.Equal(3, item.SignInCount);
        Assert.Equal(1, report.TotalVips);
        Assert.Equal(3, report.TotalSignIns);
    }

    private static async Task<Member> AddVipWithAttendanceAsync(
        Infrastructure.Persistence.AppDbContext db, int attendanceCount)
    {
        var vip = TestDb.Member($"vip-{attendanceCount}@example.com", "Vera", "Visitor");
        db.Members.Add(vip);
        db.MemberRoles.Add(new MemberRole
        {
            MemberId = vip.Id,
            RoleId = TestDb.Role(db, RoleNames.Vip).Id
        });

        for (var i = 0; i < attendanceCount; i++)
        {
            db.Attendance.Add(new Attendance
            {
                Id = Guid.NewGuid(),
                EventDayId = Guid.NewGuid(),
                MemberId = vip.Id,
                CheckedInAt = new DateTimeOffset(2026, 11, 1 + i, 8, 0, 0, TimeSpan.Zero)
            });
        }

        await db.SaveChangesAsync();
        return vip;
    }
}
