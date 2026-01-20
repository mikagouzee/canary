using CloudNativeCanary;
using CloudNativeCanary.Data;
using CloudNativeCanary.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<HealthContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("HealthDatabase")));


builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<FileStorageService>();

var host = builder.Build();

//Automatically apply migrations at startup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HealthContext>();
    db.Database.Migrate();
}

host.Run();
