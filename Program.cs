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

// Database configuration - supports both SQLite (local) and PostgreSQL (production)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var isProduction = !string.IsNullOrEmpty(databaseUrl);

if (isProduction)
{
    // Use PostgreSQL for production (Render)
    Console.WriteLine("Using PostgreSQL database for production");
    
    builder.Services.AddDbContext<CricketDbContext>(options =>
        options.UseNpgsql(databaseUrl));
}
else
{
    // Use SQLite for local development
    var dbPath = builder.Configuration.GetConnectionString("DefaultConnection") 
                 ?? Path.Combine(AppContext.BaseDirectory, "cricket.db");
    Console.WriteLine($"Using SQLite DB for development at: {dbPath}");
    
    builder.Services.AddDbContext<CricketDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));
}

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
        // In production, you might want to fail fast if migrations fail
        if (isProduction)
        {
            Console.WriteLine("Migration failed in production. Exiting...");
            throw;
        }
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAngularApp");
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { 
    status = "healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName,
    database = isProduction ? "PostgreSQL" : "SQLite"
}));

app.MapFallbackToFile("index.html");

// Configure port for Render deployment
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

Console.WriteLine($"Starting server on port {port}");
app.Run();