# Corrected Freeze & Roster Management

## Overview
State machine for roster progression (Open → Frozen → Exported) with immutable snapshots and reliable background jobs.

---

## Roster Status Machine

```
┌────────────────────────────────────────────────────────┐
│  Roster Lifecycle (Per Date, Per Tenant)              │
│                                                        │
│  1. OPEN (During day)                                 │
│     └─→ Users can book/edit/cancel                    │
│     └─→ Bookings have Status='Draft' or 'Confirmed'   │
│                                                        │
│  2. FROZEN (At freeze time)                           │
│     └─→ Background job changes Roster.Status → Frozen  │
│     └─→ All Booking.Status → 'Frozen'                 │
│     └─→ Users can no longer modify                    │
│     └─→ RosterSnapshot created (immutable)            │
│                                                        │
│  3. EXPORTED (After export)                           │
│     └─→ Admin exports roster to transport team        │
│     └─→ Roster.Status → Exported                      │
│     └─→ Admin can still view/audit                    │
│                                                        │
│  Design Principle:                                    │
│  ├─ State is explicit, not time-based                │
│  ├─ Transitions verified before operations            │
│  └─ Immutable snapshots prevent export drift          │
└────────────────────────────────────────────────────────┘
```

---

## Background Job: Automatic Freeze

Runs periodically (every 5-10 minutes) to check and apply freeze at the right time.

### Pseudocode: FreezeRosterJob

```pseudocode
function FreezeRosterJob():
    // Run every 5 minutes
    
    // Get all active tenants
    tenants = GetAllActiveTenants()
    
    for each tenant in tenants:
        // Get all shift slots for this tenant
        shiftSlots = GetShiftSlots(tenant.TenantId)
        
        for each shiftSlot in shiftSlots:
            // Get all dates that might need freezing
            // (today + MaxAdvanceDays ahead)
            dates = GetDateRange(today, today + 30)
            
            for each date in dates:
                // Check if roster for this date is already frozen
                roster = GetRoster(tenant.TenantId, date)
                if roster.Status == 'Frozen':
                    continue // Already frozen
                
                // Get current time in tenant timezone
                currentTime = NOW_IN_TIMEZONE(tenant.TimeZone)
                currentDate = TODAY_IN_TIMEZONE(tenant.TimeZone)
                
                // Get freeze time (with per-date override)
                freezeTime = shiftSlot.FreezeTime
                override = GetShiftFreezeOverride(shiftSlot.ShiftSlotId, date)
                if override:
                    freezeTime = override.CustomFreezeTime
                
                // Only check for future dates or today
                if date < currentDate:
                    continue
                
                // Check if freeze time has been reached
                if date > currentDate:
                    // For future dates, freeze at 00:00 next day? No.
                    // Freeze only when date is today
                    continue
                
                // Today's date - check if freeze time reached
                if currentTime >= freezeTime:
                    FreezeRoster(tenant.TenantId, shiftSlot.ShiftSlotId, date)
```

### Detailed: FreezeRoster Function

```pseudocode
function FreezeRoster(tenantId, shiftSlotId, date):
    
    LOG("Freezing roster", tenant=tenantId, slot=shiftSlotId, date=date)
    
    BEGIN_TRANSACTION:
        // Get or create roster for this date
        roster = GetRosterLocked(tenantId, date)
        
        if roster.Status == 'Frozen' or roster.Status == 'Exported':
            return // Already done
        
        // Get all bookings for this date and shift
        bookings = GetBookings(
            tenantId, 
            shiftSlotId, 
            date,
            status IN ['Draft', 'Confirmed']
        )
        
        // Update all booking statuses to Frozen
        for each booking in bookings:
            booking.Status = 'Frozen'
            booking.UpdatedAt = NOW_UTC()
            SaveBooking(booking)
            
            // Log audit
            LogAudit(
                bookingId: booking.BookingId,
                action: 'Frozen',
                fieldName: 'Status',
                oldValue: booking.Status,
                newValue: 'Frozen',
                changedBy: SYSTEM
            )
        
        // Create immutable snapshot
        for each booking in bookings:
            if booking.Status in ['Confirmed', 'Frozen']:
                snapshot = new RosterSnapshot {
                    SnapshotId: GenerateId(),
                    RosterId: roster.RosterId,
                    BookingId: booking.BookingId,
                    UserId: booking.UserId,
                    UserName: booking.User.FullName,
                    Mobile: booking.User.Mobile,
                    LocationName: booking.Location.Name,
                    SlotName: booking.ShiftSlot.SlotName,
                    CreatedAt: NOW_UTC()
                }
                SaveSnapshot(snapshot)
        
        // Update roster status
        roster.Status = 'Frozen'
        roster.FrozenAt = NOW_UTC()
        roster.FrozenBy = SYSTEM_USER
        SaveRoster(roster)
        
        LOG("Roster frozen", rosterId=roster.RosterId)
        
        // Send notifications to users
        for each booking in bookings:
            SendNotification(
                userId: booking.UserId,
                type: 'FreezeHappened',
                message: "Booking is now locked. View final roster at {export_link}"
            )
    
    COMMIT_TRANSACTION
```

---

## Freeze Reminder Job

Sends notifications 1 hour before freeze.

```pseudocode
function FreezeReminderJob():
    // Run every 10 minutes
    
    tenants = GetAllActiveTenants()
    
    for each tenant in tenants:
        config = GetBookingConfig(tenant.TenantId)
        reminderMinutes = config.ReminderMinutesBefore // default 60
        
        if not config.SendFreezeReminder:
            continue
        
        shiftSlots = GetShiftSlots(tenant.TenantId)
        
        for each shiftSlot in shiftSlots:
            currentTime = NOW_IN_TIMEZONE(tenant.TimeZone)
            today = TODAY_IN_TIMEZONE(tenant.TimeZone)
            
            freezeTime = shiftSlot.FreezeTime
            override = GetShiftFreezeOverride(shiftSlot.ShiftSlotId, today)
            if override:
                freezeTime = override.CustomFreezeTime
            
            reminderTime = freezeTime - reminderMinutes
            
            // Check if we're in the reminder window
            if currentTime >= reminderTime and currentTime < freezeTime:
                // Get users with active bookings for today
                bookings = GetBookings(
                    tenant.TenantId,
                    shiftSlot.ShiftSlotId,
                    today,
                    status IN ['Draft', 'Confirmed']
                )
                
                // Track who we've already notified today
                for each booking in bookings:
                    if not HasNotificationToday(
                        booking.UserId,
                        'FreezeReminder',
                        shiftSlot.ShiftSlotId
                    ):
                        SendNotification(
                            userId: booking.UserId,
                            type: 'FreezeReminder',
                            message: f"⏰ Freeze in {reminderMinutes} min for {shiftSlot.SlotName}. Lock in your booking now!",
                            deliveryChannel: 'SMS'
                        )
```

---

## Export Logic

### Step 1: Create Export
Admin initiates export, which uses RosterSnapshot.

```pseudocode
function ExportRoster(tenantId, date, templateId, exportedBy):
    
    // Get roster - must be Frozen
    roster = GetRoster(tenantId, date)
    
    if roster.Status != 'Frozen':
        return ERROR(f"Roster must be Frozen to export, currently {roster.Status}")
    
    // Get template
    template = GetExportTemplate(templateId, tenantId)
    if not template:
        return ERROR("Template not found")
    
    // Get snapshot - immutable source of truth
    snapshots = GetRosterSnapshot(roster.RosterId)
    
    if not snapshots:
        return ERROR("No snapshot available (roster might not be properly frozen)")
    
    // Build export based on template
    exportData = BuildExportData(snapshots, template)
    
    // Generate file (CSV or Excel)
    fileName = f"Roster_{date}_{TemplateType}_{NOW}.{template.Format}"
    filePath = SaveExportFile(fileName, exportData)
    
    // Record export
    roster.Status = 'Exported'
    roster.ExportedAt = NOW_UTC()
    roster.ExportedBy = exportedBy
    SaveRoster(roster)
    
    // Audit
    LogAudit(
        rosterId: roster.RosterId,
        action: 'Exported',
        newValue: filePath
    )
    
    return SUCCESS(filePath)
```

### Step 2: Export File Format

Using ExportTemplate configuration (pluggable).

**Template 1: BasicCSV**
```
Columns: [UserName, Mobile, LocationName, SlotName]
GroupBy: null
Result:
  Name,Mobile,Location,Slot
  Rajesh Kumar,9876543210,Powai,11 PM Drop
  Priya Singh,9876543211,Andheri,11 PM Drop
```

**Template 2: GroupedByLocation**
```
Columns: [UserName, Mobile, SlotName]
GroupBy: LocationName
SortBy: UserName
Result:
  === ANDHERI ===
  Name,Mobile,Slot
  Priya Singh,9876543211,11 PM Drop
  
  === POWAI ===
  Name,Mobile,Slot
  Rajesh Kumar,9876543210,11 PM Drop
```

**Template 3: Excel (Multi-Sheet)**
```
Sheet 1: Summary
  Total Users: 42
  Powai: 18
  Andheri: 15
  Thane: 9

Sheet 2: All Bookings
  Name,Mobile,Location,Slot

Sheet 3: By Location
  ...
```

---

## Reliability Features

### Feature 1: Idempotent Freezing
If job runs multiple times for same date/slot, no side effects.

```csharp
// In FreezeRoster function - check state first
if (roster.Status == 'Frozen' || roster.Status == 'Exported')
    return; // Already frozen, safe to exit
```

### Feature 2: Transaction Safety
All state changes in single transaction.

```csharp
using (var transaction = db.BeginTransaction())
{
    try
    {
        // Update all bookings
        // Create snapshots
        // Update roster status
        transaction.Commit();
    }
    catch (Exception ex)
    {
        transaction.Rollback();
        LogError("Freeze failed", ex);
        AlertAdmin("Freeze job failed - manual intervention needed");
    }
}
```

### Feature 3: Job Monitoring
Track job execution.

```sql
CREATE TABLE BackgroundJobLog (
    JobLogId UUID PRIMARY KEY,
    JobName VARCHAR(100),
    TenantId UUID,
    ExecutedDate DATE,
    StartedAt TIMESTAMP,
    CompletedAt TIMESTAMP,
    Status VARCHAR(50), -- Success, Failed, Partial
    ErrorMessage TEXT,
    ExecutedBy VARCHAR(100),
    
    CONSTRAINT job_log_unique UNIQUE(JobName, TenantId, ExecutedDate)
);
```

### Feature 4: Timezone Correctness
Always use tenant timezone for time comparisons.

```csharp
private DateTime GetNowInTenantTimeZone(Tenant tenant)
{
    var tz = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone);
    return TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
}

// In freeze check:
var nowInTenant = GetNowInTenantTimeZone(tenant);
var freezeTime = shiftSlot.FreezeTime;
if (nowInTenant.TimeOfDay >= freezeTime)
    // Freeze passed
```

### Feature 5: Admin Override
Admin can manually trigger freeze without waiting for job.

```csharp
[Authorize(Roles = "Admin")]
[HttpPost("api/admin/freeze/{date}")]
public async Task<IActionResult> ManualFreeze(DateTime date)
{
    var rosterId = await _rosterService.FreezeRosterAsync(
        tenantId: CurrentTenant.TenantId,
        date: date,
        triggeredBy: CurrentUser.UserId);
    
    return Ok(new { message = "Frozen", rosterId });
}
```

---

## C# Implementation

```csharp
public class RosterService
{
    private readonly IRosterRepository _rosterRepo;
    private readonly IBookingRepository _bookingRepo;
    private readonly IShiftSlotRepository _shiftRepo;
    private readonly IRosterSnapshotRepository _snapshotRepo;
    private readonly IBookingAuditRepository _auditRepo;
    private readonly INotificationService _notificationService;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly ILogger<RosterService> _logger;

    public async Task FreezeRosterAsync(
        Guid tenantId,
        DateTime rosterDate,
        Guid triggeredBy,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Starting freeze for tenant {TenantId} on {Date}",
            tenantId, rosterDate);

        using (var transaction = await _rosterRepo.BeginTransactionAsync(ct))
        {
            try
            {
                // Get or create roster
                var roster = await _rosterRepo.GetOrCreateAsync(
                    tenantId, rosterDate, ct);

                if (roster.Status == RosterStatus.Frozen || 
                    roster.Status == RosterStatus.Exported)
                {
                    _logger.LogWarning("Roster already frozen");
                    return;
                }

                // Get all active bookings for this date
                var bookings = await _bookingRepo.GetByDateAsync(
                    tenantId, rosterDate,
                    status: new[] { BookingStatus.Draft, BookingStatus.Confirmed },
                    ct);

                // Update booking statuses
                foreach (var booking in bookings)
                {
                    booking.Status = BookingStatus.Frozen;
                    booking.UpdatedAt = DateTime.UtcNow;
                    
                    await _bookingRepo.UpdateAsync(booking, ct);

                    // Log audit
                    await _auditRepo.LogAsync(
                        new BookingAudit
                        {
                            BookingId = booking.BookingId,
                            Action = "Frozen",
                            FieldName = "Status",
                            OldValue = BookingStatus.Confirmed.ToString(),
                            NewValue = BookingStatus.Frozen.ToString(),
                            ChangedBy = triggeredBy,
                            ChangedAt = DateTime.UtcNow
                        },
                        ct);
                }

                // Create immutable snapshots
                foreach (var booking in bookings.Where(b => 
                    b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Frozen))
                {
                    var snapshot = new RosterSnapshot
                    {
                        SnapshotId = Guid.NewGuid(),
                        RosterId = roster.RosterId,
                        BookingId = booking.BookingId,
                        UserId = booking.UserId,
                        UserName = booking.User.FullName,
                        Mobile = booking.User.Mobile,
                        LocationName = booking.Location.Name,
                        SlotName = booking.ShiftSlot.SlotName,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _snapshotRepo.AddAsync(snapshot, ct);
                }

                // Update roster status
                roster.Status = RosterStatus.Frozen;
                roster.FrozenAt = DateTime.UtcNow;
                roster.FrozenBy = triggeredBy;

                await _rosterRepo.UpdateAsync(roster, ct);

                // Send notifications
                foreach (var booking in bookings)
                {
                    await _notificationService.SendAsync(
                        booking.UserId,
                        NotificationType.FreezeHappened,
                        "Your booking is now locked",
                        ct);
                }

                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Successfully froze roster {RosterId}",
                    roster.RosterId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Freeze failed for {TenantId}", tenantId);
                throw;
            }
        }
    }

    public async Task<string> ExportRosterAsync(
        Guid tenantId,
        DateTime rosterDate,
        Guid templateId,
        Guid exportedBy,
        CancellationToken ct)
    {
        var roster = await _rosterRepo.GetByDateAsync(tenantId, rosterDate, ct);

        if (roster?.Status != RosterStatus.Frozen)
            throw new InvalidOperationException(
                $"Roster must be Frozen, currently {roster?.Status}");

        var template = await _exportTemplateRepo.GetByIdAsync(templateId, ct);
        if (template == null)
            throw new ArgumentException("Template not found");

        // Get immutable snapshot
        var snapshots = await _snapshotRepo.GetByRosterAsync(roster.RosterId, ct);

        // Build file based on template
        var exporter = new RosterExporter(template);
        var fileContent = exporter.Export(snapshots);

        var fileName = $"Roster_{rosterDate:yyyy-MM-dd}_{template.Name}.{template.Format}";
        var filePath = await _storageService.SaveAsync(fileName, fileContent, ct);

        // Record export
        roster.Status = RosterStatus.Exported;
        roster.ExportedAt = DateTime.UtcNow;
        roster.ExportedBy = exportedBy;

        await _rosterRepo.UpdateAsync(roster, ct);

        return filePath;
    }
}

// Hangfire job configuration
public class RosterBackgroundJobs
{
    private readonly RosterService _rosterService;

    [Queue("default")]
    [AutomaticRetry(Attempts = 3)]
    public async Task FreezeRostersAsync(Guid tenantId)
    {
        try
        {
            await _rosterService.FreezeRosterAsync(
                tenantId,
                DateTime.Today,
                triggeredBy: Guid.Empty); // System-triggered
        }
        catch (Exception ex)
        {
            // Logged by Hangfire
            throw;
        }
    }

    [Queue("default")]
    public async Task SendFreezeRemindersAsync(Guid tenantId)
    {
        await _rosterService.SendFreezeRemindersAsync(tenantId);
    }
}

// Schedule in Startup
public void Configure(IApplicationBuilder app, IRecurringJobManager recurringJobs)
{
    // Run freeze check every 5 minutes
    recurringJobs.AddOrUpdate(
        id: "freeze-rosters",
        methodCall: () => _backgroundJobs.FreezeRostersAsync(CurrentTenant.TenantId),
        cronExpression: Cron.MinuteInterval(5));

    // Send reminders every 10 minutes
    recurringJobs.AddOrUpdate(
        id: "freeze-reminders",
        methodCall: () => _backgroundJobs.SendFreezeRemindersAsync(CurrentTenant.TenantId),
        cronExpression: Cron.MinuteInterval(10));
}
```

---

## Monitoring & Alerts

Track and alert on:

```csharp
public class RosterMonitoring
{
    // Alert if freeze job hasn't run in 30 minutes
    public async Task MonitorFreezeJobAsync()
    {
        var lastRun = await _jobLogRepo.GetLastRunAsync("FreezeRoster");
        
        if (DateTime.UtcNow - lastRun > TimeSpan.FromMinutes(30))
            AlertAdmin("Freeze job overdue - check health");
    }

    // Alert on export failures
    public async Task MonitorExportFailuresAsync()
    {
        var failures = await _jobLogRepo.GetFailuresAsync(
            since: DateTime.UtcNow.AddHours(-1));
        
        if (failures.Count > 0)
            AlertAdmin($"{failures.Count} export failures in last hour");
    }

    // Track roster completion rate
    public async Task<RosterHealthReport> GetHealthAsync()
    {
        var today = DateTime.Today;
        var frozenCount = await _rosterRepo.CountByStatusAsync(
            status: RosterStatus.Frozen,
            sinceDate: today);
        
        var totalTenants = await _tenantRepo.GetActiveCountAsync();
        
        return new RosterHealthReport
        {
            FrozenPercentage = (frozenCount / totalTenants) * 100,
            LastFreezeTime = await _rosterRepo.GetLatestFreezeTimeAsync()
        };
    }
}
```

---

## Verification Checklist

- [ ] Roster status follows state machine
- [ ] RosterSnapshot created only at freeze
- [ ] Snapshots are immutable (historical data)
- [ ] Export uses snapshot, not live data
- [ ] Freeze jobs are idempotent
- [ ] Timezone conversions correct
- [ ] Notifications sent at freeze + reminder
- [ ] Audit trail complete
- [ ] Admin can manually trigger freeze
- [ ] Job monitoring working
- [ ] Transaction safety verified
- [ ] Concurrent exports handled
