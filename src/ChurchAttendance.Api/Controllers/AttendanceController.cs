using ChurchAttendance.Api.Extensions;
using ChurchAttendance.Application.Attendance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/days/{dayId:guid}/attendance")]
[Authorize(Roles = "Admin,CellLeader")]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceService _attendance;

    public AttendanceController(AttendanceService attendance)
    {
        _attendance = attendance;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AttendanceDayDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceDayDto>> Get(
        Guid eventId,
        Guid dayId,
        [FromQuery] string? search,
        CancellationToken ct) =>
        Ok(await _attendance.GetDayAsync(eventId, dayId, search, User.ToActor(), ct));

    [HttpPost]
    [ProducesResponseType(typeof(CheckInResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckInResultDto>> CheckIn(
        Guid eventId,
        Guid dayId,
        CheckInRequest request,
        CancellationToken ct) =>
        Ok(await _attendance.CheckInAsync(eventId, dayId, request, User.ToActor(), ct));

    [HttpPost("scan")]
    [ProducesResponseType(typeof(ScanCheckInResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScanCheckInResultDto>> ScanQrCode(
        Guid eventId,
        Guid dayId,
        ScanQrCodeRequest request,
        CancellationToken ct) =>
        Ok(await _attendance.ScanQrCodeAsync(eventId, dayId, request, User.ToActor(), ct));

    [HttpDelete("{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid eventId, Guid dayId, Guid memberId, CancellationToken ct)
    {
        await _attendance.RemoveAsync(eventId, dayId, memberId, User.ToActor(), ct);
        return NoContent();
    }
}
