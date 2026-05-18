using Dapper;
using Npgsql;

namespace CabBook.Services;

public class ShiftSlotService(NpgsqlDataSource dataSource)
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    public async Task<ShiftSlot?> CreateAsync(long tenantId, string name, TimeSpan startTime, TimeSpan endTime, TimeSpan? freezeTime)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        var id = await conn.ExecuteScalarAsync<long>(
            @"INSERT INTO shift_slots (tenant_id, name, start_time, end_time, freeze_time, is_active, created_at, updated_at)
              VALUES (@TenantId, @Name, @StartTime, @EndTime, @FreezeTime, true, NOW(), NOW())
              RETURNING id",
            new { TenantId = tenantId, Name = name, StartTime = startTime, EndTime = endTime, FreezeTime = freezeTime });

        return await GetAsync(tenantId, id);
    }

    public async Task<ShiftSlot?> GetAsync(long tenantId, long id)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<ShiftSlot>(
            @"SELECT * FROM shift_slots WHERE id = @Id AND tenant_id = @TenantId",
            new { Id = id, TenantId = tenantId });
    }

    public async Task<List<ShiftSlot>> ListAsync(long tenantId, bool activeOnly = true)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        var sql = @"SELECT * FROM shift_slots WHERE tenant_id = @TenantId";

        if (activeOnly)
            sql += " AND is_active = true";

        sql += " ORDER BY start_time ASC";

        var slots = await conn.QueryAsync<ShiftSlot>(sql, new { TenantId = tenantId });
        return slots.ToList();
    }

    public async Task<ShiftSlot?> UpdateAsync(long tenantId, long id, string? name, TimeSpan? startTime, TimeSpan? endTime, TimeSpan? freezeTime)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        var existing = await GetAsync(tenantId, id);
        if (existing == null)
            return null;

        await conn.ExecuteAsync(
            @"UPDATE shift_slots
              SET name = COALESCE(@Name, name),
                  start_time = COALESCE(@StartTime, start_time),
                  end_time = COALESCE(@EndTime, end_time),
                  freeze_time = @FreezeTime,
                  updated_at = NOW()
              WHERE id = @Id AND tenant_id = @TenantId",
            new
            {
                Id = id,
                TenantId = tenantId,
                Name = name,
                StartTime = startTime,
                EndTime = endTime,
                FreezeTime = freezeTime
            });

        return await GetAsync(tenantId, id);
    }

    public async Task<bool> DeleteAsync(long tenantId, long id)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        var result = await conn.ExecuteAsync(
            @"UPDATE shift_slots SET is_active = false, updated_at = NOW()
              WHERE id = @Id AND tenant_id = @TenantId",
            new { Id = id, TenantId = tenantId });

        return result > 0;
    }

    public async Task<bool> SetFreezeOverrideAsync(long tenantId, long shiftSlotId, DateTime overrideDate, TimeSpan? freezeTime)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        await conn.ExecuteAsync(
            @"INSERT INTO shift_freeze_overrides (tenant_id, shift_slot_id, override_date, freeze_time, created_at, updated_at)
              VALUES (@TenantId, @ShiftSlotId, @OverrideDate, @FreezeTime, NOW(), NOW())
              ON CONFLICT (tenant_id, shift_slot_id, override_date)
              DO UPDATE SET freeze_time = @FreezeTime, updated_at = NOW()",
            new
            {
                TenantId = tenantId,
                ShiftSlotId = shiftSlotId,
                OverrideDate = overrideDate.Date,
                FreezeTime = freezeTime
            });

        return true;
    }
}
