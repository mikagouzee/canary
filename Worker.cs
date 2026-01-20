using CloudNativeCanary.Data;
using CloudNativeCanary.Models;
using CloudNativeCanary.Services;

namespace CloudNativeCanary;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _configuration.GetSection("CanaryConfig").Get<CanaryConfig>();

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var target in config.Targets)
            {
                using (IServiceScope scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<HealthContext>();
                    _logger.LogInformation("Canary check {TargetName} ({TargetUrl}) running at: {Time}", target.Name, target.Url, DateTimeOffset.Now);
                    bool isUp = await CheckUrl(target.Url);
                    if (!isUp)
                    {
                        _logger.LogCritical("Health check FAILED for {TargetName}. Reporting to S3.", target.Name);
                        var fileStorageService = scope.ServiceProvider.GetRequiredService<FileStorageService>();
                        await fileStorageService.UploadFailureReportAsync(target.Name, target.Url);
                    }
                   
                    var result = new HealthCheckResult
                    {
                        Url = target.Url,
                        IsUp = isUp,
                        CheckedAt = DateTime.UtcNow
                    };
                    context.HealthChecks.Add(result);
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task<bool> CheckUrl(string url)
    {
        // If it's a DB port (usually 5432) or doesn't start with http
        if (!url.StartsWith("http") || url.Contains(":5432"))
        {
            try
            {
                // Strip "http://" if it was accidentally added
                var cleanUrl = url.Replace("http://", "").Replace("https://", "");
                var parts = cleanUrl.Split(':');
                var host = parts[0];
                var port = int.Parse(parts[1]);

                using var tcpClient = new System.Net.Sockets.TcpClient();
                // Try to connect to the raw port
                var connectTask = tcpClient.ConnectAsync(host, port);
                // Give it 2 seconds to respond
                if (await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask)
                {
                    return tcpClient.Connected;
                }
                return false;
            }
            catch { return false; }
        }

        try
        {
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
