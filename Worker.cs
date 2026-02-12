using Canary.Data;
using Canary.Models;
using Canary.Services;
using Microsoft.EntityFrameworkCore;

namespace Canary;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _clientFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory, IHttpClientFactory clientFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _clientFactory = clientFactory;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Canary Worker starting at: {Time}", DateTimeOffset.Now);

            using IServiceScope scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IReportStorage>();

            // Critical pre-flight check
            if (!await storage.IsHealthyAsync())
            {
                throw new Exception("S3 Storage (LocalStack) is not reachable.");
            }

            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Canary Worker failed pre-flight health checks.");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<HealthContext>();

                // Refresh targets from DB every loop so we can add/remove targets live
                var targets = await dbContext.Targets
                    .Where(t => t.IsActive)
                    .ToListAsync(stoppingToken);

                _logger.LogInformation("Starting health check cycle for {Count} targets.", targets.Count);

                // Run all checks in parallel : ok as we have three targets
                var tasks = targets.Select(target => ProcessTargetAsync(target, stoppingToken));
                await Task.WhenAll(tasks);
                //in a scenario with a lot of targets consider using a bounded concurrency approach:
                // var options = new ParallelOptions
                // {
                //     MaxDegreeOfParallelism = 5,
                //     CancellationToken = stoppingToken
                // };

                // await Parallel.ForEachAsync(targets, options, async (target, ct) =>
                // {
                //     await ProcessTargetAsync(target, ct);
                // });
            }

            _logger.LogInformation("Cycle complete. Waiting for next interval...");
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessTargetAsync(Target target, CancellationToken stoppingToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HealthContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IReportStorage>();

        // 1. Fetch the target within THIS scope so EF tracks it correctly
        // This is an unnecessary roundtrip to the DB, we already have the target.
        // var target = await context.Targets.FindAsync(new object[] { targetId }, stoppingToken);
        // if (target == null) return;
        // So instead of fetching it again, we will attach it to the current context:
        context.Targets.Attach(target);
        // Fetching again the data is better in high concurrency scenarios where the target
        // might have been modified or deleted since we read it last time. Here it's unnecessary as the sources probably won't change that often.

        _logger.LogInformation("Checking {TargetName} ({TargetUrl})", target.Name, target.Url);

        try
        {
            // 2. Perform the Ping
            bool isUp = await CheckUrlAsync(target.Url, stoppingToken);

            if (!isUp)
            {
                _logger.LogWarning("Health check FAILED for {TargetName}. Reporting to S3.", target.Name);
                await storage.UploadReportAsync(target.Name, target.Url);
            }

            // 3. Record the result in the history table
            context.HealthChecks.Add(new HealthCheckResult
            {
                Url = target.Url,
                IsUp = isUp,
                CheckedAt = DateTime.UtcNow
            });

            // 4. Update the Target row's timestamp (EF tracks this object now)
            target.LastCheckedAt = DateTime.UtcNow;

            // 5. Save everything in one transaction
            await context.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ProcessTargetAsync for {TargetName}", target.Name);
        }
    }

    private async Task<bool> CheckUrlAsync(string url, CancellationToken stoppingToken)
    {
        try
        {
            var client = _clientFactory.CreateClient("Canary");
            // Set a strict timeout so one slow target doesn't hang the worker
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await client.GetAsync(url, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}