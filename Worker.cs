using CloudNativeCanary.Data;
using CloudNativeCanary.Models;
using CloudNativeCanary.Services;
using MassTransit.Clients;

namespace CloudNativeCanary;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _clientFactory;
    private readonly IReportStorage _storage;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory, IConfiguration configuration, IHttpClientFactory clientFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _clientFactory = clientFactory;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Canary Worker starting at: {Time}", DateTimeOffset.Now);
        if(!await _storage.IsHealthyAsync())
        {
            throw new Exception("Storage is not healthy.");
        } 
        await base.StartAsync(cancellationToken);
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _configuration.GetSection("CanaryConfig").Get<CanaryConfig>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var tasks = config.Targets.Select(target => ProcessTargetAsync(target, stoppingToken));
            await Task.WhenAll(tasks);

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessTargetAsync(CanaryTarget target, CancellationToken stoppingToken)
    {
        using (IServiceScope scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<HealthContext>();
            var storage = scope.ServiceProvider.GetRequiredService<IReportStorage>();
            _logger.LogInformation("Canary check {TargetName} ({TargetUrl}) running at: {Time}", target.Name, target.Url, DateTimeOffset.Now);

            try
            {
                bool isUp = await CheckUrlAsync(target.Url, stoppingToken);
    
                if (!isUp)
                {
                    _logger.LogCritical("Health check FAILED for {TargetName}. Reporting to S3.", target.Name);
    
                    await _storage.UploadReportAsync(target.Name, target.Url);
                }
    
                context.HealthChecks.Add(new HealthCheckResult
                {
                    Url = target.Url,
                    IsUp = isUp,
                    CheckedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to process check for {Target}", target.Name);
                throw;
            }
        }
       
    }

    private async Task<bool> CheckUrlAsync(string url, CancellationToken stoppingToken)
    {
        #region Alternative TCP Port Check (commented out)
        // If it's a DB port (usually 5432) or doesn't start with http
        // I keep this commented so the DB's url fails and generates the error we want to see.
        // if (!url.StartsWith("http") || url.Contains(":5432"))
        // {
        //     try
        //     {
        //         // Strip "http://" if it was accidentally added
        //         var cleanUrl = url.Replace("http://", "").Replace("https://", "");
        //         var parts = cleanUrl.Split(':');
        //         var host = parts[0];
        //         var port = int.Parse(parts[1]);

        //         using var tcpClient = new System.Net.Sockets.TcpClient();
        //         // Try to connect to the raw port
        //         var connectTask = tcpClient.ConnectAsync(host, port);
        //         // Give it 2 seconds to respond
        //         if (await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask)
        //         {
        //             return tcpClient.Connected;
        //         }
        //         return false;
        //     }
        //     catch { return false; }
        // }
        #endregion
        try
        {
            var client = _clientFactory.CreateClient("Canary");
            var response = await client.GetAsync(url, stoppingToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
