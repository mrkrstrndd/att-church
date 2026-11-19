using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ChurchAttendance.Application.Common;

namespace ChurchAttendance.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static ActorContext ToActor(this ClaimsPrincipal principal)
    {
        var userId = Guid.TryParse(
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("The access token does not contain a valid user id.");

        var memberId = Guid.TryParse(principal.FindFirstValue("member_id"), out var member)
            ? member
            : Guid.Empty;

        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList();
        var displayName = principal.FindFirstValue(ClaimTypes.Name) ?? principal.FindFirstValue("username") ?? "User";

        return new ActorContext(userId, memberId, displayName, roles, null);
    }
}
