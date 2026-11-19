namespace ChurchAttendance.Application.Common;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public AppException(int statusCode, string title, string detail, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
        Errors = errors;
    }
}

public sealed class NotFoundException(string detail) : AppException(404, "Not found", detail);

public sealed class ConflictException(string detail) : AppException(409, "Conflict", detail);

public sealed class ForbiddenException(string detail) : AppException(403, "Forbidden", detail);

public sealed class UnauthorizedException(string detail = "Invalid username or password")
    : AppException(401, "Unauthorized", detail);

public sealed class AppValidationException : AppException
{
    public AppValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(400, "Validation failed", "One or more validation errors occurred.", errors)
    {
    }

    public AppValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = new[] { message } })
    {
    }
}
