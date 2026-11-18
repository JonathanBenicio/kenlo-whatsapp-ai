namespace Kenlo.Api.Assistant;

/// <summary>Interface do assistente de atendimento. Nenhuma implementação deve ser
/// interpretada como acesso ao Codex ou a um modelo real sem configuração explícita.</summary>
public interface IConversationAssistant
{
    Task<string> GenerateAsync(string conversationId, string text, CancellationToken cancellationToken = default);
}

/// <summary>Falha controlada do assistente de demonstração.</summary>
public sealed class AssistantSimulationException : Exception
{
    public AssistantSimulationException() : base("Falha simulada do assistente.") { }
}
