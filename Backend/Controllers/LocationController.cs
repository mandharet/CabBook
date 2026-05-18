using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabBook.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationController(LocationService locationService) : ControllerBase
{
    private readonly LocationService _locationService = locationService;

    private long TenantId => long.Parse(User.FindFirst("tenant_id")?.Value ?? "0");
    private string UserRole => User.FindFirst("role")?.Value ?? "";

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = true)
    {
        var locations = await _locationService.ListAsync(TenantId, activeOnly);
        return Ok(locations);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(long id)
    {
        var location = await _locationService.GetAsync(TenantId, id);
        if (location == null)
            return NotFound();

        return Ok(location);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateLocationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required" });

        try
        {
            var location = await _locationService.CreateAsync(
                TenantId,
                request.Name,
                request.Address);

            return CreatedAtAction(nameof(Get), new { id = location?.Id }, location);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateLocationRequest request)
    {
        try
        {
            var location = await _locationService.UpdateAsync(TenantId, id, request.Name, request.Address);
            if (location == null)
                return NotFound();

            return Ok(location);
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
        var success = await _locationService.DeleteAsync(TenantId, id);
        if (!success)
            return NotFound();

        return Ok(new { message = "Location deleted" });
    }
}

public class CreateLocationRequest
{
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
}

public class UpdateLocationRequest
{
    public string? Name { get; set; }
    public string? Address { get; set; }
}
