using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Server:Urls"] ?? "http://0.0.0.0:5080");

var dataDirectory = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Server:DataDirectory"] ?? "../../data"));
Directory.CreateDirectory(dataDirectory);

var reality = builder.Configuration.GetSection("Reality").Get<RealitySettings>() ?? new RealitySettings();
var configuration = reality.ToConfiguration();

builder.Services.AddSingleton(configuration);
builder.Services.AddSingleton(new SqliteRealityStore(Path.Combine(dataDirectory, "reality.db")));
builder.Services.AddHttpClient("overpass", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Geo:OverpassUrl"] ?? "https://overpass-api.de/");
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AlternateEarth/0.1 (+https://github.com/matrixxx1/AlternativeReality)");
});
builder.Services.AddHttpClient("elevation", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Geo:ElevationUrl"] ?? "https://api.opentopodata.org/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AlternateEarth/0.1 (+https://github.com/matrixxx1/AlternativeReality)");
});
builder.Services.AddSingleton<IGeographicProvider>(services =>
{
    var factory = services.GetRequiredService<IHttpClientFactory>();
    IElevationProvider elevation = builder.Configuration.GetValue("Geo:RemoteElevation", true)
        ? new OpenTopoDataElevationProvider(factory.CreateClient("elevation"))
        : new FlatElevationProvider();
    return new OverpassGeographicProvider(factory.CreateClient("overpass"), Path.Combine(dataDirectory, "geo-cache"), elevation);
});
builder.Services.AddSingleton<DeterministicWorldGenerator>();
builder.Services.AddSingleton<RealityWorld>();
builder.Services.AddSingleton<RealitySocketHub>();

var app = builder.Build();
var sourceClientDirectory = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "../AlternateEarth.Client2D"));
var publishedClientDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
var clientDirectory = Directory.Exists(sourceClientDirectory)
    ? sourceClientDirectory
    : publishedClientDirectory;
app.Environment.WebRootPath = clientDirectory;
app.Environment.WebRootFileProvider = new PhysicalFileProvider(clientDirectory);
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

var store = app.Services.GetRequiredService<SqliteRealityStore>();
await store.InitializeAsync(configuration);
var world = app.Services.GetRequiredService<RealityWorld>();
await world.InitializeAsync();

app.MapGet("/api/status", (RealityWorld state) => Results.Ok(new
{
    status = "ok",
    protocolVersion = Protocol.Version,
    reality = state.Configuration.Name,
    players = state.PlayerCount,
    baseEntities = state.BaseEntityCount,
    realityEntities = state.RealityEntityCount,
    geographicProvider = state.GeographicProvider
}));
app.MapGet("/api/world", (RealityWorld state) => Results.Ok(state.CreateSnapshot()));
app.Map("/ws", async (HttpContext context, RealitySocketHub hub) => await hub.AcceptAsync(context));
app.MapFallbackToFile("index.html");

await app.RunAsync();

public sealed class RealitySettings
{
    public string Id { get; set; } = "vancouver-prototype";
    public string Name { get; set; } = "Vancouver Prototype Reality";
    public long Seed { get; set; } = 20260902;
    public double CenterLatitude { get; set; } = 45.6387;
    public double CenterLongitude { get; set; } = -122.6615;
    public int SizeMeters { get; set; } = 2000;
    public int MaximumPlayers { get; set; } = 32;

    public RealityConfiguration ToConfiguration() => new(
        Id,
        Name,
        Seed,
        new GeographicArea(new GeoCoordinate(CenterLatitude, CenterLongitude), SizeMeters),
        MaximumPlayers: MaximumPlayers);
}
