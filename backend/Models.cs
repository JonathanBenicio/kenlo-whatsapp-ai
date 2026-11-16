namespace Kenlo.Api;

// Public data transfer objects deliberately contain only fictional demonstration data.
public sealed record Property(Guid Id, string Code, string Title, string City, string Neighborhood,
    decimal Price, int Bedrooms, int Parking, string[] Features, string? ListingUrl, DateTimeOffset UpdatedAt);

public sealed record ConversationMessage(Guid Id, string Author, string Text, DateTimeOffset CreatedAt);
public sealed record Conversation(Guid Id, string ContactName, string Status, DateTimeOffset LastMessageAt,
    IReadOnlyList<ConversationMessage> Messages);
public sealed record PropertyFilter(string? City, string? Neighborhood, decimal? MaxPrice, int? Bedrooms, int? Parking);
public sealed record SimulationMessageRequest(Guid ConversationId, string MessageId, string Text);
public enum ReceiveStatus { Accepted, NotFound, Conflict }
public sealed record ReceiveResult(ReceiveStatus Status, Conversation? Conversation);
