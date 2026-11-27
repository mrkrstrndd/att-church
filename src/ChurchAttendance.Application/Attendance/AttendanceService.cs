using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Vip;
using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.Attendance;

public class AttendanceService
{
    private readonly IApplicationDbContext _db;
    private readonly IChurchClock _clock;
    private readonly VipService _vip;

    public AttendanceService(IApplicationDbContext db, IChurchClock clock, VipService vip)
    {
        _db = db;
        _clock = clock;
        _vip = vip;
    }

    public async Task<AttendanceDayDto> GetDayAsync(
        Guid eventId, Guid dayId, string? search, ActorContext actor, CancellationToken ct = default)
    {
        var day = await LoadDayAsync(eventId, dayId, ct);
        var scopeCellId = await GetScopeCellIdAsync(actor, ct);

        var attendanceQuery = _db.Attendance
            .AsNoTracking()
            .Where(a => a.EventDayId == dayId)
            .Include(a => a.Member).ThenInclude(m => m.MemberRoles).ThenInclude(mr => mr.Role)
            .Include(a => a.Member).ThenInclude(m => m.Cell)
            .AsQueryable();

        if (scopeCellId is not null)
            attendanceQuery = attendanceQuery.Where(a => a.Member.CellId == scopeCellId);

        var attendance = await attendanceQuery.ToListAsync(ct);

        var memberQuery = _db.Members
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
            .Include(m => m.Cell)
            .AsQueryable();

        if (scopeCellId is not null)
            memberQuery = memberQuery.Where(m => m.CellId == scopeCellId);

        var members = await memberQuery.ToListAsync(ct);
        var checkedInIds = attendance.Select(a => a.MemberId).ToHashSet();

        var attendees = attendance
            .Select(a => ToDto(a.Member, a.CheckedInAt))
            .OrderBy(m => m.FullName)
            .ToList();

        var nonAttendees = members
            .Where(m => !checkedInIds.Contains(m.Id))
            .Select(m => ToDto(m, null))
            .OrderBy(m => m.FullName)
            .ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            attendees = attendees.Where(m => Matches(m, term)).ToList();
            nonAttendees = nonAttendees.Where(m => Matches(m, term)).ToList();
        }

        var activeCount = members.Count;
        var checkedInCount = attendance.Count;
        var rate = activeCount == 0 ? 0 : Math.Round(checkedInCount * 100.0 / activeCount, 1);

        return new AttendanceDayDto(
            day.EventId,
            day.Event.Name,
            day.Id,
            day.EventDate,
            attendees,
            nonAttendees,
            activeCount,
            checkedInCount,
            rate);
    }

    public async Task<CheckInResultDto> CheckInAsync(
        Guid eventId, Guid dayId, CheckInRequest request, ActorContext actor, CancellationToken ct = default)
    {
        await LoadDayAsync(eventId, dayId, ct);
        var scopeCellId = await GetScopeCellIdAsync(actor, ct);

        var ids = (request.MemberIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new AppValidationException(nameof(request.MemberIds), "Select at least one member to check in.");

        var members = await _db.Members
            .Where(m => ids.Contains(m.Id) && m.IsActive)
            .ToListAsync(ct);

        if (members.Count != ids.Count)
            throw new AppValidationException(nameof(request.MemberIds), "One or more members do not exist or are inactive.");

        if (scopeCellId is not null && members.Any(m => m.CellId != scopeCellId))
            throw new ForbiddenException("You may only record attendance for members of your own cell.");

        var existing = await _db.Attendance
            .Where(a => a.EventDayId == dayId && ids.Contains(a.MemberId))
            .Select(a => a.MemberId)
            .ToListAsync(ct);

        var existingIds = existing.ToHashSet();
        var now = _clock.UtcNow;

        foreach (var member in members.Where(m => !existingIds.Contains(m.Id)))
        {
            _db.Attendance.Add(new Domain.Entities.Attendance
            {
                Id = Guid.NewGuid(),
                EventDayId = dayId,
                MemberId = member.Id,
                CheckedInAt = now,
                RecordedBy = actor.UserId
            });
        }

        await _db.SaveChangesAsync(ct);
        await _vip.PromoteAllEligibleAsync(ct);

        var totalQuery = _db.Attendance.Where(a => a.EventDayId == dayId);
        if (scopeCellId is not null)
            totalQuery = totalQuery.Where(a => a.Member.CellId == scopeCellId);
        var total = await totalQuery.CountAsync(ct);
        return new CheckInResultDto(
            members.Count - existingIds.Count,
            existingIds.Count,
            total);
    }

    public async Task<ScanCheckInResultDto> ScanQrCodeAsync(
        Guid eventId, Guid dayId, ScanQrCodeRequest request, ActorContext actor, CancellationToken ct = default)
    {
        await LoadDayAsync(eventId, dayId, ct);
        var scopeCellId = await GetScopeCellIdAsync(actor, ct);

        if (!MemberQrCode.TryParseToken(request.QrCode, out var token))
            throw new AppValidationException(nameof(request.QrCode), "This QR code is not recognized.");

        var member = await _db.Members
            .FirstOrDefaultAsync(m => m.QrToken == token && m.IsActive, ct)
            ?? throw new NotFoundException("This QR code does not match an active member.");

        if (scopeCellId is not null && member.CellId != scopeCellId)
            throw new ForbiddenException("This member belongs to another cell.");

        var alreadyCheckedIn = await _db.Attendance
            .AnyAsync(a => a.EventDayId == dayId && a.MemberId == member.Id, ct);

        if (!alreadyCheckedIn)
        {
            _db.Attendance.Add(new Domain.Entities.Attendance
            {
                Id = Guid.NewGuid(),
                EventDayId = dayId,
                MemberId = member.Id,
                CheckedInAt = _clock.UtcNow,
                RecordedBy = actor.UserId
            });
            await _db.SaveChangesAsync(ct);
        }

        var totalQuery = _db.Attendance.Where(a => a.EventDayId == dayId);
        if (scopeCellId is not null)
            totalQuery = totalQuery.Where(a => a.Member.CellId == scopeCellId);
        var total = await totalQuery.CountAsync(ct);

        await _vip.PromoteIfEligibleAsync(member.Id, ct);

        return new ScanCheckInResultDto(
            member.Id,
            member.FullName,
            alreadyCheckedIn,
            total);
    }

    public async Task<VipCheckInResultDto> CreateOrCheckInVipAsync(
        Guid eventId,
        Guid dayId,
        CreateVipCheckInRequest request,
        ActorContext actor,
        CancellationToken ct = default)
    {
        if (!actor.IsAdmin)
            throw new ForbiddenException("Only administrators can add VIP attendees.");

        await LoadDayAsync(eventId, dayId, ct);

        Member member;
        var created = false;

        if (request.ExistingMemberId is Guid existingMemberId)
        {
            member = await _db.Members
                .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
                .FirstOrDefaultAsync(m => m.Id == existingMemberId && m.IsActive, ct)
                ?? throw new NotFoundException("VIP member not found.");

            if (!IsVip(member) || member.CellId is not null)
                throw new AppValidationException(
                    nameof(request.ExistingMemberId),
                    "The selected account is not an unassigned VIP member.");
        }
        else
        {
            var lastName = Required(request.LastName, nameof(request.LastName));
            var firstName = Required(request.FirstName, nameof(request.FirstName));
            var email = Required(request.Email, nameof(request.Email)).ToLowerInvariant();

            // Also protect direct API calls: if the exact same VIP name already exists,
            // reuse it instead of creating a duplicate.
            var matched = await _db.Members
                .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
                .FirstOrDefaultAsync(m =>
                    m.IsActive &&
                    m.CellId == null &&
                    m.LastName.ToLower() == lastName.ToLower() &&
                    m.FirstName.ToLower() == firstName.ToLower() &&
                    m.MemberRoles.Any(mr => mr.Role.Name == RoleNames.Vip), ct);

            if (matched is not null)
            {
                member = matched;
            }
            else
            {
                if (await _db.Members.AnyAsync(m => m.Email == email, ct))
                    throw new ConflictException("A member with that email already exists.");

                var inviter = await ValidateInviterAsync(request.InvitedByMemberId, ct);
                var vipRole = await _db.Roles.SingleAsync(r => r.Name == RoleNames.Vip, ct);
                var now = _clock.UtcNow;

                member = new Member
                {
                    Id = Guid.NewGuid(),
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PhoneNo = CleanOptional(request.PhoneNo),
                    CellId = null,
                    InvitedByMemberId = inviter?.Id,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                member.MemberRoles.Add(new MemberRole
                {
                    MemberId = member.Id,
                    RoleId = vipRole.Id,
                    Role = vipRole
                });

                _db.Members.Add(member);
                created = true;
            }
        }

        var alreadyCheckedIn = await _db.Attendance
            .AnyAsync(a => a.EventDayId == dayId && a.MemberId == member.Id, ct);

        if (!alreadyCheckedIn)
        {
            _db.Attendance.Add(new Domain.Entities.Attendance
            {
                Id = Guid.NewGuid(),
                EventDayId = dayId,
                MemberId = member.Id,
                CheckedInAt = _clock.UtcNow,
                RecordedBy = actor.UserId
            });
        }

        await _db.SaveChangesAsync(ct);

        var total = await _db.Attendance.CountAsync(a => a.EventDayId == dayId, ct);

        await _vip.PromoteIfEligibleAsync(member.Id, ct);

        return new VipCheckInResultDto(
            member.Id,
            member.FullName,
            created,
            alreadyCheckedIn,
            total);
    }

    public async Task RemoveAsync(Guid eventId, Guid dayId, Guid memberId, ActorContext actor, CancellationToken ct = default)
    {
        await LoadDayAsync(eventId, dayId, ct);
        var scopeCellId = await GetScopeCellIdAsync(actor, ct);

        var record = await _db.Attendance
            .Include(a => a.Member)
            .FirstOrDefaultAsync(a => a.EventDayId == dayId && a.MemberId == memberId, ct)
            ?? throw new NotFoundException("Attendance record not found.");

        if (scopeCellId is not null && record.Member.CellId != scopeCellId)
            throw new ForbiddenException("You may only undo attendance for members of your own cell.");

        _db.Attendance.Remove(record);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<EventDay> LoadDayAsync(Guid eventId, Guid dayId, CancellationToken ct)
    {
        var day = await _db.EventDays
            .Include(d => d.Event)
            .FirstOrDefaultAsync(d => d.Id == dayId && d.EventId == eventId, ct)
            ?? throw new NotFoundException("Event day not found.");
        return day;
    }

    private async Task<Guid?> GetScopeCellIdAsync(ActorContext actor, CancellationToken ct)
    {
        if (actor.IsAdmin)
            return null;

        if (!actor.IsCellLeader)
            throw new ForbiddenException("Only an administrator or cell leader can manage attendance.");

        var cell = await _db.Cells
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.LeaderMemberId == actor.MemberId && c.IsActive, ct)
            ?? throw new ForbiddenException("You do not lead an active cell.");

        return cell.Id;
    }

    private static AttendanceMemberDto ToDto(Member member, DateTimeOffset? checkedInAt) =>
        new(
            member.Id,
            member.FullName,
            member.Email,
            member.Cell?.Name,
            member.MemberRoles.OrderBy(mr => mr.RoleId).Select(mr => mr.Role.Name).ToList(),
            checkedInAt);

    private async Task<Member?> ValidateInviterAsync(Guid? inviterId, CancellationToken ct)
    {
        if (inviterId is null)
            return null;

        var inviter = await _db.Members
            .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
            .FirstOrDefaultAsync(m => m.Id == inviterId && m.IsActive, ct)
            ?? throw new AppValidationException(
                "invitedByMemberId",
                "The selected inviter is not an active member.");

        if (inviter.MemberRoles.Any(mr =>
                string.Equals(mr.Role.Name, RoleNames.Vip, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AppValidationException(
                "invitedByMemberId",
                "A VIP cannot be selected as the inviter.");
        }

        return inviter;
    }

    private static bool IsVip(Member member) =>
        member.MemberRoles.Any(mr =>
            string.Equals(mr.Role.Name, RoleNames.Vip, StringComparison.OrdinalIgnoreCase));

    private static string Required(string? value, string field)
    {
        var cleaned = (value ?? string.Empty).Trim();
        if (cleaned.Length == 0)
            throw new AppValidationException(field, "This field is required.");
        return cleaned;
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool Matches(AttendanceMemberDto member, string term) =>
        member.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        member.Email.Contains(term, StringComparison.OrdinalIgnoreCase);
}
