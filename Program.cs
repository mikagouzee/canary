using CloudNativeCanary;using CloudNativeCanary.Data;
using CloudNativeCanary.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;


Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddDbContext<HealthContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("HealthDatabase")));
    
    builder.Services.AddHttpClient("Canary")
        .AddStandardResilienceHandler(o =>
        {
            o.Retry.MaxRetryAttempts = 3;
            o.Retry.Delay = TimeSpan.FromSeconds(2);
            o.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        });
    
    builder.Services.AddHostedService<Worker>();
    
    builder.Services.AddTransient<IReportStorage, FileStorageService>();
    
    builder.Services.AddMassTransit(x =>
    {
        // Tells MassTransit to look for Consumers/Sagas in this assembly (even if we don't have them yet)
        x.SetKebabCaseEndpointNameFormatter();
    
        x.UsingRabbitMq((context, cfg) =>
        {
            var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
            var username = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
            var password = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";
    
            cfg.Host(host, "/", h =>
            {
                h.Username(username);
                h.Password(password);
            });
    
            // Resilience: If the broker is unreachable, retry 3 times with a 5s delay
            cfg.UseMessageRetry(r => 
            {
                r.Interval(3, TimeSpan.FromSeconds(5));
            });
    
            cfg.ConfigureEndpoints(context);
        });
    });
    
    
    var host = builder.Build();
    
    //Automatically apply migrations at startup
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<HealthContext>();
        db.Database.Migrate();
    }
    
    host.Run();
    
}
catch (System.Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}