namespace CabBook.Models;

public class Tenant
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string TimeZone { get; set; } = "UTC";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
