using ChurchAttendance.Api.Extensions;
using ChurchAttendance.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Admin,CellLeader")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboard;

    public DashboardController(DashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet("kpis")]
    [ProducesResponseType(typeof(DashboardKpisDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardKpisDto>> GetKpis(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Ok(await _dashboard.GetKpisAsync(from, to, User.ToActor(), ct));

    [HttpGet("weekly-trend")]
    [ProducesResponseType(typeof(WeeklyTrendDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WeeklyTrendDto>> GetWeeklyTrend(
        [FromQuery] int? weeks,
        CancellationToken ct) =>
        Ok(await _dashboard.GetWeeklyTrendAsync(weeks, User.ToActor(), ct));

    [HttpGet("attendance-by-cell")]
    [ProducesResponseType(typeof(AttendanceByCellDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceByCellDto>> GetAttendanceByCell(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Ok(await _dashboard.GetAttendanceByCellAsync(from, to, User.ToActor(), ct));

    [HttpGet("monthly-comparison")]
    [ProducesResponseType(typeof(MonthlyComparisonDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyComparisonDto>> GetMonthlyComparison(
        [FromQuery] int? months,
        [FromQuery] int? year,
        CancellationToken ct) =>
        Ok(await _dashboard.GetMonthlyComparisonAsync(months, year, User.ToActor(), ct));

    [HttpGet("recent-events")]
    [ProducesResponseType(typeof(IReadOnlyList<RecentEventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecentEventDto>>> GetRecentEvents(
        [FromQuery] int? count,
        CancellationToken ct) =>
        Ok(await _dashboard.GetRecentEventsAsync(count, User.ToActor(), ct));
}
