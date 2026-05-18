using Dapper;
using Npgsql;
using TimeZoneConverter;

namespace CabBook.Services;

public class BookingService(NpgsqlDataSource dataSource)
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    public async Task<Booking?> CreateBookingAsync(long tenantId, long userId, long shiftSlotId, long locationId, DateTime bookingDate)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        // Get tenant for timezone
        var tenant = await conn.QueryFirstOrDefaultAsync<Tenant>(
            @"SELECT * FROM tenants WHERE id = @TenantId",
            new { TenantId = tenantId });

        if (tenant == null)
            return null;

        // Get shift slot config
        var shiftSlot = await conn.QueryFirstOrDefaultAsync<ShiftSlot>(
            @"SELECT * FROM shift_slots WHERE id = @ShiftSlotId AND tenant_id = @TenantId",
            new { ShiftSlotId = shiftSlotId, TenantId = tenantId });

        if (shiftSlot == null)
            return null;

        // Validate booking window
        if (!ValidateBookingWindow(bookingDate, shiftSlot, tenant))
            throw new InvalidOperationException("Booking date outside allowed window");

        // Check if roster is frozen
        var roster = await conn.QueryFirstOrDefaultAsync<Roster>(
            @"SELECT * FROM rosters
              WHERE tenant_id = @TenantId AND shift_slot_id = @ShiftSlotId
              AND roster_date = @BookingDate",
            new { TenantId = tenantId, ShiftSlotId = shiftSlotId, BookingDate = bookingDate });

        if (roster?.Status == "Frozen")
            throw new InvalidOperationException("Roster is frozen for this date");

        // Check for duplicate booking
        var exists = await conn.QueryFirstOrDefaultAsync<Booking>(
            @"SELECT * FROM bookings
              WHERE tenant_id = @TenantId AND user_id = @UserId
              AND shift_slot_id = @ShiftSlotId AND booking_date = @BookingDate
              AND status != 'Cancelled'",
            new { TenantId = tenantId, UserId = userId, ShiftSlotId = shiftSlotId, BookingDate = bookingDate });

        if (exists != null)
            throw new InvalidOperationException("Duplicate booking for this date and shift");

        // Create booking
        var booking = new Booking
        {
            TenantId = tenantId,
            UserId = userId,
            ShiftSlotId = shiftSlotId,
            LocationId = locationId,
            BookingDate = bookingDate,
            Status = "Confirmed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var id = await conn.ExecuteScalarAsync<long>(
            @"INSERT INTO bookings (tenant_id, user_id, shift_slot_id, location_id, booking_date, status, created_at, updated_at)
              VALUES (@TenantId, @UserId, @ShiftSlotId, @LocationId, @BookingDate, @Status, @CreatedAt, @UpdatedAt)
              RETURNING id",
            booking);

        booking.Id = id;

        // Audit log
        await LogAuditAsync(tenantId, booking.Id, "CREATE", "", booking.Status, userId, conn);

        return booking;
    }

    public async Task<Booking?> GetBookingAsync(long tenantId, long bookingId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<Booking>(
            @"SELECT * FROM bookings WHERE id = @Id AND tenant_id = @TenantId",
            new { Id = bookingId, TenantId = tenantId });
    }

    public async Task<List<Booking>> ListBookingsAsync(long tenantId, long userId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        var bookings = await conn.QueryAsync<Booking>(
            @"SELECT * FROM bookings
              WHERE tenant_id = @TenantId AND user_id = @UserId
              ORDER BY booking_date DESC",
            new { TenantId = tenantId, UserId = userId });
        return bookings.ToList();
    }

    public async Task<bool> CancelBookingAsync(long tenantId, long bookingId, long userId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        var booking = await conn.QueryFirstOrDefaultAsync<Booking>(
            @"SELECT * FROM bookings WHERE id = @Id AND tenant_id = @TenantId",
            new { Id = bookingId, TenantId = tenantId });

        if (booking == null)
            return false;

        var oldStatus = booking.Status;
        booking.Status = "Cancelled";
        booking.UpdatedAt = DateTime.UtcNow;

        await conn.ExecuteAsync(
            @"UPDATE bookings SET status = @Status, updated_at = @UpdatedAt
              WHERE id = @Id",
            booking);

        await LogAuditAsync(tenantId, bookingId, "CANCEL", oldStatus, booking.Status, userId, conn);
        return true;
    }

    private bool ValidateBookingWindow(DateTime bookingDate, ShiftSlot shiftSlot, Tenant tenant)
    {
        // Convert to tenant timezone
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone);
        var tenantNow = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
        var daysAhead = (bookingDate.Date - tenantNow.Date).Days;

        // Check min advance days
        if (daysAhead < shiftSlot.FreezeAdvanceDays)
            return false;

        // Check max advance days (30 days default)
        if (daysAhead > 30)
            return false;

        // Check holiday
        using var conn = _dataSource.OpenConnection();
        var isHoliday = conn.QueryFirstOrDefault<bool>(
            @"SELECT EXISTS(SELECT 1 FROM service_calendars
              WHERE tenant_id = @TenantId AND date = @Date AND type = 'Holiday')",
            new { TenantId = tenant.Id, Date = bookingDate.Date });

        if (isHoliday)
            return false;

        return true;
    }

    private async Task LogAuditAsync(long tenantId, long bookingId, string action,
        string oldValue, string newValue, long changedBy, NpgsqlConnection conn)
    {
        await conn.ExecuteAsync(
            @"INSERT INTO booking_audits (tenant_id, booking_id, action, old_value, new_value, changed_by, created_at)
              VALUES (@TenantId, @BookingId, @Action, @OldValue, @NewValue, @ChangedBy, NOW())",
            new { TenantId = tenantId, BookingId = bookingId, Action = action,
                  OldValue = oldValue, NewValue = newValue, ChangedBy = changedBy });
    }
}
