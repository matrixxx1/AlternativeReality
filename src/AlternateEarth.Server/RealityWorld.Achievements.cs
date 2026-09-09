using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private IReadOnlyList<string> GetAchievements(string playerId) => (_progression.GetValueOrDefault(playerId)?.Rewards ?? []).Where(r => r.StartsWith("achievement:")).Select(r => r[12..]).ToArray();
    private async Task UnlockAchievementAsync(string playerId, string title, CancellationToken token)
    {
        await AwardExperienceAsync(playerId, 1, "Questionable accomplishment: " + title, "achievement:" + title, randomize: false, cancellationToken: token);
    }
    public async Task SetAchievementTitleAsync(string playerId, string title, CancellationToken token)
    {
        if (!_players.TryGetValue(playerId, out var player) || !GetAchievements(playerId).Contains(title)) throw new InvalidOperationException("Earn that title before wearing it.");
        await SavePlayerAsync(player with { DisplayTitle = title, Version = player.Version + 1 }, token);
    }
}
