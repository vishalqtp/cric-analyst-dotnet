using FileUploadApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS setup
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
        policy.WithOrigins(
            "http://localhost:4200", // Local Angular dev server
            "https://cric-analyst.vercel.app", // Your Vercel frontend
            builder.Configuration["FrontendUrl"] ?? "https://cric-analyst.vercel.app"
        )
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Database configuration - PostgreSQL only
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;

if (!string.IsNullOrEmpty(databaseUrl))
{
    Console.WriteLine("Converting DATABASE_URL to Entity Framework connection string...");
    
    // Convert Render's PostgreSQL URL to EF connection string format
    try
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');
        var dbPort = uri.Port == -1 ? 5432 : uri.Port; // Default to 5432 if port not specified
        var database = uri.LocalPath.StartsWith("/") ? uri.LocalPath.Substring(1) : uri.LocalPath;
        
        connectionString = $"Host={uri.Host};Port={dbPort};Database={database};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true;";
        
        Console.WriteLine($"Converted connection - Host: {uri.Host}, Port: {dbPort}, Database: {database}");
        Console.WriteLine("DATABASE_URL conversion successful");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error converting DATABASE_URL: {ex.Message}");
        // Fallback to original DATABASE_URL (might work in some cases)
        connectionString = databaseUrl;
    }
}
else
{
    // Fallback to configuration or default
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Database=cricket_db;Username=postgres;Password=password";
    Console.WriteLine("Using fallback connection string");
}

Console.WriteLine("Using PostgreSQL database");
// Debug connection string (without showing sensitive data)
Console.WriteLine($"DATABASE_URL environment variable: {(Environment.GetEnvironmentVariable("DATABASE_URL") != null ? "SET" : "NOT SET")}");
Console.WriteLine($"Connection string length: {connectionString?.Length ?? 0}");
Console.WriteLine($"Connection string starts with: {(connectionString?.Length > 10 ? connectionString.Substring(0, 10) : connectionString)}");

Console.WriteLine("Using PostgreSQL database");
builder.Services.AddDbContext<CricketDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CricketDbContext>();
    try
    {
        Console.WriteLine("Applying database migrations...");
        db.Database.Migrate();
        Console.WriteLine("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error applying migrations: {ex.Message}");
        Console.WriteLine("Make sure PostgreSQL is running and connection string is correct");
    }
}

app.UseRouting();
app.UseCors("AllowAngularApp");
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { 
    status = "healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName,
    database = "PostgreSQL"
}));

// Configure port for Render deployment
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

Console.WriteLine($"Starting server on port {port}");
app.Run();