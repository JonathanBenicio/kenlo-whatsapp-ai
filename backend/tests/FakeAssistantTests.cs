using Kenlo.Api.Assistant;
using Xunit;

namespace Kenlo.Api.Tests;

public sealed class FakeAssistantTests
{
    [Fact]
    public async Task ResponsesAreDeterministicAndDoNotLeakAcrossConversations()
    {
        var assistant = new FakeConversationAssistant();
        const string secret = "dado privado de teste";
        var first = await assistant.GenerateAsync("conversa-1", $"Quero uma visita. {secret}");
        var second = await assistant.GenerateAsync("conversa-2", "Busco um imóvel.");
        var repeated = await assistant.GenerateAsync("conversa-1", $"Quero uma visita. {secret}");

        Assert.Equal(first, repeated);
        Assert.Contains("visita", first);
        Assert.DoesNotContain(secret, first);
        Assert.DoesNotContain(secret, second);
        Assert.DoesNotContain("visita", second);
        Assert.StartsWith("[Simulação]", second);
    }

    [Fact]
    public async Task ConfiguredFailureIsDeterministic()
    {
        var assistant = new FakeConversationAssistant(fail: true);
        await Assert.ThrowsAsync<AssistantSimulationException>(() => assistant.GenerateAsync("conversa-1", "Olá"));
        await Assert.ThrowsAsync<AssistantSimulationException>(() => assistant.GenerateAsync("conversa-2", "Olá"));
    }

    [Fact]
    public async Task CancellationIsHonoured()
    {
        var assistant = new FakeConversationAssistant();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => assistant.GenerateAsync("conversa-1", "Olá", cts.Token));
    }
}
