using ChurchAttendance.Api.Extensions;
using ChurchAttendance.Application.Common;
using ChurchAttendance.Application.Events;
using ChurchAttendance.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Api.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly EventService _events;

    public EventsController(EventService events)
    {
        _events = events;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EventListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EventListItemDto>>> GetAll(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _events.GetPagedAsync(new EventQuery(from, to, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EventDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _events.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<EventDto>> Create(CreateEventRequest request, CancellationToken ct)
    {
        var created = await _events.CreateAsync(request, User.ToActor(), ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EventDto>> Update(Guid id, UpdateEventRequest request, CancellationToken ct) =>
        Ok(await _events.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool force = false, CancellationToken ct = default)
    {
        await _events.DeleteAsync(id, force, ct);
        return NoContent();
    }
}
