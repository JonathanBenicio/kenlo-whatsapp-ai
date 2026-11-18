namespace Kenlo.Api.Storage;

// Locking protects the in-process demo from concurrent duplicate messages and handoffs.
public sealed class MemoryDemoStore : IDemoStore
{
    private readonly object gate = new();
    private readonly List<Property> properties = DemoData.Properties().ToList();
    private readonly Dictionary<Guid, State> conversations = DemoData.Conversations().ToDictionary(x => x.Id, x => new State(x));

    private sealed class State(Conversation initial)
    {
        public Conversation Value = initial;
        public Dictionary<string, string> ExternalIds { get; } = new(StringComparer.Ordinal);
    }

    public Task<IReadOnlyList<Property>> SearchPropertiesAsync(PropertyFilter f, CancellationToken ct)
    {
        lock (gate)
        {
            IEnumerable<Property> q = properties;
            if (!string.IsNullOrWhiteSpace(f.City)) q = q.Where(p => p.City.Contains(f.City.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(f.Neighborhood)) q = q.Where(p => p.Neighborhood.Contains(f.Neighborhood.Trim(), StringComparison.OrdinalIgnoreCase));
            if (f.MaxPrice is not null) q = q.Where(p => p.Price <= f.MaxPrice);
            if (f.Bedrooms is not null) q = q.Where(p => p.Bedrooms >= f.Bedrooms);
            if (f.Parking is not null) q = q.Where(p => p.Parking >= f.Parking);
            return Task.FromResult<IReadOnlyList<Property>>(q.OrderBy(p => p.Code).ToArray());
        }
    }
    public Task<Property?> GetPropertyAsync(Guid id, CancellationToken ct) { lock (gate) return Task.FromResult(properties.FirstOrDefault(p => p.Id == id)); }
    public Task<IReadOnlyList<Conversation>> GetConversationsAsync(CancellationToken ct)
    { lock (gate) return Task.FromResult<IReadOnlyList<Conversation>>(conversations.Values.Select(s => s.Value).OrderByDescending(c => c.LastMessageAt).ToArray()); }
    public Task<Conversation?> GetConversationAsync(Guid id, CancellationToken ct)
    { lock (gate) return Task.FromResult(conversations.TryGetValue(id, out var state) ? state.Value : null); }
    public Task<Conversation?> SetConversationStatusAsync(Guid id, string status, CancellationToken ct)
    {
        lock (gate)
        {
            if (!conversations.TryGetValue(id, out var state)) return Task.FromResult<Conversation?>(null);
            state.Value = state.Value with { Status = status };
            return Task.FromResult<Conversation?>(state.Value);
        }
    }
    public Task<ReceiveResult> ReceiveAsync(Guid id, string messageId, string text, string? response, bool assistantFailed, CancellationToken ct)
    {
        lock (gate)
        {
            if (!conversations.TryGetValue(id, out var state)) return Task.FromResult(new ReceiveResult(ReceiveStatus.NotFound, null));
            if (state.ExternalIds.TryGetValue(messageId, out var existing))
                return Task.FromResult(new ReceiveResult(existing == text ? ReceiveStatus.Accepted : ReceiveStatus.Conflict, state.Value));
            var now = DateTimeOffset.UtcNow;
            var messages = state.Value.Messages.ToList();
            messages.Add(new(Guid.NewGuid(), "customer", text, now));
            var status = state.Value.Status;
            if (status == "automated" && assistantFailed) status = "human";
            else if (status == "automated" && response is not null)
                messages.Add(new(Guid.NewGuid(), "assistant", response, now));
            state.ExternalIds[messageId] = text;
            state.Value = state.Value with { Status = status, LastMessageAt = now, Messages = messages.ToArray() };
            return Task.FromResult(new ReceiveResult(ReceiveStatus.Accepted, state.Value));
        }
    }
    public Task UpsertPropertiesAsync(IReadOnlyList<Property> batch, CancellationToken ct)
    {
        lock (gate)
        {
            foreach (var item in batch)
            {
                var index = properties.FindIndex(p => p.Code == item.Code);
                if (index < 0) properties.Add(item);
                else properties[index] = item with { Id = properties[index].Id };
            }
        }
        return Task.CompletedTask;
    }
}

public static class DemoData
{
    private static readonly DateTimeOffset SampleDate = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    public static IReadOnlyList<Property> Properties() => [
        new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), "DEMO-001", "Apartamento fictício com varanda", "Campinas", "Taquaral", 650000, 3, 2, ["Varanda"], "https://example.invalid/imoveis/DEMO-001", SampleDate),
        new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"), "DEMO-002", "Casa fictícia com piscina", "Campinas", "Cambuí", 890000, 3, 2, ["Piscina"], "https://example.invalid/imoveis/DEMO-002", SampleDate),
        new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"), "DEMO-003", "Apartamento fictício compacto", "Jundiaí", "Centro", 450000, 2, 1, [], "https://example.invalid/imoveis/DEMO-003", SampleDate)
    ];
    public static IReadOnlyList<Conversation> Conversations() => [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Cliente de demonstração", "automated", SampleDate,
            [new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"), "customer", "[Simulação] Olá, procuro um apartamento.", SampleDate)])
    ];
}
