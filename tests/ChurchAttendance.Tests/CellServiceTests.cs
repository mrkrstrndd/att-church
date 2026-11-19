using ChurchAttendance.Application.Auth;
using ChurchAttendance.Application.Cells;
using ChurchAttendance.Application.Common;
using ChurchAttendance.Domain.Entities;

namespace ChurchAttendance.Tests;

public class CellServiceTests
{
    [Fact]
    public async Task CreateAsync_RejectsLeaderWithoutCellLeaderRole()
    {
        await using var db = TestDb.Create();
        var leader = TestDb.Member("leader@example.com", "Liza", "Leader");
        db.Members.Add(leader);
        db.MemberRoles.Add(new MemberRole { MemberId = leader.Id, RoleId = TestDb.Role(db, RoleNames.Member).Id });
        await db.SaveChangesAsync();

        var service = new CellService(db, new FixedChurchClock(new DateOnly(2026, 11, 4)));
        var ex = await Assert.ThrowsAsync<AppValidationException>(() =>
            service.CreateAsync(new CreateCellRequest("North Cell", null, leader.Id)));

        Assert.Contains("CellLeader role", ex.Errors!["leaderMemberId"].Single());
    }

    [Fact]
    public async Task CreateAsync_AddsLeaderToTheirCellWhenUnassigned()
    {
        await using var db = TestDb.Create();
        var leader = TestDb.Member("leader@example.com", "Liza", "Leader");
        db.Members.Add(leader);
        db.MemberRoles.Add(new MemberRole { MemberId = leader.Id, RoleId = TestDb.Role(db, RoleNames.CellLeader).Id });
        await db.SaveChangesAsync();

        var service = new CellService(db, new FixedChurchClock(new DateOnly(2026, 11, 4)));
        var created = await service.CreateAsync(new CreateCellRequest("North Cell", "North district", leader.Id));

        Assert.Equal(leader.Id, created.LeaderMemberId);
        Assert.Single(created.Members);
        Assert.True(created.Members[0].IsLeader);
    }
}
