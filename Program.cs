using FileUploadApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS setup - More permissive for debugging
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
        policy.WithOrigins(
            "http://localhost:4200", // Local Angular dev server
            "https://cric-analyst.vercel.app" // Your Vercel frontend
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Database configuration - PostgreSQL only
// Priority order: DATABASE_URL -> Individual env vars -> appsettings.json -> default
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;

// OPTION 1: If DATABASE_URL exists (old Render PostgreSQL format)
if (!string.IsNullOrEmpty(databaseUrl))
{
    Console.WriteLine("Converting DATABASE_URL to Entity Framework connection string...");
    
    // Convert Render's PostgreSQL URL to EF connection string format
    try
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo?.Split(':');
        
        if (userInfo == null || userInfo.Length != 2 || string.IsNullOrEmpty(userInfo[0]) || string.IsNullOrEmpty(userInfo[1]))
        {
            throw new InvalidOperationException("Invalid DATABASE_URL format: missing or invalid username/password");
        }
        
        var dbPort = uri.Port == -1 ? 5432 : uri.Port; // Default to 5432 if port not specified
        var database = uri.LocalPath.StartsWith("/") ? uri.LocalPath.Substring(1) : uri.LocalPath;
        
        if (string.IsNullOrEmpty(database))
        {
            throw new InvalidOperationException("Invalid DATABASE_URL format: missing database name");
        }
        
        connectionString = $"Host={uri.Host};Port={dbPort};Database={database};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true;";
        
        Console.WriteLine($"Converted connection - Host: {uri.Host}, Port: {dbPort}, Database: {database}, Username: {userInfo[0]}");
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
    // OPTION 2: Try to build connection string from individual environment variables (Supabase format)
    var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
    var dbName = Environment.GetEnvironmentVariable("DB_NAME");
    var dbUser = Environment.GetEnvironmentVariable("DB_USER");
    var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
    var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";

    if (!string.IsNullOrEmpty(dbHost) && !string.IsNullOrEmpty(dbName) && 
        !string.IsNullOrEmpty(dbUser) && !string.IsNullOrEmpty(dbPassword))
    {
        connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword};SSL Mode=Require;Trust Server Certificate=true;";
        Console.WriteLine("Using individual environment variables for database connection (Supabase)");
        Console.WriteLine($"Connecting to host: {dbHost}, database: {dbName}, user: {dbUser}");
    }
    else
    {
        // OPTION 3: Fallback to configuration (appsettings.json) - for local development
        connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=cricket_db;Username=postgres;Password=password";
        Console.WriteLine("Using fallback connection string from appsettings.json or default");
    }
}

Console.WriteLine("Using PostgreSQL database");
// Debug connection string (without showing sensitive data)
Console.WriteLine($"DATABASE_URL environment variable: {(Environment.GetEnvironmentVariable("DATABASE_URL") != null ? "SET" : "NOT SET")}");
Console.WriteLine($"DB_HOST environment variable: {(Environment.GetEnvironmentVariable("DB_HOST") != null ? "SET" : "NOT SET")}");
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
        
        if (app.Environment.IsProduction())
        {
            Console.WriteLine("Production environment - dropping and recreating database...");
            db.Database.EnsureDeleted(); // This will drop the existing database
            db.Database.EnsureCreated();  // This will create fresh tables
        }
        else
        {
            db.Database.Migrate();
        }
        
        Console.WriteLine("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error applying migrations: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        }
        Console.WriteLine("Make sure PostgreSQL is running and connection string is correct");
        // Don't throw the exception - let the app start even if migrations fail
        // This allows the health endpoint to work for debugging
    }
}

app.UseRouting();
app.UseCors("AllowAngularApp");
app.UseAuthorization();
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