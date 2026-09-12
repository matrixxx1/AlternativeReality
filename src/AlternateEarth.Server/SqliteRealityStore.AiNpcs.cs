using Microsoft.Data.Sqlite;

namespace AlternateEarth.Server;

public sealed partial class SqliteRealityStore
{
    public async Task<IReadOnlyList<HomeAiNpcRecord>> LoadHomeAiNpcsAsync(string realityId, CancellationToken cancellationToken = default)
    {
        var records = new List<HomeAiNpcRecord>();
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT BuildingId,ActorId,ActorName,Generation,SpawnedUtc,VacantSinceUtc FROM HomeAiNpcs WHERE RealityId=$reality ORDER BY BuildingId";
        command.Parameters.AddWithValue("$reality", realityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            records.Add(new(realityId, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3),
                DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture), ReadDate(reader, 5)));
        return records;
    }

    public async Task SaveHomeAiNpcAsync(HomeAiNpcRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO HomeAiNpcs(RealityId,BuildingId,ActorId,ActorName,Generation,SpawnedUtc,VacantSinceUtc)
            VALUES($reality,$building,$actor,$name,$generation,$spawned,$vacant)
            ON CONFLICT(RealityId,BuildingId) DO UPDATE SET ActorId=excluded.ActorId,ActorName=excluded.ActorName,
                Generation=excluded.Generation,SpawnedUtc=excluded.SpawnedUtc,VacantSinceUtc=excluded.VacantSinceUtc
            """;
        command.Parameters.AddWithValue("$reality", record.RealityId);
        command.Parameters.AddWithValue("$building", record.BuildingId);
        command.Parameters.AddWithValue("$actor", record.ActorId);
        command.Parameters.AddWithValue("$name", record.ActorName);
        command.Parameters.AddWithValue("$generation", record.Generation);
        command.Parameters.AddWithValue("$spawned", record.SpawnedUtc.ToString("O"));
        command.Parameters.AddWithValue("$vacant", record.VacantSinceUtc is null ? DBNull.Value : record.VacantSinceUtc.Value.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteHomeAiNpcAsync(string realityId, string buildingId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM HomeAiNpcs WHERE RealityId=$reality AND BuildingId=$building";
        command.Parameters.AddWithValue("$reality", realityId); command.Parameters.AddWithValue("$building", buildingId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed record HomeAiNpcRecord(string RealityId, string BuildingId, string ActorId, string ActorName, int Generation, DateTimeOffset SpawnedUtc, DateTimeOffset? VacantSinceUtc = null);
