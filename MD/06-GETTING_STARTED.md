# Getting Started: Lightweight CabBook

Step-by-step guide to build and deploy the system with minimal dependencies.

---

## Prerequisites

- .NET 8 SDK
- PostgreSQL 14+ (or SQLite for development)
- Docker (optional, for deployment)
- Git
- Text editor (VS Code, VS, or any)

---

## Part 1: Project Setup (15 minutes)

### Step 1: Create Project Structure

```bash
mkdir CabBook
cd CabBook

# Backend
dotnet new webapi -n Backend -f net8.0
cd Backend

# Clean up unnecessary files
rm -r WeatherForecast.cs
rm -r Controllers/WeatherForecastController.cs

# Add to .gitignore
echo "bin/
obj/
.vs/
.vscode/
*.db
appsettings.local.json
.env" >> .gitignore

cd ..
```

### Step 2: Set Up Database

**Option A: PostgreSQL (Production)**

```bash
# Docker
docker run --name cabbook-db \
  -e POSTGRES_PASSWORD=cabbook \
  -e POSTGRES_DB=cabbook \
  -p 5432:5432 \
  -d postgres:16-alpine

# Connection string
PostgreSQL://postgres:cabbook@localhost:5432/cabbook
```

**Option B: SQLite (Development)**

```bash
# No setup needed - SQLite embedded
# Connection string
Data Source=cabbook.db
```

### Step 3: Add Dependencies

```bash
cd Backend

dotnet add package Npgsql                           # PostgreSQL driver
dotnet add package System.Data.SqlClient             # SQL Server (optional)
dotnet add package System.IdentityModel.Tokens.Jwt  # JWT auth
dotnet add package Serilog                          # Optional logging
dotnet add package Serilog.Sinks.Console            # Optional console sink

# That's it! Only 4-5 packages total
```

---

## Part 2: Create Core Models (30 minutes)

### Models/User.cs

```csharp
namespace CabBook.Models;

public class User
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Mobile { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public bool IsApproved { get; set; }
    public DateTime ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Role { get; set; } = "Employee";
}
```

### Models/Booking.cs

```csharp
namespace CabBook.Models;

public class Booking
{
    public Guid BookingId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid ShiftSlotId { get; set; }
    public Guid LocationId { get; set; }
    public DateTime BookingDate { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Confirmed, Frozen, Cancelled
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    // For display
    public string? LocationName { get; set; }
    public string? ShiftSlotName { get; set; }
}
```

### Models/ShiftSlot.cs

```csharp
namespace CabBook.Models;

public class ShiftSlot
{
    public Guid ShiftSlotId { get; set; }
    public Guid TenantId { get; set; }
    public string SlotName { get; set; } // "11 PM Drop"
    public TimeSpan StartTime { get; set; }
    public TimeSpan FreezeTime { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
```

### Models/Location.cs

```csharp
namespace CabBook.Models;

public class Location
{
    public Guid LocationId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
```

---

## Part 3: Database Schema (30 minutes)

### database/schema.sql

```sql
-- Tenant
CREATE TABLE IF NOT EXISTS Tenant (
    TenantId UUID PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    TimeZone VARCHAR(50) DEFAULT 'UTC',
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP DEFAULT NOW()
);

CREATE UNIQUE INDEX idx_tenant_name ON Tenant(Name);

-- User
CREATE TABLE IF NOT EXISTS "User" (
    UserId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    Mobile VARCHAR(20) NOT NULL,
    Email VARCHAR(255),
    FullName VARCHAR(255) NOT NULL,
    Role VARCHAR(50) DEFAULT 'Employee',
    IsApproved BOOLEAN DEFAULT FALSE,
    ApprovedAt TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT NOW(),
    UpdatedAt TIMESTAMP
);

CREATE UNIQUE INDEX idx_user_mobile_tenant ON "User"(TenantId, Mobile);
CREATE INDEX idx_user_approved ON "User"(TenantId, IsApproved);

-- ShiftSlot
CREATE TABLE IF NOT EXISTS ShiftSlot (
    ShiftSlotId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    SlotName VARCHAR(100) NOT NULL,
    StartTime TIME NOT NULL,
    FreezeTime TIME NOT NULL,
    DisplayOrder INT DEFAULT 1,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_shiftslot_tenant ON ShiftSlot(TenantId, IsActive);

-- Location
CREATE TABLE IF NOT EXISTS Location (
    LocationId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    Name VARCHAR(100) NOT NULL,
    Description VARCHAR(500),
    DisplayOrder INT DEFAULT 1,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP DEFAULT NOW()
);

CREATE UNIQUE INDEX idx_location_name_tenant ON Location(TenantId, Name);

-- Booking
CREATE TABLE IF NOT EXISTS Booking (
    BookingId UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenant(TenantId),
    UserId UUID NOT NULL REFERENCES "User"(UserId),
    ShiftSlotId UUID NOT NULL REFERENCES ShiftSlot(ShiftSlotId),
    LocationId UUID NOT NULL REFERENCES Location(LocationId),
    BookingDate DATE NOT NULL,
    Status VARCHAR(50) DEFAULT 'Draft',
    CreatedAt TIMESTAMP DEFAULT NOW(),
    UpdatedAt TIMESTAMP,
    CancelledAt TIMESTAMP,
    CancelledBy UUID
);

CREATE UNIQUE INDEX idx_booking_user_date_shift ON Booking(TenantId, UserId, BookingDate, ShiftSlotId);
CREATE INDEX idx_booking_date_status ON Booking(BookingDate, Status);

-- BookingAudit
CREATE TABLE IF NOT EXISTS BookingAudit (
    AuditId UUID PRIMARY KEY,
    BookingId UUID NOT NULL REFERENCES Booking(BookingId),
    Action VARCHAR(50) NOT NULL,
    FieldName VARCHAR(100),
    OldValue TEXT,
    NewValue TEXT,
    ChangedBy UUID NOT NULL,
    ChangedAt TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_audit_booking ON BookingAudit(BookingId);

-- OtpAttempt
CREATE TABLE IF NOT EXISTS OtpAttempt (
    OtpAttemptId UUID PRIMARY KEY,
    Mobile VARCHAR(20) NOT NULL,
    OtpCode VARCHAR(6) NOT NULL,
    AttemptCount INT DEFAULT 1,
    ExpiresAt TIMESTAMP NOT NULL,
    VerifiedAt TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_otp_mobile ON OtpAttempt(Mobile, ExpiresAt);
```

### Load Schema

```bash
# PostgreSQL
psql -h localhost -U postgres -d cabbook -f database/schema.sql

# SQLite (if using)
sqlite3 cabbook.db < database/schema.sql
```

---

## Part 4: API Setup (45 minutes)

### Program.cs (Complete)

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ============ SERVICES ============

// Controllers
builder.Services.AddControllers();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Database Connection
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    return new Npgsql.NpgsqlConnection(connectionString);
});

// JWT Authentication
var jwtSecret = builder.Configuration["JwtSecret"] ?? "your-super-secret-key-min-32-chars-1234567890";
var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

// Business Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<AdminService>();

// ============ APP SETUP ============

var app = builder.Build();

// Middleware
app.UseHttpsRedirection();
app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapControllers();
app.MapFallbackToFile("index.html");

// Error handling
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        
        var exception = context.Features.Get<IExceptionHandlerFeature>();
        if (exception?.Error != null)
        {
            await context.Response.WriteAsJsonAsync(new
            {
                error = exception.Error.Message
            });
        }
    });
});

app.Run();

public partial class Program { }
```

### Services/AuthService.cs

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CabBook.Services;

public class AuthService
{
    private readonly IDbConnection _db;
    private readonly IConfiguration _config;
    private readonly Random _random = new();

    public AuthService(IDbConnection db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // Step 1: Send OTP
    public async Task<bool> SendOtpAsync(string mobile)
    {
        // Validate mobile
        if (string.IsNullOrEmpty(mobile) || mobile.Length != 10)
            throw new ArgumentException("Invalid mobile number");

        // Generate OTP
        var otp = _random.Next(100000, 999999).ToString();

        // Store in database
        const string sql = @"
            INSERT INTO OtpAttempt (OtpAttemptId, Mobile, OtpCode, AttemptCount, ExpiresAt, CreatedAt)
            VALUES (@id, @mobile, @otp, 1, NOW() + INTERVAL '5 minutes', NOW())
            ON CONFLICT (Mobile) DO UPDATE SET
                OtpCode = @otp,
                AttemptCount = 1,
                ExpiresAt = NOW() + INTERVAL '5 minutes',
                CreatedAt = NOW();
        ";

        await _db.ExecuteAsync(sql, new
        {
            id = Guid.NewGuid(),
            mobile,
            otp
        });

        // TODO: Send SMS (Twilio, AWS SNS, etc.)
        Console.WriteLine($"OTP for {mobile}: {otp}"); // Dev only

        return true;
    }

    // Step 2: Verify OTP and return token
    public async Task<(string Token, User User)> VerifyOtpAsync(string mobile, string otp)
    {
        if (string.IsNullOrEmpty(otp) || otp.Length != 6)
            throw new ArgumentException("Invalid OTP");

        // Check OTP
        const string otpSql = @"
            SELECT * FROM OtpAttempt 
            WHERE Mobile = @mobile 
            AND OtpCode = @otp 
            AND ExpiresAt > NOW()
            AND VerifiedAt IS NULL
            LIMIT 1;
        ";

        var otpRecord = await _db.QueryFirstOrDefaultAsync<dynamic>(
            otpSql,
            new { mobile, otp }
        );

        if (otpRecord == null)
            throw new UnauthorizedAccessException("Invalid or expired OTP");

        // Check if user exists
        const string userSql = @"
            SELECT * FROM "User"
            WHERE Mobile = @mobile
            LIMIT 1;
        ";

        var user = await _db.QueryFirstOrDefaultAsync<User>(
            userSql,
            new { mobile }
        );

        if (user == null)
        {
            // Create new user (pending approval)
            user = new User
            {
                UserId = Guid.NewGuid(),
                Mobile = mobile,
                FullName = "New User",
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), // Default tenant
                Role = "Employee",
                IsApproved = false,
                CreatedAt = DateTime.UtcNow
            };

            const string insertSql = @"
                INSERT INTO "User" (UserId, TenantId, Mobile, FullName, Role, IsApproved, CreatedAt)
                VALUES (@userId, @tenantId, @mobile, @fullName, @role, @isApproved, @createdAt);
            ";

            await _db.ExecuteAsync(insertSql, new
            {
                user.UserId,
                user.TenantId,
                user.Mobile,
                user.FullName,
                user.Role,
                user.IsApproved,
                user.CreatedAt
            });
        }

        // Mark OTP as verified
        const string markVerifiedSql = @"
            UPDATE OtpAttempt 
            SET VerifiedAt = NOW() 
            WHERE Mobile = @mobile AND OtpCode = @otp;
        ";

        await _db.ExecuteAsync(markVerifiedSql, new { mobile, otp });

        // Generate JWT
        var token = GenerateToken(user);

        return (token, user);
    }

    private string GenerateToken(User user)
    {
        var key = Encoding.ASCII.GetBytes(
            _config["JwtSecret"] ?? "your-super-secret-key-min-32-chars-1234567890"
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.MobilePhone, user.Mobile),
                new Claim("TenantId", user.TenantId.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
```

### Controllers/AuthController.cs

```csharp
namespace CabBook.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        try
        {
            await _authService.SendOtpAsync(request.Mobile);
            return Ok(new { message = "OTP sent" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        try
        {
            var (token, user) = await _authService.VerifyOtpAsync(request.Mobile, request.Otp);
            return Ok(new
            {
                token,
                user = new
                {
                    user.UserId,
                    user.Mobile,
                    user.FullName,
                    user.Role,
                    user.IsApproved
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class SendOtpRequest
{
    public string Mobile { get; set; }
}

public class VerifyOtpRequest
{
    public string Mobile { get; set; }
    public string Otp { get; set; }
}
```

---

## Part 5: Frontend Setup (30 minutes)

### wwwroot/index.html

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>CabBook</title>
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/oat-css@latest/oat.min.css">
    <link rel="stylesheet" href="/css/app.css">
</head>
<body>
    <div id="app"></div>
    <script src="/js/api.js"></script>
    <script src="/js/app.js"></script>
</body>
</html>
```

### wwwroot/css/app.css

```css
:root {
    --primary: #0066cc;
    --danger: #cc0000;
    --success: #00aa00;
}

body {
    font-family: system-ui, -apple-system, sans-serif;
    background: #f5f5f5;
    margin: 0;
    padding: 20px;
}

.container { max-width: 600px; margin: 0 auto; }
.card {
    background: white;
    border-radius: 8px;
    padding: 20px;
    margin-bottom: 15px;
    box-shadow: 0 1px 3px rgba(0,0,0,0.1);
}
.form-group { margin-bottom: 15px; }
.form-group label { display: block; font-weight: 600; margin-bottom: 5px; }
.form-group input, .form-group select {
    width: 100%;
    padding: 10px;
    border: 1px solid #ddd;
    border-radius: 4px;
}
```

### wwwroot/js/api.js

```javascript
const API = {
    async call(method, endpoint, data = null) {
        const headers = { 'Content-Type': 'application/json' };
        const token = localStorage.getItem('token');
        if (token) headers['Authorization'] = `Bearer ${token}`;

        const response = await fetch(`/api${endpoint}`, {
            method,
            headers,
            body: data ? JSON.stringify(data) : null
        });

        if (response.status === 401) {
            localStorage.removeItem('token');
            location.reload();
        }

        return response;
    }
};
```

### wwwroot/js/app.js

```javascript
const state = {
    user: null,
    bookings: [],
    locations: [],
    shifts: []
};

async function init() {
    const token = localStorage.getItem('token');
    if (token) {
        state.user = JSON.parse(atob(token.split('.')[1]));
        await showBookingPage();
    } else {
        await showLoginPage();
    }
}

async function showLoginPage() {
    document.getElementById('app').innerHTML = `
        <div class="container">
            <div class="card">
                <h1>CabBook Login</h1>
                <div class="form-group">
                    <label>Mobile</label>
                    <input type="tel" id="mobile" maxlength="10">
                </div>
                <button onclick="sendOtp()" class="btn">Send OTP</button>
                <div id="otp-section" style="display:none; margin-top:20px;">
                    <input type="text" id="otp" maxlength="6" placeholder="OTP">
                    <button onclick="verifyOtp()">Verify</button>
                </div>
            </div>
        </div>
    `;
}

async function sendOtp() {
    const mobile = document.getElementById('mobile').value;
    const res = await API.call('POST', '/auth/send-otp', { mobile });
    if (res.ok) {
        document.getElementById('otp-section').style.display = 'block';
        alert('OTP sent');
    }
}

async function verifyOtp() {
    const mobile = document.getElementById('mobile').value;
    const otp = document.getElementById('otp').value;
    const res = await API.call('POST', '/auth/verify-otp', { mobile, otp });
    if (res.ok) {
        const data = await res.json();
        localStorage.setItem('token', data.token);
        location.reload();
    } else {
        alert('Invalid OTP');
    }
}

async function showBookingPage() {
    const res = await API.call('GET', '/bookings/locations');
    state.locations = await res.json();

    document.getElementById('app').innerHTML = `
        <div class="container">
            <h1>Book a Cab</h1>
            <form onsubmit="createBooking(event)">
                <div class="form-group">
                    <label>Date</label>
                    <input type="date" id="date" required>
                </div>
                <div class="form-group">
                    <label>Location</label>
                    <select id="location" required>
                        ${state.locations.map(l => `<option value="${l.locationId}">${l.name}</option>`).join('')}
                    </select>
                </div>
                <button type="submit" class="btn">Book</button>
            </form>
        </div>
    `;
}

async function createBooking(e) {
    e.preventDefault();
    const res = await API.call('POST', '/bookings', {
        bookingDate: document.getElementById('date').value,
        locationId: document.getElementById('location').value,
        shiftSlotId: '00000000-0000-0000-0000-000000000001' // Default slot
    });
    if (res.ok) {
        alert('✓ Booked!');
    }
}

document.addEventListener('DOMContentLoaded', init);
```

---

## Part 6: Run Locally (5 minutes)

```bash
# Terminal 1: Database
docker run --name cabbook-db \
  -e POSTGRES_PASSWORD=cabbook \
  -e POSTGRES_DB=cabbook \
  -p 5432:5432 \
  -d postgres:16-alpine

# Load schema
psql -h localhost -U postgres -d cabbook -f database/schema.sql

# Terminal 2: API
cd Backend
dotnet user-secrets set "JwtSecret" "your-32-character-secret-key-here"
dotnet run

# Open http://localhost:5000
```

---

## Part 7: Deploy to Render (10 minutes)

### 1. Create Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CabBook.dll"]
```

### 2. Push to GitHub

```bash
git init
git add .
git commit -m "Initial commit"
git branch -M main
git remote add origin https://github.com/YOUR/cabbook.git
git push -u origin main
```

### 3. Deploy on Render

1. Go to https://render.com
2. "New +" → "Web Service"
3. Connect GitHub repo
4. Set environment variable:
   - Name: `ConnectionStrings__DefaultConnection`
   - Value: `postgresql://user:pass@host/db`
5. Click "Deploy"

**Cost:** $0/month (free tier) → Auto-upgrade if needed

---

## Part 8: Testing

### Test Checklist

- [ ] Send OTP (check console for OTP in dev)
- [ ] Verify OTP
- [ ] Create booking
- [ ] View bookings
- [ ] Cancel booking (if not frozen)
- [ ] Admin export (if admin)

### Sample Test Data

```sql
-- Insert test tenant
INSERT INTO Tenant (TenantId, Name) 
VALUES ('00000000-0000-0000-0000-000000000001', 'Test Co');

-- Insert shift
INSERT INTO ShiftSlot (ShiftSlotId, TenantId, SlotName, StartTime, FreezeTime)
VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 
        '11 PM Drop', '23:00:00', '20:00:00');

-- Insert locations
INSERT INTO Location (LocationId, TenantId, Name)
VALUES 
    ('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', 'Powai'),
    ('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', 'Andheri');
```

---

## Summary

| Step | Time | Done |
|------|------|------|
| Project setup | 15 min | [ ] |
| Models & DB | 30 min | [ ] |
| API setup | 45 min | [ ] |
| Frontend | 30 min | [ ] |
| Local test | 5 min | [ ] |
| Deploy | 10 min | [ ] |
| **Total** | **135 min (2 hrs)** | [ ] |

**You'll have a live, working cab booking system in 2 hours.** 🚀

---

## Next: Add More Features

After basic setup works:

1. Add booking validation & freeze logic
2. Add admin export
3. Add notifications (SMS)
4. Add user approval workflow
5. Add booking audit logging

Each takes ~30 minutes.

---

## Troubleshooting

**"Connection refused"**
- Check PostgreSQL is running: `docker ps`
- Check connection string in appsettings.json

**"Unable to resolve service"**
- Check services are registered in Program.cs
- Restart dotnet run

**"401 Unauthorized"**
- Check token in localStorage
- Check JWT secret matches

**"CORS error"**
- Check CORS policy in Program.cs
- Frontend should be on same origin or whitelisted

---

## Final Cost

| Item | Cost |
|------|------|
| Hosting (Render) | $0 (free tier) |
| Database (PostgreSQL) | $0 (free tier) |
| Domain | $0 (*.onrender.com) |
| **Total** | **$0/month** |

Upgrade to paid tier only if you exceed free tier limits.

---

Ready to build? Start with **Part 1**! 🎉
