using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Kenlo.Api;
using Kenlo.Api.Assistant;
using Kenlo.Api.Import;
using Kenlo.Api.Storage;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var mode = builder.Configuration["STORE_MODE"] ?? "memory";
if (mode.Equals("postgres", StringComparison.OrdinalIgnoreCase))
{
    var connection = builder.Configuration["DATABASE_URL"];
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("DATABASE_URL is required for STORE_MODE=postgres.");
    var databaseHost = new NpgsqlConnectionStringBuilder(connection).Host;
    if (databaseHost is not ("localhost" or "127.0.0.1" or "::1" or "host.docker.internal"))
        throw new InvalidOperationException("Demo mode only allows a local PostgreSQL host. Never use real customer data.");
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));
    builder.Services.AddSingleton<IDemoStore, PostgresDemoStore>();
}
else if (mode.Equals("memory", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IDemoStore, MemoryDemoStore>();
else throw new InvalidOperationException("STORE_MODE must be memory or postgres.");

builder.Services.AddSingleton<IConversationAssistant>(sp =>
    new FakeConversationAssistant(builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SIMULATION_ASSISTANT_FAIL")));
builder.Services.AddSingleton<SyntheticCatalogParser>();
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", mode = "simulation" }));
app.MapGet("/api/properties", async (string? city, string? neighborhood, decimal? maxPrice, int? bedrooms, int? parking, IDemoStore store, CancellationToken ct) =>
{
    if (maxPrice < 0 || bedrooms < 0 || parking < 0) return Results.BadRequest(new { error = "Os filtros numéricos não podem ser negativos." });
    return Results.Ok(await store.SearchPropertiesAsync(new(city, neighborhood, maxPrice, bedrooms, parking), ct));
});
app.MapGet("/api/properties/{id:guid}", async (Guid id, IDemoStore store, CancellationToken ct) =>
    await store.GetPropertyAsync(id, ct) is { } property ? Results.Ok(property) : Results.NotFound());
app.MapGet("/api/conversations", async (IDemoStore store, CancellationToken ct) => Results.Ok(await store.GetConversationsAsync(ct)));
app.MapGet("/api/conversations/{id:guid}", async (Guid id, IDemoStore store, CancellationToken ct) =>
    await store.GetConversationAsync(id, ct) is { } conversation ? Results.Ok(conversation) : Results.NotFound());
app.MapPost("/api/conversations/{id:guid}/takeover", async (Guid id, IDemoStore store, CancellationToken ct) =>
    await store.SetConversationStatusAsync(id, "human", ct) is { } conversation ? Results.Ok(conversation) : Results.NotFound());
app.MapPost("/api/conversations/{id:guid}/resume", async (Guid id, IDemoStore store, CancellationToken ct) =>
    await store.SetConversationStatusAsync(id, "automated", ct) is { } conversation ? Results.Ok(conversation) : Results.NotFound());

app.MapPost("/api/simulation/messages", async (SimulationMessageRequest input, IDemoStore store, IConversationAssistant assistant, CancellationToken ct) =>
{
    if (!app.Environment.IsDevelopment()) return Results.NotFound();
    if (input.ConversationId == Guid.Empty || string.IsNullOrWhiteSpace(input.MessageId) || input.MessageId.Length > 128 ||
        string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 4000)
        return Results.BadRequest(new { error = "Conversa, identificador e texto válidos são obrigatórios." });
    string? response = null;
    var failed = false;
    try { response = await assistant.GenerateAsync(input.ConversationId.ToString(), input.Text, ct); }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
    catch (Exception) { failed = true; } // Never log message text, tokens, or provider details.
    var result = await store.ReceiveAsync(input.ConversationId, input.MessageId, input.Text, response, failed, ct);
    return result.Status switch
    {
        ReceiveStatus.NotFound => Results.NotFound(),
        ReceiveStatus.Conflict => Results.Conflict(new { error = "Identificador já usado para outra mensagem." }),
        _ => Results.Ok(result.Conversation)
    };
});

app.MapPost("/api/simulation/import", async (HttpRequest request, SyntheticCatalogParser parser, IDemoStore store, CancellationToken ct) =>
{
    if (!app.Environment.IsDevelopment()) return Results.NotFound();
    var bytes = await RequestBody.ReadBoundedAsync(request, 1_000_000, ct);
    if (bytes is null) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    try
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var parsed = parser.Parse(stream);
        var properties = parsed.Select(p => new Property(Guid.NewGuid(), p.Code, p.Title, p.City, p.Neighborhood, p.Price, p.Bedrooms, p.Parking, p.Features, p.ListingUrl, p.UpdatedAt)).ToArray();
        await store.UpsertPropertiesAsync(properties, ct);
        return Results.Ok(new { imported = properties.Length, format = "synthetic-demo-v1" });
    }
    catch (Exception ex) when (ex is XmlException or InvalidDataException or ArgumentException)
    {
        return Results.BadRequest(new { error = "XML sintético inválido; o catálogo não foi alterado." });
    }
});

// This endpoint checks HMAC only in an explicit development simulation. It does not
// process or acknowledge actual WhatsApp events and must never be enabled for a real number.
app.MapPost("/webhooks/whatsapp", async (HttpRequest request, CancellationToken ct) =>
{
    if (!app.Environment.IsDevelopment() || !builder.Configuration.GetValue<bool>("WEBHOOK_SIGNATURE_TEST_ONLY")) return Results.NotFound();
    var secret = builder.Configuration["WEBHOOK_APP_SECRET"];
    if (string.IsNullOrEmpty(secret)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    var bytes = await RequestBody.ReadBoundedAsync(request, 1_000_000, ct);
    if (bytes is null) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    var header = request.Headers["X-Hub-Signature-256"].ToString();
    if (!WebhookSignature.IsValid(secret, bytes, header)) return Results.Unauthorized();
    return Results.Ok(new { status = "signature_verified_simulation_only" });
});
app.Run();

public partial class Program;

public static class WebhookSignature
{
    public static bool IsValid(string secret, byte[] body, string header)
    {
        if (!header.StartsWith("sha256=", StringComparison.Ordinal) || header.Length != 71) return false;
        try
        {
            var actual = Convert.FromHexString(header[7..]);
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}

public static class RequestBody
{
    public static async Task<byte[]?> ReadBoundedAsync(HttpRequest request, int maximumBytes, CancellationToken ct)
    {
        if (request.ContentLength > maximumBytes) return null;
        using var stream = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await request.Body.ReadAsync(buffer.AsMemory(), ct);
            if (count == 0) break;
            if (stream.Length + count > maximumBytes) return null;
            await stream.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        return stream.ToArray();
    }
}
