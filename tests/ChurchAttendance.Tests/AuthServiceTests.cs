using ChurchAttendance.Application.Auth;
using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Configuration;
using ChurchAttendance.Domain.Entities;
using ChurchAttendance.Infrastructure.Persistence;
using ChurchAttendance.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChurchAttendance.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_LocksAccountAfterFiveFailedAttempts()
    {
        await using var db = TestDb.Create();
        var user = await AddUserAsync(db, "Strong#123");
        var clock = new FixedChurchClock(new DateOnly(2026, 11, 5));
        var service = CreateService(db, clock);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                service.LoginAsync(new LoginRequest("admin", "Wrong#123", false)));
        }

        var stored = await db.Users.FindAsync(user.Id);
        Assert.NotNull(stored!.LockedUntil);
        Assert.True(stored.LockedUntil > clock.UtcNow);
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginRequest("admin", "Strong#123", false)));
    }

    [Fact]
    public async Task RefreshAsync_RotatesTokenAndRevokesOldToken()
    {
        await using var db = TestDb.Create();
        await AddUserAsync(db, "Strong#123");
        var clock = new FixedChurchClock(new DateOnly(2026, 11, 5));
        var service = CreateService(db, clock);

        var login = await service.LoginAsync(new LoginRequest("admin", "Strong#123", true));
        var refreshed = await service.RefreshAsync(new RefreshRequest(login.RefreshToken));

        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.NotEqual(login.AccessToken, refreshed.AccessToken);
        Assert.Equal(2, db.RefreshTokens.Count());
        Assert.Single(db.RefreshTokens.Where(t => t.RevokedAt != null));

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.RefreshAsync(new RefreshRequest(login.RefreshToken)));
        Assert.All(db.RefreshTokens, token => Assert.NotNull(token.RevokedAt));
    }

    [Fact]
    public void BcryptPasswordHasher_VerifiesOnlyTheOriginalPassword()
    {
        var hasher = new BcryptPasswordHasher();
        var hash = hasher.Hash("Strong#123");

        Assert.True(hasher.Verify("Strong#123", hash));
        Assert.False(hasher.Verify("Wrong#123", hash));
    }

    private static AuthService CreateService(AppDbContext db, FixedChurchClock clock) =>
        new(
            db,
            new FakePasswordHasher(),
            new FakeTokenService(),
            clock,
            TestDb.JwtOptions(),
            TestDb.ChurchSettings(),
            NullLogger<AuthService>.Instance);

    private static async Task<User> AddUserAsync(AppDbContext db, string password)
    {
        var member = TestDb.Member("admin-test@example.com", "Admin", "Tester");
        member.MemberRoles.Add(new MemberRole
        {
            MemberId = member.Id,
            RoleId = TestDb.Role(db, RoleNames.Admin).Id
        });
        var user = new User
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            Member = member,
            Username = "admin",
            PasswordHash = "hash:" + password,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => hash == "hash:" + password;
    }

    private sealed class FakeTokenService : ITokenService
    {
        private int _counter;

        public AccessTokenResult CreateAccessToken(User user, IReadOnlyCollection<string> roles)
        {
            _counter++;
            return new AccessTokenResult($"access-{_counter}", DateTimeOffset.UtcNow.AddMinutes(15));
        }

        public string GenerateRefreshToken() => $"refresh-{Guid.NewGuid():N}";

        public string HashRefreshToken(string refreshToken) => "hash:" + refreshToken;
    }
}
