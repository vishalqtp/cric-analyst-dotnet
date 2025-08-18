using FileUploadApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Determine SQLite database path (absolute)
string dbPath = Path.Combine(AppContext.BaseDirectory, "cricket.db");
Console.WriteLine($"Using SQLite DB at: {dbPath}");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS setup — useful for local dev and separate frontend hosting only
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
        policy.WithOrigins(
            "http://localhost:4200", // Local Angular dev server
            builder.Configuration["FrontendUrl"] ?? "https://your-frontend-url.com" // Hosted frontend domain
        )
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Add DbContext with dynamic absolute path
builder.Services.AddDbContext<CricketDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// *** ADD THIS BLOCK TO APPLY MIGRATIONS AUTOMATICALLY ***
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CricketDbContext>();
    db.Database.Migrate();
}

app.UseDefaultFiles();  // serves index.html by default
app.UseStaticFiles();   // serves all static assets in wwwroot

app.UseRouting();

// Apply CORS only if needed (e.g., during development with separate frontend)
app.UseCors("AllowAngularApp");

app.MapControllers();

// Fallback to Angular index.html for client side routes
app.MapFallbackToFile("index.html");

app.Run();
