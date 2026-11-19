namespace ChurchAttendance.Application.Members;

public record MemberDto(
    Guid Id,
    string LastName,
    string FirstName,
    string FullName,
    string Email,
    string? PhoneNo,
    Guid? CellId,
    string? CellName,
    IReadOnlyList<string> Roles,
    bool HasLogin,
    bool IsActive,
    DateTimeOffset CreatedAt);

public record MemberQrCodeDto(Guid MemberId, string MemberName, string Payload);

public record CreateLoginRequest(string? Username, string? Password);

public record CreateMemberRequest(
    string? LastName,
    string? FirstName,
    string? Email,
    string? PhoneNo,
    IReadOnlyList<string>? Roles,
    Guid? CellId,
    CreateLoginRequest? CreateLogin);

public record UpdateMemberRequest(
    string? LastName,
    string? FirstName,
    string? Email,
    string? PhoneNo,
    IReadOnlyList<string>? Roles,
    Guid? CellId);

public record MemberQuery(
    string? Search = null,
    string? Role = null,
    Guid? CellId = null,
    int Page = 1,
    int PageSize = 20,
    bool IncludeInactive = false,
    bool Unassigned = false);
