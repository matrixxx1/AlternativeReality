using Microsoft.Data.Sqlite;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class SqliteRealityStore
{
    public async Task SaveCameraExposureAsync(InventoryState inventory, string itemType, string? thumbnail, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token);
        await WriteInventoryAsync(connection, transaction, inventory, token);
        if (thumbnail is not null)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO PhotographImages(ItemType,Image) VALUES($item,$image)";
            command.Parameters.AddWithValue("$item", itemType);
            command.Parameters.AddWithValue("$image", Convert.FromBase64String(thumbnail["data:image/png;base64,".Length..]));
            await command.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    public async Task<byte[]?> LoadPhotographImageAsync(string itemType, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Image FROM PhotographImages WHERE ItemType=$item";
        command.Parameters.AddWithValue("$item", itemType);
        return await command.ExecuteScalarAsync(token) as byte[];
    }
}
