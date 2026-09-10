using System.Text.Json;
using AlternateEarth.Shared;
using Microsoft.Data.Sqlite;

namespace AlternateEarth.Server;

public sealed partial class SqliteRealityStore
{
    public async Task InitializeCasinoAsync(CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS CasinoRounds (RealityId TEXT NOT NULL, PlayerId TEXT NOT NULL, RoundId TEXT NOT NULL, RoundJson TEXT NOT NULL, PRIMARY KEY (RealityId,PlayerId,RoundId));";
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task<CasinoSavedRound?> LoadCasinoRoundAsync(string reality, string player, string? id, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT RoundJson FROM CasinoRounds WHERE RealityId=$r AND PlayerId=$p" +
            (id is null ? " ORDER BY rowid DESC LIMIT 1" : " AND RoundId=$id");
        command.Parameters.AddWithValue("$r", reality); command.Parameters.AddWithValue("$p", player);
        if (id is not null) command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteScalarAsync(token) is string json ? JsonSerializer.Deserialize<CasinoSavedRound>(json, SharedJson.Options) : null;
    }

    // Wallet, private cards, idempotency receipt, and bonus chest commit together.
    public async Task SaveCasinoTransitionAsync(string reality, PlayerState player, CasinoSavedRound round, LootDropState? reward, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token);
        var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "UPDATE Characters SET WalletCents=$w,Version=$v,UpdatedUtc=$now WHERE Id=$p AND RealityId=$r";
        command.Parameters.AddWithValue("$w", player.WalletCents); command.Parameters.AddWithValue("$v", player.Version);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$p", player.Id); command.Parameters.AddWithValue("$r", reality);
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("Your character is unavailable. Reconnect before betting.");
        command.CommandText = "INSERT INTO CasinoRounds(RealityId,PlayerId,RoundId,RoundJson) VALUES($r,$p,$id,$json) ON CONFLICT(RealityId,PlayerId,RoundId) DO UPDATE SET RoundJson=excluded.RoundJson";
        command.Parameters.AddWithValue("$id", round.RoundId); command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(round, SharedJson.Options));
        await command.ExecuteNonQueryAsync(token);
        if (reward is not null)
        {
            command.CommandText = "INSERT INTO PersistentWorldLoot(Id,RealityId,LootJson,CreatedUtc) VALUES($loot,$r,$lootJson,$now)";
            command.Parameters.AddWithValue("$loot", reward.Id); command.Parameters.AddWithValue("$lootJson", JsonSerializer.Serialize(reward, SharedJson.Options));
            await command.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }
}
