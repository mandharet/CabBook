using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabBook.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RosterController(RosterService rosterService) : ControllerBase
{
    private readonly RosterService _rosterService = rosterService;

    private long TenantId => long.Parse(User.FindFirst("tenant_id")?.Value ?? "0");
    private string UserRole => User.FindFirst("role")?.Value ?? "";

    [HttpGet]
    public async Task<IActionResult> ListRosters([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var from = fromDate ?? DateTime.Today.AddDays(-30);
        var to = toDate ?? DateTime.Today.AddDays(30);

        var rosters = await _rosterService.ListRostersAsync(TenantId, from, to);
        return Ok(rosters);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRoster(long id)
    {
        var roster = await _rosterService.GetRosterAsync(TenantId, id);
        if (roster == null)
            return NotFound();

        return Ok(roster);
    }

    [HttpPost("{id}/freeze")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> FreezeRoster(long id)
    {
        try
        {
            var success = await _rosterService.FreezeRosterAsync(TenantId, id);
            if (!success)
                return NotFound(new { error = "Roster not found or already frozen" });

            return Ok(new { message = "Roster frozen successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id}/export")]
    public async Task<IActionResult> ExportRoster(long id)
    {
        var csv = await _rosterService.ExportRosterAsync(TenantId, id);
        if (csv == null)
            return NotFound(new { error = "Roster not exported yet" });

        return File(csv, "text/csv", $"roster_{id}.csv");
    }

    [HttpPost("check-and-freeze")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CheckAndFreezeExpiredRosters()
    {
        try
        {
            var success = await _rosterService.CheckAndFreezeExpiredRostersAsync(TenantId);
            return Ok(new { message = "Roster freeze check completed", success });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
