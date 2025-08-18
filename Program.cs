using FileUploadApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Determine SQLite database path - works better for container deployment
string dbPath = Environment.GetEnvironmentVariable("DB_PATH") 
    ?? Path.Combine(AppContext.BaseDirectory, "cricket.db");
Console.WriteLine($"Using SQLite DB at: {dbPath}");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS setup - Updated to include your Vercel frontend
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

// Add DbContext with dynamic path
builder.Services.AddDbContext<CricketDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

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
        db.Database.Migrate();
        Console.WriteLine("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error applying migrations: {ex.Message}");
    }
}

app.UseDefaultFiles(); // serves index.html by default
app.UseStaticFiles();  // serves all static assets in wwwroot

app.UseRouting();

// Apply CORS
app.UseCors("AllowAngularApp");

app.MapControllers();

// Health check endpoint for Render
app.MapGet("/health", () => Results.Ok(new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName 
}));

// Fallback to Angular index.html for client side routes (if you have frontend files)
app.MapFallbackToFile("index.html");

// Configure port for Render deployment
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

Console.WriteLine($"Starting server on port {port}");
app.Run();