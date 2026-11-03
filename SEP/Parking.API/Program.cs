
using SEP.Contracts;
using SEP.Observability;
using Serilog;
using System.Diagnostics;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. LOGGING
// ==========================================

builder.AddSepLogging("Parking.API");

// ==========================================
// 2. REGISTRACIJA SERVISA
// ==========================================

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ==========================================
// 3. HTTP CLIENT - PSP
// ==========================================

builder.Services.AddHttpClient("PSP", client =>
{
    var pspUrl = builder.Configuration[
        "Services:PspBaseUrl"]
        ?? throw new InvalidOperationException(
            "PspBaseUrl nije konfigurisan.");

    client.BaseAddress = new Uri(
        pspUrl.TrimEnd('/') + "/");

    client.Timeout = TimeSpan.FromSeconds(10);
});

// ==========================================
// 4. POKRETANJE APLIKACIJE
// ==========================================

try
{
    Log.Information("Pokretanje Parking.API");

    var app = builder.Build();

    app.UseSepRequestLogging();

    // ======================================
    // 5. DEVELOPMENT ENDPOINTS
    // ======================================

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();

        // Test rada Parking.API
        app.MapGet("/debug/ping",
            (ILogger<Program> logger) =>
            {
                var traceId =
                    Activity.Current?.TraceId.ToString();

                logger.LogInformation(
                    "Parking testni zahtev. TraceId: {TraceId}",
                    traceId);

                return Results.Ok(new
                {
                    Message = "Parking API radi!",
                    Service = "Parking.API",
                    TraceId = traceId,
                    Timestamp = DateTimeOffset.UtcNow
                });
            });

        // ==================================
        // PARKING -> PSP -> BANKA
        // ==================================

        app.MapGet("/debug/trace",
            async (
                IHttpClientFactory factory,
                ILogger<Program> logger,
                CancellationToken ct) =>
            {
                var traceId =
                    Activity.Current?.TraceId.ToString();

                logger.LogInformation(
                    "Parking pokrenuo komunikaciju. " +
                    "TraceId: {TraceId}",
                    traceId);

                try
                {
                    var client =
                        factory.CreateClient("PSP");

                    logger.LogInformation(
                        "Parking salje zahtev prema PSP.API");

                    var pspResponse =
                        await client.GetFromJsonAsync
                            <DebugTraceResponse>(
                                "debug/trace",
                                ct);

                    if (pspResponse is null)
                    {
                        logger.LogWarning(
                            "PSP.API vratio prazan odgovor");

                        return Results.Problem(
                            "PSP je vratio prazan odgovor.",
                            statusCode: 502);
                    }

                    logger.LogInformation(
                        "Parking primio odgovor PSP-a. " +
                        "PspTraceId: {PspTraceId}",
                        pspResponse.TraceId);

                    return Results.Ok(
                        new DebugTraceResponse(
                            Service: "Parking.API",
                            TraceId: traceId,
                            Message:
                                "Parking je kontaktirao PSP.",
                            Downstream: pspResponse
                        )
                    );
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError(
                        ex,
                        "Greska u komunikaciji sa PSP.API");

                    return Results.Problem(
                        "Komunikacija sa PSP-om nije uspela.",
                        statusCode: 502);
                }
                catch (TaskCanceledException ex)
                    when (!ct.IsCancellationRequested)
                {
                    logger.LogError(
                        ex,
                        "PSP.API nije odgovorio na vreme");

                    return Results.Problem(
                        "PSP nije odgovorio na vreme.",
                        statusCode: 504);
                }
            });
    }

    // ======================================
    // 6. HTTP PIPELINE
    // ======================================

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("Parking.API konfigurisan");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(
        ex,
        "Kriticna greska u Parking.API");

    throw;
}
finally
{
    Log.CloseAndFlush();
}
