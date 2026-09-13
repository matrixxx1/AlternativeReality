using System.Text.Json;

namespace AlternateEarth.Server;

public sealed class LocalAiConfiguration
{
    private readonly string _path;
    private readonly object _gate = new();
    private LocalAiSettings _current;

    public LocalAiConfiguration(string path, LocalAiSettings defaults)
    {
        _path = path;
        _current = File.Exists(path)
            ? JsonSerializer.Deserialize<LocalAiSettings>(File.ReadAllText(path)) ?? defaults
            : defaults;
    }

    public LocalAiSettings Current => Volatile.Read(ref _current);

    public static void Validate(LocalAiSettings settings)
    {
        if (!Uri.TryCreate(settings.BaseUrl?.Trim(), UriKind.Absolute, out var url) ||
            (url.Scheme != "http" && url.Scheme != "https") ||
            !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
            throw new ArgumentException("Enter an HTTP or HTTPS server URL without credentials, a query, or a fragment.");
        if (settings.RequestTimeoutSeconds is < 5 or > 300)
            throw new ArgumentException("Timeout must be between 5 and 300 seconds.");
        if (!string.IsNullOrWhiteSpace(settings.ProjectGuid) && !Guid.TryParse(settings.ProjectGuid, out _))
            throw new ArgumentException("Project GUID must be a valid GUID or empty.");
        if (string.IsNullOrWhiteSpace(settings.ProjectName) && string.IsNullOrWhiteSpace(settings.ProjectGuid))
            throw new ArgumentException("Enter a project name or GUID.");
        settings.BaseUrl = url.AbsoluteUri.TrimEnd('/');
        settings.ProjectName = settings.ProjectName?.Trim() ?? "";
        settings.ProjectGuid = settings.ProjectGuid?.Trim() ?? "";
    }

    public void Save(LocalAiSettings settings)
    {
        Validate(settings);
        lock (_gate)
        {
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(settings));
            File.Move(_path + ".tmp", _path, true);
            Volatile.Write(ref _current, settings);
        }
    }
}
