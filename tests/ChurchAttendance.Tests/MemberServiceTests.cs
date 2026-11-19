using ChurchAttendance.Application.Auth;
using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Members;
using ChurchAttendance.Domain.Entities;

namespace ChurchAttendance.Tests;

public class MemberServiceTests
{
    [Fact]
    public async Task CreateAsync_AllowsVipWithoutCell()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var member = await service.CreateAsync(
            new CreateMemberRequest(
                "Visitor",
                "One",
                "vip.one@example.com",
                null,
                new[] { RoleNames.Vip },
                null,
                null),
            TestDb.AdminActor());

        Assert.Contains(RoleNames.Vip, member.Roles);
        Assert.Null(member.CellId);
    }

    [Fact]
    public async Task CreateAsync_RejectsVipCombinedWithMember()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<AppValidationException>(() =>
            service.CreateAsync(
                new CreateMemberRequest(
                    "Visitor",
                    "Two",
                    "vip.two@example.com",
                    null,
                    new[] { RoleNames.Vip, RoleNames.Member },
                    null,
                    null),
                TestDb.AdminActor()));

        Assert.Contains("VIP", exception.Errors!["roles"].Single());
    }

    [Fact]
    public async Task CreateAsync_RejectsVipAssignedToCell()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<AppValidationException>(() =>
            service.CreateAsync(
                new CreateMemberRequest(
                    "Visitor",
                    "Three",
                    "vip.three@example.com",
                    null,
                    new[] { RoleNames.Vip },
                    Guid.NewGuid(),
                    null),
                TestDb.AdminActor()));

        Assert.Contains("cell", exception.Errors!["cellId"].Single());
    }

    [Fact]
    public async Task CreateAsync_NonAdminCannotCreateLogin()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        var nonAdmin = new ActorContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Cell Leader",
            new[] { RoleNames.CellLeader },
            null);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(
                new CreateMemberRequest(
                    "Login",
                    "Blocked",
                    "login.blocked@example.com",
                    null,
                    new[] { RoleNames.Member },
                    null,
                    new CreateLoginRequest("blocked-user", "Temp#1234")),
                nonAdmin));

        Assert.Contains("administrators", exception.Message);
    }

    [Fact]
    public async Task LookupVipsByLastNameAsync_ReturnsOnlyActiveVipPrefixMatches()
    {
        await using var db = TestDb.Create();
        var vip = TestDb.Member("lookup.vip@example.com", "Vera", "Visitor");
        var member = TestDb.Member("lookup.member@example.com", "Victor", "Visitor");
        db.Members.AddRange(vip, member);
        db.MemberRoles.Add(new MemberRole { MemberId = vip.Id, RoleId = TestDb.Role(db, RoleNames.Vip).Id });
        db.MemberRoles.Add(new MemberRole { MemberId = member.Id, RoleId = TestDb.Role(db, RoleNames.Member).Id });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var results = await service.LookupVipsByLastNameAsync("vis");

        var match = Assert.Single(results);
        Assert.Equal(vip.Id, match.Id);
        Assert.Empty(await service.LookupVipsByLastNameAsync("x"));
    }

    [Fact]
    public async Task GetInviterOptionsAsync_ExcludesVipMembers()
    {
        await using var db = TestDb.Create();
        var vip = TestDb.Member("inviter.vip@example.com", "Vera", "Visitor");
        var member = TestDb.Member("inviter.member@example.com", "Mia", "Member");
        db.Members.AddRange(vip, member);
        db.MemberRoles.Add(new MemberRole { MemberId = vip.Id, RoleId = TestDb.Role(db, RoleNames.Vip).Id });
        db.MemberRoles.Add(new MemberRole { MemberId = member.Id, RoleId = TestDb.Role(db, RoleNames.Member).Id });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var options = await service.GetInviterOptionsAsync("");

        Assert.Contains(options, o => o.Id == member.Id);
        Assert.DoesNotContain(options, o => o.Id == vip.Id);
    }

    private static MemberService CreateService(Infrastructure.Persistence.AppDbContext db) =>
        new(
            db,
            new FakePasswordHasher(),
            new FixedChurchClock(new DateOnly(2026, 11, 20)),
            TestDb.ChurchSettings());

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => hash == "hash:" + password;
    }
}
