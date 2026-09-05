using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AlternateEarth.Server;

public sealed class AccountService
{
    public const string CookieName = "alternative_reality_session";
    private static readonly Regex UsernamePattern = new("^[A-Za-z0-9 -]{3,10}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly SqliteRealityStore _store;
    public AccountService(SqliteRealityStore store) => _store = store;

    public async Task<AccountLogin> SetupOrLoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        username = (username ?? string.Empty).Trim();
        if (!UsernamePattern.IsMatch(username)) throw new InvalidOperationException("Username must be 3-10 characters and contain only letters, numbers, spaces, and dashes.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6 || password.Length > 72) throw new InvalidOperationException("Password must be 6-72 characters.");
        var existing = await _store.FindAccountByUsernameAsync(username, cancellationToken);
        if (existing is not null && !Verify(password, existing.PasswordSalt, existing.PasswordHash)) throw new InvalidOperationException("That username is already in use, or the password is incorrect.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        if (existing is null)
        {
            if (await _store.CharacterNameExistsAsync(username, cancellationToken)) throw new InvalidOperationException("That character name is already in use on this server.");
            var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)); var accountId = Guid.NewGuid().ToString("N"); var characterId = Guid.NewGuid().ToString("N");
            existing = new AccountRecord(accountId, username, HashPassword(password, salt), salt, HashToken(token), characterId);
            await _store.CreateAccountAsync(existing, username, cancellationToken);
        }
        else await _store.UpdateSessionAsync(existing.Id, HashToken(token), cancellationToken);
        return new AccountLogin(existing.Id, existing.ActiveCharacterId, existing.Username, token);
    }

    public async Task<AccountLogin?> AuthenticateAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null; var account = await _store.FindAccountBySessionHashAsync(HashToken(token), cancellationToken);
        return account is null ? null : new AccountLogin(account.Id, account.ActiveCharacterId, account.Username, token);
    }

    public Task<IReadOnlyList<AccountCharacter>> GetCharactersAsync(string accountId,CancellationToken cancellationToken=default)=>_store.LoadAccountCharactersAsync(accountId,cancellationToken);
    public async Task<AccountCharacter> AddCharacterAsync(string accountId,string name,CancellationToken cancellationToken=default)
    { name=(name??string.Empty).Trim();if(!UsernamePattern.IsMatch(name))throw new InvalidOperationException("Character name must be 3-10 characters and contain only letters, numbers, spaces, and dashes.");var characters=await _store.LoadAccountCharactersAsync(accountId,cancellationToken);if(characters.Count>=10)throw new InvalidOperationException("An account can have up to ten characters.");if(await _store.CharacterNameExistsAsync(name,cancellationToken))throw new InvalidOperationException("That character name is already in use on this server.");var character=new AccountCharacter(Guid.NewGuid().ToString("N"),name);await _store.AddAccountCharacterAsync(accountId,character,cancellationToken);return character; }
    public Task<IReadOnlyList<AccountRosterEntry>> GetRosterAsync(CancellationToken cancellationToken=default)=>_store.LoadAccountRosterAsync(cancellationToken);
    public Task MarkSeenAsync(string accountId,CancellationToken cancellationToken=default)=>_store.MarkAccountSeenAsync(accountId,DateTimeOffset.UtcNow,cancellationToken);
    public Task SetActiveCharacterAsync(string accountId,string characterId,CancellationToken cancellationToken=default)=>_store.SetActiveCharacterAsync(accountId,characterId,cancellationToken);
    public async Task DeleteCharacterAsync(string accountId,string activeCharacterId,string characterId,CancellationToken cancellationToken=default)
    { var characters=await _store.LoadAccountCharactersAsync(accountId,cancellationToken);if(characterId==activeCharacterId)throw new InvalidOperationException("Switch to another character before removing this one.");if(characters.Count<=1)throw new InvalidOperationException("An account must keep at least one character.");if(!characters.Any(c=>c.Id==characterId))throw new InvalidOperationException("Character does not belong to this account.");await _store.DeleteAccountCharacterAsync(accountId,characterId,cancellationToken); }

    private static string HashPassword(string password, string salt) => Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), 120_000, HashAlgorithmName.SHA256, 32));
    private static bool Verify(string password, string salt, string expected) => CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(HashPassword(password, salt)), Convert.FromBase64String(expected));
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

public sealed record AccountLogin(string AccountId, string CharacterId, string Username, string SessionToken);
public sealed record AccountRequest(string Username, string Password);
public sealed record CharacterRequest(string Name);
