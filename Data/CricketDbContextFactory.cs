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
            optionsBuilder.UseSqlite("Data Source=cricket.db");

            return new CricketDbContext(optionsBuilder.Options);
        }
    }
}
