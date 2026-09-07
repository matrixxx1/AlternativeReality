using System.Text.Json;
using AlternateEarth.Shared;
using Microsoft.Data.Sqlite;

namespace AlternateEarth.Server;

public sealed partial class SqliteRealityStore
{
    public async Task<IReadOnlyList<RecipeStudy>> LoadRecipeStudiesAsync(string realityId, string playerId, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT StudyJson FROM RecipeStudies WHERE RealityId=$r AND PlayerId=$p";
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$p", playerId);
        var studies = new List<RecipeStudy>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) studies.Add(JsonSerializer.Deserialize<RecipeStudy>(reader.GetString(0), SharedJson.Options)!);
        return studies;
    }

    public async Task SaveRecipeStudyAsync(string realityId, string playerId, RecipeStudy study, long craftingExperience,
        InventoryState? consumedInventory = null, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token);
        var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO LearnedCraftingRecipes (RealityId,PlayerId,RecipeId) VALUES ($r,$p,$recipe);
            INSERT INTO RecipeStudies (RealityId,PlayerId,RecipeId,StudyJson) VALUES ($r,$p,$recipe,$json)
              ON CONFLICT(RealityId,PlayerId,RecipeId) DO UPDATE SET StudyJson=excluded.StudyJson;
            INSERT INTO CraftingProgress (RealityId,PlayerId,Experience) VALUES ($r,$p,$xp)
              ON CONFLICT(RealityId,PlayerId) DO UPDATE SET Experience=excluded.Experience;
            """;
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$p", playerId);
        command.Parameters.AddWithValue("$recipe", study.RecipeId); command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(study, SharedJson.Options));
        command.Parameters.AddWithValue("$xp", craftingExperience);
        await command.ExecuteNonQueryAsync(token);
        if (consumedInventory is not null) await WriteInventoryAsync(connection, transaction, consumedInventory, token);
        await transaction.CommitAsync(token);
    }

    public async Task SaveCraftAttemptAsync(string realityId, string playerId, InventoryState inventory, long craftingExperience,
        string accountId, IReadOnlyList<CanonicalEntity>? furniture, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token);
        await WriteInventoryAsync(connection, transaction, inventory, token);
        var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO CraftingProgress (RealityId,PlayerId,Experience) VALUES ($r,$p,$xp) ON CONFLICT(RealityId,PlayerId) DO UPDATE SET Experience=excluded.Experience";
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$p", playerId); command.Parameters.AddWithValue("$xp", craftingExperience);
        await command.ExecuteNonQueryAsync(token);
        if (furniture is not null)
        {
            var destroy = connection.CreateCommand(); destroy.Transaction = transaction;
            destroy.CommandText = "UPDATE HomeFurniture SET FurnitureJson=$json,UpdatedUtc=$now WHERE RealityId=$r AND AccountId=$a";
            destroy.Parameters.AddWithValue("$json", JsonSerializer.Serialize(furniture, SharedJson.Options)); destroy.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            destroy.Parameters.AddWithValue("$r", realityId); destroy.Parameters.AddWithValue("$a", accountId);
            await destroy.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    public async Task<ProgressionProfile?> LoadProgressionAsync(string realityId, string playerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT ProfileJson FROM CharacterProgression WHERE RealityId=$reality AND PlayerId=$player";
        command.Parameters.AddWithValue("$reality", realityId);
        command.Parameters.AddWithValue("$player", playerId);
        var json = await command.ExecuteScalarAsync(cancellationToken) as string;
        return json is null ? null : JsonSerializer.Deserialize<ProgressionProfile>(json, SharedJson.Options);
    }

    public async Task SaveProgressionAsync(string realityId, string playerId, ProgressionProfile profile, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO CharacterProgression (RealityId,PlayerId,ProfileJson) VALUES ($reality,$player,$profile) ON CONFLICT(RealityId,PlayerId) DO UPDATE SET ProfileJson=excluded.ProfileJson";
        command.Parameters.AddWithValue("$reality", realityId);
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$profile", JsonSerializer.Serialize(profile, SharedJson.Options));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
