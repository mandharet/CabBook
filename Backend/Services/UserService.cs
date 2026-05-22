using Npgsql;

namespace CabBook.Services;

public class UserService(NpgsqlDataSource dataSource, IEmailService emailService)
{
    private readonly NpgsqlDataSource _dataSource = dataSource;
    private readonly IEmailService _emailService = emailService;

    public async Task<dynamic> SignupAsync(string email, string? phoneNumber, string? name, string? pickupAddress, string? dropoffAddress, long tenantId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO users (tenant_id, email, phone_number, name, pickup_address, dropoff_address, address_status, role, status, is_active, created_at, updated_at)
            VALUES (@TenantId, @Email, @PhoneNumber, @Name, @PickupAddress, @DropoffAddress, 'pending', 'Employee', 'pending', true, NOW(), NOW())
            RETURNING id, email, status, address_status, created_at";

        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        cmd.Parameters.AddWithValue("@Email", email);
        cmd.Parameters.AddWithValue("@PhoneNumber", phoneNumber ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Name", name ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PickupAddress", pickupAddress ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@DropoffAddress", dropoffAddress ?? (object)DBNull.Value);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new
            {
                id = reader.GetInt64(0),
                email = reader.GetString(1),
                status = reader.GetString(2),
                address_status = reader.GetString(3),
                created_at = reader.GetDateTime(4)
            };
        }

        throw new InvalidOperationException("Failed to create user signup");
    }

    public async Task<List<dynamic>> GetPendingUsersAsync(long tenantId)
    {
        var users = new List<dynamic>();

        using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT id, email, name, phone_number, pickup_address, dropoff_address, status, address_status, created_at
            FROM users
            WHERE tenant_id = @TenantId AND status = 'pending'
            ORDER BY created_at DESC";

        cmd.Parameters.AddWithValue("@TenantId", tenantId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new
            {
                id = reader.GetInt64(0),
                email = reader.GetString(1),
                name = reader.IsDBNull(2) ? null : reader.GetString(2),
                phone_number = reader.IsDBNull(3) ? null : reader.GetString(3),
                pickup_address = reader.IsDBNull(4) ? null : reader.GetString(4),
                dropoff_address = reader.IsDBNull(5) ? null : reader.GetString(5),
                status = reader.GetString(6),
                address_status = reader.GetString(7),
                created_at = reader.GetDateTime(8)
            });
        }

        return users;
    }

    public async Task ApproveUserAsync(long userId, long tenantId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        // Get user details before updating
        using var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT email, name FROM users WHERE id = @UserId AND tenant_id = @TenantId";
        selectCmd.Parameters.AddWithValue("@UserId", userId);
        selectCmd.Parameters.AddWithValue("@TenantId", tenantId);

        string? email = null;
        string? name = null;
        using (var reader = await selectCmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                email = reader.GetString(0);
                name = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
        }

        // Update user status
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE users
            SET status = 'approved', updated_at = NOW()
            WHERE id = @UserId AND tenant_id = @TenantId";

        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);

        await cmd.ExecuteNonQueryAsync();

        // Send approval email
        if (email != null)
        {
            await _emailService.SendUserApprovedEmailAsync(email, name ?? email);
        }
    }

    public async Task RejectUserAsync(long userId, long tenantId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        // Get user details before updating
        using var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT email FROM users WHERE id = @UserId AND tenant_id = @TenantId";
        selectCmd.Parameters.AddWithValue("@UserId", userId);
        selectCmd.Parameters.AddWithValue("@TenantId", tenantId);

        string? email = null;
        using (var reader = await selectCmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                email = reader.GetString(0);
            }
        }

        // Update user status
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE users
            SET status = 'rejected', updated_at = NOW()
            WHERE id = @UserId AND tenant_id = @TenantId";

        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);

        await cmd.ExecuteNonQueryAsync();

        // Send rejection email
        if (email != null)
        {
            await _emailService.SendUserRejectedEmailAsync(email, "Your signup request was not approved at this time.");
        }
    }

    public async Task<dynamic?> GetUserAsync(long userId, long tenantId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT id, email, name, phone_number, pickup_address, dropoff_address, role, status, address_status, is_active, created_at
            FROM users
            WHERE id = @UserId AND tenant_id = @TenantId";

        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new
            {
                id = reader.GetInt64(0),
                email = reader.GetString(1),
                name = reader.IsDBNull(2) ? null : reader.GetString(2),
                phone_number = reader.IsDBNull(3) ? null : reader.GetString(3),
                pickup_address = reader.IsDBNull(4) ? null : reader.GetString(4),
                dropoff_address = reader.IsDBNull(5) ? null : reader.GetString(5),
                role = reader.GetString(6),
                status = reader.GetString(7),
                address_status = reader.GetString(8),
                is_active = reader.GetBoolean(9),
                created_at = reader.GetDateTime(10)
            };
        }

        return null;
    }

    public async Task ApproveAddressesAsync(long userId, long tenantId)
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        // Get user details before updating
        using var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT email, name FROM users WHERE id = @UserId AND tenant_id = @TenantId";
        selectCmd.Parameters.AddWithValue("@UserId", userId);
        selectCmd.Parameters.AddWithValue("@TenantId", tenantId);

        string? email = null;
        string? name = null;
        using (var reader = await selectCmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                email = reader.GetString(0);
                name = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
        }

        // Update address status
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE users
            SET address_status = 'approved', updated_at = NOW()
            WHERE id = @UserId AND tenant_id = @TenantId";

        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);

        await cmd.ExecuteNonQueryAsync();

        // Send address approval email
        if (email != null)
        {
            await _emailService.SendAddressApprovedEmailAsync(email, name ?? email);
        }
    }

    public async Task UpdateAddressesAsync(long userId, long tenantId, string? pickupAddress, string? dropoffAddress)
    {
        using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            UPDATE users
            SET pickup_address = @PickupAddress, dropoff_address = @DropoffAddress, address_status = 'pending', updated_at = NOW()
            WHERE id = @UserId AND tenant_id = @TenantId";

        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        cmd.Parameters.AddWithValue("@PickupAddress", pickupAddress ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@DropoffAddress", dropoffAddress ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }
}
