using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabBook.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShiftSlotController(ShiftSlotService shiftSlotService) : ControllerBase
{
    private readonly ShiftSlotService _shiftSlotService = shiftSlotService;

    private long TenantId => long.Parse(User.FindFirst("tenant_id")?.Value ?? "0");
    private string UserRole => User.FindFirst("role")?.Value ?? "";

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = true)
    {
        var slots = await _shiftSlotService.ListAsync(TenantId, activeOnly);
        return Ok(slots);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(long id)
    {
        var slot = await _shiftSlotService.GetAsync(TenantId, id);
        if (slot == null)
            return NotFound();

        return Ok(slot);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateShiftSlotRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required" });

        try
        {
            var slot = await _shiftSlotService.CreateAsync(
                TenantId,
                request.Name,
                request.StartTime,
                request.EndTime,
                request.FreezeTime);

            return CreatedAtAction(nameof(Get), new { id = slot?.Id }, slot);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateShiftSlotRequest request)
    {
        try
        {
            var slot = await _shiftSlotService.UpdateAsync(
                TenantId,
                id,
                request.Name,
                request.StartTime,
                request.EndTime,
                request.FreezeTime);

            if (slot == null)
                return NotFound();

            return Ok(slot);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(long id)
    {
        var success = await _shiftSlotService.DeleteAsync(TenantId, id);
        if (!success)
            return NotFound();

        return Ok(new { message = "Shift slot deleted" });
    }

    [HttpPost("{id}/freeze-override")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetFreezeOverride(long id, [FromBody] SetFreezeOverrideRequest request)
    {
        try
        {
            await _shiftSlotService.SetFreezeOverrideAsync(TenantId, id, request.OverrideDate, request.FreezeTime);
            return Ok(new { message = "Freeze override set" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class CreateShiftSlotRequest
{
    public string Name { get; set; } = null!;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public TimeSpan? FreezeTime { get; set; }
}

public class UpdateShiftSlotRequest
{
    public string? Name { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public TimeSpan? FreezeTime { get; set; }
}

public class SetFreezeOverrideRequest
{
    public DateTime OverrideDate { get; set; }
    public TimeSpan? FreezeTime { get; set; }
}
