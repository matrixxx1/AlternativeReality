using AlternateEarth.Shared;

namespace AlternateEarth.Server;

// Pure, independently testable ballot rules; RealityWorld serializes calls.
public sealed class ServerVote
{
    public DateTimeOffset EndsAtUtc { get; private set; }
    public int Round { get; private set; } = 1;
    public string[] Options { get; private set; }
    private readonly Dictionary<string, string> _votes = new();
    public ServerVote(DateTimeOffset now, IEnumerable<string> choices)
    {
        Options = new[] { "random" }.Concat(choices.Take(3)).Distinct().ToArray(); EndsAtUtc = now.AddMinutes(1);
    }
    public void SyncPlayers(IEnumerable<string> ids)
    {
        var connected = ids.ToHashSet();
        foreach (var id in _votes.Keys.Except(connected).ToArray()) _votes.Remove(id);
        if (Round == 1) foreach (var id in connected) _votes.TryAdd(id, "random");
    }
    public void Cast(string player, string option, DateTimeOffset now)
    {
        if (now >= EndsAtUtc || !Options.Contains(option)) throw new InvalidOperationException("That vote is closed or the option is unavailable.");
        _votes[player] = option;
    }
    public string? Finish(DateTimeOffset now)
    {
        if (now < EndsAtUtc) return null;
        var counts = Options.ToDictionary(id => id, id => _votes.Values.Count(v => v == id));
        var tied = counts.Where(p => p.Value == counts.Values.Max()).Select(p => p.Key).ToArray();
        if (tied.Length == 1) return tied[0];
        Options = tied; _votes.Clear(); Round++; EndsAtUtc = now.AddMinutes(1); return null;
    }
    public ServerVoteState Snapshot(IReadOnlyDictionary<string,string> names) => new(EndsAtUtc, Round,
        Options.Select(id => new VoteOption(id, id == "random" ? "Random" : InversionCatalog.All.First(e => e.Id == id).Name,
            _votes.Where(v => v.Value == id).Select(v => names.GetValueOrDefault(v.Key, v.Key)).ToArray())).ToArray());
}
