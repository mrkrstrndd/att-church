using ChurchAttendance.Application.Common;
using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.Vip;

public class VipService
{
    private const int MemberThreshold = 4;
    private readonly IApplicationDbContext _db;
    private readonly IChurchClock _clock;

    public VipService(IApplicationDbContext db, IChurchClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<VipReportDto> GetReportAsync(CancellationToken ct = default)
    {
        await PromoteAllEligibleAsync(ct);

        var members = await _db.Members
            .AsNoTracking()
            .Include(m => m.InvitedBy)
            .Where(m => m.IsActive)
            .Where(m => m.MemberRoles.Any(mr => mr.Role.Name == RoleNames.Vip))
            .Select(m => new VipReportItemDto(
                m.Id,
                m.FirstName + " " + m.LastName,
                m.Email,
                m.PhoneNo,
                m.InvitedBy == null ? null : m.InvitedBy.FirstName + " " + m.InvitedBy.LastName,
                m.AttendanceRecords.Count,
                m.CreatedAt))
            .ToListAsync(ct);

        members = members
            .OrderByDescending(m => m.SignInCount)
            .ThenBy(m => m.FullName)
            .ToList();

        return new VipReportDto(
            _clock.Today,
            members.Count,
            members.Sum(m => m.SignInCount),
            members);
    }

    /// <summary>
    /// Promotes a VIP to Member once they have attended four event days.
    /// Returns true when the member was promoted by this call.
    /// </summary>
    public async Task<bool> PromoteIfEligibleAsync(Guid memberId, CancellationToken ct = default)
    {
        var member = await _db.Members
            .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.IsActive, ct);

        if (member is null || !HasVipRole(member))
            return false;

        var attendanceCount = await _db.Attendance.CountAsync(a => a.MemberId == memberId, ct);
        if (attendanceCount < MemberThreshold)
            return false;

        await PromoteAsync(member, ct);
        return true;
    }

    /// <summary>
    /// Catches VIPs whose fourth attendance existed before promotion logic ran,
    /// or whose records were added outside the normal check-in flow.
    /// </summary>
    public async Task<int> PromoteAllEligibleAsync(CancellationToken ct = default)
    {
        var candidates = await _db.Members
            .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
            .Where(m => m.IsActive)
            .Where(m => m.MemberRoles.Any(mr => mr.Role.Name == RoleNames.Vip))
            .Where(m => m.AttendanceRecords.Count >= MemberThreshold)
            .ToListAsync(ct);

        foreach (var member in candidates)
            await PromoteAsync(member, ct);

        return candidates.Count;
    }

    private async Task PromoteAsync(Member member, CancellationToken ct)
    {
        var memberRole = await _db.Roles.SingleAsync(r => r.Name == RoleNames.Member, ct);
        var vipRoles = member.MemberRoles
            .Where(mr => string.Equals(mr.Role.Name, RoleNames.Vip, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var vipRole in vipRoles)
        {
            member.MemberRoles.Remove(vipRole);
            _db.MemberRoles.Remove(vipRole);
        }

        if (member.MemberRoles.All(mr => mr.RoleId != memberRole.Id))
        {
            member.MemberRoles.Add(new MemberRole
            {
                MemberId = member.Id,
                RoleId = memberRole.Id,
                Role = memberRole
            });
        }

        member.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static bool HasVipRole(Member member) =>
        member.MemberRoles.Any(mr =>
            string.Equals(mr.Role.Name, RoleNames.Vip, StringComparison.OrdinalIgnoreCase));
}
