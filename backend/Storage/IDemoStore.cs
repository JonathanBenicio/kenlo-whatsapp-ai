namespace Kenlo.Api.Storage;

public interface IDemoStore
{
    Task<IReadOnlyList<Property>> SearchPropertiesAsync(PropertyFilter filter, CancellationToken cancellationToken);
    Task<Property?> GetPropertyAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Conversation>> GetConversationsAsync(CancellationToken cancellationToken);
    Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken);
    Task<Conversation?> SetConversationStatusAsync(Guid id, string status, CancellationToken cancellationToken);
    Task<ReceiveResult> ReceiveAsync(Guid id, string messageId, string text, string? response, bool assistantFailed, CancellationToken cancellationToken);
    Task UpsertPropertiesAsync(IReadOnlyList<Property> properties, CancellationToken cancellationToken);
}
