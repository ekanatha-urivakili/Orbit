using Orbit.Application.Abstractions;
using Orbit.Infrastructure;
using Orbit.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ITenantContext, WorkerTenantContext>();
builder.Services.AddScoped<ICurrentPrincipal, WorkerCurrentPrincipal>();
builder.Services.AddHostedService<OutboxDispatchWorker>();

var host = builder.Build();
host.Run();
