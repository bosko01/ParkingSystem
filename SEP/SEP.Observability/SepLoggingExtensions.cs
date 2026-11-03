
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Diagnostics;

namespace SEP.Observability;

public static class SepLoggingExtensions
{
    // Konfiguracija Serilog-a za svaki servis
    public static WebApplicationBuilder AddSepLogging(
        this WebApplicationBuilder builder,
        string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var filePrefix = serviceName
            .ToLowerInvariant()
            .Replace(".", "-");

        var logPath = Path.Combine(
            builder.Environment.ContentRootPath,
            "logs",
            $"{filePrefix}-.json"
        );

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override(
                "Microsoft.AspNetCore",
                LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console()
            .WriteTo.File(
                formatter: new JsonFormatter(
                    renderMessage: true),
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true)
            .CreateLogger();

        builder.Services.AddSerilog();

        return builder;
    }

    // Logovanje HTTP zahteva
    public static WebApplication UseSepRequestLogging(
        this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext =
                (diagnosticContext, httpContext) =>
                {
                    var traceId =
                        Activity.Current?.TraceId.ToString()
                        ?? httpContext.TraceIdentifier;

                    diagnosticContext.Set(
                        "TraceId",
                        traceId);
                };
        });

        return app;
    }
}
