using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabBook.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantController(TenantService tenantService) : ControllerBase
{
    private readonly TenantService _tenantService = tenantService;

    private long TenantId => long.Parse(User.FindFirst("tenant_id")?.Value ?? "0");
    private string UserRole => User.FindFirst("role")?.Value ?? "";

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var config = await _tenantService.GetConfigAsync(TenantId);
        if (config == null)
            return NotFound();

        return Ok(config);
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Get()
    {
        var tenant = await _tenantService.GetAsync(TenantId);
        if (tenant == null)
            return NotFound();

        return Ok(tenant);
    }

    [HttpPut]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update([FromBody] UpdateTenantRequest request)
    {
        try
        {
            var tenant = await _tenantService.UpdateAsync(TenantId, request.Name, request.TimeZone);
            if (tenant == null)
                return NotFound();

            return Ok(tenant);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class UpdateTenantRequest
{
    public string? Name { get; set; }
    public string? TimeZone { get; set; }
}
