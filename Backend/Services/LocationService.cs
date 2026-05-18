using Dapper;
using Npgsql;

namespace CabBook.Services;

public class LocationService(NpgsqlDataSource dataSource)
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    public async Task<Location?> CreateAsync(long tenantId, string name, string? address = null)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        var id = await conn.ExecuteScalarAsync<long>(
            @"INSERT INTO locations (tenant_id, name, address, is_active, created_at, updated_at)
              VALUES (@TenantId, @Name, @Address, true, NOW(), NOW())
              RETURNING id",
            new { TenantId = tenantId, Name = name, Address = address });

        return await GetAsync(tenantId, id);
    }

    public async Task<Location?> GetAsync(long tenantId, long id)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<Location>(
            @"SELECT * FROM locations WHERE id = @Id AND tenant_id = @TenantId",
            new { Id = id, TenantId = tenantId });
    }

    public async Task<List<Location>> ListAsync(long tenantId, bool activeOnly = true)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        var sql = @"SELECT * FROM locations WHERE tenant_id = @TenantId";

        if (activeOnly)
            sql += " AND is_active = true";

        sql += " ORDER BY name ASC";

        var locations = await conn.QueryAsync<Location>(sql, new { TenantId = tenantId });
        return locations.ToList();
    }

    public async Task<Location?> UpdateAsync(long tenantId, long id, string? name, string? address)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        var existing = await GetAsync(tenantId, id);
        if (existing == null)
            return null;

        await conn.ExecuteAsync(
            @"UPDATE locations
              SET name = COALESCE(@Name, name),
                  address = COALESCE(@Address, address),
                  updated_at = NOW()
              WHERE id = @Id AND tenant_id = @TenantId",
            new { Id = id, TenantId = tenantId, Name = name, Address = address });

        return await GetAsync(tenantId, id);
    }

    public async Task<bool> DeleteAsync(long tenantId, long id)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        var result = await conn.ExecuteAsync(
            @"UPDATE locations SET is_active = false, updated_at = NOW()
              WHERE id = @Id AND tenant_id = @TenantId",
            new { Id = id, TenantId = tenantId });

        return result > 0;
    }
}
