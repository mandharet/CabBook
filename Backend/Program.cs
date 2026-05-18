using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplicationBuilder.CreateBuilder(args);

var config = builder.Configuration;
var jwtSecret = config["Jwt:Secret"] ?? "default-secret-change-in-production";
var jwtIssuer = config["Jwt:Issuer"] ?? "cabbook";
var dbConnection = config.GetConnectionString("DefaultConnection")
    ?? "Host=db;Database=cabbook;Username=postgres;Password=postgres";

// Add services
builder.Services.AddCors(options => options.AddDefaultPolicy(
    policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddSingleton(new NpgsqlDataSource(
    new NpgsqlDataSourceBuilder(dbConnection)
        .Build()));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<RosterService>();
builder.Services.AddScoped<ShiftSlotService>();
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<TenantService>();

// JWT Authentication
var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret));
builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Serve index.html for SPA
app.MapFallbackToFile("index.html");

app.Run();
