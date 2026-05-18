# Corrected Booking Logic

## Overview
Booking validation with configurable windows, timezone support, state machine progression, and audit trails.

---

## Booking Rules

### Rule 1: User Must Be Approved
```
IF User.IsApproved = FALSE
  REJECT with "User approval pending"
```

### Rule 2: Configurable Booking Window
**Improvement:** Replaces hardcoded "next day only"

```
MinAdvanceDays = BookingConfig.MinAdvanceDays (default 0)
MaxAdvanceDays = BookingConfig.MaxAdvanceDays (default 7)

AllowedDate must be in range:
  Today + MinAdvanceDays ≤ AllowedDate ≤ Today + MaxAdvanceDays

Example: Min=0, Max=7
  Monday → Can book Mon-Sun
  Friday → Can book Fri-Thu (next week)
  Friday (7 PM slot) → Might be locked if freeze already passed
```

### Rule 3: Service Must Be Open
**Improvement:** New feature to handle holidays

```
IF ServiceCalendar has entry for BookingDate with Type ≠ 'Open'
  REJECT with "Service unavailable on this date"
```

### Rule 4: Freeze Time Not Reached
**Improvement:** Per-slot freeze with per-date overrides

```
NOW_TIME = Current time in Tenant.TimeZone (NOT server UTC)

FreezeTime = ShiftSlot.FreezeTime
IF ShiftFreezeOverride exists for (ShiftSlot, BookingDate)
  FreezeTime = ShiftFreezeOverride.CustomFreezeTime

IF NOW_TIME ≥ FreezeTime
  REJECT with "Bookings for this date are frozen"
```

### Rule 5: One Booking Per User Per Date Per Slot
```
EXISTING = SELECT * FROM Booking WHERE
  UserId = @UserId
  AND BookingDate = @BookingDate
  AND ShiftSlotId = @ShiftSlotId
  AND Status IN ('Draft', 'Confirmed', 'Frozen')

IF EXISTING != null
  REJECT with "You already have a booking for this slot today"
```

### Rule 6: Location Must Be Predefined
**Improvement:** No free-text locations

```
IF LocationId NOT IN (
  SELECT LocationId FROM Location 
  WHERE TenantId = @TenantId AND IsActive = TRUE
)
  REJECT with "Invalid location"
```

---

## Pseudocode: Create Booking

```pseudocode
function CreateBooking(request):
    // Input validation
    if not request.ShiftSlotId or not request.LocationId:
        return ERROR("ShiftSlotId and LocationId required")
    
    // Load dependencies
    user = LoadUser(request.UserId)
    tenant = LoadTenant(user.TenantId)
    shiftSlot = LoadShiftSlot(request.ShiftSlotId, tenant.TenantId)
    location = LoadLocation(request.LocationId, tenant.TenantId)
    bookingConfig = LoadBookingConfig(tenant.TenantId)
    
    // Rule 1: User must be approved
    if not user.IsApproved:
        return ERROR("User approval pending")
    
    // Rule 2: Configurable booking window
    today = TODAY_IN_TIMEZONE(tenant.TimeZone)
    minDate = today + bookingConfig.MinAdvanceDays
    maxDate = today + bookingConfig.MaxAdvanceDays
    
    if request.BookingDate < minDate or request.BookingDate > maxDate:
        return ERROR(f"Booking must be between {minDate} and {maxDate}")
    
    // Rule 3: Service open on this date
    serviceStatus = LoadServiceCalendar(tenant.TenantId, request.BookingDate)
    if serviceStatus.Type != 'Open':
        return ERROR(f"Service unavailable: {serviceStatus.Description}")
    
    // Rule 4: Freeze time not reached
    currentTime = NOW_IN_TIMEZONE(tenant.TimeZone)
    freezeTime = shiftSlot.FreezeTime
    
    override = LoadShiftFreezeOverride(
        shiftSlot.ShiftSlotId, 
        request.BookingDate
    )
    if override exists:
        freezeTime = override.CustomFreezeTime
    
    if currentTime >= freezeTime:
        return ERROR("Bookings for this date are frozen")
    
    // Rule 5: No duplicate bookings
    existing = FindBooking(
        user.UserId,
        request.BookingDate,
        shiftSlot.ShiftSlotId
    )
    if existing exists and existing.Status in ['Draft', 'Confirmed', 'Frozen']:
        return ERROR("You already have a booking for this slot today")
    
    // Rule 6: Valid location
    if not location or not location.IsActive:
        return ERROR("Invalid location")
    
    // All rules passed - create booking
    booking = new Booking {
        BookingId: GenerateId(),
        TenantId: tenant.TenantId,
        UserId: user.UserId,
        ShiftSlotId: shiftSlot.ShiftSlotId,
        LocationId: location.LocationId,
        BookingDate: request.BookingDate,
        Status: 'Draft',
        CreatedAt: NOW_UTC()
    }
    
    SaveBooking(booking)
    
    // Create audit entry
    CreateAudit(
        bookingId: booking.BookingId,
        action: 'Created',
        changedBy: user.UserId,
        changedAt: NOW_UTC(),
        newValue: booking as JSON
    )
    
    // Send notification
    SendNotification(
        userId: user.UserId,
        type: 'BookingCreated',
        message: f"Booking confirmed for {shiftSlot.SlotName} on {booking.BookingDate}"
    )
    
    return SUCCESS(booking)
```

---

## Edit Before Freeze

Users can modify location, shift, or cancel **before freeze time**.

```pseudocode
function UpdateBooking(bookingId, updates, userId):
    booking = LoadBooking(bookingId)
    user = LoadUser(userId)
    tenant = LoadTenant(user.TenantId)
    shiftSlot = LoadShiftSlot(booking.ShiftSlotId)
    
    // Verify permission
    if booking.UserId != userId and user.Role != 'Admin':
        return ERROR("Unauthorized")
    
    // Check freeze time
    currentTime = NOW_IN_TIMEZONE(tenant.TimeZone)
    freezeTime = shiftSlot.FreezeTime
    override = LoadShiftFreezeOverride(shiftSlot.ShiftSlotId, booking.BookingDate)
    if override:
        freezeTime = override.CustomFreezeTime
    
    if currentTime >= freezeTime:
        return ERROR("Cannot modify: Booking is frozen")
    
    // If changing location
    if updates.LocationId != null and updates.LocationId != booking.LocationId:
        newLocation = LoadLocation(updates.LocationId, tenant.TenantId)
        if not newLocation:
            return ERROR("Invalid location")
        
        LogAudit(
            bookingId: booking.BookingId,
            action: 'Updated',
            fieldName: 'LocationId',
            oldValue: booking.LocationId,
            newValue: updates.LocationId,
            changedBy: userId
        )
        booking.LocationId = updates.LocationId
    
    // If changing shift
    if updates.ShiftSlotId != null and updates.ShiftSlotId != booking.ShiftSlotId:
        newSlot = LoadShiftSlot(updates.ShiftSlotId, tenant.TenantId)
        if not newSlot:
            return ERROR("Invalid shift slot")
        
        // Check rules again with new slot
        newFreezeTime = newSlot.FreezeTime
        if currentTime >= newFreezeTime:
            return ERROR("Cannot switch to this slot: Already frozen")
        
        LogAudit(
            bookingId: booking.BookingId,
            action: 'Updated',
            fieldName: 'ShiftSlotId',
            oldValue: booking.ShiftSlotId,
            newValue: updates.ShiftSlotId,
            changedBy: userId
        )
        booking.ShiftSlotId = updates.ShiftSlotId
    
    booking.UpdatedAt = NOW_UTC()
    SaveBooking(booking)
    
    SendNotification(
        userId: booking.UserId,
        type: 'BookingConfirmed',
        message: "Your booking has been updated"
    )
    
    return SUCCESS(booking)
```

---

## Cancellation

Users can cancel **before freeze**. After freeze, admin can still manage.

```pseudocode
function CancelBooking(bookingId, userId, reason):
    booking = LoadBooking(bookingId)
    user = LoadUser(userId)
    tenant = LoadTenant(user.TenantId)
    shiftSlot = LoadShiftSlot(booking.ShiftSlotId)
    
    // Verify permission
    if booking.UserId != userId and user.Role != 'Admin':
        return ERROR("Unauthorized")
    
    // Check if already frozen (unless admin)
    if user.Role != 'Admin':
        currentTime = NOW_IN_TIMEZONE(tenant.TimeZone)
        freezeTime = shiftSlot.FreezeTime
        override = LoadShiftFreezeOverride(shiftSlot.ShiftSlotId, booking.BookingDate)
        if override:
            freezeTime = override.CustomFreezeTime
        
        if currentTime >= freezeTime:
            return ERROR("Cannot cancel: Booking is frozen")
    
    // Mark as cancelled
    booking.Status = 'Cancelled'
    booking.CancelledAt = NOW_UTC()
    booking.CancelledBy = userId
    SaveBooking(booking)
    
    // Audit trail
    LogAudit(
        bookingId: booking.BookingId,
        action: 'Cancelled',
        changedBy: userId,
        newValue: reason
    )
    
    // Notify user
    SendNotification(
        userId: booking.UserId,
        type: 'BookingCancelled',
        message: "Your booking has been cancelled"
    )
    
    return SUCCESS("Booking cancelled")
```

---

## Edge Cases

### Case 1: Booking Window at Boundary
```
Today is Monday, Min=0, Max=7
User books Thursday (4 days ahead) ✓ Allowed
User books Monday (same day) ✓ Allowed if MinAdvanceDays=0
User books next Monday (8 days ahead) ✗ Beyond Max
```

### Case 2: Timezone Boundary
```
Tenant: IST (UTC+5:30)
Freeze time for slot: 20:00:00 (8 PM)

Server time: 2024-01-15 14:35:00 UTC
Tenant time: 2024-01-15 20:05:00 IST
→ Booking LOCKED (freeze passed)

Even though UTC time < freeze in UTC, tenant time = freeze.
Important: Compare times in tenant timezone, not UTC!
```

### Case 3: Midnight Crossing (Booking for Tomorrow)
```
Tenant: IST (UTC+5:30)
Tenant current time: 2024-01-15 23:50:00 IST
Tomorrow in IST: 2024-01-16

User tries to book for 2024-01-16
→ Valid if TODAY+MaxAdvanceDays ≥ 2024-01-16
→ Must use Tenant.TimeZone to calculate "today"
```

### Case 4: Service Closed Holiday
```
ServiceCalendar has:
  2024-01-26 (Republic Day) → Type='Holiday', Desc='Public Holiday'

User tries to book for 2024-01-26
→ REJECT "Service unavailable: Public Holiday"
```

### Case 5: Concurrent Bookings
```
User1 and User2 both submit booking for same slot simultaneously
→ Database UNIQUE constraint (TenantId, UserId, BookingDate, ShiftSlotId)
  ensures only one succeeds
→ Other gets "duplicate booking" error
```

### Case 6: Admin Override
```
Admin can:
  1. Create booking for any user after freeze
  2. Edit/cancel frozen bookings
  3. Force approve user without approval rules

Business logic: Admin role bypasses user-level freeze checks
```

---

## State Transitions

```
┌─────────────────────────────────────────────────────┐
│  Booking Lifecycle                                  │
│                                                     │
│  Draft                                              │
│    ├─→ (edit allowed)  Update                        │
│    ├─→ (edit allowed)  Cancel → Cancelled           │
│    └─→ (auto at freeze) Confirm → Frozen            │
│                                                     │
│  Frozen (read-only for users)                       │
│    ├─→ (admin only) Reject → Rejected               │
│    └─→ (exported) RosterSnapshot created            │
│                                                     │
│  Cancelled / Rejected                               │
│    └─→ (admin can) Restore → Draft                  │
└─────────────────────────────────────────────────────┘
```

---

## Validation Summary

| Rule | Check | Error |
|------|-------|-------|
| User approved | `User.IsApproved = true` | "User approval pending" |
| Booking window | `Today + Min ≤ Date ≤ Today + Max` | "Date out of range" |
| Service open | `ServiceCalendar.Type = 'Open'` | "Service unavailable" |
| Not frozen | `CurrentTime < FreezeTime` | "Bookings are frozen" |
| No duplicate | No existing Draft/Confirmed/Frozen | "Already booked" |
| Valid location | `Location exists & IsActive` | "Invalid location" |
| Valid shift | `ShiftSlot exists & IsActive` | "Invalid shift" |

---

## C# Service Implementation

```csharp
public class BookingService
{
    private readonly IBookingRepository _bookingRepo;
    private readonly IUserRepository _userRepo;
    private readonly IShiftSlotRepository _shiftRepo;
    private readonly ILocationRepository _locationRepo;
    private readonly IServiceCalendarRepository _calendarRepo;
    private readonly IBookingConfigRepository _configRepo;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;
    private readonly ITimeZoneProvider _timeZoneProvider;

    public async Task<BookingResult> CreateBookingAsync(
        CreateBookingRequest request, 
        Guid userId, 
        CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user == null)
            return BookingResult.Failure("User not found");

        if (!user.IsApproved)
            return BookingResult.Failure("User approval pending");

        var tenant = user.Tenant;
        var shiftSlot = await _shiftRepo.GetByIdAsync(
            request.ShiftSlotId, 
            tenant.TenantId, 
            ct);
        
        if (shiftSlot == null)
            return BookingResult.Failure("Invalid shift slot");

        var location = await _locationRepo.GetByIdAsync(
            request.LocationId, 
            tenant.TenantId, 
            ct);
        
        if (location == null || !location.IsActive)
            return BookingResult.Failure("Invalid location");

        // Validate booking window
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone);
        var now = _timeZoneProvider.GetNowInTimeZone(tz);
        var today = now.Date;
        
        var config = await _configRepo.GetByTenantAsync(tenant.TenantId, ct);
        var minDate = today.AddDays(config.MinAdvanceDays);
        var maxDate = today.AddDays(config.MaxAdvanceDays);

        if (request.BookingDate < minDate || request.BookingDate > maxDate)
            return BookingResult.Failure(
                $"Booking must be between {minDate:yyyy-MM-dd} and {maxDate:yyyy-MM-dd}");

        // Check service availability
        var serviceStatus = await _calendarRepo.GetStatusAsync(
            tenant.TenantId, 
            request.BookingDate, 
            ct);
        
        if (serviceStatus?.Type != ServiceCalendarType.Open)
            return BookingResult.Failure(
                $"Service unavailable: {serviceStatus?.Description}");

        // Check freeze time
        var freezeTime = shiftSlot.FreezeTime;
        var freezeOverride = await _shiftRepo.GetFreezeOverrideAsync(
            shiftSlot.ShiftSlotId, 
            request.BookingDate, 
            ct);
        
        if (freezeOverride != null)
            freezeTime = freezeOverride.CustomFreezeTime;

        var currentTime = now.TimeOfDay;
        if (currentTime >= freezeTime)
            return BookingResult.Failure("Bookings for this date are frozen");

        // Check duplicate
        var existing = await _bookingRepo.FindExistingAsync(
            userId, 
            request.BookingDate, 
            request.ShiftSlotId, 
            tenant.TenantId, 
            ct);
        
        if (existing != null)
            return BookingResult.Failure("You already have a booking for this slot");

        // Create booking
        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            UserId = userId,
            ShiftSlotId = request.ShiftSlotId,
            LocationId = request.LocationId,
            BookingDate = request.BookingDate,
            Status = BookingStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _bookingRepo.AddAsync(booking, ct);

        // Audit
        await _auditService.LogAsync(
            booking.BookingId,
            "Created",
            changedBy: userId,
            newValue: booking);

        // Notify
        await _notificationService.SendAsync(
            userId,
            NotificationType.BookingCreated,
            $"Booking confirmed for {shiftSlot.SlotName}");

        return BookingResult.Success(booking);
    }
}
```

---

## Testing Checklist

- [ ] Booking allowed within min/max window
- [ ] Booking rejected outside window
- [ ] Booking rejected after freeze time
- [ ] Booking rejected on closed service day
- [ ] Duplicate booking rejected
- [ ] Edit allowed before freeze
- [ ] Edit blocked after freeze (user)
- [ ] Admin can edit after freeze
- [ ] Timezone conversions correct
- [ ] Audit trail logged
- [ ] Notification sent
- [ ] Concurrent bookings handled (unique constraint)
- [ ] OTP rate limiting applied
- [ ] Tenant isolation verified
