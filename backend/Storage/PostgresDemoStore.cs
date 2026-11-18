using Npgsql;

namespace Kenlo.Api.Storage;

// Only used when STORE_MODE=postgres and a server-side connection is configured.
// Migrations must be applied explicitly; the application never changes the schema.
public sealed class PostgresDemoStore(NpgsqlDataSource dataSource) : IDemoStore
{
    private const string PropertyColumns = "id,code,title,city,neighborhood,price,bedrooms,parking,features,listing_url,updated_at";

    public async Task<IReadOnlyList<Property>> SearchPropertiesAsync(PropertyFilter f, CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand($"SELECT {PropertyColumns} FROM public.properties WHERE (@city = '' OR strpos(lower(city), lower(@city)) > 0) AND (@neighborhood = '' OR strpos(lower(neighborhood), lower(@neighborhood)) > 0) AND (@maxprice < 0 OR price <= @maxprice) AND (@bedrooms < 0 OR bedrooms >= @bedrooms) AND (@parking < 0 OR parking >= @parking) ORDER BY code");
        cmd.Parameters.AddWithValue("city", f.City?.Trim() ?? "");
        cmd.Parameters.AddWithValue("neighborhood", f.Neighborhood?.Trim() ?? "");
        cmd.Parameters.AddWithValue("maxprice", f.MaxPrice ?? -1m);
        cmd.Parameters.AddWithValue("bedrooms", f.Bedrooms ?? -1);
        cmd.Parameters.AddWithValue("parking", f.Parking ?? -1);
        var result = new List<Property>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(ReadProperty(reader));
        return result;
    }
    public async Task<Property?> GetPropertyAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand($"SELECT {PropertyColumns} FROM public.properties WHERE id=@id");
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadProperty(reader) : null;
    }
    private static Property ReadProperty(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetDecimal(5), r.GetInt32(6), r.GetInt32(7), r.GetFieldValue<string[]>(8), r.IsDBNull(9) ? null : r.GetString(9), r.GetFieldValue<DateTimeOffset>(10));

    public async Task<IReadOnlyList<Conversation>> GetConversationsAsync(CancellationToken ct)
    {
        var ids = new List<Guid>();
        await using (var cmd = dataSource.CreateCommand("SELECT id FROM public.conversations ORDER BY last_message_at DESC"))
        await using (var reader = await cmd.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0));
        var result = new List<Conversation>();
        foreach (var id in ids) { var conversation = await GetConversationAsync(id, ct); if (conversation is not null) result.Add(conversation); }
        return result;
    }
    public async Task<Conversation?> GetConversationAsync(Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await ReadConversationAsync(connection, null, id, ct);
    }
    private static async Task<Conversation?> ReadConversationAsync(NpgsqlConnection connection, NpgsqlTransaction? tx, Guid id, CancellationToken ct)
    {
        string name, status;
        DateTimeOffset last;
        await using (var cmd = new NpgsqlCommand("SELECT contact_name,status,last_message_at FROM public.conversations WHERE id=@id", connection, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            name = r.GetString(0); status = r.GetString(1); last = r.GetFieldValue<DateTimeOffset>(2);
        }
        var messages = new List<ConversationMessage>();
        await using (var cmd = new NpgsqlCommand("SELECT id,author,body,created_at FROM public.messages WHERE conversation_id=@id ORDER BY created_at,id", connection, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) messages.Add(new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetFieldValue<DateTimeOffset>(3)));
        }
        return new(id, name, status, last, messages);
    }
    public async Task<Conversation?> SetConversationStatusAsync(Guid id, string status, CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand("UPDATE public.conversations SET status=@status WHERE id=@id");
        cmd.Parameters.AddWithValue("status", status); cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync(ct) == 0 ? null : await GetConversationAsync(id, ct);
    }
    public async Task<ReceiveResult> ReceiveAsync(Guid id, string messageId, string text, string? response, bool assistantFailed, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        string status;
        // Serializes incoming events and human handoff for the same conversation.
        await using (var cmd = new NpgsqlCommand("SELECT status FROM public.conversations WHERE id=@id FOR UPDATE", connection, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is not string value) return new(ReceiveStatus.NotFound, null);
            status = value;
        }
        await using (var cmd = new NpgsqlCommand("SELECT body FROM public.messages WHERE conversation_id=@id AND external_id=@external", connection, tx))
        {
            cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("external", messageId);
            if (await cmd.ExecuteScalarAsync(ct) is string existing)
            {
                var current = await ReadConversationAsync(connection, tx, id, ct);
                await tx.CommitAsync(ct);
                return new(existing == text ? ReceiveStatus.Accepted : ReceiveStatus.Conflict, current);
            }
        }
        var now = DateTimeOffset.UtcNow;
        await InsertMessageAsync(connection, tx, id, messageId, "customer", text, now, ct);
        if (status == "automated" && assistantFailed) status = "human";
        else if (status == "automated" && response is not null)
            await InsertMessageAsync(connection, tx, id, null, "assistant", response, now.AddTicks(10), ct);
        await using (var cmd = new NpgsqlCommand("UPDATE public.conversations SET status=@status,last_message_at=@now WHERE id=@id", connection, tx))
        {
            cmd.Parameters.AddWithValue("status", status); cmd.Parameters.AddWithValue("now", now); cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        var conversation = await ReadConversationAsync(connection, tx, id, ct);
        await tx.CommitAsync(ct);
        return new(ReceiveStatus.Accepted, conversation);
    }
    private static async Task InsertMessageAsync(NpgsqlConnection connection, NpgsqlTransaction tx, Guid conversationId, string? externalId, string author, string body, DateTimeOffset createdAt, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("INSERT INTO public.messages (id,conversation_id,external_id,author,body,created_at) VALUES (@id,@conversation,@external,@author,@body,@created)", connection, tx);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid()); cmd.Parameters.AddWithValue("conversation", conversationId);
        cmd.Parameters.Add(new NpgsqlParameter("external", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object?)externalId ?? DBNull.Value });
        cmd.Parameters.AddWithValue("author", author); cmd.Parameters.AddWithValue("body", body); cmd.Parameters.AddWithValue("created", createdAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task UpsertPropertiesAsync(IReadOnlyList<Property> properties, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        foreach (var p in properties)
        {
            const string sql = "INSERT INTO public.properties (id,code,title,city,neighborhood,price,bedrooms,parking,features,listing_url,updated_at) VALUES (@id,@code,@title,@city,@neighborhood,@price,@bedrooms,@parking,@features,@url,@updated) ON CONFLICT(code) DO UPDATE SET title=EXCLUDED.title,city=EXCLUDED.city,neighborhood=EXCLUDED.neighborhood,price=EXCLUDED.price,bedrooms=EXCLUDED.bedrooms,parking=EXCLUDED.parking,features=EXCLUDED.features,listing_url=EXCLUDED.listing_url,updated_at=EXCLUDED.updated_at";
            await using var cmd = new NpgsqlCommand(sql, connection, tx);
            cmd.Parameters.AddWithValue("id", p.Id); cmd.Parameters.AddWithValue("code", p.Code); cmd.Parameters.AddWithValue("title", p.Title);
            cmd.Parameters.AddWithValue("city", p.City); cmd.Parameters.AddWithValue("neighborhood", p.Neighborhood); cmd.Parameters.AddWithValue("price", p.Price);
            cmd.Parameters.AddWithValue("bedrooms", p.Bedrooms); cmd.Parameters.AddWithValue("parking", p.Parking); cmd.Parameters.AddWithValue("features", p.Features);
            cmd.Parameters.Add(new NpgsqlParameter("url", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object?)p.ListingUrl ?? DBNull.Value }); cmd.Parameters.AddWithValue("updated", p.UpdatedAt.ToUniversalTime());
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
