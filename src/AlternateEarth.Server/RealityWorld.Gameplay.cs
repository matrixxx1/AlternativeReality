using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public const long BasePurchasePriceCents = 35_000_000;
    public const double MaximumBackpackWeightPounds = 50;
    public const int MaximumWeaponSlots = 3;
    public const int MaximumQuestSlots = 3;
    public const int MaximumOtherSlots = 6;
    private static readonly string[] FriendlyHumanNames =
    [
        "Joe", "Sam", "Dave", "Maria", "Priya", "Marcus", "Elena", "Theo",
        "Grace", "Jordan", "Leah", "Omar", "Nina", "Henry", "Maya", "Luis"
    ];
    private static readonly ItemConfiguration[] DefaultItemConfigurations =
    {
        new("fist","Fist","Permanent melee weapon; consumes no ammunition",.25,1.6,0,0,false,true,WeightPounds:0,Category:InventoryCategory.Weapon),
        new("rock","Rock","Thrown weapon and ammunition",1,25,1,100,true,false,"rock",WeightPounds:.5,Category:InventoryCategory.Weapon),
        new("ballBearing","Ball bearing","Slingshot ammunition",0,0,5,200,WeightPounds:.02),
        new("knife","Knife","Short melee weapon",2,1.6,2_000,4_000,true,true,WeightPounds:.6,Category:InventoryCategory.Weapon),
        new("sword","Sword","Extended melee weapon",5,2.3,30_000,50_000,true,true,WeightPounds:3,Category:InventoryCategory.Weapon),
        new("slingshot","Slingshot","Ranged weapon; consumes ball bearings",2,60,5_000,15_000,true,true,"ballBearing",WeightPounds:.4,Category:InventoryCategory.Weapon),
        new("crossbow","Crossbow","Ranged weapon; consumes arrows",3,100,30_000,50_000,true,true,"arrow",WeightPounds:6.5,Category:InventoryCategory.Weapon),
        new("arrow","Arrow","Crossbow ammunition",0,0,5,500,WeightPounds:.08),
        new("pistol","Pistol","Ranged weapon; consumes bullets",5,50,100_000,300_000,true,true,"bullet",WeightPounds:2,Category:InventoryCategory.Weapon),
        new("rifle","Rifle","Long-range weapon; consumes bullets",7,200,300_000,600_000,true,true,"bullet",WeightPounds:7.5,Category:InventoryCategory.Weapon),
        new("bullet","Bullet","Pistol and rifle ammunition",0,0,25,500,WeightPounds:.04),
        new("skateboard","Skateboard","Fast paved-surface travel",0,0,20_000,30_000,true,true,null,10.5,WeightPounds:5),
        new("bike","Bike","Faster travel with mild off-road penalty",0,0,40_000,50_000,true,true,null,10.5,WeightPounds:0),
        new("eBike","E-bike","Electric travel between a bike and dirt bike; battery lasts one mile",0,0,400_000,500_000,true,true,null,21.5,WeightPounds:0,CarriedInBackpack:false),
        new("dirtBike","Dirt bike","Parked motorized travel up to 40 mph",0,0,300_000,500_000,true,true,null,36.5,WeightPounds:250,CarriedInBackpack:false),
        new("motorcycle","Motorcycle","Parked motorized travel up to 90 mph",0,0,500_000,1_000_000,true,true,null,86.5,WeightPounds:0,CarriedInBackpack:false),
        new("gallonOfGas","Gallon of gas","Refuels the selected motor vehicle",0,0,500,1_000,WeightPounds:6.3),
        new("inflatableRaft","Inflatable raft","Safe travel through deep water",0,0,45_000,65_000,true,true,null,2.75,WeightPounds:0),
        new("flashlight","Flashlight","Directional light",0,0,1_000,5_000,true,true,null,0,50,WeightPounds:.5),
        new("lantern","Lantern","Circular area light",0,0,5_000,10_000,true,true,null,0,30,WeightPounds:1.5),
        new("laser","Laser","Straight light beam until collision",0,0,20_000,40_000,true,true,null,0,150,WeightPounds:.25),
        new("magicHikingShoes","Magic hiking shoes","Additive movement and stamina bonus",0,0,10_000,40_000,true,true,null,3.5,WeightPounds:2),
        new("magicRunningShoes","Magic running shoes","Larger additive movement and conditional stamina bonus",0,0,10_000,40_000,true,true,null,7,WeightPounds:1.5),
        new("hat","Sun hat","Halves water drain and slightly reduces heat gain",0,0,3_000,7_500,true,true,WeightPounds:.25),
        new("coolingHat","Cooling hat","Reduces heat gain and helps body heat fall in hot weather",0,0,2_500,7_000,true,true,WeightPounds:.3),
        new("warmHat","Warm knit hat","Reduces warmth loss in cold weather",0,0,2_000,6_500,true,true,WeightPounds:.35),
        new("tShirt","T-shirt","Light shirt that sheds heat easily",0,0,1_500,4_500,true,true,WeightPounds:.4),
        new("coolingShirt","Cooling shirt","Reduces heat generated while moving and speeds cooling",0,0,3_000,9_000,true,true,WeightPounds:.45),
        new("longSleeveShirt","Long-sleeve shirt","Light insulation for cool weather",0,0,2_500,7_500,true,true,WeightPounds:.65),
        new("sweater","Sweater","Moderate insulation and movement warmth",0,0,3_500,11_000,true,true,WeightPounds:1.2),
        new("lightJacket","Light jacket","Strong cool-weather insulation without winter-jacket weight",0,0,6_000,18_000,true,true,WeightPounds:2),
        new("winterJacket","Winter jacket","Heavy insulation and warmth retention in cold weather",0,0,8_000,30_000,true,true,WeightPounds:3.5),
        new("coolingShorts","Cooling shorts","Greatly reduce movement heat and shed heat quickly",0,0,2_500,7_500,true,true,WeightPounds:.5),
        new("warmingPants","Warming pants","Retain warmth and generate extra warmth while moving",0,0,4_500,12_000,true,true,WeightPounds:1.5),
        new("water","Water","One half-liter bottle; restores water and 2 hearts",0,0,50,200,WeightPounds:1.1),
        new("food","Food","Packed meal; restores stamina and 2 hearts",0,0,200,500,WeightPounds:.75),
        new("areaMap","Map of this block","Permanently reveals the current geographic block",0,0,100,100_000,true,true,WeightPounds:.05),
        new("pencil","Pencil","A useful everyday writing tool",0,0,25,150,WeightPounds:.02),
        new("pen","Pen","A dependable ink pen",0,0,50,300,WeightPounds:.03),
        new("marker","Marker","A permanent marker",0,0,100,500,WeightPounds:.05),
        new("sprayPaint","Spray paint","A can of colored spray paint",0,0,500,1_500,WeightPounds:1),
        new("book","Book","A readable book",0,0,300,3_000,WeightPounds:1.5),
        new("calculator","Calculator","A pocket calculator",0,0,500,4_000,WeightPounds:.4),
        new("cellPhone","Cell phone","A found mobile phone",0,0,2_000,40_000,WeightPounds:.45),
        new("newspaper","Newspaper","A delivered newspaper worth 25 cents",0,0,25,25,WeightPounds:.35),
        new("wood","Wood","Usable wood cut from a tree",0,0,100,600,WeightPounds:2),
        new("kindling","Kindling","Dry kindling cut from a bush",0,0,25,150,WeightPounds:.25)
        ,new("metal","Scrap metal","Reusable metal recovered from litter or a mailbox",0,0,50,500,WeightPounds:1)
        ,new("lockPickSet","Lock pick set","Reusable tool with a 15% chance to open a locked door",0,0,2_500,9_000,true,true,WeightPounds:.4)
    };
    private readonly ConcurrentDictionary<string, ItemConfiguration> _itemConfigurations = new(DefaultItemConfigurations.ToDictionary(item => item.ItemType, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
    private static readonly MovementConfiguration DefaultMovementConfiguration = new(
        3.5,
        100,
        new Dictionary<TerrainType, double>
        {
            [TerrainType.Grass] = -.75, [TerrainType.Forest] = -1.5, [TerrainType.Sand] = -2.5,
            [TerrainType.Pavement] = 0, [TerrainType.Road] = 0, [TerrainType.Sidewalk] = 0,
            [TerrainType.ShallowWater] = -3, [TerrainType.DeepWater] = -3.25, [TerrainType.Mud] = -2.75
        },
        new Dictionary<TravelMode, double>
        {
            [TravelMode.Walk] = 0, [TravelMode.Run] = 3.5, [TravelMode.Skateboard] = 0,
            [TravelMode.Bike] = 0, [TravelMode.Raft] = 0, [TravelMode.DirtBike] = 0, [TravelMode.Motorcycle] = 0, [TravelMode.EBike] = 0
        });
    private MovementConfiguration _movementConfiguration = DefaultMovementConfiguration;
    private static readonly string[] WeaponPowerOrder = ["rifle", "sword", "pistol", "crossbow", "knife", "slingshot", "rock", "fist"];
    private static readonly HashSet<string> HatItems = new(["hat", "coolingHat", "warmHat"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ShirtItems = new(["tShirt", "coolingShirt", "longSleeveShirt", "sweater", "lightJacket", "winterJacket"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> PantsItems = new(["coolingShorts", "warmingPants"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> OffhandItems = new(["flashlight", "lantern", "laser"], StringComparer.OrdinalIgnoreCase);
    private const double DirtBikeTankGallons = 2;
    private const double MotorcycleTankGallons = 4;
    private readonly ConcurrentDictionary<string, Dictionary<string, int>> _inventories = new();
    private readonly ConcurrentDictionary<(string Player, string Actor), double> _relationships = new();
    private readonly ConcurrentDictionary<string, DungeonState> _dungeons = new();
    private readonly ConcurrentDictionary<string, WorldPosition> _returnPositions = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastIdleHeal = new();
    private readonly ConcurrentDictionary<(string Player, string Merchant, long Rotation), TradeQuote> _tradeQuotes = new();
    private readonly ConcurrentDictionary<string, LootDropState> _loot = new();
    private readonly ConcurrentDictionary<string, TreasureChestState> _outdoorChests = new();
    private readonly ConcurrentDictionary<(string Actor, string Player), DateTimeOffset> _lastActorAttack = new();
    private readonly ConcurrentDictionary<string, string> _playerAccounts = new();
    private readonly ConcurrentDictionary<string, string> _baseBuildings = new();
    private readonly ConcurrentDictionary<(string Player, string Area), byte> _revealedWorldAreas = new();
    private readonly ConcurrentDictionary<string, List<CanonicalEntity>> _homeFurniture = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, int>> _homeItemStorage = new();
    private readonly ConcurrentDictionary<(string Player, string Quest), QuestState> _quests = new();
    private readonly ConcurrentDictionary<(string Player, string Quest), QuestState> _questOffers = new();
    private readonly ConcurrentDictionary<string, PendingPoliceResponse> _pendingPolice = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastWantedDecay = new();
    private readonly ConcurrentDictionary<string, byte> _pickedLocks = new();
    private readonly SemaphoreSlim _homeFurnitureLock = new(1, 1);
    private readonly SemaphoreSlim _homeItemStorageLock = new(1, 1);

    public PlayerPrivateState GetPrivateState(string playerId)
    {
        var inventory = GetInventoryState(playerId);
        _players.TryGetValue(playerId, out var player);
        var dungeon = player is not null && player.LocationId != "outdoor" && _dungeons.TryGetValue(player.LocationId, out var found) ? WithDiscovery(playerId, found) : null;
        var relationships = _relationships.Where(pair => pair.Key.Player == playerId)
            .Select(pair => new RelationshipState(playerId, pair.Key.Actor, pair.Value)).ToArray();
        var location = player?.LocationId ?? "outdoor";
        var chests = location == "outdoor" ? _outdoorChests.Values.ToArray() : dungeon?.Chests ?? Array.Empty<TreasureChestState>();
        var loot = _loot.Values.Where(drop => drop.LocationId == location).ToArray();
        BaseState? baseState = null;
        if (_playerAccounts.TryGetValue(playerId, out var accountId) && _baseBuildings.TryGetValue(accountId, out var buildingId) && _baseEntities.TryGetValue(buildingId, out var building))
        {
            var door = _baseEntities.Values.FirstOrDefault(entity => entity.Kind == EntityKind.Door && entity.Properties.GetValueOrDefault("buildingId") == buildingId);
            if (door is not null)
            {
                var squareFeet = BuildingSquareFeet(building);
                baseState = new BaseState(buildingId, door.Id, door.Position, player?.Name ?? "Explorer", squareFeet, CalculateBuildingPriceCents(building));
            }
        }
        var serverConfiguration = new ServerConfigurationState(_itemConfigurations.Values.OrderBy(item => item.DisplayName).ToArray(), _movementConfiguration);
        var revealedAreas = _revealedWorldAreas.Keys.Where(key => key.Player == playerId).Select(key => key.Area).OrderBy(key => key).ToArray();
        IReadOnlyList<CanonicalEntity>? homeStorage = null;
        InventoryState? homeItemStorage = null;
        if (dungeon?.IsHome == true && _playerAccounts.TryGetValue(playerId, out var homeAccount) && _homeFurniture.TryGetValue(homeAccount, out var furniture))
        {
            homeStorage = furniture.Where(IsStoredFurniture).ToArray();
            homeItemStorage = GetHomeItemStorage(homeAccount);
        }
        var quests = _quests.Where(pair => pair.Key.Player == playerId).Select(pair => pair.Value).OrderBy(quest => quest.Status).ThenBy(quest => quest.Title).ToArray();
        return new PlayerPrivateState(inventory, dungeon, relationships, chests, loot, baseState, ServerConfiguration: serverConfiguration, RevealedWorldAreas: revealedAreas, HomeStorage: homeStorage, HomeItemStorage: homeItemStorage, Quests: quests);
    }

    public async Task<PlayerState> SetGodModeAsync(string playerId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        var hikingShoesOn = enabled ? player.MagicHikingShoesOn : player.MagicHikingShoesOn && InventoryQuantity(playerId, "magicHikingShoes") > 0;
        var runningShoesOn = enabled ? player.MagicRunningShoesOn : player.MagicRunningShoesOn && InventoryQuantity(playerId, "magicRunningShoes") > 0;
        if (hikingShoesOn && runningShoesOn) runningShoesOn = false;
        var offhand = ActiveOffhand(player);
        if (!enabled && offhand != "none" && InventoryQuantity(playerId, offhand) <= 0) offhand = "none";
        var updated = player with
        {
            GodMode = enabled,
            Stamina = enabled ? player.MaximumStamina : player.Stamina,
            Water = enabled ? player.MaximumWater : player.Water,
            WalletCents = enabled ? Math.Max(50_000, player.WalletCents) : player.WalletCents,
            HealthHearts = enabled ? Math.Max(1, player.HealthHearts) : player.HealthHearts,
            TravelMode = !enabled && TravelModeUnavailable(playerId, player) ? TravelMode.Walk : player.TravelMode,
            FlashlightOn = offhand == "flashlight",
            LanternOn = offhand == "lantern",
            LaserOn = offhand == "laser",
            MagicHikingShoesOn = hikingShoesOn,
            MagicRunningShoesOn = runningShoesOn,
            EquippedHat = RetainedEquipment(playerId, player.EquippedHat, HatItems, enabled),
            EquippedShirt = RetainedEquipment(playerId, player.EquippedShirt, ShirtItems, enabled),
            EquippedPants = RetainedEquipment(playerId, player.EquippedPants, PantsItems, enabled),
            HatOn = (enabled ? player.EquippedHat : RetainedEquipment(playerId, player.EquippedHat, HatItems, false)).Equals("hat", StringComparison.OrdinalIgnoreCase),
            EquippedWeapon = enabled ? player.EquippedWeapon : BestUsableWeapon(playerId, player.EquippedWeapon, false),
            BodyHeat = enabled ? 50 : player.BodyHeat,
            Version = player.Version + 1
        };
        await SavePlayerAsync(updated, cancellationToken); return updated;
    }

    private bool TravelModeUnavailable(string playerId, PlayerState player) => player.TravelMode switch
    {
        TravelMode.Skateboard => InventoryQuantity(playerId, "skateboard") <= 0,
        TravelMode.Bike => InventoryQuantity(playerId, "bike") <= 0,
        TravelMode.EBike => InventoryQuantity(playerId, "eBike") <= 0 || player.EBikeRemainingMeters <= 0,
        TravelMode.Raft => InventoryQuantity(playerId, "inflatableRaft") <= 0,
        TravelMode.DirtBike => InventoryQuantity(playerId, "dirtBike") <= 0 || player.DirtBikeGasGallons <= 0,
        TravelMode.Motorcycle => InventoryQuantity(playerId, "motorcycle") <= 0 || player.MotorcycleGasGallons <= 0,
        _ => false
    };

    public Task<PlayerState> SetLightsAsync(string playerId, bool flashlight, bool lantern, bool laser, CancellationToken cancellationToken = default) =>
        SetEquipmentAsync(playerId, "offhand", laser ? "laser" : lantern ? "lantern" : flashlight ? "flashlight" : null, cancellationToken);

    public Task<PlayerState> SetMagicHikingShoesAsync(string playerId, bool enabled, CancellationToken cancellationToken = default) =>
        SetEquipmentAsync(playerId, "shoes", enabled ? "magicHikingShoes" : null, cancellationToken);

    public Task<PlayerState> SetMagicRunningShoesAsync(string playerId, bool enabled, CancellationToken cancellationToken = default) =>
        SetEquipmentAsync(playerId, "shoes", enabled ? "magicRunningShoes" : null, cancellationToken);

    public async Task<PlayerState> SetEquipmentAsync(string playerId, string slot, string? itemType, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        slot = (slot ?? string.Empty).Trim().ToLowerInvariant();
        itemType = string.IsNullOrWhiteSpace(itemType) ? null : itemType.Trim();
        PlayerState updated;
        if (slot == "shoes")
        {
            if (itemType is not null && itemType is not ("magicHikingShoes" or "magicRunningShoes")) throw new InvalidOperationException("That item cannot be worn as shoes.");
            if (itemType is not null && !player.GodMode && InventoryQuantity(playerId, itemType) <= 0) throw new InvalidOperationException($"You need {DisplayItem(itemType)} in your inventory.");
            updated = player with { MagicHikingShoesOn = itemType == "magicHikingShoes", MagicRunningShoesOn = itemType == "magicRunningShoes", SpeedMetersPerSecond = 0, Version = player.Version + 1 };
        }
        else if (slot == "hat")
        {
            if (itemType is not null && !HatItems.Contains(itemType)) throw new InvalidOperationException("That item cannot be worn as a hat.");
            if (itemType is not null && !player.GodMode && InventoryQuantity(playerId, itemType) <= 0) throw new InvalidOperationException($"You need {DisplayItem(itemType)} in your inventory.");
            var equipped = itemType ?? "none";
            updated = player with { EquippedHat = equipped, HatOn = equipped.Equals("hat", StringComparison.OrdinalIgnoreCase), Version = player.Version + 1 };
        }
        else if (slot == "shirt")
        {
            if (itemType is not null && !ShirtItems.Contains(itemType)) throw new InvalidOperationException("That item cannot be worn as a shirt or jacket.");
            if (itemType is not null && !player.GodMode && InventoryQuantity(playerId, itemType) <= 0) throw new InvalidOperationException($"You need {DisplayItem(itemType)} in your inventory.");
            updated = player with { EquippedShirt = itemType ?? "none", Version = player.Version + 1 };
        }
        else if (slot == "pants")
        {
            if (itemType is not null && !PantsItems.Contains(itemType)) throw new InvalidOperationException("That item cannot be worn as pants or shorts.");
            if (itemType is not null && !player.GodMode && InventoryQuantity(playerId, itemType) <= 0) throw new InvalidOperationException($"You need {DisplayItem(itemType)} in your inventory.");
            updated = player with { EquippedPants = itemType ?? "none", Version = player.Version + 1 };
        }
        else if (slot == "offhand")
        {
            if (itemType is not null && !OffhandItems.Contains(itemType)) throw new InvalidOperationException("That item cannot be equipped in your offhand.");
            if (itemType is not null && !player.GodMode && InventoryQuantity(playerId, itemType) <= 0) throw new InvalidOperationException($"You need {DisplayItem(itemType)} in your inventory.");
            updated = player with
            {
                FlashlightOn = itemType?.Equals("flashlight", StringComparison.OrdinalIgnoreCase) == true,
                LanternOn = itemType?.Equals("lantern", StringComparison.OrdinalIgnoreCase) == true,
                LaserOn = itemType?.Equals("laser", StringComparison.OrdinalIgnoreCase) == true,
                Version = player.Version + 1
            };
        }
        else if (slot == "weapon")
        {
            itemType ??= "none";
            itemType = itemType.ToLowerInvariant();
            if (itemType != "none" && !WeaponPowerOrder.Contains(itemType)) throw new InvalidOperationException("That item cannot be equipped as a weapon.");
            if (itemType != "none" && !player.GodMode && !OwnsWeapon(playerId, itemType)) throw new InvalidOperationException($"You need {DisplayItem(itemType)} in your backpack.");
            updated = player with { EquippedWeapon = itemType, Version = player.Version + 1 };
        }
        else throw new InvalidOperationException("That equipment slot is not available yet.");
        await SavePlayerAsync(updated, cancellationToken);
        return updated;
    }

    private bool playerIsGod(string playerId) => _players.TryGetValue(playerId, out var player) && player.GodMode;

    public async Task<IReadOnlyList<PlayerState>> AdvanceVitalsAsync(TimeSpan elapsed, CancellationToken cancellationToken)
    {
        var changed = new List<PlayerState>(); var now = DateTimeOffset.UtcNow;
        foreach (var pair in _players.ToArray())
        {
            var player = pair.Value; var updated = player;
            var idle = _lastMovement.TryGetValue(pair.Key, out var lastMove) ? now - lastMove : TimeSpan.Zero;
            if (player.WantedLevel > 0)
            {
                var lastDecay = _lastWantedDecay.GetOrAdd(pair.Key, now);
                var levels = (int)((now - lastDecay).TotalMinutes / 5);
                if (levels > 0) { updated = updated with { WantedLevel = Math.Max(0, player.WantedLevel - levels) }; _lastWantedDecay[pair.Key] = lastDecay.AddMinutes(levels * 5); }
            }
            if (player.GodMode)
            {
                updated = updated with
                {
                    Stamina = player.MaximumStamina, Water = player.MaximumWater,
                    WalletCents = Math.Max(50_000, player.WalletCents),
                    HealthHearts = Math.Min(player.MaximumHealthHearts, Math.Max(1, player.HealthHearts) + (elapsed.TotalSeconds / 5)),
                    BodyHeat = 50
                };
            }
            else
            {
                if (idle >= TimeSpan.FromSeconds(1) && player.Stamina < player.MaximumStamina)
                    updated = updated with { Stamina = Math.Min(player.MaximumStamina, player.Stamina + (.25 * elapsed.TotalSeconds)) };
                var wearingHat = player.HatOn && InventoryQuantity(pair.Key, "hat") > 0;
                if (!(player.WaterProtectedUntilUtc > now)) updated = updated with { Water = Math.Max(0, updated.Water - WorldNavigation.WaterDrain(elapsed.TotalSeconds, wearingHat)) };
                updated = ApplyBodyTemperature(updated, idle, elapsed);
                if (updated.BodyHeat >= 85)
                    updated = updated with { Stamina = Math.Max(0, updated.Stamina - (.05 + (updated.BodyHeat - 85) / 100) * elapsed.TotalSeconds) };
                if (updated.BodyHeat <= .001)
                    updated = updated with { HealthHearts = Math.Max(0, updated.HealthHearts - elapsed.TotalSeconds / 120) };
                if (updated.HealthHearts <= 0) updated = ResetPlayer(updated);
                else if (idle >= TimeSpan.FromSeconds(30) && updated.BodyHeat > 0 && player.HealthHearts < player.MaximumHealthHearts &&
                    (!_lastIdleHeal.TryGetValue(pair.Key, out var lastHeal) || now - lastHeal >= TimeSpan.FromSeconds(30)))
                {
                    updated = updated with { HealthHearts = Math.Min(player.MaximumHealthHearts, updated.HealthHearts + .5) };
                    _lastIdleHeal[pair.Key] = now;
                }
            }
            if (updated.Stamina == player.Stamina && updated.Water == player.Water && updated.WalletCents == player.WalletCents && updated.HealthHearts == player.HealthHearts && updated.BodyHeat == player.BodyHeat && updated.WantedLevel == player.WantedLevel) continue;
            updated = updated with { SpeedMetersPerSecond = idle > TimeSpan.FromSeconds(.5) ? 0 : updated.SpeedMetersPerSecond, Version = player.Version + 1 };
            if (await SavePlayerAsync(updated, cancellationToken)) changed.Add(updated);
        }
        foreach (var expired in _loot.Where(pair => pair.Value.ExpiresAtUtc <= now).Select(pair => pair.Key).ToArray()) _loot.TryRemove(expired, out _);
        foreach (var expired in _outdoorChests.Where(pair => pair.Value.ExpiresAtUtc <= now).Select(pair => pair.Key).ToArray()) _outdoorChests.TryRemove(expired, out _);
        return changed;
    }

    private PlayerState ApplyBodyTemperature(PlayerState player, TimeSpan idle, TimeSpan elapsed)
    {
        var seconds = elapsed.TotalSeconds;
        if (player.LocationId != "outdoor" || !Weather.IsAvailable)
            return player with { BodyHeat = MoveToward(player.BodyHeat, 50, .08 * seconds) };
        var temperatureF = Weather.TemperatureCelsius * 9 / 5 + 32;
        var moving = idle < TimeSpan.FromSeconds(1) && player.SpeedMetersPerSecond > .05;
        var motion = moving ? Math.Clamp(player.SpeedMetersPerSecond / WorldNavigation.MilesPerHour(3.5), .25, 3) : 0;
        var clothing = ThermalProtection(player);
        var heat = player.BodyHeat;
        if (temperatureF < 50)
        {
            var coldLoss = (50 - temperatureF) / 30 * .035 * (1 - clothing.ColdProtection) * seconds;
            var movementWarmth = moving ? (.012 + .016 * clothing.MovementWarmth) * motion * seconds : 0;
            heat += movementWarmth - coldLoss;
        }
        else if (temperatureF > 80 && moving)
        {
            var heatGain = (temperatureF - 80) / 20 * .045 * motion * (1 - clothing.HeatProtection) * seconds;
            heat += heatGain;
        }
        else
        {
            heat = MoveToward(heat, 50, (.025 + .04 * clothing.CoolingBonus) * seconds);
        }
        if (!moving && heat > 50) heat = MoveToward(heat, 50, (.025 + .05 * clothing.CoolingBonus) * seconds);
        return player with { BodyHeat = Math.Clamp(heat, 0, player.MaximumBodyHeat) };
    }

    private static double MoveToward(double value, double target, double amount) => value < target ? Math.Min(target, value + amount) : Math.Max(target, value - amount);

    private static (double ColdProtection, double MovementWarmth, double HeatProtection, double CoolingBonus) ThermalProtection(PlayerState player)
    {
        var cold = 0d; var warmth = 0d; var heat = 0d; var cooling = 0d;
        foreach (var item in new[] { player.EquippedHat, player.EquippedShirt, player.EquippedPants })
        {
            var effect = item switch
            {
                "warmHat" => (.18, .08, 0d, 0d), "hat" => (0d, 0d, .10, .08), "coolingHat" => (0d, 0d, .20, .18),
                "tShirt" => (0d, 0d, .08, .10), "coolingShirt" => (0d, 0d, .38, .38), "longSleeveShirt" => (.12, .08, 0d, 0d),
                "sweater" => (.35, .20, 0d, 0d), "lightJacket" => (.48, .24, 0d, 0d), "winterJacket" => (.65, .35, 0d, 0d),
                "coolingShorts" => (0d, 0d, .48, .55), "warmingPants" => (.32, .42, 0d, 0d),
                _ => (0d, 0d, 0d, 0d)
            };
            cold += effect.Item1; warmth += effect.Item2; heat += effect.Item3; cooling += effect.Item4;
        }
        return (Math.Clamp(cold, 0, .85), Math.Clamp(warmth, 0, 1), Math.Clamp(heat, 0, .85), Math.Clamp(cooling, 0, 1));
    }

    private string RetainedEquipment(string playerId, string itemType, IReadOnlySet<string> allowed, bool godMode)
    {
        if (string.IsNullOrWhiteSpace(itemType) || itemType == "none" || !allowed.Contains(itemType)) return "none";
        return godMode || InventoryQuantity(playerId, itemType) > 0 ? itemType : "none";
    }

    private static string ActiveOffhand(PlayerState player) => player.LaserOn ? "laser" : player.LanternOn ? "lantern" : player.FlashlightOn ? "flashlight" : "none";

    public async Task<PlayerState> ConsumeItemAsync(string playerId, string itemType, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        var normalized = itemType.Trim().ToLowerInvariant(); if (normalized is not ("food" or "water" or "gallonofgas")) throw new InvalidOperationException("That item cannot be consumed.");
        if (normalized == "gallonofgas")
        {
            if (player.TravelMode is not (TravelMode.DirtBike or TravelMode.Motorcycle)) throw new InvalidOperationException("Select your dirt bike or motorcycle before adding gas.");
            var current = FuelGallons(player);
            var capacity = player.TravelMode == TravelMode.DirtBike ? DirtBikeTankGallons : MotorcycleTankGallons;
            if (current >= capacity - .0001) throw new InvalidOperationException($"Your {VehicleName(player.TravelMode)} tank is already full.");
        }
        if (!RemoveInventory(playerId, normalized, 1)) throw new InvalidOperationException($"You do not have any {normalized}.");
        var now = DateTimeOffset.UtcNow;
        var updated = normalized == "food"
            ? player with { Stamina = player.MaximumStamina, HealthHearts = Math.Min(player.MaximumHealthHearts, player.HealthHearts + 2), FoodProtectedUntilUtc = now.AddMinutes(5), Version = player.Version + 1 }
            : normalized == "water"
                ? player with { Water = player.MaximumWater, HealthHearts = Math.Min(player.MaximumHealthHearts, player.HealthHearts + 2), WaterProtectedUntilUtc = now.AddMinutes(5), Version = player.Version + 1 }
                : player.TravelMode == TravelMode.DirtBike
                    ? player with { DirtBikeGasGallons = Math.Min(DirtBikeTankGallons, player.DirtBikeGasGallons + 1), Version = player.Version + 1 }
                    : player with { MotorcycleGasGallons = Math.Min(MotorcycleTankGallons, player.MotorcycleGasGallons + 1), Version = player.Version + 1 };
        await SaveInventoryAsync(playerId, cancellationToken); await SavePlayerAsync(updated, cancellationToken); return updated;
    }

    public async Task<(PlayerState Player, DungeonState Dungeon)> EnterDungeonAsync(string playerId, string doorId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || player.LocationId != "outdoor") throw new InvalidOperationException("You cannot enter that interior now.");
        var door = _baseEntities.Values.FirstOrDefault(entity => entity.Id == doorId && entity.Kind == EntityKind.Door) ?? throw new InvalidOperationException("That door does not exist.");
        if (player.Position.Distance2D(door.Position) > 6) throw new InvalidOperationException("Move closer to the door first.");
        var buildingId = door.Properties.GetValueOrDefault("buildingId") ?? throw new InvalidOperationException("The door has no building.");
        var building = _baseEntities.Values.First(entity => entity.Id == buildingId);
        var ownsBase = _playerAccounts.TryGetValue(playerId, out var accountId) && _baseBuildings.GetValueOrDefault(accountId) == buildingId;
        if (!ownsBase && IsBuildingLocked(building) && !_pickedLocks.ContainsKey($"{playerId}:{doorId}:{CurrentDoorLockCycle}")) throw new InvalidOperationException("This building is locked. Door locks change every four hours.");
        var dungeonId = ownsBase ? $"home:{accountId}:{buildingId}" : $"dungeon:{buildingId}:{playerId}:{Guid.NewGuid():N}";
        if (ownsBase) await ResetDungeonSessionAsync(playerId, dungeonId, cancellationToken);
        var dungeon = ownsBase ? GenerateHome(dungeonId, building) : CreateDungeonSession(dungeonId, building);
        _dungeons[dungeonId] = dungeon;
        foreach (var actor in dungeon.Actors)
            _relationships[(playerId, actor.Id)] = actor.FriendRating;
        _returnPositions[playerId] = player.Position;
        var discovery = new HashSet<string>();
        if (!dungeon.IsHome)
        {
            if (dungeon.IsStore)
            {
                for (var x = 0; x <= dungeon.Width; x += 3)
                for (var y = 0; y <= dungeon.Height; y += 3)
                    RevealDungeonCells(discovery, x, y);
            }
            else RevealDungeonCells(discovery, dungeon.Exit.X, dungeon.Exit.Y);
            foreach (var cell in discovery) await _store.SaveDiscoveryAsync(Configuration.Id, playerId, dungeonId, cell, cancellationToken);
        }
        var updated = player with { LocationId = dungeonId, Position = dungeon.Exit, Terrain = TerrainType.Pavement, TravelMode = TravelMode.Walk, SpeedMetersPerSecond = 0, Version = player.Version + 2 };
        await SavePlayerAsync(updated, cancellationToken); return (updated, dungeon with { RevealedCells = discovery.ToArray() });
    }

    public async Task<(PlayerState Player, DungeonState Dungeon)> ChangeDungeonLevelAsync(string playerId, int direction, CancellationToken cancellationToken = default)
    {
        if (direction is not (-1 or 1)) throw new InvalidOperationException("Choose stairs up or down.");
        if (!_players.TryGetValue(playerId, out var player) || player.LocationId == "outdoor" || !_dungeons.TryGetValue(player.LocationId, out var current) || current.IsHome)
            throw new InvalidOperationException("You are not inside a multi-level dungeon.");
        var stairs = current.Stairs ?? throw new InvalidOperationException("This dungeon has no stairs.");
        if (current.LevelCount <= 1) throw new InvalidOperationException("This dungeon has no stairs.");
        if (!player.GodMode && player.Position.Distance2D(stairs) > 3) throw new InvalidOperationException("Move closer to the stairs first.");
        var targetLevel = current.Level + direction;
        if (targetLevel < 1) throw new InvalidOperationException("You are already on the first level.");
        if (targetLevel > current.LevelCount) throw new InvalidOperationException("You are already on the last level.");
        if (!_baseEntities.TryGetValue(current.BuildingId, out var building)) throw new InvalidOperationException("That building is unavailable.");
        var sessionId = current.SessionId ?? current.Id;
        var targetId = targetLevel == 1 ? sessionId : $"{sessionId}:level:{targetLevel}";
        var target = _dungeons.GetOrAdd(targetId, _ => GenerateDungeonFloor(sessionId, building, targetLevel, current.LevelCount, current.Stairs));
        foreach (var actor in target.Actors) _relationships.TryAdd((playerId, actor.Id), actor.FriendRating);
        var updated = player with { LocationId = target.Id, Position = target.Stairs ?? target.Exit, SpeedMetersPerSecond = 0, Version = player.Version + 2 };
        await SavePlayerAsync(updated, cancellationToken);
        await RevealAsync(updated, target, cancellationToken);
        return (updated, WithDiscovery(playerId, target));
    }

    public async Task<PlayerState> ExitDungeonAsync(string playerId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || player.LocationId == "outdoor") throw new InvalidOperationException("You are not inside a dungeon or Home.");
        var dungeonId = player.LocationId;
        var destination = _returnPositions.TryRemove(playerId, out var saved) ? saved : Navigation.FindNearestWalkable(new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center));
        var updated = player with { LocationId = "outdoor", Position = destination, Terrain = Navigation.TerrainAt(destination.X, destination.Y), SpeedMetersPerSecond = 0, Version = player.Version + 2 };
        await SavePlayerAsync(updated, cancellationToken);
        await ResetDungeonSessionAsync(playerId, dungeonId, cancellationToken);
        return updated;
    }

    private async Task ResetDungeonSessionAsync(string playerId, string dungeonId, CancellationToken cancellationToken)
    {
        var sessionId = _dungeons.TryGetValue(dungeonId, out var current) ? current.SessionId ?? dungeonId : dungeonId;
        var floorIds = _dungeons.Keys.Where(id => id == sessionId || id.StartsWith(sessionId + ":level:", StringComparison.Ordinal)).ToArray();
        if (floorIds.Length == 0) floorIds = [dungeonId];
        foreach (var floorId in floorIds)
        {
            _dungeons.TryRemove(floorId, out _);
            await _store.ResetDungeonStateAsync(Configuration.Id, playerId, floorId, cancellationToken);
        }
        foreach (var relationship in _relationships.Keys.Where(key => key.Player == playerId && key.Actor.StartsWith(sessionId + ":", StringComparison.Ordinal)).ToArray()) _relationships.TryRemove(relationship, out _);
        foreach (var drop in _loot.Where(pair => pair.Value.LocationId == sessionId || pair.Value.LocationId.StartsWith(sessionId + ":level:", StringComparison.Ordinal)).Select(pair => pair.Key).ToArray()) _loot.TryRemove(drop, out _);
        foreach (var quote in _tradeQuotes.Keys.Where(key => key.Player == playerId && key.Merchant.StartsWith(sessionId + ":", StringComparison.Ordinal)).ToArray()) _tradeQuotes.TryRemove(quote, out _);
    }

    public async Task<(PlayerState Player, long PriceCents)> PurchaseBaseAsync(string playerId, PurchaseBaseRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || player.LocationId != "outdoor") throw new InvalidOperationException("A base can only be purchased from outside its door.");
        if (!_playerAccounts.TryGetValue(playerId, out var accountId)) throw new InvalidOperationException("An authenticated account is required to purchase a base.");
        var door = _baseEntities.Values.FirstOrDefault(entity => entity.Id == request.DoorId && entity.Kind == EntityKind.Door) ?? throw new InvalidOperationException("That door does not exist.");
        if (player.Position.Distance2D(door.Position) > 6) throw new InvalidOperationException("Move within 6 meters of the door before purchasing this base.");
        var buildingId = door.Properties.GetValueOrDefault("buildingId") ?? throw new InvalidOperationException("The door has no building.");
        var building = _baseEntities.GetValueOrDefault(buildingId) ?? throw new InvalidOperationException("That building is unavailable.");
        if (StoreProfileForBuilding(building) is not null) throw new InvalidOperationException("Stores are commercial properties and cannot be purchased as a Home.");
        if (_baseBuildings.GetValueOrDefault(accountId) == buildingId) throw new InvalidOperationException("This building is already your base.");
        var price = CalculateBuildingPriceCents(building);
        if (!player.GodMode && player.WalletCents < price) throw new InvalidOperationException($"This base costs ${price / 100m:N2}; you do not have enough money.");

        await _basePurchaseLock.WaitAsync(cancellationToken);
        try
        {
            var owner = await _store.LoadBaseOwnerAsync(Configuration.Id, buildingId, cancellationToken);
            if (owner is not null && owner != accountId) throw new InvalidOperationException("That building is already another player's base.");
            await _store.SaveBaseBuildingAsync(accountId, Configuration.Id, buildingId, building.Position, cancellationToken);
            await MoveFurnitureToNewBaseAsync(accountId, building, cancellationToken);
            _baseBuildings[accountId] = buildingId;
            var homeId = $"home:{accountId}:{buildingId}";
            _dungeons.GetOrAdd(homeId, _ => GenerateHome(homeId, building));
            foreach (var linkedPlayer in _playerAccounts.Where(pair => pair.Value == accountId).Select(pair => pair.Key)) SetBaseReturnPosition(linkedPlayer, buildingId);
            var updated = player with { WalletCents = player.GodMode ? player.WalletCents : player.WalletCents - price, Version = player.Version + 1 };
            await SavePlayerAsync(updated, cancellationToken);
            return (updated, price);
        }
        finally { _basePurchaseLock.Release(); }
    }

    private async Task<MovementOutcome> MoveInDungeonAsync(PlayerState player, MoveRequest request, CancellationToken cancellationToken)
    {
        if (!_dungeons.TryGetValue(player.LocationId, out var dungeon)) return new(player, false, true, false, false, false, player.LocationId.StartsWith("home:", StringComparison.Ordinal) ? "Home unavailable." : "Dungeon unavailable.");
        if (player.TravelMode is TravelMode.Bike or TravelMode.EBike or TravelMode.DirtBike or TravelMode.Motorcycle)
        {
            player = player with { TravelMode = TravelMode.Walk, SpeedMetersPerSecond = 0, Version = player.Version + 1 };
            await SavePlayerAsync(player, cancellationToken);
        }
        if (!player.GodMode && IsMotorized(player.TravelMode) && FuelGallons(player) <= 0)
        {
            var stopped = player with { SpeedMetersPerSecond = 0, Version = player.Version + 1 };
            await SavePlayerAsync(stopped, cancellationToken);
            return new(stopped, false, true, false, false, false, $"Your {VehicleName(player.TravelMode)} is out of gas. Add gasoline before using it again.");
        }
        var (dx, dy, remainingDistance) = ResolveMovementVector(player, request);
        var now = DateTimeOffset.UtcNow; var previous = _lastMovement.AddOrUpdate(player.Id, now, (_, old) => now); var elapsed = Math.Clamp((now - previous).TotalSeconds, .01, .15);
        var wearingMagicHikingShoes = player.MagicHikingShoesOn && (player.GodMode || InventoryQuantity(player.Id, "magicHikingShoes") > 0);
        var wearingMagicRunningShoes = player.MagicRunningShoesOn && (player.GodMode || InventoryQuantity(player.Id, "magicRunningShoes") > 0);
        var speed = ConfiguredSpeedMetersPerSecond(player, TerrainType.Pavement, player.Stamina / player.MaximumStamina, wearingMagicHikingShoes, wearingMagicRunningShoes);
        var maximumStep = request.MaximumDistanceMeters is > 0 and < double.MaxValue ? request.MaximumDistanceMeters.Value : double.MaxValue;
        if (remainingDistance is not null) maximumStep = Math.Min(maximumStep, remainingDistance.Value);
        var step = Math.Min(speed * elapsed, maximumStep);
        var next = player.Position with { X = Math.Clamp(player.Position.X + dx * step, .5, dungeon.Width - .5), Y = Math.Clamp(player.Position.Y + dy * step, .5, dungeon.Height - .5), Z = 0 };
        var blocked = dungeon.Walls.Any(wall => CrossesDungeonWall(player.Position, next, wall)) ||
            (dungeon.Furnishings?.Any(item => item.Properties.GetValueOrDefault("objectType") != "rug" && FurnitureContains(item, next, .32)) ?? false);
        if (blocked) next = player.Position;
        var distance = player.Position.Distance2D(next);
        var dirtBikeGas = player.DirtBikeGasGallons;
        var motorcycleGas = player.MotorcycleGasGallons;
        if (!player.GodMode && distance > .001)
        {
            if (player.TravelMode == TravelMode.DirtBike) dirtBikeGas = FuelAfterTravel(dirtBikeGas, distance, DirtBikeMilesPerGallon);
            if (player.TravelMode == TravelMode.Motorcycle) motorcycleGas = FuelAfterTravel(motorcycleGas, distance, MotorcycleMilesPerGallon);
        }
        var updated = player with { Position = next, SpeedMetersPerSecond = blocked ? 0 : player.Position.Distance2D(next) / elapsed, Terrain = TerrainType.Pavement,
            Stamina = player.TravelMode == TravelMode.Run && !(player.FoodProtectedUntilUtc > now) ? Math.Max(0, player.Stamina - WorldNavigation.RunningStaminaDrain(elapsed, wearingMagicHikingShoes || wearingMagicRunningShoes && WorldNavigation.MagicRunningShoesReduceStaminaOn(TerrainType.Pavement))) : player.Stamina,
            DirtBikeGasGallons = dirtBikeGas, MotorcycleGasGallons = motorcycleGas, Version = player.Version + 1 };
        await RevealAsync(updated, dungeon, cancellationToken); await SavePlayerAsync(updated, cancellationToken); return new(updated, !blocked, blocked, false, false, false, null);
    }

    private async Task RevealAsync(PlayerState player, DungeonState dungeon, CancellationToken cancellationToken)
    {
        var current = await _store.LoadDiscoveryAsync(Configuration.Id, player.Id, dungeon.Id, cancellationToken); var before = current.Count;
        RevealDungeonCells(current, player.Position.X, player.Position.Y);
        if (current.Count == before) return; foreach (var cell in current) await _store.SaveDiscoveryAsync(Configuration.Id, player.Id, dungeon.Id, cell, cancellationToken);
    }

    private static void RevealDungeonCells(HashSet<string> cells, double x, double y)
    {
        var cx = (int)Math.Floor(x / 3); var cy = (int)Math.Floor(y / 3); for (var ox = -1; ox <= 1; ox++) for (var oy = -1; oy <= 1; oy++) cells.Add($"{cx + ox},{cy + oy}");
    }

    private DungeonState WithDiscovery(string playerId, DungeonState dungeon)
    {
        var cells = _store.LoadDiscoveryAsync(Configuration.Id, playerId, dungeon.Id).GetAwaiter().GetResult(); return dungeon with { RevealedCells = cells.ToArray() };
    }

    private DungeonState CreateDungeonSession(string sessionId, CanonicalEntity building)
    {
        var store = StoreProfileForBuilding(building);
        if (store is not null) return GenerateStoreInterior(sessionId, building, store);
        var levelCount = RandomDungeonLevelCount(building);
        var layout = CreateInteriorLayout(building);
        var stairRandom = new Random(StableInt($"{sessionId}:stairs"));
        WorldPosition? stairs = levelCount > 1 ? RandomInteriorPosition(stairRandom, layout, building.Position.Region, layout.Exit) : null;
        return GenerateDungeonFloor(sessionId, building, 1, levelCount, stairs);
    }

    private static int RandomDungeonLevelCount(CanonicalEntity building)
    {
        if (int.TryParse(building.Properties.GetValueOrDefault("dungeon:levels"), out var specified)) return Math.Clamp(specified, 1, 10);
        // Seventy percent are single-level; the rest are uniformly distributed
        // from two through ten levels for an occasional genuinely deep dungeon.
        return RandomNumberGenerator.GetInt32(100) < 70 ? 1 : RandomNumberGenerator.GetInt32(2, 11);
    }

    private DungeonState GenerateDungeonFloor(string sessionId, CanonicalEntity building, int level, int levelCount, WorldPosition? stairs)
    {
        var id = level == 1 ? sessionId : $"{sessionId}:level:{level}";
        var layout = CreateInteriorLayout(building);
        var width = layout.Width; var height = layout.Height; var seed = StableInt(id); var random = new Random(seed);
        var walls = layout.ExteriorWalls.ToList(); var exteriorWallCount = walls.Count;
        var rooms = new List<DungeonRoom>();
        if (IsAxisAlignedRectangle(layout.Footprint))
        {
            var splitX = width * (.4 + random.NextDouble() * .2); var splitY = height * (.4 + random.NextDouble() * .2); const double door = 2.2;
            walls.Add(new DungeonWall(splitX, 0, splitX, height, splitY - door / 2, splitY + door / 2));
            walls.Add(new DungeonWall(0, splitY, width, splitY, splitX - door / 2, splitX + door / 2));
            rooms.AddRange([new(0, 0, splitX, splitY), new(splitX, 0, width - splitX, splitY), new(0, splitY, splitX, height - splitY), new(splitX, splitY, width - splitX, height - splitY)]);
        }
        else rooms.Add(new DungeonRoom(0, 0, width, height));
        var region = building.Position.Region; var actors = new List<ActorState>();
        for (var i = 0; i < random.Next(3, 7); i++)
        {
            var merchant = i == 0 && random.NextDouble() < .55; var foe = merchant ? random.NextDouble() * 2 : -2 + random.NextDouble() * 4;
            var position = RandomInteriorPosition(random, layout, region, stairs);
            var actorName = merchant || foe >= 0 ? FriendlyHumanName(id, i) : $"Dungeon Dweller {i + 1}";
            actors.Add(new ActorState($"{id}:npc:{i}", EntityKind.Npc, merchant ? "merchant" : "resident", actorName,
                position, HealthHearts: 4 + random.Next(5), MaximumHealthHearts: 8,
                FriendRating: foe, IsMerchant: merchant, TravelMode: (TravelMode)random.Next(0, 4), LocationId: id,
                EquippedWeapon: merchant ? "pistol" : "none"));
        }
        var chests = Enumerable.Range(0, random.Next(1, 4)).Select(i => new TreasureChestState($"{id}:chest:{i}", RandomInteriorPosition(random, layout, region, stairs), id)).ToArray();
        return new DungeonState(id, building.Id, width, height, rooms, walls, layout.Exit, actors, chests, Array.Empty<string>(),
            Footprint: layout.Footprint, ExteriorWallCount: exteriorWallCount, Level: level, LevelCount: levelCount,
            Stairs: stairs, Doorway: layout.Doorway, SessionId: sessionId);
    }

    private sealed record StoreProfile(string Name, string Category);

    private StoreProfile? StoreProfileForBuilding(CanonicalEntity building)
    {
        CanonicalEntity? source = building.Properties.ContainsKey("merchantCategory") ? building : null;
        source ??= _baseEntities.Values
            .Where(entity => entity.Kind == EntityKind.PointOfInterest && entity.Properties.ContainsKey("merchantCategory"))
            .Where(entity => PointInsideWorldFootprint(entity.Position, building.Geometry))
            .OrderBy(entity => entity.Position.Distance2D(building.Position))
            .FirstOrDefault();
        if (source is null) return null;
        var category = source.Properties.GetValueOrDefault("merchantCategory") ?? "general";
        var name = source.Properties.GetValueOrDefault("name") ?? source.Properties.GetValueOrDefault("brand") ?? $"{char.ToUpperInvariant(category[0])}{category[1..]} store";
        return new StoreProfile(name, category);
    }

    private static bool PointInsideWorldFootprint(WorldPosition point, IReadOnlyList<GeometryPoint> footprint)
    {
        if (footprint.Count < 3) return false;
        var inside = false;
        for (var index = 0; index < footprint.Count; index++)
        {
            var previous = index == 0 ? footprint.Count - 1 : index - 1;
            var a = footprint[index]; var b = footprint[previous];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (Math.Abs(b.Y - a.Y) < .000001 ? double.Epsilon : b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private DungeonState GenerateStoreInterior(string sessionId, CanonicalEntity building, StoreProfile store)
    {
        var layout = CreateInteriorLayout(building);
        var random = new Random(StableInt($"{sessionId}:store"));
        var factionId = $"{sessionId}:staff";
        var actors = new List<ActorState>();
        var merchantPosition = RandomInteriorPosition(random, layout, building.Position.Region, layout.Exit);
        actors.Add(new ActorState($"{sessionId}:merchant", EntityKind.Npc, "storeMerchant", FriendlyHumanName(sessionId, 0), merchantPosition,
            HealthHearts: 8, MaximumHealthHearts: 8, FriendRating: 1, IsMerchant: true, LocationId: sessionId,
            MerchantCategory: store.Category, EquippedWeapon: "pistol", FactionId: factionId));
        var employeeCount = random.Next(2, 7);
        for (var index = 0; index < employeeCount; index++)
        {
            var position = RandomInteriorPosition(random, layout, building.Position.Region, layout.Exit);
            actors.Add(new ActorState($"{sessionId}:employee:{index}", EntityKind.Npc, "storeEmployee", FriendlyHumanName(sessionId, index + 1), position,
                HealthHearts: 6, MaximumHealthHearts: 6, FriendRating: 1, LocationId: sessionId,
                EquippedWeapon: "fist", FactionId: factionId));
        }
        return new DungeonState(sessionId, building.Id, layout.Width, layout.Height,
            new[] { new DungeonRoom(0, 0, layout.Width, layout.Height) }, layout.ExteriorWalls, layout.Exit,
            actors, Array.Empty<TreasureChestState>(), Array.Empty<string>(), Footprint: layout.Footprint,
            ExteriorWallCount: layout.ExteriorWalls.Count, Doorway: layout.Doorway, SessionId: sessionId,
            IsStore: true, StoreCategory: store.Category);
    }

    private DungeonState GenerateHome(string id, CanonicalEntity building)
        => BuildHome(id, building);

    private static string FriendlyHumanName(string scope, int ordinal)
    {
        var start = (StableInt($"friendly-name:{scope}") & int.MaxValue) % FriendlyHumanNames.Length;
        return FriendlyHumanNames[(start + ordinal) % FriendlyHumanNames.Length];
    }

    public async Task<PlayerState> RestAtBedAsync(string playerId,string bedId,CancellationToken cancellationToken=default)
    {
        if(!_players.TryGetValue(playerId,out var player)||!_dungeons.TryGetValue(player.LocationId,out var home)||!home.IsHome)throw new InvalidOperationException("You can only rest in your own bed.");
        var bed=home.Furnishings?.FirstOrDefault(item=>item.Id==bedId&&item.Properties.GetValueOrDefault("objectType")=="bed")??throw new InvalidOperationException("Bed not found.");
        if(player.Position.Distance2D(bed.Position)>4)throw new InvalidOperationException("Move closer to the bed.");var until=DateTimeOffset.UtcNow.AddMinutes(5);
        var updated=player with{HealthHearts=player.MaximumHealthHearts,Stamina=player.MaximumStamina,Water=player.MaximumWater,FoodProtectedUntilUtc=until,WaterProtectedUntilUtc=until,Version=player.Version+1};await SavePlayerAsync(updated,cancellationToken);return updated;
    }

    public TradeQuote RequestTrade(string playerId, string merchantId)
    {
        var merchant = FindActor(playerId, merchantId); if (merchant is null || !merchant.IsMerchant) throw new InvalidOperationException("That character is not a merchant.");
        var player = _players[playerId]; if (player.LocationId != merchant.LocationId || player.Position.Distance2D(merchant.Position) > 5) throw new InvalidOperationException("Move closer to trade.");
        var rotation = MerchantInventoryRotation(DateTimeOffset.UtcNow);
        foreach (var expired in _tradeQuotes.Keys.Where(key => key.Player == playerId && key.Merchant == merchantId && key.Rotation != rotation).ToArray()) _tradeQuotes.TryRemove(expired, out _);
        return _tradeQuotes.GetOrAdd((playerId, merchantId, rotation), _ => GenerateTradeQuote(playerId, merchant));
    }

    public async Task<(PlayerState Player, InventoryState Inventory, RelationshipState Relationship)> ConfirmTradeAsync(string playerId, ConfirmTradeRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player."); var quote = RequestTrade(playerId, request.MerchantId);
        var requested = request.Purchases.Where(line => line.Quantity > 0).ToArray();
        var sales = (request.Sales ?? Array.Empty<PurchaseLine>()).Where(line => line.Quantity > 0).ToArray();
        if (requested.Length == 0 && sales.Length == 0) throw new InvalidOperationException("Select at least one item to buy or sell.");
        if (requested.Length > 0 && sales.Length > 0) throw new InvalidOperationException("Complete purchases and sales as separate transactions.");
        long total = 0; foreach (var line in requested) { var offer = quote.Offers.FirstOrDefault(o => o.ItemType.Equals(line.ItemType, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("Item not offered."); if (line.Quantity > offer.Quantity) throw new InvalidOperationException("Not enough stock."); checked { total += offer.UnitPriceCents * line.Quantity; } }
        long proceeds = 0; foreach (var line in sales) { var offer = (quote.BuyOffers ?? Array.Empty<MerchantOffer>()).FirstOrDefault(o => o.ItemType.Equals(line.ItemType, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("The merchant will not buy that item."); if (line.Quantity > offer.Quantity || InventoryQuantity(playerId, line.ItemType) < line.Quantity) throw new InvalidOperationException("You do not have that many to sell."); checked { proceeds += offer.UnitPriceCents * line.Quantity; } }
        if (!player.GodMode && total > player.WalletCents) throw new InvalidOperationException("You cannot spend more than you have.");
        var backpackPurchases = requested.Where(line => !line.ItemType.Equals("areaMap", StringComparison.OrdinalIgnoreCase) && !FurnitureCatalog.TryParse(line.ItemType, out _, out _, out _))
            .Select(line => InventoryStack(line.ItemType, line.Quantity)).ToArray();
        if (!CanAddToBackpack(playerId, backpackPurchases, out var capacityMessage)) throw new InvalidOperationException(capacityMessage);
        foreach (var line in requested)
        {
            if (line.ItemType.Equals("areaMap", StringComparison.OrdinalIgnoreCase))
            {
                var mapMerchant = FindActor(playerId, request.MerchantId) ?? throw new InvalidOperationException("Merchant unavailable.");
                var areaKey = AreaKeyFor(mapMerchant.Position.X, mapMerchant.Position.Y);
                _revealedWorldAreas[(playerId, areaKey)] = 0;
                await _store.SaveWorldMapDiscoveryAsync(Configuration.Id, playerId, areaKey, cancellationToken);
            }
            else if (FurnitureCatalog.TryParse(line.ItemType, out _, out _, out _))
            {
                for (var count = 0; count < line.Quantity; count++) await AddPurchasedFurnitureAsync(playerId, line.ItemType, cancellationToken);
            }
            else AddInventory(playerId, line.ItemType, line.Quantity);
        }
        foreach (var line in sales) RemoveInventory(playerId, line.ItemType, line.Quantity);
        var wallet = player.GodMode ? Math.Max(50_000, player.WalletCents + proceeds) : player.WalletCents - total + proceeds;
        var updated = NormalizeEquipmentAfterInventoryChange(player with { WalletCents = wallet, Version = player.Version + 1 });
        var friend = Relationship(playerId, request.MerchantId) + .5; _relationships[(playerId, request.MerchantId)] = friend; await _store.SaveRelationshipAsync(Configuration.Id, new(playerId, request.MerchantId, friend), cancellationToken);
        foreach (var key in _tradeQuotes.Keys.Where(key => key.Player == playerId).ToArray()) _tradeQuotes.TryRemove(key, out _);
        await SaveInventoryAsync(playerId, cancellationToken); await SavePlayerAsync(updated, cancellationToken);
        return (updated, GetInventoryState(playerId), new RelationshipState(playerId, request.MerchantId, friend));
    }

    private TradeQuote GenerateTradeQuote(string playerId, ActorState merchant)
    {
        var baseOffers = BaseMerchantOffers(merchant, playerId); var friendship = Relationship(playerId, merchant.Id); var factor = Math.Clamp(1 - friendship * .025, .6, 1.4);
        var offers = baseOffers.Select(offer =>
        {
            if (FurnitureCatalog.TryParse(offer.ItemType, out var furniture, out _, out _))
                return offer with { UnitPriceCents = (long)Math.Clamp(Math.Round(offer.UnitPriceCents * factor), furniture.MinimumPriceCents, furniture.MaximumPriceCents) };
            var range = _itemConfigurations[offer.ItemType];
            return offer with { UnitPriceCents = (long)Math.Clamp(Math.Round(offer.UnitPriceCents * factor), range.MinimumPriceCents, range.MaximumPriceCents) };
        }).ToArray();
        var buyOffers = GetInventoryState(playerId).Items
            .Where(item => item.ItemType != "fist" && !item.ItemType.StartsWith("quest:", StringComparison.OrdinalIgnoreCase) && item.ItemType != "areaMap")
            .Select(item =>
            {
                var definition = InventoryDefinition(item.ItemType);
                var price = item.ItemType == "newspaper" ? 25 : Math.Max(1, (long)Math.Round(((definition.MinimumPriceCents + definition.MaximumPriceCents) / 2d) * .4 * Math.Clamp(1 + friendship * .02, .6, 1.5)));
                return new MerchantOffer(item.ItemType, item.Quantity, price, definition.DisplayName, item.ItemType, new Dictionary<string, string> { ["description"] = $"Merchant pays {price / 100m:C} each" });
            }).ToArray();
        return new TradeQuote(merchant.Id, merchant.Name, friendship, offers, buyOffers);
    }

    private MerchantOffer[] BaseMerchantOffers(ActorState merchant, string? playerId = null)
    {
        var random = new Random(StableInt($"{merchant.Id}:{MerchantInventoryRotation(DateTimeOffset.UtcNow)}"));
        if (merchant.MerchantCategory == "furniture")
        {
            var furnitureOffers = FurnitureCatalog.All.OrderBy(_ => random.Next()).Take(random.Next(10, 19)).Select(item => FurnitureCatalog.CreateOffer(item, random)).ToList();
            if (playerId is not null && merchant.LocationId == "outdoor" && !_revealedWorldAreas.ContainsKey((playerId, AreaKeyFor(merchant.Position.X, merchant.Position.Y))))
            {
                var map = _itemConfigurations["areaMap"];
                furnitureOffers.Add(new MerchantOffer(map.ItemType, 1, random.NextInt64(map.MinimumPriceCents, map.MaximumPriceCents + 1), map.DisplayName, "map", new Dictionary<string, string> { ["description"] = map.Effect }));
            }
            return furnitureOffers.ToArray();
        }
        var allowed = merchant.MerchantCategory switch
        {
            "gas" => new HashSet<string>(["gallonOfGas", "food", "water"], StringComparer.OrdinalIgnoreCase),
            "clothing" => new HashSet<string>(["hat", "coolingHat", "warmHat", "tShirt", "coolingShirt", "longSleeveShirt", "sweater", "lightJacket", "winterJacket", "coolingShorts", "warmingPants", "magicHikingShoes", "magicRunningShoes"], StringComparer.OrdinalIgnoreCase),
            "food" => new HashSet<string>(["food", "water"], StringComparer.OrdinalIgnoreCase),
            "convenience" => new HashSet<string>(["food", "water"], StringComparer.OrdinalIgnoreCase),
            "weapons" => new HashSet<string>(["rock", "ballBearing", "knife", "sword", "slingshot", "crossbow", "arrow", "pistol", "rifle", "bullet"], StringComparer.OrdinalIgnoreCase),
            "hardware" => new HashSet<string>(["rock", "ballBearing", "knife", "sword", "slingshot", "crossbow", "arrow", "pistol", "rifle", "bullet", "bike", "flashlight", "lantern", "laser", "lockPickSet"], StringComparer.OrdinalIgnoreCase),
            "sportingGoods" => new HashSet<string>(["rock", "ballBearing", "knife", "sword", "slingshot", "crossbow", "arrow", "pistol", "rifle", "bullet", "skateboard", "bike", "magicHikingShoes", "magicRunningShoes", "inflatableRaft"], StringComparer.OrdinalIgnoreCase),
            "vehicles" => new HashSet<string>(["eBike", "dirtBike", "motorcycle", "gallonOfGas"], StringComparer.OrdinalIgnoreCase),
            _ => null
        };
        var pool = _itemConfigurations.Values.Where(item => item.ForSale && item.ItemType != "areaMap" && (allowed is null || allowed.Contains(item.ItemType))).ToArray();
        var selectedCount = allowed is null ? random.Next(3, 7) : random.Next(Math.Min(2, pool.Length), Math.Min(pool.Length, Math.Max(3, (int)Math.Ceiling(pool.Length * .7))) + 1);
        var selected = pool.OrderBy(_ => random.Next()).Take(selectedCount).ToList();
        void EnsureSelected(string itemType) { if (!selected.Any(item => item.ItemType.Equals(itemType, StringComparison.OrdinalIgnoreCase))) selected.Add(_itemConfigurations[itemType]); }
        if (merchant.MerchantCategory == "gas") EnsureSelected("gallonOfGas");
        if (merchant.MerchantCategory is "hardware" or "sportingGoods")
        {
            EnsureSelected("bike");
            var weapon = pool.Where(item => item.Category == InventoryCategory.Weapon).OrderBy(_ => random.Next()).FirstOrDefault();
            if (weapon is not null) EnsureSelected(weapon.ItemType);
        }
        if (merchant.MerchantCategory == "vehicles") { EnsureSelected("eBike"); EnsureSelected("dirtBike"); EnsureSelected("motorcycle"); }
        if (playerId is not null && merchant.LocationId == "outdoor" && !_revealedWorldAreas.ContainsKey((playerId, AreaKeyFor(merchant.Position.X, merchant.Position.Y)))) selected.Add(_itemConfigurations["areaMap"]);
        return selected.Select(item => new MerchantOffer(item.ItemType, item.Single ? 1 : random.Next(3, 31), random.NextInt64(item.MinimumPriceCents, item.MaximumPriceCents + 1), item.DisplayName, item.ItemType, new Dictionary<string, string> { ["description"] = item.Effect })).ToArray();
    }

    private static long MerchantInventoryRotation(DateTimeOffset value) => value.ToUnixTimeSeconds() / (4 * 60 * 60);

    private static string DisplayItem(string itemType) => itemType switch
    {
        "magicHikingShoes" => "magic hiking shoes",
        "magicRunningShoes" => "magic running shoes",
        "coolingShorts" => "cooling shorts",
        "warmingPants" => "warming pants",
        "coolingShirt" => "a cooling shirt",
        "longSleeveShirt" => "a long-sleeve shirt",
        "lightJacket" => "a light jacket",
        "winterJacket" => "a winter jacket",
        "coolingHat" => "a cooling hat",
        "warmHat" => "a warm knit hat",
        "dirtBike" => "a dirt bike",
        "gallonOfGas" => "a gallon of gas",
        "ballBearing" => "a ball bearing",
        "fist" => "your fist",
        "none" => "no weapon",
        "crossbow" => "a crossbow",
        "knife" => "a knife",
        "sword" => "a sword",
        "pistol" => "a pistol",
        "rifle" => "a rifle",
        _ => itemType
    };

    public async Task<CombatResult> AttackAsync(string playerId, CombatRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (request.TargetId == playerId) throw new InvalidOperationException("You cannot attack yourself.");
        var actorTarget = FindActor(playerId, request.TargetId);
        var playerTarget = actorTarget is null && _players.TryGetValue(request.TargetId, out var other) ? other : null;
        if (actorTarget is null && playerTarget is null) throw new InvalidOperationException("Target not found.");
        if (playerTarget is not null && !Configuration.PvpEnabled) throw new InvalidOperationException("Player-versus-player combat is disabled in this reality.");
        var targetLocation = actorTarget?.LocationId ?? playerTarget!.LocationId;
        if (targetLocation != player.LocationId) throw new InvalidOperationException("Target is not here.");
        var targetPosition = actorTarget?.Position ?? playerTarget!.Position;
        var targetName = actorTarget?.Name ?? playerTarget!.Name;
        var targetHealth = actorTarget?.HealthHearts ?? playerTarget!.HealthHearts;
        if (player.EquippedWeapon.Equals("none", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Equip a weapon before attacking.");
        var weapon = BestUsableWeapon(playerId, player.EquippedWeapon, player.GodMode);
        var (range, baseDamage, ammo) = WeaponDefinition(weapon);
        var distance = player.Position.Distance2D(targetPosition);
        if (distance > range) throw new InvalidOperationException($"{targetName} is beyond the {range:0.#}-meter range of your {DisplayItem(weapon)}.");
        if (!player.GodMode && ammo is not null && !RemoveInventory(playerId, ammo, 1)) throw new InvalidOperationException($"You need {DisplayItem(ammo)}.");
        var hitChance = weapon == "fist" ? 1 : Math.Clamp(.97 - distance / (range * 1.25), .15, .97);
        var hit = RandomNumberGenerator.GetInt32(1_000_000) < hitChance * 1_000_000;
        var damage = hit ? baseDamage : 0;
        var died = hit && !((playerTarget?.GodMode) ?? false) && targetHealth - damage <= 0;
        RelationshipState? relationship = null; QuestState? resolvedQuest = null;
        if (actorTarget is not null)
        {
            if (actorTarget.Subtype == "policeOfficer") player = await ReportCrimeAsync(playerId, actorTarget.Position, cancellationToken);
            if (!string.IsNullOrWhiteSpace(actorTarget.FactionId))
            {
                var allies = ActorsAtLocation(player.LocationId).Where(actor => actor.FactionId == actorTarget.FactionId).ToArray();
                foreach (var ally in allies)
                {
                    var relation = Math.Min(-3, Relationship(playerId, ally.Id) - 1);
                    _relationships[(playerId, ally.Id)] = relation;
                    await _store.SaveRelationshipAsync(Configuration.Id, new RelationshipState(playerId, ally.Id, relation), cancellationToken);
                }
                relationship = new(playerId, actorTarget.Id, Relationship(playerId, actorTarget.Id));
            }
            else
            {
                var relation = Relationship(playerId, actorTarget.Id) - 1; _relationships[(playerId, actorTarget.Id)] = relation;
                relationship = new(playerId, actorTarget.Id, relation);
                await _store.SaveRelationshipAsync(Configuration.Id, relationship, cancellationToken);
            }
            if (hit) UpdateActorHealth(player, actorTarget, Math.Max(0, targetHealth - damage), died);
            if (died)
            {
                resolvedQuest = await RecordQuestKillAsync(playerId, actorTarget, cancellationToken);
                if (player.LocationId == "outdoor" && actorTarget.Kind == EntityKind.Npc) player = await ReportCrimeAsync(playerId, actorTarget.Position, cancellationToken);
            }
        }
        PlayerState? updatedTarget = null;
        if (playerTarget is not null && hit)
        {
            var remaining = playerTarget.GodMode ? Math.Max(1, targetHealth - damage) : Math.Max(0, targetHealth - damage);
            updatedTarget = died ? ResetPlayer(playerTarget with { HealthHearts = 0 }) : playerTarget with { HealthHearts = remaining, Version = playerTarget.Version + 1 };
            await SavePlayerAsync(updatedTarget, cancellationToken);
        }
        if (!player.GodMode && ammo is not null) await SaveInventoryAsync(playerId, cancellationToken);
        var nextWeapon = player.GodMode ? weapon : BestUsableWeapon(playerId, weapon, false);
        var updatedAttacker = player.EquippedWeapon == nextWeapon ? player : player with { EquippedWeapon = nextWeapon, Version = player.Version + 1 };
        if (!ReferenceEquals(updatedAttacker, player)) await SavePlayerAsync(updatedAttacker, cancellationToken);
        var consequences = new List<CombatEvent>();
        if (resolvedQuest?.Kind == "missingPet" && !updatedAttacker.GodMode)
        {
            var giver = _actors.GetValueOrDefault(resolvedQuest.GiverId);
            if (giver is not null)
            {
                giver = giver with { Position = updatedAttacker.Position with { X = updatedAttacker.Position.X + 4 }, EquippedWeapon = "pistol", Version = giver.Version + 1 }; _actors[giver.Id] = giver;
                for (var shot = 0; shot < 15; shot++) consequences.Add(new CombatEvent(giver.Id, updatedAttacker.Id, "pistol", giver.Position, updatedAttacker.Position, true, 1, shot == 14, $"{giver.Name} shot {updatedAttacker.Name} for 1 heart after the pet was killed."));
                updatedAttacker = ResetPlayer(updatedAttacker with { HealthHearts = 0, Version = updatedAttacker.Version + 1 });
                await SavePlayerAsync(updatedAttacker, cancellationToken);
            }
        }
        var message = hit
            ? died
                ? $"{player.Name} defeated {targetName}."
                : $"{player.Name} hit {targetName} for {damage:0.##} heart{(damage == 1 ? "" : "s")} damage."
            : $"{player.Name} missed {targetName}.";
        if (nextWeapon != weapon) message += $" Switched to {DisplayItem(nextWeapon)}.";
        var eventHealth = updatedTarget?.HealthHearts ?? (hit ? Math.Max(0, targetHealth - damage) : targetHealth);
        var combat = new CombatEvent(playerId, request.TargetId, weapon, player.Position, targetPosition, hit, damage, died, message, eventHealth);
        var dungeon = player.LocationId != "outdoor" && _dungeons.TryGetValue(player.LocationId, out var d) ? WithDiscovery(playerId, d) : null;
        return new(combat, updatedAttacker, updatedTarget, GetInventoryState(playerId), relationship, dungeon, consequences);
    }

    private bool OwnsWeapon(string playerId, string weapon) => weapon == "fist" || InventoryQuantity(playerId, weapon) > 0;

    private bool CanUseWeapon(string playerId, string weapon, bool godMode)
    {
        if (godMode) return WeaponPowerOrder.Contains(weapon);
        if (!OwnsWeapon(playerId, weapon)) return false;
        var ammo = WeaponDefinition(weapon).Ammo;
        return ammo is null || InventoryQuantity(playerId, ammo) > 0;
    }

    private string BestUsableWeapon(string playerId, string requestedWeapon, bool godMode)
    {
        requestedWeapon = (requestedWeapon ?? "fist").ToLowerInvariant();
        if (requestedWeapon == "none") return "none";
        if (CanUseWeapon(playerId, requestedWeapon, godMode)) return requestedWeapon;
        var currentDamage = _itemConfigurations.GetValueOrDefault(requestedWeapon)?.Damage ?? double.MaxValue;
        var fallback = WeaponPowerOrder.Where(weapon => weapon != "fist" && CanUseWeapon(playerId, weapon, godMode) && _itemConfigurations[weapon].Damage < currentDamage)
            .OrderByDescending(weapon => _itemConfigurations[weapon].Damage).ThenBy(weapon => Array.IndexOf(WeaponPowerOrder, weapon)).FirstOrDefault();
        if (fallback is not null) return fallback;
        return "fist";
    }

    private (double Range, double Damage, string? Ammo) WeaponDefinition(string weapon)
    {
        if (!_itemConfigurations.TryGetValue(weapon, out var item) || !WeaponPowerOrder.Contains(weapon)) throw new InvalidOperationException("Unknown weapon.");
        return (item.RangeMeters, item.Damage, item.AmmoType);
    }

    public double ConfiguredSpeedMetersPerSecond(PlayerState player, TerrainType terrain, double? staminaFraction = null, bool? magicHikingShoes = null, bool? magicRunningShoes = null)
    {
        var modeModifier = _movementConfiguration.TravelModeSpeedModifiersMph.GetValueOrDefault(player.TravelMode);
        if (player.TravelMode == TravelMode.Run)
            modeModifier *= Math.Clamp(staminaFraction ?? (player.MaximumStamina <= 0 ? 0 : player.Stamina / player.MaximumStamina), 0, 1);
        var mph = _movementConfiguration.BaseSpeedMph
            + _movementConfiguration.TerrainSpeedModifiersMph.GetValueOrDefault(terrain)
            + modeModifier;
        foreach (var itemType in ActiveMovementItems(player, magicHikingShoes, magicRunningShoes))
            if (_itemConfigurations.TryGetValue(itemType, out var item)) mph += item.SpeedModifierMph ?? 0;
        mph = Math.Max(.1, mph);
        var loadMultiplier = player.GodMode ? 1 : Math.Clamp(1 - GetInventoryState(player.Id).WeightPounds / 100d, .5, 1);
        return WorldNavigation.MilesPerHour(mph) * (player.Water <= 0 ? .5 : 1) * (player.GodMode ? 5 : 1) * loadMultiplier;
    }

    private static IEnumerable<string> ActiveMovementItems(PlayerState player, bool? magicHikingShoes = null, bool? magicRunningShoes = null)
    {
        var travelItem = player.TravelMode switch
        {
            TravelMode.Skateboard => "skateboard", TravelMode.Bike => "bike", TravelMode.EBike => "eBike", TravelMode.Raft => "inflatableRaft",
            TravelMode.DirtBike => "dirtBike", TravelMode.Motorcycle => "motorcycle", _ => null
        };
        if (travelItem is not null) yield return travelItem;
        if (magicHikingShoes ?? player.MagicHikingShoesOn) yield return "magicHikingShoes";
        if (magicRunningShoes ?? player.MagicRunningShoesOn) yield return "magicRunningShoes";
        if (player.HatOn) yield return "hat";
        if (player.FlashlightOn) yield return "flashlight";
        if (player.LanternOn) yield return "lantern";
        if (player.LaserOn) yield return "laser";
        if (player.EquippedWeapon != "none") yield return player.EquippedWeapon;
    }

    public async Task<ItemConfiguration> UpdateItemConfigurationAsync(string playerId, UpdateItemConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        if (!playerIsGod(playerId)) throw new InvalidOperationException("God Mode must be enabled to change server configuration.");
        if (!_itemConfigurations.TryGetValue(request.ItemType, out var current)) throw new InvalidOperationException("Unknown inventory item.");
        if (!double.IsFinite(request.Damage) || request.Damage is < 0 or > 100) throw new InvalidOperationException("Damage must be between 0 and 100 hearts.");
        if (!double.IsFinite(request.RangeMeters) || request.RangeMeters is < 0 or > 2000) throw new InvalidOperationException("Range must be between 0 and 2,000 meters.");
        if (!double.IsFinite(request.SpeedModifierMph) || request.SpeedModifierMph is < -200 or > 200) throw new InvalidOperationException("Speed modifier must be between -200 and +200 mph.");
        if (!double.IsFinite(request.VisibilityModifierMeters) || request.VisibilityModifierMeters is < -5000 or > 5000) throw new InvalidOperationException("Visibility modifier must be between -5,000 and +5,000 meters.");
        if (request.MinimumPriceCents < 0 || request.MaximumPriceCents < request.MinimumPriceCents || request.MaximumPriceCents > 100_000_000_000) throw new InvalidOperationException("Enter a valid minimum and maximum price.");
        var updated = current with { Damage = request.Damage, RangeMeters = request.RangeMeters, MinimumPriceCents = request.MinimumPriceCents, MaximumPriceCents = request.MaximumPriceCents, SpeedModifierMph = request.SpeedModifierMph, VisibilityModifierMeters = request.VisibilityModifierMeters };
        _itemConfigurations[current.ItemType] = updated; _tradeQuotes.Clear();
        await _store.SaveItemConfigurationsAsync(Configuration.Id, _itemConfigurations.Values.OrderBy(item=>item.ItemType).ToArray(), cancellationToken);
        return updated;
    }

    public async Task<MovementConfiguration> UpdateMovementConfigurationAsync(string playerId, UpdateMovementConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        if (!playerIsGod(playerId)) throw new InvalidOperationException("God Mode must be enabled to change server configuration.");
        if (!double.IsFinite(request.BaseSpeedMph) || request.BaseSpeedMph is < .1 or > 200) throw new InvalidOperationException("Base speed must be between 0.1 and 200 mph.");
        if (!double.IsFinite(request.BaseVisibilityMeters) || request.BaseVisibilityMeters is < 1 or > 5000) throw new InvalidOperationException("Base visibility must be between 1 and 5,000 meters.");
        var terrain = Enum.GetValues<TerrainType>().ToDictionary(value => value, value => ValidateSpeedModifier(request.TerrainSpeedModifiersMph.GetValueOrDefault(value)));
        var travel = Enum.GetValues<TravelMode>().ToDictionary(value => value, value => ValidateSpeedModifier(request.TravelModeSpeedModifiersMph.GetValueOrDefault(value)));
        _movementConfiguration = new MovementConfiguration(request.BaseSpeedMph, request.BaseVisibilityMeters, terrain, travel);
        await _store.SaveMovementConfigurationAsync(Configuration.Id, _movementConfiguration, cancellationToken);
        return _movementConfiguration;
    }

    private static double ValidateSpeedModifier(double value)
    {
        if (!double.IsFinite(value) || value is < -200 or > 200) throw new InvalidOperationException("Each speed modifier must be between -200 and +200 mph.");
        return value;
    }

    private void UpdateActorHealth(PlayerState player, ActorState actor, double health, bool died)
    {
        if (player.LocationId == "outdoor") { if (died) _actors.TryRemove(actor.Id, out _); else _actors[actor.Id] = actor with { HealthHearts = health, Version = actor.Version + 1 }; }
        else if (_dungeons.TryGetValue(player.LocationId, out var dungeon)) _dungeons[player.LocationId] = dungeon with { Actors = died ? dungeon.Actors.Where(a => a.Id != actor.Id).ToArray() : dungeon.Actors.Select(a => a.Id == actor.Id ? a with { HealthHearts = health, Version = a.Version + 1 } : a).ToArray() };
        if (died)
        {
            var random = new Random(); var items = new List<ItemStack>();
            if (random.NextDouble() < .8) items.Add(InventoryStack("rock", random.Next(1, 6)));
            if (random.NextDouble() < .6) items.Add(InventoryStack("ballBearing", random.Next(1, 9)));
            string[] usefulItems = ["pencil", "pen", "marker", "sprayPaint", "book", "calculator", "cellPhone", "arrow", "gallonOfGas", "knife", "slingshot"];
            var extraCount = random.Next(1, 4);
            for (var index = 0; index < extraCount; index++) items.Add(InventoryStack(usefulItems[random.Next(usefulItems.Length)], 1));
            var drop = new LootDropState($"loot:{Guid.NewGuid():N}", actor.Position, player.LocationId, random.Next(1, 1001), items, DateTimeOffset.UtcNow.AddMinutes(5)); _loot[drop.Id] = drop;
        }
    }

    public async Task<(PlayerState Player, InventoryState Inventory, string Message)> OpenChestAsync(string playerId, string chestId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        TreasureChestState? chest;
        DungeonState? dungeon = null;
        if (player.LocationId == "outdoor") chest = _outdoorChests.GetValueOrDefault(chestId);
        else { _dungeons.TryGetValue(player.LocationId, out dungeon); chest = dungeon?.Chests.FirstOrDefault(c => c.Id == chestId); }
        if (chest is null) throw new InvalidOperationException("Chest not found."); if (player.Position.Distance2D(chest.Position) > 4) throw new InvalidOperationException("Move closer to the chest.");
        var random = new Random(StableInt($"{chestId}:{playerId}")); var money = random.Next(25, 5001); var rocks = random.Next(1, 8); var bearings = random.Next(0, 12);
        string[] bonusPool = ["knife", "sword", "slingshot", "crossbow", "pistol", "rifle", "arrow", "bullet", "pencil", "pen", "marker", "sprayPaint", "book", "calculator", "cellPhone"];
        var rewards = new List<ItemStack> { InventoryStack("rock", rocks) };
        if (bearings > 0) rewards.Add(InventoryStack("ballBearing", bearings));
        if (random.NextDouble() < .75) rewards.Add(InventoryStack(bonusPool[random.Next(bonusPool.Length)], 1));
        if (!CanAddToBackpack(playerId, rewards, out var capacityMessage)) throw new InvalidOperationException(capacityMessage + " Store something at Home before opening this chest.");
        foreach (var reward in rewards) AddInventory(playerId, reward.ItemType, reward.Quantity);
        if (player.LocationId == "outdoor") _outdoorChests.TryRemove(chestId, out _); else _dungeons[player.LocationId] = dungeon! with { Chests = dungeon!.Chests.Where(c => c.Id != chestId).ToArray() }; var updated = player with { WalletCents = player.WalletCents + money, Version = player.Version + 1 };
        var contents = rewards.Select(reward => $"{reward.Quantity} × {InventoryDefinition(reward.ItemType).DisplayName}");
        await SaveInventoryAsync(playerId, cancellationToken); await SavePlayerAsync(updated, cancellationToken); return (updated, GetInventoryState(playerId), $"Found {money / 100.0:C}, {string.Join(", ", contents)}.");
    }

    public TreasureChestState MarkChestSeen(string playerId, string chestId)
    {
        if (!_players.ContainsKey(playerId)) throw new InvalidOperationException("Unknown player.");
        if (!_outdoorChests.TryGetValue(chestId, out var chest)) throw new InvalidOperationException("Chest not found.");
        if (chest.ExpiresAtUtc is not null) return chest;
        var updated = chest with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(3) }; _outdoorChests[chestId] = updated; return updated;
    }

    public async Task<HostileTick> AdvanceHostilityAsync(TimeSpan elapsed, CancellationToken cancellationToken = default)
    {
        var changedActors = new Dictionary<string, ActorState>(); var changedPlayers = new List<PlayerState>(); var combat = new List<CombatEvent>();
        var now = DateTimeOffset.UtcNow;
        foreach (var pending in _pendingPolice.ToArray())
        {
            if (now < pending.Value.DueAtUtc || !_pendingPolice.TryRemove(pending.Key, out var response) || !_players.TryGetValue(response.PlayerId, out var suspect)) continue;
            var spawn = Navigation.FindNearestWalkable(response.WitnessPosition with { X = response.WitnessPosition.X + 8, Y = response.WitnessPosition.Y + 8 });
            var id = $"cop:{Guid.NewGuid():N}";
            var cop = new ActorState(id, EntityKind.Npc, "policeOfficer", "Officer Morgan", spawn, EquippedWeapon: "pistol");
            _actors[id] = cop; _relationships[(suspect.Id, id)] = -10;
            await _store.SaveRelationshipAsync(Configuration.Id, new RelationshipState(suspect.Id, id, -10), cancellationToken);
            changedActors[id] = cop;
        }
        foreach (var ufo in _actors.Values.Where(actor => actor.Subtype == "ufo").ToArray())
        foreach (var target in _players.Values.Where(player => player.LocationId == "outdoor").ToArray())
        {
            if (ufo.Position.Distance2D(target.Position) > 8 || !_ufoHits.TryAdd($"{ufo.Id}:{target.Id}", 0)) continue;
            var remaining = target.GodMode ? Math.Max(1, target.HealthHearts - 10) : Math.Max(0, target.HealthHearts - 10);
            var died = !target.GodMode && remaining <= 0;
            var updatedTarget = died ? ResetPlayer(target with { HealthHearts = 0, Version = target.Version + 1 }) : target with { HealthHearts = remaining, Version = target.Version + 1 };
            await SavePlayerAsync(updatedTarget, cancellationToken); changedPlayers.Add(updatedTarget);
            combat.Add(new CombatEvent(ufo.Id, target.Id, "greenBeam", ufo.Position, target.Position, true, 10, died, $"A UFO struck {target.Name} with a green beam for 10 hearts.", updatedTarget.HealthHearts));
        }
        foreach (var ufo in _actors.Values.Where(actor => actor.Subtype == "ufo").ToArray())
        foreach (var victim in _actors.Values.Where(actor => actor.Subtype != "ufo" && actor.LocationId == "outdoor").ToArray())
        {
            if (ufo.Position.Distance2D(victim.Position) > 8 || !_ufoHits.TryAdd($"{ufo.Id}:{victim.Id}", 0)) continue;
            _actors.TryRemove(victim.Id, out _); _actorRoutes.TryRemove(victim.Id, out _);
            combat.Add(new CombatEvent(ufo.Id, victim.Id, "greenBeam", ufo.Position, victim.Position, true, 10, true, $"A UFO struck {victim.Name} with a green beam for 10 hearts.", 0));
        }
        foreach (var originalPredator in _actors.Values.Where(actor => actor.Subtype is "tRex" or "eventBear").ToArray())
        {
            var predator = originalPredator; PlayerState? playerVictim = null; ActorState? actorVictim = null; var nearest = 60d;
            foreach (var candidate in _players.Values.Where(item => item.LocationId == "outdoor")) { var distance = predator.Position.Distance2D(candidate.Position); if (distance < nearest) { nearest = distance; playerVictim = candidate; actorVictim = null; } }
            foreach (var candidate in _actors.Values.Where(item => item.Id != predator.Id && item.Subtype is not ("ufo" or "tRex" or "eventBear") && item.LocationId == "outdoor")) { var distance = predator.Position.Distance2D(candidate.Position); if (distance < nearest) { nearest = distance; actorVictim = candidate; playerVictim = null; } }
            var targetPosition = playerVictim?.Position ?? actorVictim?.Position; if (targetPosition is null) continue;
            if (nearest > 2.5)
            {
                var step = Math.Min(nearest, (predator.Subtype == "tRex" ? 6 : 4) * elapsed.TotalSeconds); var dx = (targetPosition.Value.X - predator.Position.X) / nearest; var dy = (targetPosition.Value.Y - predator.Position.Y) / nearest;
                var next = predator.Position with { X = predator.Position.X + dx * step, Y = predator.Position.Y + dy * step };
                if (Navigation.CanTraverse(predator.Position, next, true)) { predator = predator with { Position = next, Facing = Math.Abs(dx) > Math.Abs(dy) ? dx > 0 ? "east" : "west" : dy > 0 ? "north" : "south", IsMoving = true, Version = predator.Version + 1 }; _actors[predator.Id] = predator; changedActors[predator.Id] = predator; }
                continue;
            }
            var victimId = playerVictim?.Id ?? actorVictim!.Id; var cooldownKey = $"{predator.Id}:{victimId}";
            if (_eventAttackCooldowns.TryGetValue(cooldownKey, out var lastAttack) && now - lastAttack < TimeSpan.FromSeconds(1)) continue; _eventAttackCooldowns[cooldownKey] = now;
            if (playerVictim is not null)
            {
                var died = !playerVictim.GodMode && playerVictim.HealthHearts <= 10; var health = playerVictim.GodMode ? Math.Max(1, playerVictim.HealthHearts - 10) : Math.Max(0, playerVictim.HealthHearts - 10);
                var updated = died ? ResetPlayer(playerVictim with { HealthHearts = 0, Version = playerVictim.Version + 1 }) : playerVictim with { HealthHearts = health, Version = playerVictim.Version + 1 }; await SavePlayerAsync(updated, cancellationToken); changedPlayers.Add(updated);
                combat.Add(new CombatEvent(predator.Id, playerVictim.Id, "bite", predator.Position, playerVictim.Position, true, 10, died, $"{predator.Name} attacked {playerVictim.Name} for 10 hearts.", updated.HealthHearts));
            }
            else
            {
                _actors.TryRemove(actorVictim!.Id, out _); _actorRoutes.TryRemove(actorVictim.Id, out _);
                combat.Add(new CombatEvent(predator.Id, actorVictim.Id, "bite", predator.Position, actorVictim.Position, true, 10, true, $"{predator.Name} killed {actorVictim.Name}.", 0));
            }
        }
        foreach (var player in _players.Values.ToArray())
        {
            var sight = Weather.IsDay ? 45d : 16d + Math.Max(0, Weather.MoonIllumination) * 18d + (player.FlashlightOn?22:0) + (player.LanternOn?12:0) + (player.LaserOn?30:0);
            _dungeons.TryGetValue(player.LocationId, out var currentDungeon);
            var actors = player.LocationId == "outdoor" ? _actors.Values.ToArray() : currentDungeon?.Actors.ToArray() ?? Array.Empty<ActorState>();
            var target = actors.Select(actor => (Actor: actor, Rating: Relationship(player.Id, actor.Id)))
                .Where(item => item.Rating < 0 && (item.Actor.Subtype != "policeOfficer" || player.WantedLevel > 0) && item.Actor.Position.Distance2D(player.Position) <= sight)
                .OrderBy(item => item.Actor.Position.Distance2D(player.Position)).FirstOrDefault();
            if (target.Actor is null) continue;
            var actor = target.Actor; var distance = actor.Position.Distance2D(player.Position); var hostility = Math.Abs(target.Rating);
            if (distance > 1.35)
            {
                var dx = (player.Position.X - actor.Position.X) / distance; var dy = (player.Position.Y - actor.Position.Y) / distance;
                var terrain = player.LocationId == "outdoor" ? Navigation.TerrainAt(actor.Position.X, actor.Position.Y) : TerrainType.Pavement;
                var travel = actor.Kind == EntityKind.Animal ? (hostility >= 2 ? TravelMode.Run : TravelMode.Walk) : actor.TravelMode;
                if (!WorldNavigation.SupportsTravelMode(terrain, travel)) travel = TravelMode.Walk;
                var speed = Navigation.SpeedFor(terrain, travel) * Math.Clamp(1 + hostility * .12, 1, 2.4);
                var next = actor.Position with { X = actor.Position.X + dx * speed * elapsed.TotalSeconds, Y = actor.Position.Y + dy * speed * elapsed.TotalSeconds };
                var blocked = player.LocationId == "outdoor" ? !Navigation.CanTraverse(actor.Position, next, true) : currentDungeon!.Walls.Any(wall => CrossesDungeonWall(actor.Position, next, wall));
                if (!blocked)
                {
                    if (player.LocationId == "outdoor") next = next with { Z = Navigation.ElevationAt(next.X, next.Y) };
                    var facing = Math.Abs(dx) > Math.Abs(dy) ? (dx > 0 ? "east" : "west") : (dy > 0 ? "north" : "south");
                    actor = actor with { Position = next, Facing = facing, IsMoving = true, TravelMode = travel, Version = actor.Version + 1 };
                    SetActor(player.LocationId, actor); changedActors[actor.Id] = actor;
                }
                continue;
            }
            if (_lastActorAttack.TryGetValue((actor.Id, player.Id), out var last) && now - last < TimeSpan.FromSeconds(3)) continue;
            _lastActorAttack[(actor.Id, player.Id)] = now;
            var damage = Math.Min(3, .5 + hostility * .25); var health = player.GodMode ? Math.Max(1, player.HealthHearts - damage) : Math.Max(0, player.HealthHearts - damage);
            var died = health <= 0; var updated = died ? ResetPlayer(player with { HealthHearts = 0 }) : player with { HealthHearts = health, Version = player.Version + 1 };
            if (!await SavePlayerAsync(updated, cancellationToken)) continue;
            changedPlayers.Add(updated);
            combat.Add(new CombatEvent(actor.Id, player.Id, "attack", actor.Position, player.Position, true, damage, died,
                died ? $"{actor.Name} defeated {player.Name}." : $"{actor.Name} hit {player.Name} for {damage:0.##} heart{(damage == 1 ? "" : "s")} damage.", updated.HealthHearts));
        }
        return new HostileTick(changedActors.Values.ToArray(), changedPlayers, combat);
    }

    private void SetActor(string locationId, ActorState actor)
    {
        if (locationId == "outdoor") _actors[actor.Id] = actor;
        else if (_dungeons.TryGetValue(locationId, out var dungeon)) _dungeons[locationId] = dungeon with { Actors = dungeon.Actors.Select(item => item.Id == actor.Id ? actor : item).ToArray() };
    }

    public bool IsInOwnHome(string playerId)=>_players.TryGetValue(playerId,out var player)&&player.LocationId.StartsWith("home:",StringComparison.Ordinal);
    public async Task PrepareCharacterSwitchAsync(string currentPlayerId,string nextPlayerId,string nextName,CancellationToken cancellationToken=default)
    {
        if(!_players.TryGetValue(currentPlayerId,out var current)||!IsInOwnHome(currentPlayerId))throw new InvalidOperationException("Characters can only be switched inside your base.");
        var existing=await _store.LoadCharacterAsync(Configuration.Id,nextPlayerId,cancellationToken);
        var next=(existing??new PlayerState(nextPlayerId,nextName,current.Position)) with{Name=nextName,Position=current.Position,LocationId=current.LocationId,SpeedMetersPerSecond=0,Version=(existing?.Version??0)+1};
        await _store.SaveCharacterAsync(Configuration.Id,next,cancellationToken);
    }

    public async Task<(PlayerState Player, InventoryState Inventory, string Message)> CollectLootAsync(string playerId, string lootId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || !_loot.TryGetValue(lootId, out var loot) || loot.LocationId != player.LocationId) throw new InvalidOperationException("Treasure is not available.");
        if (player.Position.Distance2D(loot.Position) > 4) throw new InvalidOperationException("Move closer to the treasure.");
        if (!CanAddToBackpack(playerId, loot.Items.Select(item => InventoryStack(item.ItemType, item.Quantity)), out var capacityMessage)) throw new InvalidOperationException(capacityMessage + " Store something at Home before collecting this treasure.");
        _loot.TryRemove(lootId, out _); foreach (var item in loot.Items) AddInventory(playerId, item.ItemType, item.Quantity);
        var updated = player with { WalletCents = player.WalletCents + loot.MoneyCents, Version = player.Version + 1 };
        await SaveInventoryAsync(playerId, cancellationToken); await SavePlayerAsync(updated, cancellationToken);
        var collectedContents = new List<string>();
        if (loot.MoneyCents > 0) collectedContents.Add($"{loot.MoneyCents / 100.0:C}");
        collectedContents.AddRange(loot.Items.Where(item => item.Quantity > 0)
            .Select(item => $"{item.Quantity} × {InventoryDefinition(item.ItemType).DisplayName}"));
        var message = collectedContents.Count == 0 ? "Collected treasure." : $"Collected {string.Join(", ", collectedContents)}.";
        return (updated, GetInventoryState(playerId), message);
    }

    private ActorState? FindActor(string playerId, string actorId)
    {
        if (!_players.TryGetValue(playerId, out var player)) return null; if (player.LocationId == "outdoor") return _actors.GetValueOrDefault(actorId);
        return _dungeons.TryGetValue(player.LocationId, out var dungeon) ? dungeon.Actors.FirstOrDefault(actor => actor.Id == actorId) : null;
    }

    private IReadOnlyList<ActorState> ActorsAtLocation(string locationId) =>
        locationId == "outdoor" ? _actors.Values.ToArray() : _dungeons.TryGetValue(locationId, out var dungeon) ? dungeon.Actors : Array.Empty<ActorState>();

    private double Relationship(string playerId, string actorId) => _relationships.GetValueOrDefault((playerId, actorId));
    private int InventoryQuantity(string playerId, string item) => _inventories.TryGetValue(playerId, out var inventory) && inventory.TryGetValue(item, out var quantity) ? quantity : 0;
    private void AddInventory(string playerId, string item, int quantity) { var inventory = _inventories.GetOrAdd(playerId, _ => new(StringComparer.OrdinalIgnoreCase)); lock (inventory) inventory[item] = inventory.GetValueOrDefault(item) + quantity; }
    private bool RemoveInventory(string playerId, string item, int quantity) { var inventory = _inventories.GetOrAdd(playerId, _ => new(StringComparer.OrdinalIgnoreCase)); lock (inventory) { var current = inventory.GetValueOrDefault(item); if (current < quantity) return false; inventory[item] = current - quantity; if (inventory[item] <= 0) inventory.Remove(item); return true; } }

    private ItemConfiguration InventoryDefinition(string itemType) => _itemConfigurations.TryGetValue(itemType, out var definition)
        ? definition
        : new ItemConfiguration(itemType, DisplayItem(itemType), "Uncatalogued item", 0, 0, 0, 0, false, WeightPounds: 1,
            Category: itemType.StartsWith("quest:", StringComparison.OrdinalIgnoreCase) ? InventoryCategory.Quest : InventoryCategory.Other);

    private ItemStack InventoryStack(string itemType, int quantity)
    {
        var definition = InventoryDefinition(itemType);
        return new ItemStack(itemType, quantity, definition.Category, definition.WeightPounds, definition.CarriedInBackpack);
    }

    private IReadOnlyList<ItemStack> GetInventoryItems(string playerId)
    {
        if (!_inventories.TryGetValue(playerId, out var inventory)) return Array.Empty<ItemStack>();
        lock (inventory) return inventory.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key).Select(pair => InventoryStack(pair.Key, pair.Value)).ToArray();
    }

    private InventoryState GetInventoryState(string playerId)
    {
        var items = GetInventoryItems(playerId);
        var carried = items.Where(item => item.CarriedInBackpack).ToArray();
        return new InventoryState(playerId, items,
            Math.Round(carried.Sum(item => item.UnitWeightPounds * item.Quantity), 3), MaximumBackpackWeightPounds,
            carried.Count(item => item.Category == InventoryCategory.Weapon), MaximumWeaponSlots,
            carried.Count(item => item.Category == InventoryCategory.Quest), MaximumQuestSlots,
            carried.Count(item => item.Category == InventoryCategory.Other), MaximumOtherSlots);
    }

    private bool CanAddToBackpack(string playerId, IEnumerable<ItemStack> additions, out string message)
    {
        var combined = GetInventoryItems(playerId).ToDictionary(item => item.ItemType, item => item.Quantity, StringComparer.OrdinalIgnoreCase);
        foreach (var addition in additions.Where(item => item.Quantity > 0)) combined[addition.ItemType] = combined.GetValueOrDefault(addition.ItemType) + addition.Quantity;
        var items = combined.Select(pair => InventoryStack(pair.Key, pair.Value)).Where(item => item.CarriedInBackpack).ToArray();
        var weaponSlots = items.Count(item => item.Category == InventoryCategory.Weapon);
        var questSlots = items.Count(item => item.Category == InventoryCategory.Quest);
        var otherSlots = items.Count(item => item.Category == InventoryCategory.Other);
        var weight = items.Sum(item => item.UnitWeightPounds * item.Quantity);
        if (weaponSlots > MaximumWeaponSlots) message = $"Your backpack only has {MaximumWeaponSlots} weapon slots (your fist is always free).";
        else if (questSlots > MaximumQuestSlots) message = $"Your backpack only has {MaximumQuestSlots} quest-item slots.";
        else if (otherSlots > MaximumOtherSlots) message = $"Your backpack only has {MaximumOtherSlots} other-item slots.";
        else if (weight > MaximumBackpackWeightPounds + .0001) message = $"That would make your backpack weigh {weight:0.##} lb; the absolute maximum is {MaximumBackpackWeightPounds:0} lb.";
        else { message = string.Empty; return true; }
        return false;
    }

    private Task SaveInventoryAsync(string playerId, CancellationToken cancellationToken) => _store.SaveInventoryAsync(GetInventoryState(playerId), cancellationToken);

    private static bool CrossesDungeonWall(WorldPosition start, WorldPosition end, DungeonWall wall)
    {
        static double Cross(double ax,double ay,double bx,double by,double cx,double cy)=>(bx-ax)*(cy-ay)-(by-ay)*(cx-ax);
        var c1=Cross(start.X,start.Y,end.X,end.Y,wall.X1,wall.Y1);var c2=Cross(start.X,start.Y,end.X,end.Y,wall.X2,wall.Y2);var c3=Cross(wall.X1,wall.Y1,wall.X2,wall.Y2,start.X,start.Y);var c4=Cross(wall.X1,wall.Y1,wall.X2,wall.Y2,end.X,end.Y);if(!((c1<=0&&c2>=0||c1>=0&&c2<=0)&&(c3<=0&&c4>=0||c3>=0&&c4<=0)))return false;
        if(wall.DoorStart>=0){var coordinate=Math.Abs(wall.X1-wall.X2)<.01?(start.Y+end.Y)/2:(start.X+end.X)/2;if(coordinate>=wall.DoorStart&&coordinate<=wall.DoorEnd)return false;}return true;
    }

    private static int StableInt(string value) => BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(value)), 0);
}

public sealed record HostileTick(IReadOnlyList<ActorState> Actors, IReadOnlyList<PlayerState> Players, IReadOnlyList<CombatEvent> Combat);
public sealed record CombatResult(CombatEvent Event, PlayerState Attacker, PlayerState? TargetPlayer, InventoryState Inventory, RelationshipState? Relationship, DungeonState? Dungeon, IReadOnlyList<CombatEvent>? Consequences = null);
