namespace ChurchAttendance.Application.Cells;

public record CellMemberDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNo,
    IReadOnlyList<string> Roles,
    bool IsLeader);

public record CellCandidateDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNo,
    string? CellName,
    IReadOnlyList<string> Roles);

public record CellListItemDto(
    Guid Id,
    string Name,
    string? Description,
    Guid LeaderMemberId,
    string LeaderName,
    int MemberCount,
    DateTimeOffset CreatedAt);

public record CellDto(
    Guid Id,
    string Name,
    string? Description,
    Guid LeaderMemberId,
    string LeaderName,
    IReadOnlyList<CellMemberDto> Members,
    DateTimeOffset CreatedAt);

public record CreateCellRequest(string? Name, string? Description, Guid LeaderMemberId);

public record UpdateCellRequest(string? Name, string? Description, Guid LeaderMemberId);

public record AddCellMembersRequest(IReadOnlyList<Guid>? MemberIds);
