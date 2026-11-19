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
