
using SEP.Contracts;
using SEP.Observability;
using Serilog;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. LOGGING KONFIGURACIJA
// ==========================================

builder.AddSepLogging("Bank.API");

// ==========================================
// 2. REGISTRACIJA SERVISA
// ==========================================

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ==========================================
// 3. POKRETANJE APLIKACIJE
// ==========================================

try
{
    Log.Information("Pokretanje Bank.API");

    var app = builder.Build();

    // ======================================
    // 4. HTTP REQUEST LOGGING
    // ======================================

    app.UseSepRequestLogging();

    // ======================================
    // 5. DEVELOPMENT ENDPOINTS
    // ======================================

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();

        // Provera da li Bank.API radi
        app.MapGet("/debug/ping",
            (ILogger<Program> logger) =>
            {
                var traceId =
                    Activity.Current?.TraceId.ToString();

                logger.LogInformation(
                    "Bank testni zahtev. TraceId: {TraceId}",
                    traceId);

                return Results.Ok(new
                {
                    Message = "Bank API radi!",
                    Service = "Bank.API",
                    TraceId = traceId,
                    Timestamp = DateTimeOffset.UtcNow
                });
            });

        // ==================================
        // DISTRIBUTED TRACING TEST
        // ==================================

        // Ovaj endpoint poziva PSP.API
        // kako bismo proverili da li se
        // TraceId prenosi izmedju servisa.

        app.MapGet("/debug/trace",
            (ILogger<Program> logger) =>
            {
                var traceId =
                    Activity.Current?.TraceId.ToString();

                logger.LogInformation(
                    "Banka primila zahtev. TraceId: {TraceId}",
                    traceId);

                return Results.Ok(
                    new DebugTraceResponse(
                        Service: "Bank.API",
                        TraceId: traceId,
                        Message: "Banka je primila zahtev."
                    )
                );
            });
    }

    // ======================================
    // 6. HTTP PIPELINE
    // ======================================

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    Log.Information("Bank.API konfigurisan");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(
        ex,
        "Kriticna greska u Bank.API");

    throw;
}
finally
{
    // Upis preostalih logova pre gasenja
    Log.CloseAndFlush();
}
