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
builder.Services.AddSingleton<DeterministicWorldGenerator>();
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
app.MapGet("/api/weather", (RealityWorld state) => Results.Ok(state.Weather));
app.MapGet("/api/account/me", async (HttpContext context, AccountService accounts) =>
{
    var login = await accounts.AuthenticateAsync(context.Request.Cookies[AccountService.CookieName], context.RequestAborted);
    return login is null ? Results.Unauthorized() : Results.Ok(new { login.AccountId, login.CharacterId, login.Username });
});
app.MapPost("/api/account/setup", async (HttpContext context, AccountService accounts, AccountRequest request) =>
{
    try
    {
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
    public bool ObjectPlacementEnabled { get; set; } = false;

    public RealityConfiguration ToConfiguration() => new(
        Id,
        Name,
        Seed,
        new GeographicArea(new GeoCoordinate(CenterLatitude, CenterLongitude), SizeMeters),
        MaximumPlayers: MaximumPlayers,
        ObjectPlacementEnabled: ObjectPlacementEnabled);
}
