using Npgsql;

namespace CabBook.Services;

public class DatabaseInitializer(NpgsqlDataSource dataSource, ILogger<DatabaseInitializer> logger)
{
    private readonly NpgsqlDataSource _dataSource = dataSource;
    private readonly ILogger<DatabaseInitializer> _logger = logger;

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing database schema...");

            using var conn = await _dataSource.OpenConnectionAsync();

            // Read schema from embedded resource or create tables directly
            var schema = GetDatabaseSchema();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = schema;
            await cmd.ExecuteNonQueryAsync();

            _logger.LogInformation("Database schema initialized successfully");

            // Seed test data
            await SeedTestDataAsync(conn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database schema");
            throw;
        }
    }

    private async Task SeedTestDataAsync(NpgsqlConnection conn)
    {
        try
        {
            // Check if test data already exists
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM tenants";
            var count = (long?)await checkCmd.ExecuteScalarAsync() ?? 0;

            if (count > 0)
            {
                _logger.LogInformation("Test data already exists, skipping seed");
                return;
            }

            _logger.LogInformation("Seeding test data...");

            // Insert test tenant
            using var tenantCmd = conn.CreateCommand();
            tenantCmd.CommandText = @"
                INSERT INTO tenants (name, time_zone, is_active, created_at, updated_at)
                VALUES ('Test Company', 'UTC', true, NOW(), NOW())
                RETURNING id";
            var tenantId = (long?)await tenantCmd.ExecuteScalarAsync() ?? 1;

            // Insert test user
            using var userCmd = conn.CreateCommand();
            userCmd.CommandText = @"
                INSERT INTO users (tenant_id, email, phone_number, name, pickup_address, dropoff_address, address_status, role, status, is_active, created_at, updated_at)
                VALUES (@TenantId, 'test@example.com', '1234567890', 'Test User', '123 Main Street', '456 Oak Avenue', 'approved', 'Employee', 'approved', true, NOW(), NOW())";
            userCmd.Parameters.AddWithValue("@TenantId", tenantId);
            await userCmd.ExecuteNonQueryAsync();

            // Insert test shift slot
            using var shiftCmd = conn.CreateCommand();
            shiftCmd.CommandText = @"
                INSERT INTO shift_slots (tenant_id, name, start_time, end_time, freeze_time, freeze_advance_days, is_active, created_at, updated_at)
                VALUES (@TenantId, 'Morning', '06:00:00', '14:00:00', '23:00:00', 1, true, NOW(), NOW())";
            shiftCmd.Parameters.AddWithValue("@TenantId", tenantId);
            await shiftCmd.ExecuteNonQueryAsync();

            // Insert test location
            using var locCmd = conn.CreateCommand();
            locCmd.CommandText = @"
                INSERT INTO locations (tenant_id, name, address, is_active, created_at, updated_at)
                VALUES (@TenantId, 'Office A', '123 Main Street', true, NOW(), NOW())";
            locCmd.Parameters.AddWithValue("@TenantId", tenantId);
            await locCmd.ExecuteNonQueryAsync();

            _logger.LogInformation("Test data seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to seed test data (may already exist)");
        }
    }

    private static string GetDatabaseSchema()
    {
        return @"
-- CabBook Database Schema
CREATE SCHEMA IF NOT EXISTS public;

-- Tenants (company accounts)
CREATE TABLE IF NOT EXISTS tenants (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    time_zone VARCHAR(50) DEFAULT 'UTC',
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Users
CREATE TABLE IF NOT EXISTS users (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    email VARCHAR(255) NOT NULL,
    phone_number VARCHAR(20),
    name VARCHAR(255),
    pickup_address VARCHAR(500),
    dropoff_address VARCHAR(500),
    address_status VARCHAR(50) DEFAULT 'pending',
    role VARCHAR(50) DEFAULT 'Employee',
    status VARCHAR(50) DEFAULT 'pending',
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, email)
);

CREATE INDEX IF NOT EXISTS idx_users_tenant ON users(tenant_id);
CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);
CREATE INDEX IF NOT EXISTS idx_users_status ON users(status);

-- Shift Slots (configurable shift times)
CREATE TABLE IF NOT EXISTS shift_slots (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    start_time TIME NOT NULL,
    end_time TIME NOT NULL,
    freeze_time TIME,
    freeze_advance_days INT DEFAULT 1,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, name)
);

CREATE INDEX IF NOT EXISTS idx_shift_slots_tenant ON shift_slots(tenant_id);

-- Locations (predefined locations)
CREATE TABLE IF NOT EXISTS locations (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    address VARCHAR(500),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, name)
);

CREATE INDEX IF NOT EXISTS idx_locations_tenant ON locations(tenant_id);

-- Rosters (state machine: Open -> Frozen -> Exported)
CREATE TABLE IF NOT EXISTS rosters (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    shift_slot_id BIGINT NOT NULL REFERENCES shift_slots(id),
    roster_date DATE NOT NULL,
    status VARCHAR(50) DEFAULT 'Open',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, shift_slot_id, roster_date)
);

CREATE INDEX IF NOT EXISTS idx_rosters_tenant ON rosters(tenant_id);
CREATE INDEX IF NOT EXISTS idx_rosters_status ON rosters(status);
CREATE INDEX IF NOT EXISTS idx_rosters_date ON rosters(roster_date);

-- Bookings
CREATE TABLE IF NOT EXISTS bookings (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    user_id BIGINT NOT NULL REFERENCES users(id),
    shift_slot_id BIGINT NOT NULL REFERENCES shift_slots(id),
    location_id BIGINT NOT NULL REFERENCES locations(id),
    booking_date DATE NOT NULL,
    status VARCHAR(50) DEFAULT 'Confirmed',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_bookings_tenant ON bookings(tenant_id);
CREATE INDEX IF NOT EXISTS idx_bookings_user ON bookings(user_id);
CREATE INDEX IF NOT EXISTS idx_bookings_date ON bookings(booking_date);
CREATE INDEX IF NOT EXISTS idx_bookings_status ON bookings(status);

-- Booking Audit Trail
CREATE TABLE IF NOT EXISTS booking_audits (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    booking_id BIGINT NOT NULL REFERENCES bookings(id),
    action VARCHAR(50) NOT NULL,
    old_value TEXT,
    new_value TEXT,
    changed_by BIGINT NOT NULL REFERENCES users(id),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_booking_audits_booking ON booking_audits(booking_id);
CREATE INDEX IF NOT EXISTS idx_booking_audits_tenant ON booking_audits(tenant_id);

-- OTP Attempts (for rate limiting)
CREATE TABLE IF NOT EXISTS otp_attempts (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users(id),
    otp VARCHAR(6) NOT NULL,
    attempt_count INT DEFAULT 0,
    expires_at TIMESTAMP NOT NULL,
    is_used BOOLEAN DEFAULT false,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_otp_attempts_user ON otp_attempts(user_id);
CREATE INDEX IF NOT EXISTS idx_otp_attempts_expires ON otp_attempts(expires_at);

-- Roster Snapshots (immutable frozen state)
CREATE TABLE IF NOT EXISTS roster_snapshots (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    roster_id BIGINT NOT NULL REFERENCES rosters(id),
    snapshot_date TIMESTAMP NOT NULL,
    csv_data BYTEA NOT NULL,
    content_type VARCHAR(50) DEFAULT 'text/csv',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_roster_snapshots_roster ON roster_snapshots(roster_id);
CREATE INDEX IF NOT EXISTS idx_roster_snapshots_tenant ON roster_snapshots(tenant_id);

-- Shift Freeze Overrides (per-date freeze configuration)
CREATE TABLE IF NOT EXISTS shift_freeze_overrides (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    shift_slot_id BIGINT NOT NULL REFERENCES shift_slots(id),
    override_date DATE NOT NULL,
    freeze_time TIME,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, shift_slot_id, override_date)
);

CREATE INDEX IF NOT EXISTS idx_freeze_overrides_tenant ON shift_freeze_overrides(tenant_id);

-- Service Calendar (holidays, maintenance days)
CREATE TABLE IF NOT EXISTS service_calendars (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    date DATE NOT NULL,
    type VARCHAR(50) DEFAULT 'Holiday',
    description VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, date)
);

CREATE INDEX IF NOT EXISTS idx_service_calendars_tenant ON service_calendars(tenant_id);
CREATE INDEX IF NOT EXISTS idx_service_calendars_date ON service_calendars(date);
";
    }
}
