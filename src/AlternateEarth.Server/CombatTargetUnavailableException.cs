namespace AlternateEarth.Server;

public sealed class CombatTargetUnavailableException(string targetId) : InvalidOperationException("That target is no longer available.")
{
    public string TargetId { get; } = targetId;
}
