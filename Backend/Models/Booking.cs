namespace CabBook.Models;

public class Booking
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public long ShiftSlotId { get; set; }
    public long LocationId { get; set; }
    public DateTime BookingDate { get; set; }
    public string Status { get; set; } = "Confirmed";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BookingAudit
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long BookingId { get; set; }
    public string Action { get; set; } = null!;
    public string OldValue { get; set; } = null!;
    public string NewValue { get; set; } = null!;
    public long ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ShiftSlot
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public string Name { get; set; } = null!;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public TimeSpan? FreezeTime { get; set; }
    public int FreezeAdvanceDays { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Location
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
