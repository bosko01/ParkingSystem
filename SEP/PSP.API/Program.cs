
using SEP.Contracts;
using SEP.Observability;
using Serilog;
using System.Diagnostics;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. LOGGING
// ==========================================

builder.AddSepLogging("PSP.API");

// ==========================================
// 2. REGISTRACIJA SERVISA
// ==========================================

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ==========================================
// 3. HTTP CLIENT - KOMUNIKACIJA SA BANKOM
// ==========================================

builder.Services.AddHttpClient("Bank", client =>
{
    var bankUrl = builder.Configuration[
        "Services:BankBaseUrl"]
        ?? throw new InvalidOperationException(
            "BankBaseUrl nije konfigurisan.");

    client.BaseAddress = new Uri(
        bankUrl.TrimEnd('/') + "/");

    client.Timeout = TimeSpan.FromSeconds(10);
});

// ==========================================
// 4. POKRETANJE APLIKACIJE
// ==========================================

try
{
    Log.Information("Pokretanje PSP.API");

    var app = builder.Build();

    // Automatsko logovanje HTTP zahteva
    app.UseSepRequestLogging();

    // ======================================
    // 5. DEVELOPMENT ENDPOINTS
    // ======================================

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();

        // Provera da li PSP radi
        app.MapGet("/debug/ping",
            (ILogger<Program> logger) =>
            {
                var traceId =
                    Activity.Current?.TraceId.ToString();

                logger.LogInformation(
                    "PSP testni zahtev. TraceId: {TraceId}",
                    traceId);

                return Results.Ok(new
                {
                    Message = "PSP API radi!",
                    Service = "PSP.API",
                    TraceId = traceId,
                    Timestamp = DateTimeOffset.UtcNow
                });
            });

        // ==================================
        // TEST KOMUNIKACIJE PSP -> BANKA
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
                    "PSP primio zahtev. TraceId: {TraceId}",
                    traceId);

                try
                {
                    // Kreiranje HTTP klijenta
                    var client =
                        factory.CreateClient("Bank");

                    logger.LogInformation(
                        "PSP salje zahtev prema Bank.API");

                    // Pozivanje bankarskog servisa
                    var bankResponse =
                        await client.GetFromJsonAsync
                            <DebugTraceResponse>(
                                "debug/trace",
                                ct);

                    if (bankResponse is null)
                    {
                        logger.LogWarning(
                            "Bank.API vratio prazan odgovor");

                        return Results.Problem(
                            "Banka je vratila prazan odgovor.",
                            statusCode: 502);
                    }

                    logger.LogInformation(
                        "PSP primio odgovor banke. " +
                        "BankTraceId: {BankTraceId}",
                        bankResponse.TraceId);

                    // Vracamo odgovor zajedno
                    // sa podacima iz Bank.API
                    return Results.Ok(
                        new DebugTraceResponse(
                            Service: "PSP.API",
                            TraceId: traceId,
                            Message:
                                "PSP je kontaktirao banku.",
                            Downstream: bankResponse
                        )
                    );
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError(
                        ex,
                        "Greska u komunikaciji sa Bank.API");

                    return Results.Problem(
                        "Komunikacija sa bankom nije uspela.",
                        statusCode: 502);
                }
                catch (TaskCanceledException ex)
                    when (!ct.IsCancellationRequested)
                {
                    logger.LogError(
                        ex,
                        "Bank.API nije odgovorio na vreme");

                    return Results.Problem(
                        "Banka nije odgovorila na vreme.",
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

    Log.Information("PSP.API konfigurisan");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(
        ex,
        "Kriticna greska u PSP.API");

    throw;
}
finally
{
    Log.CloseAndFlush();
}
