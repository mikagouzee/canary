using Microsoft.EntityFrameworkCore;
using CloudNativeCanary.Models;

namespace CloudNativeCanary.Data;

public class HealthContext : DbContext
{
    public HealthContext(DbContextOptions<HealthContext> options) : base(options) { }

    public DbSet<HealthCheckResult> HealthChecks => Set<HealthCheckResult>();
}