namespace AlternateEarth.Shared;

public sealed record WorldSoundEvent(string Id, string Sound, WorldPosition Position, string LocationId = "outdoor",
    string? SpeakerId = null, string? Text = null);
