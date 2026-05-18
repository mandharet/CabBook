# CabBook Backend - .NET 10 with Docker

Production-ready backend for cab booking system with:
- **Auth:** OTP-based login with JWT tokens
- **Booking:** Multi-tenant booking with validation
- **Roster:** State machine with automatic freezing
- **Export:** Immutable CSV snapshots

## Quick Start

### Prerequisites
- .NET 10 SDK
- Docker & Docker Compose
- PostgreSQL 16 (or use docker-compose)

### Local Development

1. **Start database:**
   ```bash
   docker-compose up db
   ```

2. **Run migrations:**
   ```bash
   dotnet build
   dotnet run
   ```

3. **Test endpoints:**
   ```bash
   curl -X POST http://localhost:8080/api/auth/send-otp \
     -H "Content-Type: application/json" \
     -d '{"email":"user@example.com","tenantId":1}'
   ```

### Docker

**Build & run together:**
```bash
docker-compose up --build
```

**Access:**
- API: http://localhost:8080
- Database: localhost:5432 (postgres/postgres)

## Project Structure

```
Backend/
├── Models/              # Data models (Tenant, User, Booking, etc.)
├── Services/            # Business logic (Auth, Booking, Roster)
├── Controllers/         # REST API endpoints
├── Program.cs           # Entry point & DI setup
├── appsettings.json     # Configuration
└── CabBook.csproj       # Dependencies (Npgsql, JWT, Dapper)
```

## API Endpoints

### Authentication
- `POST /api/auth/send-otp` - Send OTP to email
- `POST /api/auth/verify-otp` - Verify OTP and get JWT token

### Bookings
- `POST /api/booking` - Create booking
- `GET /api/booking` - List user bookings
- `GET /api/booking/{id}` - Get booking details
- `POST /api/booking/{id}/cancel` - Cancel booking

### Rosters (Admin)
- `GET /api/roster` - List rosters
- `POST /api/roster/{id}/freeze` - Freeze roster
- `GET /api/roster/{id}/export` - Download frozen roster CSV

## Configuration

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=cabbook;..."
  },
  "Jwt": {
    "Secret": "your-super-secret-key",
    "Issuer": "cabbook"
  }
}
```

**Environment Variables (Docker):**
- `ConnectionStrings__DefaultConnection` - Database connection
- `Jwt__Secret` - JWT signing key
- `Jwt__Issuer` - JWT issuer claim

## Database

Schema includes:
- **tenants** - Company accounts
- **users** - Employees with OTP tracking
- **shift_slots** - Configurable shift times
- **locations** - Predefined locations
- **bookings** - Booking records with status
- **rosters** - Shift rosters (Open → Frozen → Exported)
- **booking_audits** - Change history
- **otp_attempts** - Rate limiting
- **roster_snapshots** - Immutable frozen state
- **service_calendars** - Holidays/maintenance

## Testing

Run tests:
```bash
dotnet test
```

## Deployment

See [06-GETTING_STARTED.md](/md/06-GETTING_STARTED.md) for Render deployment.

## Key Features

✅ Multi-tenant isolation (TenantId enforcement)
✅ OTP-based auth with 5-min expiry
✅ Booking validation (window, frozen check, duplicates)
✅ State machine roster (reliable freeze)
✅ Immutable snapshots (export consistency)
✅ Full audit trail (all changes tracked)
✅ Per-slot configuration (no hardcoding)
✅ Timezone support (convert at display)
