using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CabBook.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController(UserService userService) : ControllerBase
{
    private readonly UserService _userService = userService;

    private long GetTenantId()
    {
        return long.Parse(User.FindFirst("tenant_id")?.Value ?? "0");
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingUsers()
    {
        try
        {
            var tenantId = GetTenantId();
            var users = await _userService.GetPendingUsersAsync(tenantId);
            return Ok(new { users });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{userId}/approve")]
    public async Task<IActionResult> ApproveUser(long userId)
    {
        try
        {
            var tenantId = GetTenantId();
            await _userService.ApproveUserAsync(userId, tenantId);
            return Ok(new { message = "User approved" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{userId}/reject")]
    public async Task<IActionResult> RejectUser(long userId)
    {
        try
        {
            var tenantId = GetTenantId();
            await _userService.RejectUserAsync(userId, tenantId);
            return Ok(new { message = "User rejected" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUser(long userId)
    {
        try
        {
            var tenantId = GetTenantId();
            var user = await _userService.GetUserAsync(userId, tenantId);
            if (user == null)
                return NotFound(new { error = "User not found" });

            return Ok(user);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{userId}/approve-addresses")]
    public async Task<IActionResult> ApproveAddresses(long userId)
    {
        try
        {
            var tenantId = GetTenantId();
            await _userService.ApproveAddressesAsync(userId, tenantId);
            return Ok(new { message = "Addresses approved" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{userId}/addresses")]
    public async Task<IActionResult> UpdateAddresses(long userId, [FromBody] UpdateAddressesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PickupAddress) || string.IsNullOrWhiteSpace(request.DropoffAddress))
            return BadRequest(new { error = "Both addresses are required" });

        try
        {
            var tenantId = GetTenantId();
            await _userService.UpdateAddressesAsync(userId, tenantId, request.PickupAddress, request.DropoffAddress);
            return Ok(new { message = "Addresses updated. Awaiting admin approval." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class UpdateAddressesRequest
{
    public string PickupAddress { get; set; } = null!;
    public string DropoffAddress { get; set; } = null!;
}
