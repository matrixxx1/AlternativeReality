using System.Text.Json;
using AlternateEarth.Shared;
using Microsoft.Data.Sqlite;

namespace AlternateEarth.Server;

public sealed partial class SqliteRealityStore
{
    public async Task<HomeWorkshopProgress> LoadHomeWorkshopAsync(string realityId, string accountId, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT ProgressJson FROM HomeWorkshops WHERE RealityId=$r AND AccountId=$a";
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$a", accountId);
        var json = await command.ExecuteScalarAsync(token) as string;
        return json is null ? new([]) : JsonSerializer.Deserialize<HomeWorkshopProgress>(json, SharedJson.Options)!;
    }

    public async Task SaveHomeWorkshopAsync(string realityId, string accountId, HomeWorkshopProgress progress,
        PlayerState? purchase = null, InventoryState? inventory = null, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token);
        var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO HomeWorkshops (RealityId,AccountId,ProgressJson) VALUES ($r,$a,$json) ON CONFLICT(RealityId,AccountId) DO UPDATE SET ProgressJson=excluded.ProgressJson";
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$a", accountId);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(progress, SharedJson.Options));
        await command.ExecuteNonQueryAsync(token);
        if (purchase is not null)
        {
            var payment = connection.CreateCommand(); payment.Transaction = transaction;
            payment.CommandText = "UPDATE Characters SET WalletCents=$wallet,Version=$version WHERE Id=$id AND RealityId=$r";
            payment.Parameters.AddWithValue("$wallet", purchase.WalletCents); payment.Parameters.AddWithValue("$version", purchase.Version);
            payment.Parameters.AddWithValue("$id", purchase.Id); payment.Parameters.AddWithValue("$r", realityId);
            if (await payment.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("The Home purchase could not be saved.");
        }
        if (inventory is not null) await WriteInventoryAsync(connection, transaction, inventory, token);
        await transaction.CommitAsync(token);
    }
}
