namespace CabBook.Models;

public class User
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public string Email { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string? Name { get; set; }
    public string Role { get; set; } = "Employee";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class OtpAttempt
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Otp { get; set; } = null!;
    public int AttemptCount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}
