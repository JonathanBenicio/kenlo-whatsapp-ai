using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Kenlo.Api.Tests;

public sealed class ApiIntegrationTests
{
    private static readonly Guid ConversationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string ValidXml = """
        <synthetic-catalog format="demo-v1">
          <property code="SIM-01"><title>Casa fictícia</title><city>Campinas</city><neighborhood>Centro</neighborhood>
            <price>700000.00</price><bedrooms>3</bedrooms><parking>2</parking>
            <features><feature>Varanda</feature></features><listingUrl>https://example.invalid/imoveis/SIM-01</listingUrl>
            <updatedAt>2026-01-01T00:00:00Z</updatedAt></property>
        </synthetic-catalog>
        """;

    private static WebApplicationFactory<Program> Factory(params KeyValuePair<string, string?>[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        });

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());

    [Fact]
    public async Task CatalogFiltersComposeAndRejectNegativeValues()
    {
        await using var factory = Factory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/properties?city=campinas&maxPrice=700000&bedrooms=3&parking=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var property = Assert.Single((await Json(response)).EnumerateArray());
        Assert.Equal("DEMO-001", property.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/properties?bedrooms=-1")).StatusCode);
    }

    [Fact]
    public async Task RepeatedMessageIsIdempotentAndHumanHandoffPausesAutomatedReplies()
    {
        await using var factory = Factory();
        var client = factory.CreateClient();
        var message = new { conversationId = ConversationId, messageId = "event-1", text = "Quero visitar" };
        var first = await client.PostAsJsonAsync("/api/simulation/messages", message);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await Json(first);
        var initialCount = firstBody.GetProperty("messages").GetArrayLength();
        Assert.Equal("assistant", firstBody.GetProperty("messages")[initialCount - 1].GetProperty("author").GetString());
        var repeated = await client.PostAsJsonAsync("/api/simulation/messages", message);
        Assert.Equal(initialCount, (await Json(repeated)).GetProperty("messages").GetArrayLength());
        var conflicting = await client.PostAsJsonAsync("/api/simulation/messages", new { conversationId = ConversationId, messageId = "event-1", text = "Outra mensagem" });
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);
        var takeover = await client.PostAsync($"/api/conversations/{ConversationId}/takeover", null);
        Assert.Equal("human", (await Json(takeover)).GetProperty("status").GetString());
        var humanMessage = await client.PostAsJsonAsync("/api/simulation/messages", new { conversationId = ConversationId, messageId = "event-2", text = "Obrigado" });
        var humanBody = await Json(humanMessage);
        var humanMessages = humanBody.GetProperty("messages");
        Assert.Equal(initialCount + 1, humanMessages.GetArrayLength());
        Assert.Equal("customer", humanMessages[humanMessages.GetArrayLength() - 1].GetProperty("author").GetString());
        var resume = await client.PostAsync($"/api/conversations/{ConversationId}/resume", null);
        Assert.Equal("automated", (await Json(resume)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task ImportIsIdempotentAndInvalidXmlDoesNotChangeCatalog()
    {
        await using var factory = Factory();
        var client = factory.CreateClient();
        async Task<HttpResponseMessage> Import(string xml) => await client.PostAsync("/api/simulation/import", new StringContent(xml, Encoding.UTF8, "application/xml"));
        Assert.Equal(HttpStatusCode.OK, (await Import(ValidXml)).StatusCode);
        var before = await Json(await client.GetAsync("/api/properties"));
        var imported = Assert.Single(before.EnumerateArray(), item => item.GetProperty("code").GetString() == "SIM-01");
        Assert.Equal(HttpStatusCode.OK, (await Import(ValidXml.Replace("SIM-01", "sim-01"))).StatusCode);
        var after = await Json(await client.GetAsync("/api/properties"));
        var repeated = Assert.Single(after.EnumerateArray(), item => item.GetProperty("code").GetString() == "SIM-01");
        Assert.Equal(imported.GetProperty("id").GetString(), repeated.GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Import("<!DOCTYPE synthetic-catalog [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]><synthetic-catalog format='demo-v1'>&xxe;</synthetic-catalog>")).StatusCode);
        var preserved = await Json(await client.GetAsync("/api/properties"));
        Assert.Equal(after.GetArrayLength(), preserved.GetArrayLength());
        Assert.Contains(preserved.EnumerateArray(), item => item.GetProperty("id").GetString() == imported.GetProperty("id").GetString());
    }

    [Fact]
    public async Task WebhookIsDisabledByDefaultAndChecksSignatureOnlyWhenExplicitlyEnabled()
    {
        await using var disabled = Factory();
        using var disabledClient = disabled.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await disabledClient.PostAsync("/webhooks/whatsapp", new StringContent("{}"))).StatusCode);

        await using var enabled = Factory(new("WEBHOOK_SIGNATURE_TEST_ONLY", "true"), new("WEBHOOK_APP_SECRET", "fake-secret"));
        using var client = enabled.CreateClient();
        const string payload = "{\"entry\":[]}";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new StringContent(payload) };
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes("fake-secret"), Encoding.UTF8.GetBytes(payload));
        request.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexString(signature).ToLowerInvariant());
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("signature_verified_simulation_only", (await Json(response)).GetProperty("status").GetString());
        using var invalid = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new StringContent(payload) };
        invalid.Headers.Add("X-Hub-Signature-256", "sha256=" + new string('0', 64));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(invalid)).StatusCode);
    }

    [Fact]
    public async Task AssistantFailureTransitionsToHumanWithoutAssistantMessage()
    {
        await using var factory = Factory(new KeyValuePair<string, string?>("SIMULATION_ASSISTANT_FAIL", "true"));
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/simulation/messages", new { conversationId = ConversationId, messageId = "failure-1", text = "Olá" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("human", body.GetProperty("status").GetString());
        var messages = body.GetProperty("messages");
        Assert.Equal("customer", messages[messages.GetArrayLength() - 1].GetProperty("author").GetString());
    }
}
