using Dapper;
using Npgsql;
using System.Text;

namespace CabBook.Services;

public class RosterService(NpgsqlDataSource dataSource)
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    public async Task<Roster?> GetRosterAsync(long tenantId, long rosterId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<Roster>(
            @"SELECT * FROM rosters WHERE id = @Id AND tenant_id = @TenantId",
            new { Id = rosterId, TenantId = tenantId });
    }

    public async Task<List<Roster>> ListRostersAsync(long tenantId, DateTime fromDate, DateTime toDate)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        var rosters = await conn.QueryAsync<Roster>(
            @"SELECT * FROM rosters
              WHERE tenant_id = @TenantId
              AND roster_date >= @FromDate AND roster_date <= @ToDate
              ORDER BY roster_date DESC",
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate });
        return rosters.ToList();
    }

    public async Task<bool> FreezeRosterAsync(long tenantId, long rosterId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            var roster = await conn.QueryFirstOrDefaultAsync<Roster>(
                @"SELECT * FROM rosters WHERE id = @Id AND tenant_id = @TenantId FOR UPDATE",
                new { Id = rosterId, TenantId = tenantId },
                transaction: transaction);

            if (roster == null)
                return false;

            if (roster.Status != "Open")
                return false;

            // Update roster status
            await conn.ExecuteAsync(
                @"UPDATE rosters SET status = @Status, updated_at = NOW()
                  WHERE id = @Id",
                new { Status = "Frozen", Id = rosterId },
                transaction: transaction);

            // Create immutable snapshot
            var csv = await GenerateRosterCsvAsync(tenantId, rosterId, conn, transaction);
            await conn.ExecuteAsync(
                @"INSERT INTO roster_snapshots (tenant_id, roster_id, snapshot_date, csv_data, content_type, created_at)
                  VALUES (@TenantId, @RosterId, NOW(), @CsvData, 'text/csv', NOW())",
                new
                {
                    TenantId = tenantId,
                    RosterId = rosterId,
                    CsvData = csv
                },
                transaction: transaction);

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<byte[]?> ExportRosterAsync(long tenantId, long rosterId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        var snapshot = await conn.QueryFirstOrDefaultAsync<RosterSnapshot>(
            @"SELECT * FROM roster_snapshots
              WHERE tenant_id = @TenantId AND roster_id = @RosterId
              ORDER BY created_at DESC LIMIT 1",
            new { TenantId = tenantId, RosterId = rosterId });

        return snapshot?.CsvData;
    }

    public async Task<bool> CheckAndFreezeExpiredRostersAsync(long tenantId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        // Get all active rosters
        var rosters = await conn.QueryAsync<Roster>(
            @"SELECT * FROM rosters WHERE tenant_id = @TenantId AND status = 'Open'
              ORDER BY roster_date ASC",
            new { TenantId = tenantId });

        var tenant = await conn.QueryFirstOrDefaultAsync<Tenant>(
            @"SELECT * FROM tenants WHERE id = @TenantId",
            new { TenantId = tenantId });

        if (tenant == null)
            return false;

        var tz = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone);
        var tenantNow = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);

        foreach (var roster in rosters)
        {
            var shiftSlot = await conn.QueryFirstOrDefaultAsync<ShiftSlot>(
                @"SELECT * FROM shift_slots WHERE id = @ShiftSlotId",
                new { ShiftSlotId = roster.ShiftSlotId });

            if (shiftSlot == null)
                continue;

            // Check for per-date override
            TimeSpan? freezeTime = null;
            var overrideExist = await conn.QueryFirstOrDefaultAsync<ShiftFreezeOverride>(
                @"SELECT * FROM shift_freeze_overrides
                  WHERE tenant_id = @TenantId AND shift_slot_id = @ShiftSlotId
                  AND override_date = @RosterDate",
                new
                {
                    TenantId = tenantId,
                    ShiftSlotId = shiftSlot.Id,
                    RosterDate = roster.RosterDate.Date
                });

            freezeTime = overrideExist?.FreezeTime ?? shiftSlot.FreezeTime;

            if (freezeTime == null)
                continue;

            var rosterDateTime = roster.RosterDate.Date.Add(freezeTime.Value);
            if (tenantNow > rosterDateTime)
            {
                await FreezeRosterAsync(tenantId, roster.Id);
            }
        }

        return true;
    }

    private async Task<byte[]> GenerateRosterCsvAsync(long tenantId, long rosterId, NpgsqlConnection conn, NpgsqlTransaction transaction)
    {
        var bookings = await conn.QueryAsync(
            @"SELECT b.*, u.email, u.name, s.name as shift_name, l.name as location_name
              FROM bookings b
              JOIN users u ON b.user_id = u.id
              JOIN shift_slots s ON b.shift_slot_id = s.id
              JOIN locations l ON b.location_id = l.id
              WHERE b.tenant_id = @TenantId AND EXISTS(
                SELECT 1 FROM rosters WHERE id = @RosterId AND shift_slot_id = b.shift_slot_id
              )
              ORDER BY b.booking_date, u.email",
            new { TenantId = tenantId, RosterId = rosterId },
            transaction: transaction);

        var csv = new StringBuilder();
        csv.AppendLine("Date,Shift,Employee Email,Employee Name,Location");

        foreach (var booking in bookings)
        {
            csv.AppendLine($"{booking.booking_date:yyyy-MM-dd},{booking.shift_name},{booking.email},{booking.name},{booking.location_name}");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }
}
