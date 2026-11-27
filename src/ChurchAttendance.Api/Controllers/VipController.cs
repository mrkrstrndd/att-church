using ChurchAttendance.Application.Vip;
using ChurchAttendance.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Api.Controllers;

[ApiController]
[Route("api/vip")]
[Authorize(Roles = RoleNames.Admin)]
public class VipController : ControllerBase
{
    private readonly VipService _vip;

    public VipController(VipService vip)
    {
        _vip = vip;
    }

    [HttpGet("report")]
    [ProducesResponseType(typeof(VipReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VipReportDto>> GetReport(CancellationToken ct) =>
        Ok(await _vip.GetReportAsync(ct));
}
