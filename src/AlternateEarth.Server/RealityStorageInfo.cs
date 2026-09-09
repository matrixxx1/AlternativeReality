namespace AlternateEarth.Server;

public sealed record RealityStorageInfo(long TotalBytes, long MapBytes, int MapBlocks, long DatabaseBytes, long LegacySourceBytes, bool Complete)
{
    public static RealityStorageInfo Read(string directory)
    {
        long total = 0, maps = 0, database = 0, legacy = 0;
        var blocks = 0; var complete = true;
        // Skip links so configured storage cannot accidentally include another volume or directory tree.
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        try
        {
            foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*", options))
            {
                try
                {
                    var size = file.Length; total += size;
                    var relative = Path.GetRelativePath(directory, file.FullName).Replace('\\', '/');
                    if (relative.StartsWith("world-cache/", StringComparison.Ordinal) && file.Name.StartsWith("world-") && file.Extension == ".json") { maps += size; blocks++; }
                    if (relative is "reality.db" or "reality.db-wal" or "reality.db-shm") database += size;
                    if (relative.StartsWith("geo-cache/", StringComparison.Ordinal)) legacy += size;
                }
                catch (IOException) { complete = false; }
                catch (UnauthorizedAccessException) { complete = false; }
            }
        }
        catch (IOException) { complete = false; }
        catch (UnauthorizedAccessException) { complete = false; }
        return new(total, maps, blocks, database, legacy, complete);
    }
}
