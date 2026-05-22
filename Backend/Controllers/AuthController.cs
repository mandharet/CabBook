using Microsoft.AspNetCore.Mvc;

namespace CabBook.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService, UserService userService) : ControllerBase
{
    private readonly AuthService _authService = authService;
    private readonly UserService _userService = userService;

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required" });

        try
        {
            var result = await _authService.SendOtpAsync(request.Email, request.TenantId);
            if (result == null)
                return NotFound(new { error = "User not found" });

            return Ok(new { message = "OTP sent to email", otp = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
            return BadRequest(new { error = "Email and OTP are required" });

        try
        {
            var token = await _authService.VerifyOtpAsync(request.Email, request.Otp, request.TenantId);
            if (token == null)
            {
                return Unauthorized(new { error = "Invalid or expired OTP. Please request a new OTP and try again." });
            }

            return Ok(new { token });
        }
        catch (InvalidOperationException ex)
        {
            // User status not approved
            return StatusCode(403, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required" });

        if (string.IsNullOrWhiteSpace(request.PickupAddress))
            return BadRequest(new { error = "Pickup address is required" });

        if (string.IsNullOrWhiteSpace(request.DropoffAddress))
            return BadRequest(new { error = "Dropoff address is required" });

        try
        {
            var result = await _userService.SignupAsync(request.Email, request.PhoneNumber, request.Name, request.PickupAddress, request.DropoffAddress, request.TenantId);
            return CreatedAtAction(nameof(Signup), new { message = "Signup successful. Awaiting admin approval.", user = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class SendOtpRequest
{
    public string Email { get; set; } = null!;
    public long TenantId { get; set; }
}

public class VerifyOtpRequest
{
    public string Email { get; set; } = null!;
    public string Otp { get; set; } = null!;
    public long TenantId { get; set; }
}

public class SignupRequest
{
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? Name { get; set; }
    public string PickupAddress { get; set; } = null!;
    public string DropoffAddress { get; set; } = null!;
    public long TenantId { get; set; }
}
