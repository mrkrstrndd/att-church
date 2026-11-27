namespace ChurchAttendance.Application.Vip;

public record VipReportItemDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNo,
    string? InvitedByName,
    int SignInCount,
    DateTimeOffset JoinedAt);

public record VipReportDto(
    DateOnly GeneratedOn,
    int TotalVips,
    int TotalSignIns,
    IReadOnlyList<VipReportItemDto> Members);
