using System.Security.Cryptography;

namespace ChurchAttendance.Domain.Entities;

/// <summary>
/// A member's QR code is an opaque bearer token, not their database id.
/// The token is generated once per member and can be rotated if needed.
/// </summary>
public static class MemberQrCode
{
    public const string PayloadPrefix = "church-attendance://member/";
    public const int TokenLength = 64;

    public static string NewToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static string BuildPayload(string token) => $"{PayloadPrefix}{token}";

    public static bool TryParseToken(string? value, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.StartsWith(PayloadPrefix, StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[PayloadPrefix.Length..];
        }

        if (candidate.Length != TokenLength || !candidate.All(Uri.IsHexDigit))
        {
            return false;
        }

        token = candidate.ToLowerInvariant();
        return true;
    }
}
