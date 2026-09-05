using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Server:Urls"] ?? "http://0.0.0.0:5080");

var dataDirectory = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Server:DataDirectory"] ?? "../../data"));
Directory.CreateDirectory(dataDirectory);

var reality = builder.Configuration.GetSection("Reality").Get<RealitySettings>() ?? new RealitySettings();
var databasePath = Path.Combine(dataDirectory, "reality.db");
var locationPath = Path.Combine(dataDirectory, "reality-location.json");
var setupMarkerPath = Path.Combine(dataDirectory, ".reality-setup-required");
var savedLocation = RealitySetupState.Load(locationPath);
var setupRequired = savedLocation is null && (!File.Exists(databasePath) || File.Exists(setupMarkerPath));
if (savedLocation is not null)
{
    reality.CenterLatitude = savedLocation.Latitude;
    reality.CenterLongitude = savedLocation.Longitude;
}
if (setupRequired && !File.Exists(setupMarkerPath)) File.WriteAllText(setupMarkerPath, "Initial geographic setup is pending.");
var configuration = reality.ToConfiguration();
var realitySetup = new RealitySetupState(setupRequired, locationPath, setupMarkerPath);

builder.Services.AddSingleton(configuration);
builder.Services.AddSingleton(realitySetup);
builder.Services.AddSingleton(new SqliteRealityStore(databasePath));
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
builder.Services.AddHttpClient("weather", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Weather:Url"] ?? "https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AlternateEarth/0.2 (+https://github.com/matrixxx1/AlternativeReality)");
});
builder.Services.AddSingleton<IGeographicProvider>(services =>
{
    var factory = services.GetRequiredService<IHttpClientFactory>();
    IElevationProvider elevation = builder.Configuration.GetValue("Geo:RemoteElevation", true)
        ? new OpenTopoDataElevationProvider(factory.CreateClient("elevation"))
        : new FlatElevationProvider();
    return new OverpassGeographicProvider(factory.CreateClient("overpass"), Path.Combine(dataDirectory, "geo-cache"), elevation);
});
builder.Services.AddSingleton(services => new DeterministicWorldGenerator(
    services.GetRequiredService<IGeographicProvider>(),
    Path.Combine(dataDirectory, "world-cache")));
builder.Services.AddSingleton<IWeatherProvider>(services =>
    new OpenMeteoWeatherProvider(services.GetRequiredService<IHttpClientFactory>().CreateClient("weather")));
builder.Services.AddSingleton<RealityWorld>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<RealitySocketHub>();
builder.Services.AddHostedService<WeatherRefreshService>();
builder.Services.AddHostedService<ActorSimulationService>();

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
if (!realitySetup.Required) await world.InitializeAsync();

app.MapGet("/api/reality/setup", (RealitySetupState setup, RealityWorld state) => Results.Ok(new
{
    required = setup.Required,
    initialized = state.IsInitialized,
    latitude = state.Configuration.Area.Center.Latitude,
    longitude = state.Configuration.Area.Center.Longitude
}));
app.MapPost("/api/reality/setup", async (RealitySetupRequest request, RealitySetupState setup, RealityWorld state, CancellationToken cancellationToken) =>
{
    try
    {
        if (!setup.Required) return Results.BadRequest(new { message = "This reality's starting location is already configured." });
        var coordinate = new GeoCoordinate(request.Latitude, request.Longitude);
        await state.ConfigureInitialLocationAsync(coordinate, cancellationToken);
        await setup.CompleteAsync(coordinate, cancellationToken);
        return Results.Ok(new { latitude = coordinate.Latitude, longitude = coordinate.Longitude, state.Configuration.Name });
    }
    catch (InvalidOperationException exception) { return Results.BadRequest(new { message = exception.Message }); }
});

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
app.MapGet("/api/diagnostics", async (HttpContext context, AccountService accounts, RealityWorld state) =>
{
    var login = await accounts.AuthenticateAsync(context.Request.Cookies[AccountService.CookieName], context.RequestAborted);
    if (login is null) return Results.Unauthorized();
    if (!state.IsGodModeEnabled(login.CharacterId)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    using var process = Process.GetCurrentProcess();
    return Results.Ok(new
    {
        activeOperation = state.ActiveMapOperation,
        activeOperations = state.ActiveMapOperations,
        lastAreaLoadMilliseconds = state.LastAreaLoadMilliseconds,
        lastAreaPrefetchMilliseconds = state.LastAreaPrefetchMilliseconds,
        loadedAreas = state.LoadedAreaCount,
        preparedAreas = state.PreparedAreaCount,
        baseEntities = state.BaseEntityCount,
        realityEntities = state.RealityEntityCount,
        actors = state.ActorCount,
        elevationSamples = state.ElevationSampleCount,
        workingSetMegabytes = process.WorkingSet64 / 1_048_576d,
        managedMegabytes = GC.GetTotalMemory(false) / 1_048_576d,
        uptimeSeconds = (DateTimeOffset.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds
    });
});
app.MapGet("/api/world", (RealityWorld state) => Results.Ok(state.CreateSnapshot()));
app.MapGet("/api/weather", (RealityWorld state) => Results.Ok(state.Weather));
app.MapPost("/api/world/prefetch", async (HttpContext context, AccountService accounts, RealityWorld state, PrefetchAreaRequest request) =>
{
    var login = await accounts.AuthenticateAsync(context.Request.Cookies[AccountService.CookieName], context.RequestAborted);
    if (login is null) return Results.Unauthorized();
    return Results.Ok(await state.PrefetchAreasAsync(request, context.RequestAborted));
});
app.MapGet("/api/account/me", async (HttpContext context, AccountService accounts) =>
{
    var login = await accounts.AuthenticateAsync(context.Request.Cookies[AccountService.CookieName], context.RequestAborted);
    return login is null ? Results.Unauthorized() : Results.Ok(new { login.AccountId, login.CharacterId, login.Username });
});
app.MapGet("/api/account/roster", async (AccountService accounts,RealityWorld state,CancellationToken cancellationToken) =>
{
    var online=state.ActiveAccountIds;
    var roster=await accounts.GetRosterAsync(cancellationToken);
    return Results.Ok(roster.Select(account=>new{account.Username,online=online.Contains(account.AccountId),account.LastSeenUtc,characters=account.Characters.Select(character=>new{character.Name,label=$"[{account.Username}] {character.Name}"})}));
});
app.MapPost("/api/account/setup", async (HttpContext context, AccountService accounts, RealitySetupState setup, AccountRequest request) =>
{
    try
    {
        if (setup.Required) return Results.BadRequest(new { message = "Choose the server's starting location before creating an account." });
        var login = await accounts.SetupOrLoginAsync(request.Username, request.Password, context.RequestAborted);
        context.Response.Cookies.Append(AccountService.CookieName, login.SessionToken, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Path = "/",
            MaxAge = TimeSpan.FromDays(365),
            Expires = DateTimeOffset.UtcNow.AddDays(365)
        });
        return Results.Ok(new { login.AccountId, login.CharacterId, login.Username, login.SessionToken });
    }
    catch (InvalidOperationException exception) { return Results.BadRequest(new { message = exception.Message }); }
});
app.MapGet("/api/account/characters", async (HttpContext context, AccountService accounts) =>
{
    var login=await accounts.AuthenticateAsync(context.Request.Cookies[AccountService.CookieName],context.RequestAborted);if(login is null)return Results.Unauthorized();
    return Results.Ok(new{activeCharacterId=login.CharacterId,characters=await accounts.GetCharactersAsync(login.AccountId,context.RequestAborted)});
});
app.MapPost("/api/account/characters", async (HttpContext context,AccountService accounts,RealityWorld state,CharacterRequest request) =>
{
    try{var login=await accounts.AuthenticateAsync(context.Request.Cookies[AccountService.CookieName],context.RequestAborted);if(login is null)return Results.Unauthorized();if(!state.IsInOwnHome(login.CharacterId))return Results.BadRequest(new{message="Characters can only be managed inside your base."});var character=await accounts.AddCharacterAsync(login.AccountId,request.Name,context.RequestAborted);return Results.Ok(character);}catch(InvalidOperationException exception){return Results.BadRequest(new{message=exception.Message});}
});
app.MapPost("/api/account/characters/{characterId}/select", async (HttpContext context,AccountService accounts,RealityWorld state,string characterId) =>
{
    try{var login=await accounts.AuthenticateAsync(context.Request.Cookies[AccountService.CookieName],context.RequestAborted);if(login is null)return Results.Unauthorized();var characters=await accounts.GetCharactersAsync(login.AccountId,context.RequestAborted);var target=characters.FirstOrDefault(c=>c.Id==characterId)??throw new InvalidOperationException("Character does not belong to this account.");await state.PrepareCharacterSwitchAsync(login.CharacterId,target.Id,target.Name,context.RequestAborted);await accounts.SetActiveCharacterAsync(login.AccountId,target.Id,context.RequestAborted);return Results.Ok(target);}catch(InvalidOperationException exception){return Results.BadRequest(new{message=exception.Message});}
});
app.MapDelete("/api/account/characters/{characterId}",async(HttpContext context,AccountService accounts,RealityWorld state,string characterId)=>
{
    try{var login=await accounts.AuthenticateAsync(context.Request.Cookies[AccountService.CookieName],context.RequestAborted);if(login is null)return Results.Unauthorized();if(!state.IsInOwnHome(login.CharacterId))return Results.BadRequest(new{message="Characters can only be managed inside your base."});await accounts.DeleteCharacterAsync(login.AccountId,login.CharacterId,characterId,context.RequestAborted);return Results.Ok();}catch(InvalidOperationException exception){return Results.BadRequest(new{message=exception.Message});}
});
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
    public bool PvpEnabled { get; set; } = true;
    public bool ObjectPlacementEnabled { get; set; } = false;

    public RealityConfiguration ToConfiguration() => new(
        Id,
        Name,
        Seed,
        new GeographicArea(new GeoCoordinate(CenterLatitude, CenterLongitude), SizeMeters),
        MaximumPlayers: MaximumPlayers,
        PvpEnabled: PvpEnabled,
        ObjectPlacementEnabled: ObjectPlacementEnabled);
}

public sealed record RealitySetupRequest(double Latitude, double Longitude);
