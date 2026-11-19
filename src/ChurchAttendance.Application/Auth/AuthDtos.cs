namespace ChurchAttendance.Application.Auth;

public record LoginRequest(string? Username, string? Password, bool RememberMe);

public record RefreshRequest(string? RefreshToken);

public record LogoutRequest(string? RefreshToken);

public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public record UserProfileDto(
    Guid Id,
    Guid MemberId,
    string Username,
    string DisplayName,
    string FirstName,
    string LastName,
    string Email,
    IReadOnlyList<string> Roles,
    Guid? CellId,
    string? CellName,
    bool MustChangePassword);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserProfileDto User);
