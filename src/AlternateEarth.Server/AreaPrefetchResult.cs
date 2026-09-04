namespace AlternateEarth.Server;

public sealed record AreaPrefetchResult(
    int Prepared,
    int AlreadyPrepared,
    long ElapsedMilliseconds,
    bool AlreadyRunning);
