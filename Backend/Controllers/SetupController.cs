using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace CabBook.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SetupController(
    UserService userService,
    ShiftSlotService shiftSlotService,
    LocationService locationService,
    IConfiguration configuration) : ControllerBase
{
    private readonly UserService _userService = userService;
    private readonly ShiftSlotService _shiftSlotService = shiftSlotService;
    private readonly LocationService _locationService = locationService;
    private readonly IConfiguration _configuration = configuration;

    /// <summary>
    /// Initialize a new tenant with admin account and default configuration.
    /// Call this ONCE when setting up a new client.
    /// </summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> InitializeTenant([FromBody] TenantSetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantName))
            return BadRequest(new { error = "Tenant name is required" });

        if (string.IsNullOrWhiteSpace(request.AdminEmail))
            return BadRequest(new { error = "Admin email is required" });

        if (string.IsNullOrWhiteSpace(request.AdminName))
            return BadRequest(new { error = "Admin name is required" });

        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found in configuration");

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            // Check if tenant with this name already exists
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT id FROM tenants WHERE name = @Name LIMIT 1";
            checkCmd.Parameters.AddWithValue("@Name", request.TenantName);

            var existingTenant = await checkCmd.ExecuteScalarAsync();
            if (existingTenant != null)
                return BadRequest(new { error = "Tenant with this name already exists" });

            // Create tenant
            using var tenantCmd = conn.CreateCommand();
            tenantCmd.CommandText = @"
                INSERT INTO tenants (name, time_zone, is_active, created_at, updated_at)
                VALUES (@Name, @TimeZone, true, NOW(), NOW())
                RETURNING id";
            tenantCmd.Parameters.AddWithValue("@Name", request.TenantName);
            tenantCmd.Parameters.AddWithValue("@TimeZone", request.TimeZone ?? "UTC");

            var tenantId = (long?)await tenantCmd.ExecuteScalarAsync();
            if (tenantId == null)
                throw new InvalidOperationException("Failed to create tenant");

            // Create admin user (pre-approved)
            using var adminCmd = conn.CreateCommand();
            adminCmd.CommandText = @"
                INSERT INTO users (tenant_id, email, phone_number, name, pickup_address, dropoff_address, address_status, role, status, is_active, created_at, updated_at)
                VALUES (@TenantId, @Email, @Phone, @Name, @Pickup, @Dropoff, 'approved', 'Admin', 'approved', true, NOW(), NOW())
                RETURNING id, email, name";
            adminCmd.Parameters.AddWithValue("@TenantId", tenantId.Value);
            adminCmd.Parameters.AddWithValue("@Email", request.AdminEmail);
            adminCmd.Parameters.AddWithValue("@Phone", request.AdminPhone ?? (object)DBNull.Value);
            adminCmd.Parameters.AddWithValue("@Name", request.AdminName);
            adminCmd.Parameters.AddWithValue("@Pickup", request.AdminPickupAddress ?? "Admin Pickup Location");
            adminCmd.Parameters.AddWithValue("@Dropoff", request.AdminDropoffAddress ?? "Admin Dropoff Location");

            long adminUserId = 0;
            using (var reader = await adminCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    adminUserId = reader.GetInt64(0);
                }
            }

            // Create default shift slot (Morning: 6 AM - 2 PM, freeze at 11 PM)
            using var shiftCmd = conn.CreateCommand();
            shiftCmd.CommandText = @"
                INSERT INTO shift_slots (tenant_id, name, start_time, end_time, freeze_time, freeze_advance_days, is_active, created_at, updated_at)
                VALUES (@TenantId, @Name, @StartTime, @EndTime, @FreezeTime, @FreezeAdvanceDays, true, NOW(), NOW())";
            shiftCmd.Parameters.AddWithValue("@TenantId", tenantId.Value);
            shiftCmd.Parameters.AddWithValue("@Name", "Morning");
            shiftCmd.Parameters.AddWithValue("@StartTime", "06:00:00");
            shiftCmd.Parameters.AddWithValue("@EndTime", "14:00:00");
            shiftCmd.Parameters.AddWithValue("@FreezeTime", "23:00:00");
            shiftCmd.Parameters.AddWithValue("@FreezeAdvanceDays", 1);
            await shiftCmd.ExecuteNonQueryAsync();

            // Create default location
            using var locCmd = conn.CreateCommand();
            locCmd.CommandText = @"
                INSERT INTO locations (tenant_id, name, address, is_active, created_at, updated_at)
                VALUES (@TenantId, @Name, @Address, true, NOW(), NOW())";
            locCmd.Parameters.AddWithValue("@TenantId", tenantId.Value);
            locCmd.Parameters.AddWithValue("@Name", "Main Office");
            locCmd.Parameters.AddWithValue("@Address", request.CompanyAddress ?? "123 Business Street");
            await locCmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                message = "Tenant initialized successfully",
                tenant = new
                {
                    id = tenantId,
                    name = request.TenantName,
                    time_zone = request.TimeZone ?? "UTC"
                },
                admin = new
                {
                    id = adminUserId,
                    email = request.AdminEmail,
                    name = request.AdminName,
                    role = "Admin",
                    status = "Approved",
                    note = "Admin can login immediately with email OTP"
                },
                default_config = new
                {
                    shift_slots = new[] { "Morning (6 AM - 2 PM)" },
                    locations = new[] { "Main Office" }
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Health check - verify setup endpoint is available
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new { status = "ready", message = "Setup endpoint is available" });
    }
}

public class TenantSetupRequest
{
    public string TenantName { get; set; } = null!;
    public string AdminEmail { get; set; } = null!;
    public string AdminName { get; set; } = null!;
    public string? AdminPhone { get; set; }
    public string? AdminPickupAddress { get; set; }
    public string? AdminDropoffAddress { get; set; }
    public string? CompanyAddress { get; set; }
    public string? TimeZone { get; set; } = "UTC";
}
