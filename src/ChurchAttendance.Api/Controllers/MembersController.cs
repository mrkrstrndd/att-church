using ChurchAttendance.Api.Extensions;
using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Members;
using ChurchAttendance.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Api.Controllers;

[ApiController]
[Route("api/members")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly MemberService _members;

    public MembersController(MemberService members)
    {
        _members = members;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MemberDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] Guid? cellId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool unassigned = false,
        CancellationToken ct = default)
    {
        var result = await _members.GetPagedAsync(
            new MemberQuery(search, role, cellId, page, pageSize, includeInactive, unassigned),
            User.ToActor(),
            ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MemberDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _members.GetByIdAsync(id, ct));

    [HttpGet("{id:guid}/qr-code")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(MemberQrCodeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MemberQrCodeDto>> GetQrCode(Guid id, CancellationToken ct) =>
        Ok(await _members.GetQrCodeAsync(id, ct));

    [HttpPost("{id:guid}/qr-code/rotate")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(MemberQrCodeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MemberQrCodeDto>> RotateQrCode(Guid id, CancellationToken ct) =>
        Ok(await _members.RotateQrCodeAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<MemberDto>> Create(CreateMemberRequest request, CancellationToken ct)
    {
        var created = await _members.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MemberDto>> Update(Guid id, UpdateMemberRequest request, CancellationToken ct) =>
        Ok(await _members.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _members.DeleteAsync(id, User.ToActor(), ct);
        return NoContent();
    }
}
