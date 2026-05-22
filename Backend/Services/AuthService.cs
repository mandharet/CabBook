using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace CabBook.Services;

public class AuthService(NpgsqlDataSource dataSource, IConfiguration config)
{
    private readonly NpgsqlDataSource _dataSource = dataSource;
    private readonly IConfiguration _config = config;
    private const int OTP_LENGTH = 6;
    private const int OTP_EXPIRY_MINUTES = 5;
    private const int MAX_ATTEMPTS = 3;

    public async Task<string?> SendOtpAsync(string email, long tenantId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        // Get or create user
        var user = await conn.QueryFirstOrDefaultAsync<User>(
            @"SELECT * FROM users WHERE email = @Email AND tenant_id = @TenantId",
            new { Email = email, TenantId = tenantId });

        if (user == null)
        {
            return null;
        }

        // Generate OTP
        var otp = Random.Shared.Next(100000, 999999).ToString();

        // Store OTP with expiry
        await conn.ExecuteAsync(
            @"INSERT INTO otp_attempts (user_id, otp, attempt_count, expires_at, is_used, created_at)
              VALUES (@UserId, @Otp, 0, @ExpiresAt, false, NOW())",
            new
            {
                UserId = user.Id,
                Otp = otp,
                ExpiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRY_MINUTES)
            });

        return otp;
    }

    public async Task<string?> VerifyOtpAsync(string email, string otp, long tenantId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        var user = await conn.QueryFirstOrDefaultAsync<User>(
            @"SELECT u.* FROM users u
              WHERE u.email = @Email AND u.tenant_id = @TenantId",
            new { Email = email, TenantId = tenantId });

        if (user == null)
        {
            return null;
        }

        // Check if user is approved
        if (user.Status != "approved")
        {
            throw new InvalidOperationException($"User account is {user.Status}. Please wait for admin approval.");
        }

        // Get latest OTP attempt
        var otpAttempt = await conn.QueryFirstOrDefaultAsync<OtpAttempt>(
            @"SELECT * FROM otp_attempts
              WHERE user_id = @UserId AND otp = @Otp
              ORDER BY created_at DESC LIMIT 1",
            new { UserId = user.Id, Otp = otp });

        if (otpAttempt == null)
        {
            // Record failed attempt
            var lastAttempt = await conn.QueryFirstOrDefaultAsync<OtpAttempt>(
                @"SELECT * FROM otp_attempts WHERE user_id = @UserId
                  ORDER BY created_at DESC LIMIT 1",
                new { UserId = user.Id });

            if (lastAttempt != null && lastAttempt.AttemptCount >= MAX_ATTEMPTS)
            {
                return null;
            }

            return null;
        }

        // Check expiry
        if (DateTime.UtcNow > otpAttempt.ExpiresAt)
        {
            return null;
        }

        // Check already used
        if (otpAttempt.IsUsed)
        {
            return null;
        }

        // Mark as used
        await conn.ExecuteAsync(
            @"UPDATE otp_attempts SET is_used = true WHERE id = @Id",
            new { Id = otpAttempt.Id });

        // Generate JWT
        var token = GenerateJwt(user);
        return token;
    }

    private string GenerateJwt(User user)
    {
        var jwtSecret = _config["Jwt:Secret"] ?? "default-secret";
        var jwtIssuer = _config["Jwt:Issuer"] ?? "cabbook";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", user.Id.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("email", user.Email),
            new Claim("role", user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: null,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
