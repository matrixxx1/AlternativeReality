namespace AlternateEarth.Server;

public sealed record NpcDialogueHistoryMessage(long Sequence, string Role, string Text, string SaidAtUtc);
public sealed record NpcDialogueHistoryPage(IReadOnlyList<NpcDialogueHistoryMessage> Messages, long? BeforeSequence);

public sealed partial class SqliteRealityStore
{
    public async Task SaveNpcDialogueMessageAsync(string realityId, string playerId, string npcId, string interactionId, string role, string text, CancellationToken token = default, string? saidAtUtc = null)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO NpcDialogueMessages(RealityId,PlayerId,NpcId,InteractionId,Role,Text,SaidAtUtc) VALUES($reality,$player,$npc,$interaction,$role,$text,$time)";
        command.Parameters.AddWithValue("$reality", realityId);
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$npc", npcId);
        command.Parameters.AddWithValue("$interaction", interactionId);
        command.Parameters.AddWithValue("$role", role);
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$time", saidAtUtc ?? DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task<NpcDialogueHistoryPage> LoadNpcDialogueHistoryAsync(string realityId, string playerId, string npcId, long? before = null, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Sequence,Role,Text,SaidAtUtc FROM NpcDialogueMessages WHERE RealityId=$reality AND PlayerId=$player AND NpcId=$npc AND Sequence<$before ORDER BY Sequence DESC LIMIT 51";
        command.Parameters.AddWithValue("$reality", realityId);
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$npc", npcId);
        command.Parameters.AddWithValue("$before", before ?? long.MaxValue);
        var messages = new List<NpcDialogueHistoryMessage>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) messages.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        var hasMore = messages.Count > 50;
        if (hasMore) messages.RemoveAt(50);
        messages.Reverse();
        return new(messages, hasMore ? messages[0].Sequence : null);
    }
}
