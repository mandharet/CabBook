using Microsoft.AspNetCore.Mvc;

namespace CabBook.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService) : ControllerBase
{
    private readonly AuthService _authService = authService;

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
                return Unauthorized(new { error = "Invalid or expired OTP" });

            return Ok(new { token });
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
