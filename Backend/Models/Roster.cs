namespace CabBook.Models;

public class Roster
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long ShiftSlotId { get; set; }
    public DateTime RosterDate { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RosterSnapshot
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long RosterId { get; set; }
    public DateTime SnapshotDate { get; set; }
    public byte[] CsvData { get; set; } = null!;
    public string ContentType { get; set; } = "text/csv";
    public DateTime CreatedAt { get; set; }
}

public class ShiftFreezeOverride
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long ShiftSlotId { get; set; }
    public DateTime OverrideDate { get; set; }
    public TimeSpan? FreezeTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ServiceCalendar
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public DateTime Date { get; set; }
    public string Type { get; set; } = "Holiday";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
