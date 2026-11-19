using ChurchAttendance.Domain.Entities;

namespace ChurchAttendance.Application.Auth;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user, IReadOnlyCollection<string> roles);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
