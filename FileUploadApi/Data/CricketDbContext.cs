using Microsoft.EntityFrameworkCore;
using FileUploadApi.Models;

namespace FileUploadApi.Data;

public class CricketDbContext : DbContext
{
    public CricketDbContext(DbContextOptions<CricketDbContext> options) : base(options)
    {
    }

    public DbSet<MatchInfo> Matches { get; set; }
}
