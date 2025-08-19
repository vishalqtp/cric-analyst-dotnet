// Data/CricketDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FileUploadApi.Data
{
    public class CricketDbContextFactory : IDesignTimeDbContextFactory<CricketDbContext>
    {
        public CricketDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<CricketDbContext>();
            
            // Use PostgreSQL instead of SQLite
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
                ?? "Host=localhost;Database=cricket_db;Username=postgres;Password=password";
            
            optionsBuilder.UseNpgsql(connectionString);

            return new CricketDbContext(optionsBuilder.Options);
        }
    }
}