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
    public async Task CreateAsync_LeavesLeaderCellMembershipIndependent()
    {
        await using var db = TestDb.Create();
        var leader = TestDb.Member("leader@example.com", "Liza", "Leader");
        db.Members.Add(leader);
        db.MemberRoles.Add(new MemberRole { MemberId = leader.Id, RoleId = TestDb.Role(db, RoleNames.CellLeader).Id });
        await db.SaveChangesAsync();

        var service = new CellService(db, new FixedChurchClock(new DateOnly(2026, 11, 4)));
        var created = await service.CreateAsync(new CreateCellRequest("North Cell", "North district", leader.Id));

        Assert.Equal(leader.Id, created.LeaderMemberId);
        Assert.Empty(created.Members);
        Assert.Null((await db.Members.FindAsync(leader.Id))!.CellId);
    }

    [Fact]
    public async Task AddMembersAsync_RejectsAddingCurrentCellLeader()
    {
        await using var db = TestDb.Create();
        var leader = TestDb.Member("leader@example.com", "Liza", "Leader");
        db.Members.Add(leader);
        db.MemberRoles.Add(new MemberRole { MemberId = leader.Id, RoleId = TestDb.Role(db, RoleNames.CellLeader).Id });
        await db.SaveChangesAsync();

        var service = new CellService(db, new FixedChurchClock(new DateOnly(2026, 11, 4)));
        var cell = await service.CreateAsync(new CreateCellRequest("North Cell", null, leader.Id));

        await Assert.ThrowsAsync<AppValidationException>(() =>
            service.AddMembersAsync(cell.Id, new AddCellMembersRequest(new[] { leader.Id })));
    }

    [Fact]
    public async Task AddMembersAsync_AllowsCellLeaderFromAnotherCell()
    {
        await using var db = TestDb.Create();
        var leaderA = TestDb.Member("leader.a@example.com", "Liza", "Leader");
        var leaderB = TestDb.Member("leader.b@example.com", "Mark", "Leader");
        db.Members.AddRange(leaderA, leaderB);
        db.MemberRoles.Add(new MemberRole { MemberId = leaderA.Id, RoleId = TestDb.Role(db, RoleNames.CellLeader).Id });
        db.MemberRoles.Add(new MemberRole { MemberId = leaderB.Id, RoleId = TestDb.Role(db, RoleNames.CellLeader).Id });
        await db.SaveChangesAsync();

        var service = new CellService(db, new FixedChurchClock(new DateOnly(2026, 11, 4)));
        var cellA = await service.CreateAsync(new CreateCellRequest("North Cell", null, leaderA.Id));
        var cellB = await service.CreateAsync(new CreateCellRequest("South Cell", null, leaderB.Id));

        // Simulate a legacy row where the leader is already a member of their own cell.
        leaderB.CellId = cellB.Id;
        await db.SaveChangesAsync();

        // Cell leaders are allowed to be moved to another cell.
        await service.AddMembersAsync(cellA.Id, new AddCellMembersRequest(new[] { leaderB.Id }));

        var moved = await db.Members.FindAsync(leaderB.Id);
        Assert.Equal(cellA.Id, moved!.CellId);
    }

    [Fact]
    public async Task GetCandidatesAsync_ShowsOtherCellLeadersButExcludesCurrentLeaderAndVip()
    {
        await using var db = TestDb.Create();
        var leaderA = TestDb.Member("leader.a@example.com", "Liza", "Leader");
        var leaderB = TestDb.Member("leader.b@example.com", "Mark", "Leader");
        var regular = TestDb.Member("regular@example.com", "Juan", "Member");
        var newMember = TestDb.Member("new@example.com", "Ana", "Member");
        var vip = TestDb.Member("vip@example.com", "Vera", "Visitor");
        db.Members.AddRange(leaderA, leaderB, regular, newMember, vip);
        db.MemberRoles.Add(new MemberRole { MemberId = leaderA.Id, RoleId = TestDb.Role(db, RoleNames.CellLeader).Id });
        db.MemberRoles.Add(new MemberRole { MemberId = leaderB.Id, RoleId = TestDb.Role(db, RoleNames.CellLeader).Id });
        db.MemberRoles.Add(new MemberRole { MemberId = regular.Id, RoleId = TestDb.Role(db, RoleNames.Member).Id });
        db.MemberRoles.Add(new MemberRole { MemberId = newMember.Id, RoleId = TestDb.Role(db, RoleNames.Member).Id });
        db.MemberRoles.Add(new MemberRole { MemberId = vip.Id, RoleId = TestDb.Role(db, RoleNames.Vip).Id });
        await db.SaveChangesAsync();

        var service = new CellService(db, new FixedChurchClock(new DateOnly(2026, 11, 4)));
        var cellA = await service.CreateAsync(new CreateCellRequest("North Cell", null, leaderA.Id));
        var cellB = await service.CreateAsync(new CreateCellRequest("South Cell", null, leaderB.Id));
        await service.AddMembersAsync(cellB.Id, new AddCellMembersRequest(new[] { regular.Id }));

        var candidates = await service.GetCandidatesAsync(cellA.Id, null);
        var ids = candidates.Select(candidate => candidate.Id).ToList();

        Assert.Contains(leaderB.Id, ids);
        Assert.Contains(newMember.Id, ids);
        Assert.DoesNotContain(leaderA.Id, ids);
        Assert.DoesNotContain(regular.Id, ids);
        Assert.DoesNotContain(vip.Id, ids);
    }

    [Fact]
    public async Task AddMembersAsync_RejectsVipMembers()
    {
        await using var db = TestDb.Create();
        var leader = TestDb.Member("leader@example.com", "Liza", "Leader");
        var vip = TestDb.Member("vip@example.com", "Victor", "Visitor");
        db.Members.AddRange(leader, vip);
        db.MemberRoles.Add(new MemberRole { MemberId = leader.Id, RoleId = TestDb.Role(db, RoleNames.CellLeader).Id });
        db.MemberRoles.Add(new MemberRole { MemberId = vip.Id, RoleId = TestDb.Role(db, RoleNames.Vip).Id });
        await db.SaveChangesAsync();

        var service = new CellService(db, new FixedChurchClock(new DateOnly(2026, 11, 4)));
        var cell = await service.CreateAsync(new CreateCellRequest("North Cell", null, leader.Id));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AddMembersAsync(cell.Id, new AddCellMembersRequest(new[] { vip.Id })));

        Assert.Contains("VIP", exception.Message);
    }
}
