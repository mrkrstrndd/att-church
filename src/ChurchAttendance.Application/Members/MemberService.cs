using ChurchAttendance.Application.Auth;
using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Configuration;
using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChurchAttendance.Application.Members;

public class MemberService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IChurchClock _clock;
    private readonly ChurchSettings _settings;

    public MemberService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IChurchClock clock,
        IOptions<ChurchSettings> settings)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _settings = settings.Value;
    }

    public async Task<PagedResult<MemberDto>> GetPagedAsync(
        MemberQuery query, ActorContext actor, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? _settings.DefaultPageSize : query.PageSize, 1, _settings.MaxPageSize);

        var members = _db.Members
            .AsNoTracking()
            .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
            .Include(m => m.Cell)
            .Include(m => m.User)
            .AsQueryable();

        if (!query.IncludeInactive || !actor.IsAdmin)
            members = members.Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            members = members.Where(m =>
                m.FirstName.ToLower().Contains(search) ||
                m.LastName.ToLower().Contains(search) ||
                m.Email.ToLower().Contains(search) ||
                (m.PhoneNo != null && m.PhoneNo.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = NormalizeRole(query.Role) ?? throw new AppValidationException(nameof(query.Role), "Unknown role.");
            members = members.Where(m => m.MemberRoles.Any(mr => mr.Role.Name == role));
        }

        if (query.CellId is not null)
            members = members.Where(m => m.CellId == query.CellId);

        if (query.Unassigned)
            members = members.Where(m => m.CellId == null);

        var total = await members.CountAsync(ct);

        var items = await members
            .OrderBy(m => m.LastName)
            .ThenBy(m => m.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<MemberDto>.Create(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<MemberDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var member = await QueryMembers().FirstOrDefaultAsync(m => m.Id == id, ct)
                     ?? throw new NotFoundException("Member not found.");
        return ToDto(member);
    }

    public async Task<MemberQrCodeDto> GetQrCodeAsync(Guid id, CancellationToken ct = default)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == id && m.IsActive, ct)
                     ?? throw new NotFoundException("Member not found.");

        if (string.IsNullOrWhiteSpace(member.QrToken) || !MemberQrCode.TryParseToken(member.QrToken, out _))
        {
            member.QrToken = await NewUniqueQrTokenAsync(ct);
            member.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return ToQrCodeDto(member);
    }

    public async Task<MemberQrCodeDto> RotateQrCodeAsync(Guid id, CancellationToken ct = default)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == id && m.IsActive, ct)
                     ?? throw new NotFoundException("Member not found.");

        member.QrToken = await NewUniqueQrTokenAsync(ct);
        member.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToQrCodeDto(member);
    }

    public async Task<MemberDto> CreateAsync(
        CreateMemberRequest request, ActorContext actor, CancellationToken ct = default)
    {
        if (request.CreateLogin is not null && !actor.IsAdmin)
            throw new ForbiddenException("Only administrators can create member login accounts.");

        var email = NormalizeEmail(request.Email);
        if (await _db.Members.AnyAsync(m => m.Email == email, ct))
            throw new ConflictException("A member with that email already exists.");

        var roleNames = NormalizeRoles(request.Roles);
        if (roleNames.Count == 0)
            roleNames = new[] { RoleNames.Member };

        EnsureVipRules(roleNames, request.CellId);

        var roles = await LoadRolesAsync(roleNames, ct);
        var cell = await GetAssignableCellAsync(request.CellId, ct);

        var now = _clock.UtcNow;
        var member = new Member
        {
            LastName = Clean(request.LastName),
            FirstName = Clean(request.FirstName),
            Email = email,
            PhoneNo = CleanOptional(request.PhoneNo),
            CellId = cell?.Id,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var role in roles)
            member.MemberRoles.Add(new MemberRole { MemberId = member.Id, RoleId = role.Id, Role = role });

        if (request.CreateLogin is not null)
        {
            var username = NormalizeUsername(request.CreateLogin.Username);
            if (await _db.Users.AnyAsync(u => u.Username == username, ct))
                throw new ConflictException("That username is already taken.");

            member.User = new User
            {
                Id = Guid.NewGuid(),
                MemberId = member.Id,
                Username = username,
                PasswordHash = _passwordHasher.Hash(request.CreateLogin.Password!),
                MustChangePassword = true,
                IsActive = true,
                CreatedAt = now
            };
        }

        _db.Members.Add(member);
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(member.Id, ct);
    }

    public async Task<MemberDto> UpdateAsync(Guid id, UpdateMemberRequest request, CancellationToken ct = default)
    {
        var member = await QueryTracked().FirstOrDefaultAsync(m => m.Id == id && m.IsActive, ct)
                     ?? throw new NotFoundException("Member not found.");

        var email = NormalizeEmail(request.Email);
        if (await _db.Members.AnyAsync(m => m.Email == email && m.Id != id, ct))
            throw new ConflictException("A member with that email already exists.");

        var roleNames = NormalizeRoles(request.Roles);
        if (roleNames.Count == 0)
            throw new AppValidationException(nameof(request.Roles), "Select at least one role.");

        EnsureVipRules(roleNames, request.CellId);

        var isLeader = await _db.Cells.AnyAsync(c => c.IsActive && c.LeaderMemberId == member.Id, ct);
        if (isLeader && !roleNames.Contains(RoleNames.CellLeader, StringComparer.OrdinalIgnoreCase))
            throw new ConflictException("This member leads an active cell, so the CellLeader role cannot be removed. Assign a new leader first.");

        var roles = await LoadRolesAsync(roleNames, ct);
        var cell = await GetAssignableCellAsync(request.CellId, ct);

        member.LastName = Clean(request.LastName);
        member.FirstName = Clean(request.FirstName);
        member.Email = email;
        member.PhoneNo = CleanOptional(request.PhoneNo);
        member.CellId = cell?.Id;
        member.UpdatedAt = _clock.UtcNow;

        var requestedRoleIds = roles.Select(r => r.Id).ToHashSet();
        foreach (var existing in member.MemberRoles.Where(mr => !requestedRoleIds.Contains(mr.RoleId)).ToList())
            _db.MemberRoles.Remove(existing);

        foreach (var role in roles.Where(r => member.MemberRoles.All(mr => mr.RoleId != r.Id)))
            member.MemberRoles.Add(new MemberRole { MemberId = member.Id, RoleId = role.Id, Role = role });

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(member.Id, ct);
    }

    public async Task DeleteAsync(Guid id, ActorContext actor, CancellationToken ct = default)
    {
        if (id == actor.MemberId)
            throw new ConflictException("You cannot deactivate your own member account.");

        var member = await QueryTracked().FirstOrDefaultAsync(m => m.Id == id && m.IsActive, ct)
                     ?? throw new NotFoundException("Member not found.");

        if (await _db.Cells.AnyAsync(c => c.IsActive && c.LeaderMemberId == id, ct))
            throw new ConflictException("This member is the leader of an active cell. Assign a new leader before deactivating them.");

        var now = _clock.UtcNow;
        member.IsActive = false;
        member.CellId = null;
        member.UpdatedAt = now;

        if (member.User is not null)
        {
            member.User.IsActive = false;
            var tokens = await _db.RefreshTokens.Where(t => t.UserId == member.User.Id && t.RevokedAt == null).ToListAsync(ct);
            foreach (var token in tokens)
                token.RevokedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    private IQueryable<Member> QueryMembers() => _db.Members
        .AsNoTracking()
        .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
        .Include(m => m.Cell)
        .Include(m => m.User);

    private IQueryable<Member> QueryTracked() => _db.Members
        .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
        .Include(m => m.Cell)
        .Include(m => m.User);

    private async Task<List<Role>> LoadRolesAsync(IReadOnlyCollection<string> roleNames, CancellationToken ct)
    {
        var roles = await _db.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync(ct);
        if (roles.Count != roleNames.Count)
            throw new AppValidationException("roles", "One or more roles are invalid.");
        return roles;
    }

    private async Task<string> NewUniqueQrTokenAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var token = MemberQrCode.NewToken();
            if (!await _db.Members.AnyAsync(m => m.QrToken == token, ct))
                return token;
        }

        throw new ConflictException("Could not generate a unique member QR code. Please try again.");
    }

    private static void EnsureVipRules(IReadOnlyCollection<string> roles, Guid? cellId)
    {
        if (!roles.Contains(RoleNames.Vip, StringComparer.OrdinalIgnoreCase))
            return;

        if (roles.Count > 1)
            throw new AppValidationException("roles", "A VIP cannot have any other roles.");
        if (cellId is not null)
            throw new AppValidationException("cellId", "A VIP cannot be assigned to a cell.");
    }

    private async Task<Cell?> GetAssignableCellAsync(Guid? cellId, CancellationToken ct)
    {
        if (cellId is null)
            return null;

        var cell = await _db.Cells.FirstOrDefaultAsync(c => c.Id == cellId && c.IsActive, ct)
                   ?? throw new AppValidationException("cellId", "The selected cell is not active or does not exist.");
        return cell;
    }

    private static IReadOnlyList<string> NormalizeRoles(IReadOnlyList<string>? roles)
    {
        if (roles is null)
            return Array.Empty<string>();

        var normalized = new List<string>();
        foreach (var role in roles)
        {
            var canonical = NormalizeRole(role);
            if (canonical is null)
                throw new AppValidationException("roles", $"Unknown role '{role}'.");
            if (!normalized.Contains(canonical, StringComparer.OrdinalIgnoreCase))
                normalized.Add(canonical);
        }
        return normalized;
    }

    private static string? NormalizeRole(string? role) =>
        RoleNames.All.FirstOrDefault(r => string.Equals(r, role?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
    private static string NormalizeUsername(string? username) => (username ?? string.Empty).Trim().ToLowerInvariant();
    private static string Clean(string? value) => (value ?? string.Empty).Trim();
    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MemberDto ToDto(Member member)
    {
        var roles = member.MemberRoles
            .OrderBy(mr => mr.RoleId)
            .Select(mr => mr.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MemberDto(
            member.Id,
            member.LastName,
            member.FirstName,
            member.FullName,
            member.Email,
            member.PhoneNo,
            member.CellId,
            member.Cell?.Name,
            roles,
            member.User is not null,
            member.IsActive,
            member.CreatedAt);
    }

    private static MemberQrCodeDto ToQrCodeDto(Member member) =>
        new(member.Id, member.FullName, MemberQrCode.BuildPayload(member.QrToken));
}
