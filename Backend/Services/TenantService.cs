using Dapper;
using Npgsql;

namespace CabBook.Services;

public class TenantService(NpgsqlDataSource dataSource)
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    public async Task<Tenant?> GetAsync(long tenantId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<Tenant>(
            @"SELECT * FROM tenants WHERE id = @Id",
            new { Id = tenantId });
    }

    public async Task<object?> GetConfigAsync(long tenantId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        var tenant = await GetAsync(tenantId);
        if (tenant == null)
            return null;

        var shiftSlots = await conn.QueryAsync<ShiftSlot>(
            @"SELECT id, name, start_time, end_time FROM shift_slots
              WHERE tenant_id = @TenantId AND is_active = true
              ORDER BY start_time",
            new { TenantId = tenantId });

        var locations = await conn.QueryAsync<Location>(
            @"SELECT id, name FROM locations
              WHERE tenant_id = @TenantId AND is_active = true
              ORDER BY name",
            new { TenantId = tenantId });

        return new
        {
            tenant.Id,
            tenant.Name,
            tenant.TimeZone,
            ShiftSlots = shiftSlots.ToList(),
            Locations = locations.ToList()
        };
    }

    public async Task<Tenant?> UpdateAsync(long tenantId, string? name, string? timeZone)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        await conn.ExecuteAsync(
            @"UPDATE tenants
              SET name = COALESCE(@Name, name),
                  time_zone = COALESCE(@TimeZone, time_zone),
                  updated_at = NOW()
              WHERE id = @Id",
            new { Id = tenantId, Name = name, TimeZone = timeZone });

        return await GetAsync(tenantId);
    }
}
