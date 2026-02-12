using Microsoft.EntityFrameworkCore;
using Canary.Models;

namespace Canary.Data;

public class HealthContext : DbContext
{
    public HealthContext(DbContextOptions<HealthContext> options) : base(options) { }

    public DbSet<HealthCheckResult> HealthChecks => Set<HealthCheckResult>();

    public DbSet<Target> Targets => Set<Target>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Target>().HasData(
        new Target
        {
            Id = 1,
            Name = "Local-DB-Check",
            Url = "http://postgres-service:5432",
            IsActive = true
        },
        new Target
        {
            Id = 2,
            Name = "Google-Homepage",
            Url = "https://www.google.com",
            IsActive = true
        },
        new Target
        {
            Id = 3,
            Name = "Microsoft-Homepage",
            Url = "https://www.microsoft.com",
            IsActive = true
        }
    );
    }
}