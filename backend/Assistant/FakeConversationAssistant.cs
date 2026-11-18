namespace Kenlo.Api.Assistant;

/// <summary>
/// Assistente puramente local e sem estado. Não acessa a rede, o Codex ou dados do catálogo.
/// </summary>
public sealed class FakeConversationAssistant(bool fail = false) : IConversationAssistant
{
    public Task<string> GenerateAsync(string conversationId, string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (fail)
        {
            throw new AssistantSimulationException();
        }

        // Respostas fixas não reproduzem conteúdo de clientes nem alegam dados reais.
        var answer = text.Contains("visita", StringComparison.OrdinalIgnoreCase)
            ? "[Simulação] Um corretor poderá ajudar com o agendamento da visita."
            : "[Simulação] Recebi sua mensagem. Um corretor poderá ajudar com sua busca.";

        return Task.FromResult(answer);
    }
}
