using Hangfire;
using Serilog;
using Showcase.Application;
using Showcase.Infrastructure;
using Showcase.Worker;

// Runs Hangfire jobs: model file processing now, Showcase builds later (GĐ5).
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((sp, cfg) => cfg.ReadFrom.Configuration(builder.Configuration).WriteTo.Console());
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IModelFileProcessor, ModelFileProcessor>();
builder.Services.AddTransient<Heartbeat>();
builder.Services.AddHangfireServer(o =>
{
    // Blender and gltf-transform are heavy: process one file at a time.
    o.WorkerCount = builder.Configuration.GetValue("Worker:Concurrency", 1);
    o.ServerName = $"worker-{Environment.MachineName}";
});

var host = builder.Build();

host.Services.GetRequiredService<IRecurringJobManager>()
    .AddOrUpdate<Heartbeat>("heartbeat", h => h.Run(), Cron.Hourly);

host.Run();
