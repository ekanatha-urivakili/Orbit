using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Orbit.Application.Abstractions;
using Orbit.Infrastructure;
using Orbit.Infrastructure.Messaging;
using Orbit.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ITenantContext, WorkerTenantContext>();
builder.Services.AddScoped<ICurrentPrincipal, WorkerCurrentPrincipal>();
builder.Services.AddHostedService<OutboxDispatchWorker>();
builder.Services.AddHostedService<AttachmentScanDispatchWorker>();

// §13.7.2 (ADR-023): exports to the orbit-otel Collector via OTLP. AddSource(OutboxEmailProcessor's
// ActivitySource) picks up the outbox.email.dispatch spans re-parented under the API's trace via
// the trace_parent column captured at insert time, so the two processes join into one trace.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("orbit-worker"))
    .WithTracing(tracing => tracing
        .AddSource(OutboxEmailProcessor.ActivitySourceName)
        .AddHttpClientInstrumentation()
        .AddNpgsql()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(OutboxEmailProcessor.MeterName)
        .AddOtlpExporter());

var host = builder.Build();
host.Run();
