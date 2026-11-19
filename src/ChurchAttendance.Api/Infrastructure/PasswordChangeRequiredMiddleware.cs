using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Api.Infrastructure;

/// <summary>
/// Forces accounts flagged with a temporary password to change it before using the rest of the API.
/// </summary>
public sealed class PasswordChangeRequiredMiddleware
{
    private readonly RequestDelegate _next;

    public PasswordChangeRequiredMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            string.Equals(context.User.FindFirstValue("must_change_password"), "true", StringComparison.OrdinalIgnoreCase) &&
            context.Request.Path.StartsWithSegments("/api") &&
            !context.Request.Path.StartsWithSegments("/api/auth"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Password change required",
                Detail = "Change your temporary password before continuing.",
                Instance = context.Request.Path
            };
            problem.Extensions["code"] = "password_change_required";

            await context.Response.WriteAsJsonAsync(problem);
            return;
        }

        await _next(context);
    }
}
