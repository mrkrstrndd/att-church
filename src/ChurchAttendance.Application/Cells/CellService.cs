using ChurchAttendance.Application.Common;
using ChurchAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.Cells;

public class CellService
{
    private readonly IApplicationDbContext _db;
    private readonly IChurchClock _clock;

    public CellService(IApplicationDbContext db, IChurchClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CellListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var cells = await _db.Cells
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Include(c => c.Leader)
            .Include(c => c.Members)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        return cells.Select(c => new CellListItemDto(
            c.Id,
            c.Name,
            c.Description,
            c.LeaderMemberId,
            c.Leader.FullName,
            c.Members.Count(m => m.IsActive),
            c.CreatedAt)).ToList();
    }

    public async Task<CellDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cell = await _db.Cells
            .AsNoTracking()
            .Where(c => c.Id == id && c.IsActive)
            .Include(c => c.Leader)
            .Include(c => c.Members).ThenInclude(m => m.MemberRoles).ThenInclude(mr => mr.Role)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Cell not found.");

        var members = cell.Members
            .Where(m => m.IsActive)
            .OrderBy(m => m.LastName)
            .ThenBy(m => m.FirstName)
            .Select(m => new CellMemberDto(
                m.Id,
                m.FullName,
                m.Email,
                m.PhoneNo,
                m.MemberRoles.OrderBy(mr => mr.RoleId).Select(mr => mr.Role.Name).ToList(),
                m.Id == cell.LeaderMemberId))
            .ToList();

        return new CellDto(cell.Id, cell.Name, cell.Description, cell.LeaderMemberId, cell.Leader.FullName, members, cell.CreatedAt);
    }

    public async Task<CellDto> CreateAsync(CreateCellRequest request, CancellationToken ct = default)
    {
        var name = CleanName(request.Name);
        await EnsureNameAvailableAsync(name, null, ct);
        var leader = await ValidateLeaderAsync(request.LeaderMemberId, null, ct);

        var now = _clock.UtcNow;
        var cell = new Cell
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = CleanOptional(request.Description),
            LeaderMemberId = leader.Id,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Cells.Add(cell);

        if (leader.CellId is null)
        {
            leader.CellId = cell.Id;
            leader.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(cell.Id, ct);
    }

    public async Task<CellDto> UpdateAsync(Guid id, UpdateCellRequest request, CancellationToken ct = default)
    {
        var cell = await _db.Cells
            .Include(c => c.Leader)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct)
            ?? throw new NotFoundException("Cell not found.");

        var name = CleanName(request.Name);
        await EnsureNameAvailableAsync(name, id, ct);
        var leader = await ValidateLeaderAsync(request.LeaderMemberId, id, ct);

        cell.Name = name;
        cell.Description = CleanOptional(request.Description);
        cell.LeaderMemberId = leader.Id;
        cell.UpdatedAt = _clock.UtcNow;

        if (leader.CellId is null)
            leader.CellId = cell.Id;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(cell.Id, ct);
    }

    public async Task AddMembersAsync(Guid cellId, AddCellMembersRequest request, CancellationToken ct = default)
    {
        var cell = await _db.Cells.FirstOrDefaultAsync(c => c.Id == cellId && c.IsActive, ct)
                   ?? throw new NotFoundException("Cell not found.");

        var ids = (request.MemberIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new AppValidationException(nameof(request.MemberIds), "Select at least one member.");

        var members = await _db.Members
            .Where(m => ids.Contains(m.Id) && m.IsActive)
            .ToListAsync(ct);

        if (members.Count != ids.Count)
            throw new AppValidationException(nameof(request.MemberIds), "One or more selected members do not exist or are inactive.");

        var inOtherCell = members.Where(m => m.CellId is not null && m.CellId != cellId).ToList();
        if (inOtherCell.Count > 0)
            throw new ConflictException(
                "These members already belong to another cell: " +
                string.Join(", ", inOtherCell.Select(m => m.FullName)) + ".");

        var now = _clock.UtcNow;
        foreach (var member in members.Where(m => m.CellId != cellId))
        {
            member.CellId = cellId;
            member.UpdatedAt = now;
        }

        cell.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid cellId, Guid memberId, CancellationToken ct = default)
    {
        var cell = await _db.Cells.FirstOrDefaultAsync(c => c.Id == cellId && c.IsActive, ct)
                   ?? throw new NotFoundException("Cell not found.");

        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId && m.IsActive, ct)
                     ?? throw new NotFoundException("Member not found.");

        if (member.CellId != cellId)
            throw new NotFoundException("That member is not in this cell.");

        member.CellId = null;
        member.UpdatedAt = _clock.UtcNow;
        cell.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var cell = await _db.Cells
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct)
            ?? throw new NotFoundException("Cell not found.");

        var now = _clock.UtcNow;
        cell.IsActive = false;
        cell.UpdatedAt = now;

        foreach (var member in cell.Members)
        {
            member.CellId = null;
            member.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? excludeId, CancellationToken ct)
    {
        var lower = name.ToLowerInvariant();
        if (await _db.Cells.AnyAsync(c => c.Name.ToLower() == lower && c.Id != excludeId, ct))
            throw new ConflictException("A cell with that name already exists.");
    }

    private async Task<Member> ValidateLeaderAsync(Guid leaderMemberId, Guid? currentCellId, CancellationToken ct)
    {
        var leader = await _db.Members
            .Include(m => m.MemberRoles).ThenInclude(mr => mr.Role)
            .FirstOrDefaultAsync(m => m.Id == leaderMemberId && m.IsActive, ct)
            ?? throw new AppValidationException(nameof(leaderMemberId), "The selected leader does not exist or is inactive.");

        if (!leader.MemberRoles.Any(mr =>
                string.Equals(mr.Role.Name, RoleNames.CellLeader, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AppValidationException(
                nameof(leaderMemberId),
                "The selected leader must have the CellLeader role. Grant that role on the member record first.");
        }

        var leadsAnotherCell = await _db.Cells.AnyAsync(
            c => c.IsActive && c.LeaderMemberId == leaderMemberId && c.Id != currentCellId, ct);
        if (leadsAnotherCell)
            throw new ConflictException("That member already leads another active cell.");

        return leader;
    }

    private static string CleanName(string? name) => (name ?? string.Empty).Trim();
    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
