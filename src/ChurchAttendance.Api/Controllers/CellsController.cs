using ChurchAttendance.Application.Cells;
using ChurchAttendance.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Api.Controllers;

[ApiController]
[Route("api/cells")]
[Authorize]
public class CellsController : ControllerBase
{
    private readonly CellService _cells;

    public CellsController(CellService cells)
    {
        _cells = cells;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CellListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CellListItemDto>>> GetAll(CancellationToken ct) =>
        Ok(await _cells.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CellDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CellDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _cells.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(CellDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CellDto>> Create(CreateCellRequest request, CancellationToken ct)
    {
        var created = await _cells.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(CellDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CellDto>> Update(Guid id, UpdateCellRequest request, CancellationToken ct) =>
        Ok(await _cells.UpdateAsync(id, request, ct));

    [HttpGet("{id:guid}/candidates")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<CellCandidateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CellCandidateDto>>> GetCandidates(
        Guid id, [FromQuery] string? search, CancellationToken ct) =>
        Ok(await _cells.GetCandidatesAsync(id, search, ct));

    [HttpPost("{id:guid}/members")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddMembers(Guid id, AddCellMembersRequest request, CancellationToken ct)
    {
        await _cells.AddMembersAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct)
    {
        await _cells.RemoveMemberAsync(id, memberId, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _cells.DeleteAsync(id, ct);
        return NoContent();
    }
}
