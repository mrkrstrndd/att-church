using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Configuration;
using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChurchAttendance.Application.Auth;

public class AuthService
{
    private static readonly Lazy<string> DummyHash = new(() => BCrypt.Net.BCrypt.HashPassword("not-a-real-password", 11));

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IChurchClock _clock;
    private readonly JwtOptions _jwt;
    private readonly ChurchSettings _settings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IChurchClock clock,
        IOptions<JwtOptions> jwt,
        IOptions<ChurchSettings> settings,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _clock = clock;
        _jwt = jwt.Value;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var username = (request.Username ?? string.Empty).Trim().ToLowerInvariant();
        var user = await QueryUser()
            .FirstOrDefaultAsync(u => u.Username == username, ct);

        if (user is null)
        {
            _passwordHasher.Verify(request.Password ?? string.Empty, DummyHash.Value);
            _logger.LogWarning("Failed login for unknown username {Username}", username);
            throw new UnauthorizedException();
        }

        var now = _clock.UtcNow;

        if (!user.IsActive || !user.Member.IsActive)
        {
            _logger.LogWarning("Login rejected for inactive account {Username}", username);
            throw new UnauthorizedException();
        }

        if (user.LockedUntil is not null && user.LockedUntil > now)
        {
            _logger.LogWarning("Login rejected for locked account {Username}", username);
            throw new UnauthorizedException();
        }

        if (!_passwordHasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            user.FailedAttempts++;
            if (user.FailedAttempts >= _settings.LoginMaxFailedAttempts)
            {
                user.LockedUntil = now.AddMinutes(_settings.LoginLockoutMinutes);
                user.FailedAttempts = 0;
                _logger.LogWarning("Account {Username} locked after repeated failed logins", username);
            }
            else
            {
                _logger.LogWarning("Failed login for {Username}; attempt {Attempt}", username, user.FailedAttempts);
            }

            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedException();
        }

        user.FailedAttempts = 0;
        user.LockedUntil = null;

        var response = IssueTokens(user, request.RememberMe, now);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("User {Username} signed in", username);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var rawToken = request.RefreshToken ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new UnauthorizedException("Your session has expired. Please sign in again.");

        var hash = _tokenService.HashRefreshToken(rawToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.Member).ThenInclude(m => m.MemberRoles).ThenInclude(mr => mr.Role)
            .Include(t => t.User).ThenInclude(u => u.Member).ThenInclude(m => m.Cell)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
            throw new UnauthorizedException("Your session has expired. Please sign in again.");

        var now = _clock.UtcNow;

        if (stored.RevokedAt is not null)
        {
            // Reuse of a rotated token: revoke every outstanding session for this user.
            var activeTokens = await _db.RefreshTokens
                .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var token in activeTokens)
                token.RevokedAt = now;
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Refresh token reuse detected for user {UserId}", stored.UserId);
            throw new UnauthorizedException("Your session has expired. Please sign in again.");
        }

        if (stored.ExpiresAt <= now)
        {
            stored.RevokedAt = now;
            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedException("Your session has expired. Please sign in again.");
        }

        var user = stored.User;
        if (!user.IsActive || !user.Member.IsActive)
            throw new UnauthorizedException("Your account is no longer active.");

        stored.RevokedAt = now;
        var response = IssueTokens(user, stored.RememberMe, now);
        await _db.SaveChangesAsync(ct);
        return response;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return;

        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("User account not found.");

        if (!_passwordHasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
            throw new AppValidationException(nameof(request.CurrentPassword), "The current password is incorrect.");

        if (request.NewPassword == request.CurrentPassword)
            throw new AppValidationException(nameof(request.NewPassword), "Choose a password different from the current one.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword!);
        user.MustChangePassword = false;
        user.FailedAttempts = 0;
        user.LockedUntil = null;

        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in tokens)
            token.RevokedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Password changed for user {UserId}", userId);
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await QueryUser().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("User account not found.");
        return ToProfile(user);
    }

    private IQueryable<User> QueryUser() => _db.Users
        .Include(u => u.Member).ThenInclude(m => m.MemberRoles).ThenInclude(mr => mr.Role)
        .Include(u => u.Member).ThenInclude(m => m.Cell);

    private AuthResponse IssueTokens(User user, bool rememberMe, DateTimeOffset now)
    {
        var roles = user.Member.MemberRoles
            .OrderBy(mr => mr.RoleId)
            .Select(mr => mr.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roles.Length == 0)
            roles = new[] { RoleNames.Member };

        var access = _tokenService.CreateAccessToken(user, roles);
        var rawRefresh = _tokenService.GenerateRefreshToken();
        var refreshExpiry = rememberMe
            ? now.AddDays(_jwt.RememberMeDays)
            : now.AddHours(_jwt.RefreshTokenHours);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashRefreshToken(rawRefresh),
            ExpiresAt = refreshExpiry,
            RememberMe = rememberMe,
            CreatedAt = now
        });

        return new AuthResponse(access.Token, rawRefresh, access.ExpiresAt, ToProfile(user, roles));
    }

    private static UserProfileDto ToProfile(User user, IReadOnlyList<string>? roles = null)
    {
        var roleNames = roles ?? user.Member.MemberRoles
            .OrderBy(mr => mr.RoleId)
            .Select(mr => mr.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new UserProfileDto(
            user.Id,
            user.MemberId,
            user.Username,
            user.Member.FullName,
            user.Member.FirstName,
            user.Member.LastName,
            user.Member.Email,
            roleNames,
            user.Member.CellId,
            user.Member.Cell?.Name,
            user.MustChangePassword);
    }
}
