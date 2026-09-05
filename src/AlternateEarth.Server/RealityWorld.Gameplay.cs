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
    private static readonly string[] FriendlyHumanSurnames =
    [
        "Morgan", "Rivera", "Chen", "Patel", "Brooks", "Nguyen", "Taylor", "Reed",
        "Foster", "Kim", "Ramirez", "Bennett", "Price", "Diaz", "Ward", "Hayes",
        "Sullivan", "Ortiz", "Parker", "Shaw", "Coleman", "Wright", "Flores", "Stone"
    ];
    private static readonly ItemConfiguration[] DefaultItemConfigurations =
    {
        new("fist","Fist","Permanent melee weapon; consumes no ammunition",.25,1.6,0,0,false,true,WeightPounds:0,Category:InventoryCategory.Weapon,Accuracy:1,AttackIntervalSeconds:.45),
        new("rock","Rock","Thrown weapon and ammunition",1,25,1,100,true,false,"rock",WeightPounds:.5,Category:InventoryCategory.Weapon,Accuracy:.72),
        new("ballBearing","Ball bearing","Slingshot ammunition",0,0,5,200,WeightPounds:.02),
        new("knife","Knife","Short melee weapon",2,1.6,2_000,4_000,true,true,WeightPounds:.6,Category:InventoryCategory.Weapon,Accuracy:.95,AttackIntervalSeconds:.35),
        new("sword","Sword","Extended melee weapon",5,2.3,30_000,50_000,true,true,WeightPounds:3,Category:InventoryCategory.Weapon,Accuracy:.93,AttackIntervalSeconds:.75),
        new("slingshot","Slingshot","Ranged weapon; consumes ball bearings",2,60,5_000,15_000,true,true,"ballBearing",WeightPounds:.4,Category:InventoryCategory.Weapon,Accuracy:.78),
        new("crossbow","Crossbow","Ranged weapon; consumes arrows",3,100,30_000,50_000,true,true,"arrow",WeightPounds:6.5,Category:InventoryCategory.Weapon,Accuracy:.86,AttackIntervalSeconds:1.4),
        new("arrow","Arrow","Crossbow ammunition",0,0,5,500,WeightPounds:.08),
        new("pistol","Pistol","Ranged weapon; consumes bullets",5,50,100_000,300_000,true,true,"bullet",WeightPounds:2,Category:InventoryCategory.Weapon,Accuracy:.82,AttackIntervalSeconds:.3),
        new("rifle","Rifle","Long-range weapon; consumes bullets",7,200,300_000,600_000,true,true,"bullet",WeightPounds:7.5,Category:InventoryCategory.Weapon,Accuracy:.9,AttackIntervalSeconds:.4),
        new("ar15","AR15","Selectable single-fire or three-round burst rifle; consumes bullets",7,200,80_000,180_000,true,true,"bullet",WeightPounds:6.5,Category:InventoryCategory.Weapon,Accuracy:.9,AttackIntervalSeconds:.4),
        new("machineGun","Machine gun","Ten-round-per-second automatic rifle with poor accuracy; consumes bullets",7,200,200_000,600_000,true,true,"bullet",WeightPounds:8.5,Category:InventoryCategory.Weapon,Accuracy:.42,AttackIntervalSeconds:.1),
        new("flamethrower","Flamethrower","Projects fire and consumes 0.2 gallon of gas per use",1,25,80_000,200_000,true,true,WeightPounds:13,Category:InventoryCategory.Weapon,Accuracy:.88,AttackIntervalSeconds:.8),
        new("rocketLauncher","Rocket launcher","Shoulder-fired explosive weapon; consumes rockets",50,300,150_000,300_000,true,true,"rocket",WeightPounds:15,Category:InventoryCategory.Weapon,Accuracy:.82,AttackIntervalSeconds:1.8),
        new("rocket","Rocket","Rocket-launcher ammunition",0,0,50_000,200_000,WeightPounds:5),
        new("grenade","Grenade","Thrown explosive with an eight-meter blast radius",30,35,3_000,10_000,true,false,"grenade",WeightPounds:.9,Category:InventoryCategory.Weapon,Accuracy:.7),
        new("molotovCocktail","Molotov cocktail","Thrown incendiary with a six-meter fire radius lasting ten seconds",10,30,500,2_500,true,false,"molotovCocktail",WeightPounds:1.5,Category:InventoryCategory.Weapon,Accuracy:.68),
        new("probulator","Probulator","UFO-mounted ten-second abduction beam",4,100,0,0,false,true,WeightPounds:0,Category:InventoryCategory.Weapon,CarriedInBackpack:false,Accuracy:.92),
        new("gorillaSmash","Gorilla smash","Stronghold gorilla melee attack",6,2.5,0,0,false,true,WeightPounds:0,Category:InventoryCategory.Weapon,CarriedInBackpack:false,Accuracy:.9,AttackIntervalSeconds:1.2),
        new("bullet","Bullet","Pistol and rifle ammunition",0,0,25,500,WeightPounds:.04),
        new("skateboard","Skateboard","Fast paved-surface travel",0,0,20_000,30_000,true,true,null,10.5,WeightPounds:5),
        new("bike","Bike","Faster travel with mild off-road penalty",0,0,40_000,50_000,true,true,null,10.5,WeightPounds:0),
        new("eBike","E-bike","Electric travel between a bike and dirt bike; battery lasts one mile",0,0,400_000,500_000,true,true,null,21.5,WeightPounds:0,CarriedInBackpack:false),
        new("dirtBike","Dirt bike","Parked motorized travel up to 40 mph",0,0,300_000,500_000,true,true,null,36.5,WeightPounds:250,CarriedInBackpack:false),
        new("motorcycle","Motorcycle","Parked motorized travel up to 90 mph",0,0,500_000,1_000_000,true,true,null,86.5,WeightPounds:0,CarriedInBackpack:false),
        new("ufo","UFO","Flying vehicle with a built-in Probulator beam",0,0,10_000_000,25_000_000,true,true,null,56.5,WeightPounds:0,CarriedInBackpack:false),
        new("gallonOfGas","Gallon of gas","Refuels the selected motor vehicle",0,0,500,1_000,WeightPounds:6.3),
        new("inflatableRaft","Inflatable raft","Safe travel through deep water",0,0,45_000,65_000,true,true,null,2.75,WeightPounds:0),
        new("flashlight","Flashlight","Directional light",0,0,1_000,5_000,true,true,null,0,50,WeightPounds:.5),
        new("lantern","Lantern","Circular area light",0,0,5_000,10_000,true,true,null,0,30,WeightPounds:1.5),
        new("candle","Candle","Consumable one-minute circular light at half lantern strength",0,0,1,500,true,false,null,0,15,WeightPounds:.1),
        new("laser","Laser","Straight light beam until collision",0,0,20_000,40_000,true,true,null,0,150,WeightPounds:.25),
        new("shield","Shield","Offhand shield; 50% ranged deflection chance and 25% damage reduction",0,0,5_000,10_000,true,true,WeightPounds:5),
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
        new("fireproofJacket","Fireproof jacket","Prevents the wearer from catching fire",0,0,12_000,35_000,true,true,WeightPounds:4),
        new("coolingShorts","Cooling shorts","Greatly reduce movement heat and shed heat quickly",0,0,2_500,7_500,true,true,WeightPounds:.5),
        new("warmingPants","Warming pants","Retain warmth and generate extra warmth while moving",0,0,4_500,12_000,true,true,WeightPounds:1.5),
        new("water","Water","One half-liter bottle; restores water and 2 hearts",0,0,50,200,WeightPounds:1.1),
        new("food","Food","Packed meal; restores stamina and 2 hearts",0,0,200,500,WeightPounds:.75),
        new("energyDrink","Energy drink","15 minutes of 2× speed and carrying capacity, followed by a 5-minute ⅕-speed and capacity crash",0,0,200,1_000,WeightPounds:1.1),
        new("areaMap","Map of this block","Permanently reveals the current geographic block",0,0,100,100_000,true,true,WeightPounds:.05),
        new("personalFlag","Personal flag","Place and name one of up to five shared map flags",0,0,0,0,false,false,WeightPounds:.01),
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
            [TravelMode.Bike] = 0, [TravelMode.Raft] = 0, [TravelMode.DirtBike] = 0, [TravelMode.Motorcycle] = 0, [TravelMode.EBike] = 0, [TravelMode.Ufo] = 0
        });
    private MovementConfiguration _movementConfiguration = DefaultMovementConfiguration;
    private static readonly ServerEventConfiguration DefaultEventConfiguration = new();
    private ServerEventConfiguration _eventConfiguration = DefaultEventConfiguration;
    private static readonly string[] WeaponPowerOrder = ["probulator", "rocketLauncher", "machineGun", "ar15", "flamethrower", "grenade", "molotovCocktail", "rifle", "sword", "pistol", "crossbow", "knife", "slingshot", "rock", "fist"];
    private static readonly HashSet<string> HatItems = new(["hat", "coolingHat", "warmHat"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ShirtItems = new(["tShirt", "coolingShirt", "longSleeveShirt", "sweater", "lightJacket", "winterJacket", "fireproofJacket"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> PantsItems = new(["coolingShorts", "warmingPants"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> OffhandItems = new(["flashlight", "lantern", "candle", "laser", "shield"], StringComparer.OrdinalIgnoreCase);
    private const double DirtBikeTankGallons = 2;
    private const double MotorcycleTankGallons = 4;
    private readonly ConcurrentDictionary<string, Dictionary<string, int>> _inventories = new();
    private readonly ConcurrentDictionary<(string Player, string Item), string> _weaponQualities = new();
    private readonly ConcurrentDictionary<(string Player, string Actor), double> _relationships = new();
    private readonly ConcurrentDictionary<string, DungeonState> _dungeons = new();
    private readonly ConcurrentDictionary<string, WorldPosition> _returnPositions = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastIdleHeal = new();
    private readonly ConcurrentDictionary<(string Player, string Merchant, long Rotation), TradeQuote> _tradeQuotes = new();
    private readonly ConcurrentDictionary<string, LootDropState> _loot = new();
    private readonly ConcurrentDictionary<string, TreasureChestState> _outdoorChests = new();
    private readonly ConcurrentDictionary<string, ChestContentsState> _chestContents = new();
    private readonly ConcurrentDictionary<(string Actor, string Player), DateTimeOffset> _lastActorAttack = new();
    private readonly ConcurrentDictionary<(string Player, string Weapon), DateTimeOffset> _lastPlayerAttack = new();
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
    private readonly ConcurrentDictionary<string, ActiveProbulatorBeam> _activeProbulatorBeams = new();
    private readonly ConcurrentDictionary<string, byte> _probulatorHits = new();
    private readonly ConcurrentDictionary<string, ActiveFireZone> _fireZones = new();
    private readonly ConcurrentDictionary<string, BurningTarget> _burningTargets = new();
    private readonly ConcurrentDictionary<string, byte> _swatDeployedFor = new();
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
        var serverConfiguration = new ServerConfigurationState(_itemConfigurations.Values.OrderBy(item => item.DisplayName).ToArray(), _movementConfiguration, ClientEventConfiguration);
        var revealedAreas = _revealedWorldAreas.Keys.Where(key => key.Player == playerId).Select(key => key.Area).OrderBy(key => key).ToArray();
        IReadOnlyList<CanonicalEntity>? homeStorage = null;
        InventoryState? homeItemStorage = null;
        string? homeAccount = null;
        var canEditHome = dungeon?.IsHome == true && _playerAccounts.TryGetValue(playerId, out homeAccount) && _baseBuildings.GetValueOrDefault(homeAccount) == dungeon.BuildingId;
        if (canEditHome && _homeFurniture.TryGetValue(homeAccount!, out var furniture))
        {
            homeStorage = furniture.Where(IsStoredFurniture).ToArray();
            homeItemStorage = GetHomeItemStorage(homeAccount!);
        }
        var quests = _quests.Where(pair => pair.Key.Player == playerId).Select(pair => pair.Value).OrderBy(quest => quest.Status).ThenBy(quest => quest.Title).ToArray();
        return new PlayerPrivateState(inventory, dungeon, relationships, chests, loot, baseState, ServerConfiguration: serverConfiguration, RevealedWorldAreas: revealedAreas, HomeStorage: homeStorage, HomeItemStorage: homeItemStorage, Quests: quests, CanEditHome: canEditHome);
    }

    public async Task<PlayerState> SetGodModeAsync(string playerId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        var hikingShoesOn = enabled ? player.MagicHikingShoesOn : player.MagicHikingShoesOn && InventoryQuantity(playerId, "magicHikingShoes") > 0;
        var runningShoesOn = enabled ? player.MagicRunningShoesOn : player.MagicRunningShoesOn && InventoryQuantity(playerId, "magicRunningShoes") > 0;
        if (hikingShoesOn && runningShoesOn) runningShoesOn = false;
        var offhand = ActiveOffhand(player);
        if (!enabled && offhand != "none" && offhand != "candle" && InventoryQuantity(playerId, offhand) <= 0) offhand = "none";
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
            ShieldOn = offhand == "shield",
            CandleUntilUtc = offhand == "candle" ? player.CandleUntilUtc : null,
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
        TravelMode.Ufo => InventoryQuantity(playerId, "ufo") <= 0,
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
            var lightingCandle = itemType?.Equals("candle", StringComparison.OrdinalIgnoreCase) == true;
            if (lightingCandle && !player.GodMode)
            {
                if (!RemoveInventory(playerId, "candle", 1)) throw new InvalidOperationException("You need a candle in your inventory.");
                await SaveInventoryAsync(playerId, cancellationToken);
            }
            updated = player with
            {
                FlashlightOn = itemType?.Equals("flashlight", StringComparison.OrdinalIgnoreCase) == true,
                LanternOn = itemType?.Equals("lantern", StringComparison.OrdinalIgnoreCase) == true,
                LaserOn = itemType?.Equals("laser", StringComparison.OrdinalIgnoreCase) == true,
                ShieldOn = itemType?.Equals("shield", StringComparison.OrdinalIgnoreCase) == true,
                CandleUntilUtc = lightingCandle ? DateTimeOffset.UtcNow.AddMinutes(1) : null,
                Version = player.Version + 1
            };
        }
        else if (slot == "weapon")
        {
            if (player.TravelMode == TravelMode.Ufo) throw new InvalidOperationException("The UFO weapon slot is locked to its Probulator.");
            itemType ??= "none";
            itemType = itemType.ToLowerInvariant();
            if (itemType == "probulator") throw new InvalidOperationException("The Probulator can only be used while piloting a UFO.");
            if (itemType != "none" && !WeaponPowerOrder.Contains(itemType)) throw new InvalidOperationException("That item cannot be equipped as a weapon.");
            if (itemType != "none" && !player.GodMode && !OwnsWeapon(playerId, itemType)) throw new InvalidOperationException($"You need {DisplayItem(itemType)} in your backpack.");
            updated = player with { EquippedWeapon = itemType, Version = player.Version + 1 };
        }
        else throw new InvalidOperationException("That equipment slot is not available yet.");
        await SavePlayerAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<PlayerState> SetWeaponModeAsync(string playerId, string mode, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (!player.EquippedWeapon.Equals("ar15", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Equip the AR15 before changing its fire mode.");
        mode = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (mode is not ("single" or "burst")) throw new InvalidOperationException("AR15 mode must be single or burst.");
        var updated = player with { Ar15FireMode = mode, Version = player.Version + 1 };
        await SavePlayerAsync(updated, cancellationToken);
        return updated;
    }

    private bool playerIsGod(string playerId) => _players.TryGetValue(playerId, out var player) && player.GodMode;
    public bool IsGodModeEnabled(string playerId) => playerIsGod(playerId);

    public async Task<IReadOnlyList<PlayerState>> AdvanceVitalsAsync(TimeSpan elapsed, CancellationToken cancellationToken)
    {
        var changed = new List<PlayerState>(); var now = DateTimeOffset.UtcNow;
        foreach (var pair in _players.ToArray())
        {
            var player = pair.Value; var updated = player;
            var idle = _lastMovement.TryGetValue(pair.Key, out var lastMove) ? now - lastMove : TimeSpan.Zero;
            if (player.CandleUntilUtc is { } candleUntil && candleUntil <= now) updated = updated with { CandleUntilUtc = null };
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
            updated = ApplyIdleRaftDrift(pair.Key, updated, idle, now);
            if (updated.Position == player.Position && updated.Stamina == player.Stamina && updated.Water == player.Water && updated.WalletCents == player.WalletCents && updated.HealthHearts == player.HealthHearts && updated.BodyHeat == player.BodyHeat && updated.WantedLevel == player.WantedLevel && updated.CandleUntilUtc == player.CandleUntilUtc) continue;
            updated = updated with { SpeedMetersPerSecond = idle > TimeSpan.FromSeconds(.5) ? 0 : updated.SpeedMetersPerSecond, Version = player.Version + 1 };
            if (await SavePlayerAsync(updated, cancellationToken)) changed.Add(updated);
        }
        foreach (var expired in _loot.Where(pair => pair.Value.ExpiresAtUtc <= now).Select(pair => pair.Key).ToArray()) _loot.TryRemove(expired, out _);
        foreach (var expired in _outdoorChests.Where(pair => pair.Value.ExpiresAtUtc <= now).Select(pair => pair.Key).ToArray()) { _outdoorChests.TryRemove(expired, out _); _chestContents.TryRemove(expired, out _); }
        return changed;
    }

    private PlayerState ApplyIdleRaftDrift(string playerId, PlayerState player, TimeSpan idle, DateTimeOffset now)
    {
        if (player.TravelMode != TravelMode.Raft || player.LocationId != "outdoor" ||
            player.Terrain is not (TerrainType.ShallowWater or TerrainType.DeepWater) ||
            !Weather.IsAvailable || Weather.WindSpeedKilometersPerHour <= 0)
        {
            _lastRaftDrift.TryRemove(playerId, out _);
            return player;
        }
        if (idle < TimeSpan.FromSeconds(1))
        {
            _lastRaftDrift[playerId] = now;
            return player;
        }

        var previous = _lastRaftDrift.GetOrAdd(playerId, now.AddSeconds(-1));
        var driftSeconds = Math.Min(2, (now - previous).TotalSeconds);
        if (driftSeconds < .95) return player;
        _lastRaftDrift[playerId] = now;

        var windSpeedMetersPerSecond = Weather.WindSpeedKilometersPerHour / 3.6;
        var driftSpeed = Math.Clamp(windSpeedMetersPerSecond * .03, 0, .35);
        if (driftSpeed < .005) return player;
        // Meteorological bearings describe where wind comes from; a free raft moves downwind.
        var downwindRadians = (Weather.WindDirectionDegrees + 180) * Math.PI / 180;
        var distance = driftSpeed * driftSeconds;
        var candidate = player.Position with
        {
            X = player.Position.X + Math.Sin(downwindRadians) * distance,
            Y = player.Position.Y + Math.Cos(downwindRadians) * distance
        };
        if (!_loadedAreas.Values.Any(area => area.Contains(candidate.X, candidate.Y)) ||
            !Navigation.CanTraverse(player.Position, candidate)) return player;
        var terrain = Navigation.TerrainAt(candidate.X, candidate.Y);
        if (terrain is not (TerrainType.ShallowWater or TerrainType.DeepWater)) return player;
        return player with
        {
            Position = candidate with { Z = Navigation.ElevationAt(candidate.X, candidate.Y) },
            Terrain = terrain,
            SpeedMetersPerSecond = 0
        };
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

    private static bool CandleActive(PlayerState player, DateTimeOffset? at = null) => player.CandleUntilUtc is { } until && until > (at ?? DateTimeOffset.UtcNow);
    private static string ActiveOffhand(PlayerState player) => player.ShieldOn ? "shield" : player.LaserOn ? "laser" : player.LanternOn ? "lantern" : player.FlashlightOn ? "flashlight" : CandleActive(player) ? "candle" : "none";
    private static bool IsFireproof(PlayerState player) => player.EquippedShirt.Equals("fireproofJacket", StringComparison.OrdinalIgnoreCase);
    private static double ShieldReducedDamage(PlayerState player, double damage) => player.ShieldOn ? damage * .75 : damage;
    private static bool ShieldDeflects(PlayerState player, bool ranged) => ranged && player.ShieldOn && RandomNumberGenerator.GetInt32(2) == 0;

    public async Task<PlayerState> ConsumeItemAsync(string playerId, string itemType, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        var normalized = itemType.Trim().ToLowerInvariant(); if (normalized is not ("food" or "water" or "energydrink" or "gallonofgas")) throw new InvalidOperationException("That item cannot be consumed.");
        if (normalized == "gallonofgas")
        {
            var fillingFlamethrower = player.EquippedWeapon == "flamethrower";
            if (!fillingFlamethrower && player.TravelMode is not (TravelMode.DirtBike or TravelMode.Motorcycle)) throw new InvalidOperationException("Equip your flamethrower or select your dirt bike or motorcycle before adding gas.");
            var current = fillingFlamethrower ? player.FlamethrowerGasGallons : FuelGallons(player);
            var capacity = fillingFlamethrower ? 5 : player.TravelMode == TravelMode.DirtBike ? DirtBikeTankGallons : MotorcycleTankGallons;
            if (current >= capacity - .0001) throw new InvalidOperationException(fillingFlamethrower ? "Your flamethrower tank is already full." : $"Your {VehicleName(player.TravelMode)} tank is already full.");
        }
        if (!RemoveInventory(playerId, normalized, 1)) throw new InvalidOperationException($"You do not have any {DisplayItem(normalized)}.");
        var now = DateTimeOffset.UtcNow;
        var updated = normalized == "food"
            ? player with { Stamina = player.MaximumStamina, HealthHearts = Math.Min(player.MaximumHealthHearts, player.HealthHearts + 2), FoodProtectedUntilUtc = now.AddMinutes(5), Version = player.Version + 1 }
            : normalized == "water"
                ? player with { Water = player.MaximumWater, HealthHearts = Math.Min(player.MaximumHealthHearts, player.HealthHearts + 2), WaterProtectedUntilUtc = now.AddMinutes(5), Version = player.Version + 1 }
                : normalized == "energydrink"
                    ? player with { EnergyDrinkBoostUntilUtc = now.AddMinutes(15), EnergyDrinkCrashUntilUtc = now.AddMinutes(20), Version = player.Version + 1 }
                : player.EquippedWeapon == "flamethrower"
                    ? player with { FlamethrowerGasGallons = Math.Min(5, player.FlamethrowerGasGallons + 1), Version = player.Version + 1 }
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
        if (building.Properties.GetValueOrDefault("state") == "rubble") throw new InvalidOperationException("That building has been destroyed.");
        var ownsBase = _playerAccounts.TryGetValue(playerId, out var accountId) && _baseBuildings.GetValueOrDefault(accountId) == buildingId;
        var homeClaim = _publicBaseClaims.GetValueOrDefault(buildingId);
        var isClaimedHome = homeClaim is not null;
        if (!ownsBase && !isClaimedHome && IsBuildingLocked(building) && !_pickedLocks.ContainsKey($"{playerId}:{doorId}:{CurrentDoorLockCycle}")) throw new InvalidOperationException("This building is locked. Door locks change every four hours.");
        string dungeonId; DungeonState dungeon;
        if (isClaimedHome)
        {
            dungeonId = $"home:{homeClaim!.AccountId}:{buildingId}";
            await EnsureHomeFurnitureAsync(homeClaim.AccountId, building, cancellationToken);
            dungeon = _dungeons.GetOrAdd(dungeonId, _ => GenerateHome(dungeonId, building));
        }
        else
        {
            dungeonId = $"dungeon:{buildingId}";
            dungeon = _dungeons.GetOrAdd(dungeonId, _ => CreateDungeonSession(dungeonId, building));
        }
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
        var target = _dungeons.GetOrAdd(targetId, _ => GenerateDungeonFloor(sessionId, building, targetLevel, current.LevelCount, current.Stairs, current.Difficulty));
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
        if (!PlayersOccupyingSession(dungeonId, playerId)) await ResetDungeonSessionAsync(playerId, dungeonId, cancellationToken);
        return updated;
    }

    private bool PlayersOccupyingSession(string dungeonId, string excludingPlayerId)
    {
        var sessionId = _dungeons.TryGetValue(dungeonId, out var dungeon) ? dungeon.SessionId ?? dungeonId : dungeonId;
        return _players.Values.Any(player => player.Id != excludingPlayerId && (player.LocationId == sessionId || player.LocationId.StartsWith(sessionId + ":level:", StringComparison.Ordinal)));
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
        await _store.ResetSharedDungeonStateAsync(Configuration.Id, sessionId, cancellationToken);
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
            foreach (var former in _publicBaseClaims.Where(pair => pair.Value.AccountId == accountId && pair.Key != buildingId).Select(pair => pair.Key).ToArray()) _publicBaseClaims.TryRemove(former, out _);
            _baseBuildings[accountId] = buildingId;
            _publicBaseClaims[buildingId] = new PublicBaseClaim(accountId, buildingId, player.Name);
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
        var difficulty = DungeonDifficulty(building);
        var levelCount = RandomDungeonLevelCount(building, difficulty);
        var layout = CreateInteriorLayout(building);
        var stairRandom = new Random(StableInt($"{sessionId}:stairs"));
        WorldPosition? stairs = levelCount > 1 ? RandomInteriorPosition(stairRandom, layout, building.Position.Region, layout.Exit) : null;
        return GenerateDungeonFloor(sessionId, building, 1, levelCount, stairs, difficulty);
    }

    public static int DungeonDifficulty(CanonicalEntity building)
    {
        var squareFeet = BuildingSquareFeet(building);
        if (squareFeet <= 2_000) return 1;
        var difficulty = 1 + (squareFeet - 2_000) * 49 / 8_000d;
        return Math.Clamp((int)Math.Round(difficulty, MidpointRounding.AwayFromZero), 1, 100);
    }

    private static int RandomDungeonLevelCount(CanonicalEntity building, int difficulty)
    {
        if (int.TryParse(building.Properties.GetValueOrDefault("dungeon:levels"), out var specified)) return Math.Clamp(specified, 1, 10);
        if (difficulty <= 1) return 1;
        var minimum = difficulty > 50 ? 5 : difficulty >= 40 ? 3 : difficulty >= 20 ? 2 : 1;
        var maximum = Math.Clamp(1 + (int)Math.Ceiling(difficulty / 7d), minimum, 10);
        return RandomNumberGenerator.GetInt32(minimum, maximum + 1);
    }

    private DungeonState GenerateDungeonFloor(string sessionId, CanonicalEntity building, int level, int levelCount, WorldPosition? stairs, int difficulty)
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
        var expectedActors = Math.Clamp(3 + difficulty / 8 + (level - 1) / 2, 3, 17);
        var actorCount = random.Next(Math.Max(3, expectedActors - 1), Math.Min(19, expectedActors + 2));
        for (var i = 0; i < actorCount; i++)
        {
            var merchant = i == 0 && random.NextDouble() < .55; var foe = merchant ? random.NextDouble() * 2 : -2 + random.NextDouble() * 4;
            var position = RandomInteriorPosition(random, layout, region, stairs);
            var actorId = $"{id}:npc:{i}";
            var actorName = UniqueNpcName(FriendlyHumanName(id, i), actorId, actors);
            var maximumHealth = Math.Round(Math.Clamp(4 + difficulty * .2 + (level - 1) * .55 + random.NextDouble() * 2.5, 4, 35) * 2) / 2;
            var weapon = merchant ? "pistol" : DungeonWeaponFor(difficulty, level, random);
            actors.Add(new ActorState(actorId, EntityKind.Npc, merchant ? "merchant" : "resident", actorName,
                position, HealthHearts: maximumHealth, MaximumHealthHearts: maximumHealth,
                FriendRating: foe, IsMerchant: merchant, TravelMode: (TravelMode)random.Next(0, 4), LocationId: id,
                EquippedWeapon: weapon, WeaponQuality: DungeonWeaponQuality(difficulty, level, random)));
        }
        if (difficulty > 50 && random.NextDouble() < Math.Min(.85, .25 + difficulty / 150d))
        {
            var gorillaId = $"{id}:gorilla"; var gorillaPosition = RandomInteriorPosition(random, layout, region, stairs);
            actors.Add(new ActorState(gorillaId, EntityKind.Animal, "giantGorilla", UniqueNpcName("Goliath", gorillaId, actors), gorillaPosition,
                HealthHearts: 50, MaximumHealthHearts: 50, FriendRating: -8, TravelMode: TravelMode.Run, LocationId: id,
                EquippedWeapon: "gorillaSmash", WeaponQuality: DungeonWeaponQuality(difficulty, level, random)));
        }
        var chests = Enumerable.Range(0, random.Next(1, 4)).Select(i => new TreasureChestState($"{id}:chest:{i}", RandomInteriorPosition(random, layout, region, stairs), id)).ToArray();
        return new DungeonState(id, building.Id, width, height, rooms, walls, layout.Exit, actors, chests, Array.Empty<string>(),
            Footprint: layout.Footprint, ExteriorWallCount: exteriorWallCount, Level: level, LevelCount: levelCount,
            Stairs: stairs, Doorway: layout.Doorway, SessionId: sessionId, Difficulty: difficulty);
    }

    private static string DungeonWeaponFor(int difficulty, int level, Random random)
    {
        var threat = Math.Clamp(difficulty + (level - 1) * 2, 1, 100);
        string[] weapons = threat switch
        {
            <= 2 => ["fist"],
            <= 8 => ["fist", "fist", "knife", "rock"],
            <= 18 => ["knife", "rock", "sword", "slingshot"],
            <= 35 => ["sword", "slingshot", "crossbow"],
            <= 50 => ["sword", "crossbow", "pistol"],
            <= 75 => ["sword", "crossbow", "pistol", "rifle"],
            _ => ["crossbow", "pistol", "rifle", "rifle"]
        };
        return weapons[random.Next(weapons.Length)];
    }

    private static string DungeonWeaponQuality(int difficulty, int level, Random random)
    {
        var threat = Math.Clamp(difficulty + (level - 1) * 2, 1, 100);
        var center = Math.Clamp((threat - 1) / 11, 0, WeaponQualityNames.Length - 1);
        return WeaponQualityNames[Math.Clamp(center + random.Next(-1, 2), 0, WeaponQualityNames.Length - 1)];
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
        var merchantId = $"{sessionId}:merchant";
        actors.Add(new ActorState(merchantId, EntityKind.Npc, "storeMerchant", UniqueNpcName(FriendlyHumanName(sessionId, 0), merchantId, actors), merchantPosition,
            HealthHearts: 8, MaximumHealthHearts: 8, FriendRating: 1, IsMerchant: true, LocationId: sessionId,
            MerchantCategory: store.Category, EquippedWeapon: "pistol", FactionId: factionId));
        var employeeCount = random.Next(2, 7);
        for (var index = 0; index < employeeCount; index++)
        {
            var position = RandomInteriorPosition(random, layout, building.Position.Region, layout.Exit);
            var employeeId = $"{sessionId}:employee:{index}";
            actors.Add(new ActorState(employeeId, EntityKind.Npc, "storeEmployee", UniqueNpcName(FriendlyHumanName(sessionId, index + 1), employeeId, actors), position,
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
        var surname = (StableInt($"friendly-surname:{scope}:{ordinal}") & int.MaxValue) % FriendlyHumanSurnames.Length;
        return $"{FriendlyHumanNames[(start + ordinal) % FriendlyHumanNames.Length]} {FriendlyHumanSurnames[surname]}";
    }

    private string UniqueNpcName(string preferred, string actorId, IEnumerable<ActorState>? pending = null)
    {
        var used = _actors.Values.Select(actor => actor.Name)
            .Concat(_dungeons.Values.SelectMany(dungeon => dungeon.Actors).Select(actor => actor.Name))
            .Concat(pending?.Select(actor => actor.Name) ?? Array.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = preferred.Contains(' ') ? preferred : $"{preferred} {FriendlyHumanSurnames[(StableInt(actorId) & int.MaxValue) % FriendlyHumanSurnames.Length]}";
        if (!used.Contains(candidate)) return candidate;
        for (var number = 2; ; number++) if (!used.Contains($"{candidate} {number}")) return $"{candidate} {number}";
    }

    private string UniqueAnimalName(string subtype, string preferred, string actorId)
    {
        string[] names = subtype switch
        {
            "dog" => ["Bark Twain", "Indiana Bones", "Chewbarka", "Woofgang", "Captain Sniff", "Biscuit"],
            "cat" => ["Chairman Meow", "Purrlock Holmes", "Fuzz Aldrin", "Tuna Turner", "Cat Benatar", "Mittens"],
            "rabbit" => ["Thumper", "Clover", "Hazel", "Nibbles", "Hopper", "Bun Jovi"],
            "bird" => ["Chirpy", "Sky", "Feathers", "Kawvin", "Peep", "Wings"],
            "deer" => ["Fern", "Maple", "Buckley", "Willow", "Antler", "Meadow"],
            "cougar" => ["Shadow", "Ember", "Canyon", "Sierra", "Claw", "Puma Thurman"],
            "bear" => ["Marmalade", "Kodiak", "Honey", "Grumbles", "Paddington", "Ursa"],
            _ => [preferred]
        };
        var used = _actors.Values.Select(actor => actor.Name).Concat(_dungeons.Values.SelectMany(dungeon => dungeon.Actors).Select(actor => actor.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = names[(StableInt($"animal-name:{actorId}") & int.MaxValue) % names.Length];
        if (!used.Contains(candidate)) return candidate;
        for (var number = 2; ; number++) if (!used.Contains($"{candidate} {number}")) return $"{candidate} {number}";
    }

    public async Task<PlayerState> RestAtBedAsync(string playerId,string bedId,CancellationToken cancellationToken=default)
    {
        if(!_players.TryGetValue(playerId,out var player)||!_dungeons.TryGetValue(player.LocationId,out var home)||!home.IsHome||!_playerAccounts.TryGetValue(playerId,out var restAccount)||_baseBuildings.GetValueOrDefault(restAccount)!=home.BuildingId)throw new InvalidOperationException("You can only rest in your own bed.");
        var bed=home.Furnishings?.FirstOrDefault(item=>item.Id==bedId&&item.Properties.GetValueOrDefault("objectType")=="bed")??throw new InvalidOperationException("Bed not found.");
        var now=DateTimeOffset.UtcNow;
        if(EnergyDrinkBoostActive(player,now))throw new InvalidOperationException("You are too energized to sleep. Wait for the 15-minute boost to end.");
        if(player.Position.Distance2D(bed.Position)>4)throw new InvalidOperationException("Move closer to the bed.");var until=now.AddMinutes(5);
        var updated=player with{HealthHearts=player.MaximumHealthHearts,Stamina=player.MaximumStamina,Water=player.MaximumWater,FoodProtectedUntilUtc=until,WaterProtectedUntilUtc=until,EnergyDrinkBoostUntilUtc=null,EnergyDrinkCrashUntilUtc=null,ProbedUntilUtc=null,Version=player.Version+1};await SavePlayerAsync(updated,cancellationToken);return updated;
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
        long proceeds = 0; foreach (var line in sales) { if (line.ItemType.Equals("personalFlag", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Personal flags cannot be sold."); var offer = (quote.BuyOffers ?? Array.Empty<MerchantOffer>()).FirstOrDefault(o => o.ItemType.Equals(line.ItemType, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("The merchant will not buy that item."); if (line.Quantity > offer.Quantity || InventoryQuantity(playerId, line.ItemType) < line.Quantity) throw new InvalidOperationException("You do not have that many to sell."); checked { proceeds += offer.UnitPriceCents * line.Quantity; } }
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
            else
            {
                var quality = quote.Offers.FirstOrDefault(offer => offer.ItemType.Equals(line.ItemType, StringComparison.OrdinalIgnoreCase))?.Properties?.GetValueOrDefault("quality");
                AddInventory(playerId, line.ItemType, line.Quantity, quality);
            }
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
            .Where(item => item.ItemType != "fist" && !item.ItemType.StartsWith("quest:", StringComparison.OrdinalIgnoreCase) && item.ItemType != "areaMap" && item.ItemType != "personalFlag")
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
            "gas" => new HashSet<string>(["gallonOfGas", "food", "water", "energyDrink"], StringComparer.OrdinalIgnoreCase),
            "clothing" => new HashSet<string>(["hat", "coolingHat", "warmHat", "tShirt", "coolingShirt", "longSleeveShirt", "sweater", "lightJacket", "winterJacket", "fireproofJacket", "coolingShorts", "warmingPants", "magicHikingShoes", "magicRunningShoes"], StringComparer.OrdinalIgnoreCase),
            "food" => new HashSet<string>(["food", "water", "energyDrink"], StringComparer.OrdinalIgnoreCase),
            "convenience" => new HashSet<string>(["food", "water", "energyDrink", "candle"], StringComparer.OrdinalIgnoreCase),
            "weapons" => new HashSet<string>(["rock", "ballBearing", "knife", "sword", "slingshot", "crossbow", "arrow", "pistol", "rifle", "ar15", "machineGun", "flamethrower", "bullet", "rocketLauncher", "rocket", "grenade", "molotovCocktail"], StringComparer.OrdinalIgnoreCase),
            "hardware" => new HashSet<string>(["rock", "ballBearing", "knife", "sword", "slingshot", "crossbow", "arrow", "pistol", "rifle", "ar15", "machineGun", "bullet", "rocketLauncher", "rocket", "grenade", "molotovCocktail", "shield", "bike", "flashlight", "lantern", "candle", "laser", "lockPickSet"], StringComparer.OrdinalIgnoreCase),
            "sportingGoods" => new HashSet<string>(["rock", "ballBearing", "knife", "sword", "slingshot", "crossbow", "arrow", "pistol", "rifle", "ar15", "bullet", "grenade", "molotovCocktail", "shield", "skateboard", "bike", "magicHikingShoes", "magicRunningShoes", "inflatableRaft"], StringComparer.OrdinalIgnoreCase),
            "vehicles" => new HashSet<string>(["eBike", "dirtBike", "motorcycle", "ufo", "gallonOfGas"], StringComparer.OrdinalIgnoreCase),
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
        if (merchant.MerchantCategory == "vehicles") { EnsureSelected("eBike"); EnsureSelected("dirtBike"); EnsureSelected("motorcycle"); EnsureSelected("ufo"); }
        if (playerId is not null && merchant.LocationId == "outdoor" && !_revealedWorldAreas.ContainsKey((playerId, AreaKeyFor(merchant.Position.X, merchant.Position.Y)))) selected.Add(_itemConfigurations["areaMap"]);
        return selected.Select(item =>
        {
            var properties = new Dictionary<string, string> { ["description"] = item.Effect };
            if (item.Category == InventoryCategory.Weapon && item.ItemType is not ("fist" or "probulator")) properties["quality"] = RandomWeaponQuality(random);
            return new MerchantOffer(item.ItemType, item.Single ? 1 : random.Next(3, 31), random.NextInt64(item.MinimumPriceCents, item.MaximumPriceCents + 1), item.DisplayName, item.ItemType, properties);
        }).ToArray();
    }

    private long MerchantInventoryRotation(DateTimeOffset value) => value.ToUnixTimeSeconds() / (Math.Max(1, _eventConfiguration.MerchantRefreshMinutes) * 60L);

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
        "energydrink" => "an energy drink",
        "energyDrink" => "an energy drink",
        "ballBearing" => "a ball bearing",
        "fist" => "your fist",
        "none" => "no weapon",
        "crossbow" => "a crossbow",
        "knife" => "a knife",
        "sword" => "a sword",
        "pistol" => "a pistol",
        "rifle" => "a rifle",
        "ar15" => "an AR15",
        "machineGun" => "a machine gun",
        "flamethrower" => "a flamethrower",
        "rocketLauncher" => "a rocket launcher",
        "rocket" => "a rocket",
        "grenade" => "a grenade",
        "molotovCocktail" => "a Molotov cocktail",
        "probulator" => "the Probulator",
        "ufo" => "a UFO",
        "shield" => "a shield",
        "fireproofJacket" => "a fireproof jacket",
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
        if (player.TravelMode == TravelMode.Ufo) return ActivateProbulator(player, targetPosition, targetName);
        if (player.EquippedWeapon.Equals("probulator", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The Probulator can only be fired from your UFO.");
        if (player.EquippedWeapon.Equals("none", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Equip a weapon before attacking.");
        var weapon = BestUsableWeapon(playerId, player.EquippedWeapon, player.GodMode);
        var (range, baseDamage, ammo) = WeaponDefinition(weapon);
        baseDamage = WeaponDamageFor(playerId, weapon, baseDamage);
        var distance = player.Position.Distance2D(targetPosition);
        if (distance > range) throw new InvalidOperationException($"{targetName} is beyond the {range:0.#}-meter range of your {DisplayItem(weapon)}.");
        var ranged = weapon is not ("fist" or "knife" or "sword");
        if (ranged)
        {
            var blocked = player.LocationId == "outdoor"
                ? !Navigation.CanTraverse(player.Position, targetPosition)
                : _dungeons.TryGetValue(player.LocationId, out var lineOfSightDungeon) && lineOfSightDungeon.Walls.Any(wall => CrossesDungeonWall(player.Position, targetPosition, wall));
            if (blocked) throw new InvalidOperationException($"A wall, building, tree, or other solid object blocks your shot at {targetName}.");
        }
        var now = DateTimeOffset.UtcNow;
        var attackInterval = TimeSpan.FromSeconds(weapon == "ar15" && player.Ar15FireMode == "burst" ? 1 : Math.Clamp(_itemConfigurations.GetValueOrDefault(weapon)?.AttackIntervalSeconds ?? .5, .05, 10));
        if (_lastPlayerAttack.TryGetValue((playerId, weapon), out var priorAttack) && now - priorAttack < attackInterval)
            throw new InvalidOperationException($"Your {DisplayItem(weapon)} is not ready yet.");
        var shotCount = weapon == "ar15" && player.Ar15FireMode == "burst" ? 3 : 1;
        if (!player.GodMode && ammo is not null && !RemoveInventory(playerId, ammo, shotCount)) throw new InvalidOperationException($"You need {shotCount} {DisplayItem(ammo)}{(shotCount == 1 ? "" : "s")}.");
        if (weapon == "flamethrower" && !player.GodMode)
        {
            if (player.FlamethrowerGasGallons < .2) throw new InvalidOperationException("The flamethrower needs at least 0.2 gallon of gas.");
            player = player with { FlamethrowerGasGallons = Math.Max(0, player.FlamethrowerGasGallons - .2), Version = player.Version + 1 };
        }
        _lastPlayerAttack[(playerId, weapon)] = now;
        if (playerTarget?.TravelMode == TravelMode.Ufo && !ranged) throw new InvalidOperationException("Melee attacks cannot reach an occupied UFO. Use a ranged weapon.");
        var configuredAccuracy = Math.Clamp(_itemConfigurations.GetValueOrDefault(weapon)?.Accuracy ?? 1, 0, 1);
        if (shotCount == 3) configuredAccuracy *= .67;
        var rangeFraction = range <= 0 ? 1 : Math.Clamp(distance / range, 0, 1);
        var hitChance = Math.Clamp(configuredAccuracy * (1 - rangeFraction * .7), .01, 1);
        var shotHits = Enumerable.Range(0, shotCount).Select(_ => RandomNumberGenerator.GetInt32(1_000_000) < hitChance * 1_000_000).ToArray();
        var shieldDeflected = playerTarget is not null && shotHits.Any(value => value) && ShieldDeflects(playerTarget, ranged);
        if (shieldDeflected) Array.Fill(shotHits, false);
        var hit = shotHits.Any(value => value);
        var damage = hit ? (playerTarget is null ? baseDamage : ShieldReducedDamage(playerTarget, baseDamage)) * shotHits.Count(value => value) : 0;
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
        if (shotCount > 1)
        {
            for (var round = 1; round < shotCount; round++)
            {
                var roundDamage = shotHits[round] ? playerTarget is null ? baseDamage : ShieldReducedDamage(playerTarget, baseDamage) : 0;
                consequences.Add(new CombatEvent(player.Id, request.TargetId, "ar15", player.Position, targetPosition, shotHits[round], roundDamage, died && round == shotCount - 1, shotHits[round] ? $"AR15 burst round hit {targetName}." : $"AR15 burst round missed {targetName}.", hit ? Math.Max(0, targetHealth - damage) : targetHealth));
            }
        }
        if (hit && weapon is "molotovCocktail" or "flamethrower")
        {
            var fireNow = DateTimeOffset.UtcNow; var id = $"fire:{Guid.NewGuid():N}";
            _fireZones[id] = new ActiveFireZone(id, player.Id, player.LocationId, targetPosition, weapon == "flamethrower" ? 3 : 6, fireNow.AddSeconds(10), fireNow);
        }
        if (hit && weapon is "rocketLauncher" or "grenade" or "molotovCocktail")
        {
            var radius = weapon == "rocketLauncher" ? 12d : weapon == "grenade" ? 8d : 6d;
            var nearbyActors = ActorsAtLocation(player.LocationId).Where(target => target.Id != request.TargetId && target.Position.Distance2D(targetPosition) <= radius).ToArray();
            foreach (var target in nearbyActors)
            {
                var blastDistance = target.Position.Distance2D(targetPosition); var splash = Math.Max(1, baseDamage - (baseDamage - 1) * blastDistance / radius);
                var remaining = Math.Max(0, target.HealthHearts - splash); var blastKilled = remaining <= 0;
                UpdateActorHealth(player, target, remaining, blastKilled);
                consequences.Add(new CombatEvent(player.Id, target.Id, weapon + "Explosion", targetPosition, target.Position, true, splash, blastKilled, $"{target.Name} took {splash:0.##} hearts of blast damage.", remaining));
            }
            if (Configuration.PvpEnabled)
            foreach (var target in _players.Values.Where(target => target.Id != player.Id && target.Id != request.TargetId && target.LocationId == player.LocationId && target.Position.Distance2D(targetPosition) <= radius).ToArray())
            {
                var blastDistance = target.Position.Distance2D(targetPosition); var splash = ShieldReducedDamage(target, Math.Max(1, baseDamage - (baseDamage - 1) * blastDistance / radius));
                var remaining = target.GodMode ? Math.Max(1, target.HealthHearts - splash) : Math.Max(0, target.HealthHearts - splash); var blastKilled = remaining <= 0;
                var blastTarget = blastKilled ? ResetPlayer(target with { HealthHearts = 0 }) : target with { HealthHearts = remaining, Version = target.Version + 1 };
                await SavePlayerAsync(blastTarget, cancellationToken);
                consequences.Add(new CombatEvent(player.Id, target.Id, weapon + "Explosion", targetPosition, target.Position, true, splash, blastKilled, $"{target.Name} took {splash:0.##} hearts of blast damage.", blastTarget.HealthHearts));
            }
        }
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
        var message = shieldDeflected
            ? $"{targetName}'s shield deflected {player.Name}'s {DisplayItem(weapon)}."
            : hit
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

    private CombatResult ActivateProbulator(PlayerState player, WorldPosition targetPosition, string targetName)
    {
        if (player.LocationId != "outdoor") throw new InvalidOperationException("The UFO and its Probulator can only be used outdoors.");
        if (_activeProbulatorBeams.TryGetValue(player.Id, out var existing) && existing.EndsAtUtc > DateTimeOffset.UtcNow)
            throw new InvalidOperationException("The Probulator is already active.");
        var configuration = _itemConfigurations["probulator"];
        var distance = player.Position.Distance2D(targetPosition);
        if (distance > configuration.RangeMeters) throw new InvalidOperationException($"{targetName} is beyond the {configuration.RangeMeters:0.#}-meter Probulator range.");
        var dx = targetPosition.X - player.Position.X; var dy = targetPosition.Y - player.Position.Y;
        var length = Math.Max(.001, Math.Sqrt(dx * dx + dy * dy)); dx /= length; dy /= length;
        var accuracy = Math.Clamp(configuration.Accuracy * (1 - Math.Clamp(distance / Math.Max(1, configuration.RangeMeters), 0, 1) * .7), .01, 1);
        if (RandomNumberGenerator.GetInt32(1_000_000) >= accuracy * 1_000_000)
        {
            var missRadians = (8 + RandomNumberGenerator.GetInt32(18)) * Math.PI / 180 * (RandomNumberGenerator.GetInt32(2) == 0 ? -1 : 1);
            (dx, dy) = (dx * Math.Cos(missRadians) - dy * Math.Sin(missRadians), dx * Math.Sin(missRadians) + dy * Math.Cos(missRadians));
        }
        var now = DateTimeOffset.UtcNow; var ends = now.AddSeconds(10); var beamId = $"probulator:{player.Id}:{Guid.NewGuid():N}";
        _activeProbulatorBeams[player.Id] = new ActiveProbulatorBeam(beamId, player.Id, dx, dy, configuration.RangeMeters, now, ends);
        var end = player.Position with { X = player.Position.X + dx * configuration.RangeMeters, Y = player.Position.Y + dy * configuration.RangeMeters };
        var message = $"{player.Name} activated the Probulator for 10 seconds.";
        return new CombatResult(new CombatEvent(player.Id, string.Empty, "probulator", player.Position, end, false, 0, false, message, StatusEffect: "Probulator active", StatusEffectUntilUtc: ends),
            player, null, GetInventoryState(player.Id), null, null);
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
        var loadMultiplier = player.GodMode ? 1 : Math.Clamp(1 - GetInventoryState(player.Id).WeightPounds / (MaximumBackpackWeightPounds * 2), .5, 1);
        return WorldNavigation.MilesPerHour(mph) * (player.Water <= 0 ? .5 : 1) * (player.GodMode ? 5 : 1) * EnergyDrinkSpeedMultiplier(player) * (ProbedActive(player) ? .5 : 1) * loadMultiplier;
    }

    private static bool EnergyDrinkBoostActive(PlayerState player, DateTimeOffset? at = null) => player.EnergyDrinkBoostUntilUtc is { } boostUntil && boostUntil > (at ?? DateTimeOffset.UtcNow);
    private static bool EnergyDrinkCrashActive(PlayerState player, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        return player.EnergyDrinkBoostUntilUtc is { } boostUntil && boostUntil <= now && player.EnergyDrinkCrashUntilUtc is { } crashUntil && crashUntil > now;
    }
    private static double EnergyDrinkSpeedMultiplier(PlayerState player) => EnergyDrinkBoostActive(player) ? 2 : EnergyDrinkCrashActive(player) ? .2 : 1;
    private static double EnergyDrinkCarryingCapacity(PlayerState player) => MaximumBackpackWeightPounds * (EnergyDrinkBoostActive(player) ? 2 : EnergyDrinkCrashActive(player) ? .2 : 1);
    private double PlayerCarryingCapacity(string playerId) => _players.TryGetValue(playerId, out var player) ? EnergyDrinkCarryingCapacity(player) : MaximumBackpackWeightPounds;
    private static bool ProbedActive(PlayerState player, DateTimeOffset? at = null) => player.ProbedUntilUtc is { } until && until > (at ?? DateTimeOffset.UtcNow);

    private static IEnumerable<string> ActiveMovementItems(PlayerState player, bool? magicHikingShoes = null, bool? magicRunningShoes = null)
    {
        var travelItem = player.TravelMode switch
        {
            TravelMode.Skateboard => "skateboard", TravelMode.Bike => "bike", TravelMode.EBike => "eBike", TravelMode.Raft => "inflatableRaft",
            TravelMode.DirtBike => "dirtBike", TravelMode.Motorcycle => "motorcycle", TravelMode.Ufo => "ufo", _ => null
        };
        if (travelItem is not null) yield return travelItem;
        if (magicHikingShoes ?? player.MagicHikingShoesOn) yield return "magicHikingShoes";
        if (magicRunningShoes ?? player.MagicRunningShoesOn) yield return "magicRunningShoes";
        if (player.HatOn) yield return "hat";
        if (player.FlashlightOn) yield return "flashlight";
        if (player.LanternOn) yield return "lantern";
        if (CandleActive(player)) yield return "candle";
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
        if (!double.IsFinite(request.Accuracy) || request.Accuracy is < 0 or > 1) throw new InvalidOperationException("Accuracy must be between 0 and 1.");
        if (!double.IsFinite(request.AttackIntervalSeconds) || request.AttackIntervalSeconds is < .05 or > 10) throw new InvalidOperationException("Attack interval must be between 0.05 and 10 seconds.");
        if (request.MinimumPriceCents < 0 || request.MaximumPriceCents < request.MinimumPriceCents || request.MaximumPriceCents > 100_000_000_000) throw new InvalidOperationException("Enter a valid minimum and maximum price.");
        var updated = current with { Damage = request.Damage, RangeMeters = request.RangeMeters, MinimumPriceCents = request.MinimumPriceCents, MaximumPriceCents = request.MaximumPriceCents, SpeedModifierMph = request.SpeedModifierMph, VisibilityModifierMeters = request.VisibilityModifierMeters, Accuracy = request.Accuracy, AttackIntervalSeconds = request.AttackIntervalSeconds };
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

    public ServerEventConfiguration EventConfiguration => _eventConfiguration;
    public ServerEventConfiguration ClientEventConfiguration => _eventConfiguration with { ServerUtcOffsetMinutes = ServerUtcOffsetMinutes(DateTimeOffset.UtcNow) };

    public async Task<ServerEventConfiguration> UpdateServerEventConfigurationAsync(string playerId, UpdateServerEventsRequest request, CancellationToken cancellationToken = default)
    {
        if (!playerIsGod(playerId)) throw new InvalidOperationException("God Mode must be enabled to change server events.");
        static int InRange(int value, int minimum, int maximum, string name)
        {
            if (value < minimum || value > maximum) throw new InvalidOperationException($"{name} must be between {minimum} and {maximum}.");
            return value;
        }
        static string EventName(string? value, string label)
        {
            var name = (value ?? string.Empty).Trim();
            if (name.Length is < 3 or > 60 || name.Any(char.IsControl))
                throw new InvalidOperationException($"{label} must be 3 to 60 printable characters.");
            return name;
        }
        var serverTimeMode = (request.ServerTimeMode ?? string.Empty).Trim().ToLowerInvariant();
        if (serverTimeMode is not ("auto" or "manual")) throw new InvalidOperationException("Server-time mode must be auto or manual.");
        var serverTimeOffset = serverTimeMode == "manual"
            ? InRange(request.ServerTimeOffsetMinutes, -52_596_000, 52_596_000, "Manual server-time offset")
            : 0;
        var updated = new ServerEventConfiguration(
            InRange(request.WeatherRefreshMinutes, 1, 1_440, "Weather refresh"),
            InRange(request.StreetLightsOnHour, 0, 23, "Street-light on hour"),
            InRange(request.StreetLightsOffHour, 0, 23, "Street-light off hour"),
            InRange(request.BuildingLightsRefreshMinutes, 1, 1_440, "Building-light refresh"),
            InRange(request.MerchantRefreshMinutes, 1, 10_080, "Merchant refresh"),
            InRange(request.DoorLockRefreshMinutes, 1, 10_080, "Door-lock refresh"),
            InRange(request.UfoIntervalHours, 1, 168, "UFO interval"),
            InRange(request.UfoDurationMinutes, 1, 120, "UFO duration"),
            InRange(request.TrexIntervalHours, 1, 168, "T-Rex interval"),
            InRange(request.TrexDurationMinutes, 1, 120, "T-Rex duration"),
            InRange(request.BearIntervalHours, 1, 168, "Bear interval"),
            InRange(request.BearDurationMinutes, 1, 120, "Bear duration"),
            serverTimeOffset,
            request.WeatherMode.Trim().ToLowerInvariant(),
            request.TemperatureCelsius,
            InRange(request.BrontosaurusIntervalHours, 1, 168, "Brontosaurus interval"),
            InRange(request.BrontosaurusDurationMinutes, 1, 120, "Brontosaurus duration"),
            InRange(request.StegosaurusIntervalHours, 1, 168, "Stegosaurus interval"),
            InRange(request.StegosaurusDurationMinutes, 1, 120, "Stegosaurus duration"),
            InRange(request.RaptorIntervalHours, 1, 168, "Raptor interval"),
            InRange(request.RaptorDurationMinutes, 1, 120, "Raptor duration"),
            InRange(request.LandOfGiantsIntervalHours, 1, 168, "Land of the Giants interval"),
            InRange(request.LandOfGiantsDurationMinutes, 1, 120, "Land of the Giants duration"),
            EventName(request.UfoEventName, "UFO event name"),
            EventName(request.TrexEventName, "T-Rex event name"),
            EventName(request.BrontosaurusEventName, "Brontosaurus event name"),
            EventName(request.StegosaurusEventName, "Stegosaurus event name"),
            EventName(request.RaptorEventName, "Raptor event name"),
            EventName(request.LandOfGiantsEventName, "Land of the Giants event name"),
            EventName(request.BearEventName, "Bear event name"),
            serverTimeMode,
            0,
            InRange(request.WantedSwatThreshold, 1, 100, "SWAT wanted-level threshold"));
        if (updated.StreetLightsOnHour == updated.StreetLightsOffHour) throw new InvalidOperationException("Street-light on and off hours must differ.");
        if (updated.WeatherMode is not ("live" or "clear" or "rain" or "snow" or "fog" or "storm")) throw new InvalidOperationException("Weather mode must be live, clear, rain, snow, fog, or storm.");
        if (updated.TemperatureCelsius is < -90 or > 60) throw new InvalidOperationException("Temperature must be between -90 and 60 °C.");
        _eventConfiguration = updated;
        _tradeQuotes.Clear();
        ResetScheduledEventCycles(DateTimeOffset.UtcNow);
        await _store.SaveServerEventConfigurationAsync(Configuration.Id, updated, cancellationToken);
        await RefreshWeatherAsync(cancellationToken);
        return updated;
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
            if (random.NextDouble() < .8) items.Add(InventoryStack("rock", random.Next(1, 6), quality: RandomWeaponQuality(random)));
            if (random.NextDouble() < .6) items.Add(InventoryStack("ballBearing", random.Next(1, 9)));
            string[] usefulItems = ["pencil", "pen", "marker", "sprayPaint", "book", "calculator", "cellPhone", "arrow", "gallonOfGas", "knife", "slingshot"];
            var extraCount = random.Next(1, 4);
            for (var index = 0; index < extraCount; index++)
            {
                var itemType = usefulItems[random.Next(usefulItems.Length)];
                items.Add(InventoryStack(itemType, 1, quality: InventoryDefinition(itemType).Category == InventoryCategory.Weapon ? RandomWeaponQuality(random) : null));
            }
            var drop = new LootDropState($"loot:{Guid.NewGuid():N}", actor.Position, player.LocationId, random.Next(1, 1001), items, DateTimeOffset.UtcNow.AddMinutes(5)); _loot[drop.Id] = drop;
        }
    }

    public async Task<ChestOpenResult> OpenChestAsync(string playerId, string chestId, CancellationToken cancellationToken = default)
    {
        var (player, _, _) = ValidateTreasureChest(playerId, chestId);
        var contents = _chestContents.GetOrAdd(chestId, CreateChestContents);
        var collectedMoney = contents.MoneyCents;
        var updated = player;
        if (collectedMoney > 0)
        {
            updated = player with { WalletCents = player.WalletCents + collectedMoney, Version = player.Version + 1 };
            _chestContents[chestId] = contents = contents with { MoneyCents = 0 };
            await SavePlayerAsync(updated, cancellationToken);
        }
        var message = collectedMoney > 0 ? $"Collected {collectedMoney / 100m:C} cash. Choose any items you want to carry." : "Choose any remaining items you want to carry.";
        return new ChestOpenResult(updated, contents, message);
    }

    public async Task<ChestTakeResult> TakeChestItemsAsync(string playerId, TakeChestItemsRequest request, CancellationToken cancellationToken = default)
    {
        var (player, _, dungeon) = ValidateTreasureChest(playerId, request.ChestId);
        if (!_chestContents.TryGetValue(request.ChestId, out var contents)) throw new InvalidOperationException("Open the treasure chest first.");
        var requested = request.Items.Where(item => item.Quantity > 0).GroupBy(item => item.ItemType, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PurchaseLine(group.Key, group.Sum(line => line.Quantity))).ToArray();
        foreach (var line in requested)
        {
            var available = contents.Items.FirstOrDefault(item => item.ItemType.Equals(line.ItemType, StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0;
            if (line.Quantity > available) throw new InvalidOperationException($"The chest does not contain {line.Quantity} × {DisplayItem(line.ItemType)}.");
        }
        var rewards = requested.Select(line =>
        {
            var source = contents.Items.First(item => item.ItemType.Equals(line.ItemType, StringComparison.OrdinalIgnoreCase));
            return InventoryStack(line.ItemType, line.Quantity, quality: source.Quality);
        }).ToArray();
        if (!CanAddToBackpack(playerId, rewards, out var capacityMessage)) throw new InvalidOperationException(capacityMessage);
        foreach (var reward in rewards) AddInventory(playerId, reward.ItemType, reward.Quantity, reward.Quality);
        var remaining = contents.Items.Select(item => item with
        {
            Quantity = item.Quantity - requested.Where(line => line.ItemType.Equals(item.ItemType, StringComparison.OrdinalIgnoreCase)).Sum(line => line.Quantity)
        }).Where(item => item.Quantity > 0).ToArray();
        var removed = remaining.Length == 0;
        if (removed)
        {
            _chestContents.TryRemove(request.ChestId, out _);
            if (player.LocationId == "outdoor") _outdoorChests.TryRemove(request.ChestId, out _);
            else if (dungeon is not null) _dungeons[player.LocationId] = dungeon with { Chests = dungeon.Chests.Where(chest => chest.Id != request.ChestId).ToArray() };
        }
        else _chestContents[request.ChestId] = contents = contents with { Items = remaining };
        await SaveInventoryAsync(playerId, cancellationToken);
        var message = rewards.Length == 0 ? "Left all items in the chest." : $"Took {string.Join(", ", rewards.Select(item => $"{item.Quantity} × {DisplayItem(item.ItemType)}"))}.";
        return new ChestTakeResult(player, GetInventoryState(playerId), removed ? null : contents, removed, message);
    }

    private (PlayerState Player, TreasureChestState Chest, DungeonState? Dungeon) ValidateTreasureChest(string playerId, string chestId)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        DungeonState? dungeon = null;
        TreasureChestState? chest;
        if (player.LocationId == "outdoor") chest = _outdoorChests.GetValueOrDefault(chestId);
        else { _dungeons.TryGetValue(player.LocationId, out dungeon); chest = dungeon?.Chests.FirstOrDefault(item => item.Id == chestId); }
        if (chest is null) throw new InvalidOperationException("Chest not found.");
        if (chest.ExpiresAtUtc is not null && chest.ExpiresAtUtc <= DateTimeOffset.UtcNow) throw new InvalidOperationException("That treasure chest has disappeared.");
        if (player.Position.Distance2D(chest.Position) > 4) throw new InvalidOperationException("Move closer to the chest.");
        return (player, chest, dungeon);
    }

    private ChestContentsState CreateChestContents(string chestId)
    {
        var random = new Random(StableInt($"treasure:{Configuration.Seed}:{chestId}"));
        var rewards = new List<ItemStack> { InventoryStack("rock", random.Next(1, 8), quality: RandomWeaponQuality(random)) };
        var bearings = random.Next(0, 12);
        if (bearings > 0) rewards.Add(InventoryStack("ballBearing", bearings));
        string[] bonusPool = ["knife", "sword", "slingshot", "crossbow", "pistol", "rifle", "arrow", "bullet", "pencil", "pen", "marker", "sprayPaint", "book", "calculator", "cellPhone"];
        if (random.NextDouble() < .75)
        {
            var itemType = bonusPool[random.Next(bonusPool.Length)];
            rewards.Add(InventoryStack(itemType, 1, quality: InventoryDefinition(itemType).Category == InventoryCategory.Weapon ? RandomWeaponQuality(random) : null));
        }
        var combined = rewards.GroupBy(item => item.ItemType, StringComparer.OrdinalIgnoreCase)
            .Select(group => InventoryStack(group.Key, group.Sum(item => item.Quantity), quality: group.FirstOrDefault(item => item.Quality is not null)?.Quality)).ToArray();
        return new ChestContentsState(chestId, random.Next(25, 5001), combined);
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
        var changedActors = new Dictionary<string, ActorState>(); var changedPlayers = new List<PlayerState>(); var combat = new List<CombatEvent>(); var removedWorldObjects = new List<string>();
        var now = DateTimeOffset.UtcNow;
        foreach (var rubble in _baseEntities.Values.Where(entity => entity.Kind == EntityKind.Building && entity.Properties.GetValueOrDefault("state") == "rubble" && DateTimeOffset.TryParse(entity.Properties.GetValueOrDefault("destroyedUntilUtc"), out var until) && until <= now).ToArray())
        {
            var related = _baseEntities.Values.Where(entity => entity.Id == rubble.Id || entity.Kind == EntityKind.Door && entity.Properties.GetValueOrDefault("buildingId") == rubble.Id).ToArray();
            foreach (var entity in related)
            {
                if (!_baseEntities.TryRemove(entity.Id, out _)) continue;
                _removedBaseEntityIds[entity.Id] = 0; removedWorldObjects.Add(entity.Id);
                await _store.RemoveEntityAsync(Configuration.Id, entity, cancellationToken);
            }
        }
        if (removedWorldObjects.Count > 0) _navigation = new WorldNavigation(_loadedBounds ?? Configuration.Area.Bounds, _baseEntities.Values.Concat(_realityEntities.Values).ToArray(), _elevationSamples.Values.ToArray());
        foreach (var suspect in _players.Values.Where(player => player.LocationId == "outdoor").ToArray())
        {
            if (suspect.WantedLevel >= _eventConfiguration.WantedSwatThreshold && _swatDeployedFor.TryAdd(suspect.Id, 0))
            {
                for (var index = 0; index < 10; index++)
                {
                    var angle = index / 10d * Math.PI * 2; var requested = suspect.Position with { X = suspect.Position.X + Math.Cos(angle) * (28 + index), Y = suspect.Position.Y + Math.Sin(angle) * (28 + index) };
                    var spawn = Navigation.FindNearestWalkable(requested); var id = $"swat:{suspect.Id}:{Guid.NewGuid():N}";
                    var officer = new ActorState(id, EntityKind.Npc, "swatOfficer", UniqueNpcName($"SWAT Officer {FriendlyHumanName(id, index)}", id), spawn, MaximumHealthHearts: 10, HealthHearts: 10, TravelMode: TravelMode.Run, EquippedWeapon: "rifle", FactionId: $"swat:{suspect.Id}");
                    _actors[id] = officer; _relationships[(suspect.Id, id)] = -10; changedActors[id] = officer;
                    await _store.SaveRelationshipAsync(Configuration.Id, new RelationshipState(suspect.Id, id, -10), cancellationToken);
                }
            }
            else if (suspect.WantedLevel < _eventConfiguration.WantedSwatThreshold && _swatDeployedFor.TryRemove(suspect.Id, out _))
            {
                foreach (var officer in _actors.Values.Where(actor => actor.FactionId == $"swat:{suspect.Id}").ToArray()) _actors.TryRemove(officer.Id, out _);
            }
        }
        foreach (var pending in _pendingPolice.ToArray())
        {
            if (now < pending.Value.DueAtUtc || !_pendingPolice.TryRemove(pending.Key, out var response) || !_players.TryGetValue(response.PlayerId, out var suspect)) continue;
            var spawn = Navigation.FindNearestWalkable(response.WitnessPosition with { X = response.WitnessPosition.X + 8, Y = response.WitnessPosition.Y + 8 });
            var id = $"cop:{Guid.NewGuid():N}";
            var cop = new ActorState(id, EntityKind.Npc, "policeOfficer", UniqueNpcName($"Officer {FriendlyHumanName(id, 0)}", id), spawn, EquippedWeapon: "pistol");
            _actors[id] = cop; _relationships[(suspect.Id, id)] = -10;
            await _store.SaveRelationshipAsync(Configuration.Id, new RelationshipState(suspect.Id, id, -10), cancellationToken);
            changedActors[id] = cop;
        }
        foreach (var pair in _activeProbulatorBeams.ToArray())
        {
            var beam = pair.Value;
            if (beam.EndsAtUtc <= now || !_players.TryGetValue(beam.PlayerId, out var pilot) || pilot.TravelMode != TravelMode.Ufo || pilot.LocationId != "outdoor")
            {
                _activeProbulatorBeams.TryRemove(pair.Key, out _);
                continue;
            }
            var damage = _itemConfigurations.GetValueOrDefault("probulator")?.Damage ?? 4;
            bool Touches(WorldPosition position)
            {
                var x = position.X - pilot.Position.X; var y = position.Y - pilot.Position.Y;
                var along = x * beam.DirectionX + y * beam.DirectionY;
                if (along < 0 || along > beam.RangeMeters) return false;
                return Math.Abs(x * beam.DirectionY - y * beam.DirectionX) <= 2.5;
            }
            foreach (var target in _players.Values.Where(target => target.Id != pilot.Id && target.LocationId == "outdoor" && Configuration.PvpEnabled && Touches(target.Position)).ToArray())
            {
                if (!_probulatorHits.TryAdd($"{beam.Id}:{target.Id}", 0)) continue;
                if (ShieldDeflects(target, true))
                {
                    combat.Add(new CombatEvent(pilot.Id, target.Id, "probulator", pilot.Position, target.Position, false, 0, false, $"{target.Name}'s shield deflected the Probulator beam.", target.HealthHearts));
                    continue;
                }
                var original = target.Position; var drop = FindNearbySafeDrop(target.Position, $"{beam.Id}:{target.Id}");
                var defendedDamage = ShieldReducedDamage(target, damage);
                var health = target.GodMode ? Math.Max(1, target.HealthHearts - defendedDamage) : Math.Max(0, target.HealthHearts - defendedDamage); var died = health <= 0;
                var updated = died ? ResetPlayer(target with { HealthHearts = 0 }) : target with { Position = drop, Terrain = Navigation.TerrainAt(drop.X, drop.Y), TravelMode = TravelMode.Walk, EquippedWeapon = target.EquippedWeapon == "probulator" ? "fist" : target.EquippedWeapon, SpeedMetersPerSecond = 0, HealthHearts = health, Version = target.Version + 1 };
                if (!await SavePlayerAsync(updated, cancellationToken)) continue;
                changedPlayers.Add(updated);
                var dialogue = ProbulatorDialogue();
                combat.Add(new CombatEvent(pilot.Id, target.Id, "probulator", pilot.Position, original, true, defendedDamage, died, $"{pilot.Name}'s Probulator abducted {target.Name} for {defendedDamage:0.##} hearts damage.", updated.HealthHearts, died ? null : drop, "Abducted", now.AddSeconds(2), dialogue));
            }
            foreach (var target in _actors.Values.Where(target => target.Subtype != "ufo" && target.LocationId == "outdoor" && Touches(target.Position)).ToArray())
            {
                if (!_probulatorHits.TryAdd($"{beam.Id}:{target.Id}", 0)) continue;
                var original = target.Position; var health = Math.Max(0, target.HealthHearts - damage); var died = health <= 0;
                WorldPosition? drop = null;
                if (died) UpdateActorHealth(pilot, target, 0, true);
                else
                {
                    drop = FindNearbySafeDrop(target.Position, $"{beam.Id}:{target.Id}");
                    var updated = target with { Position = drop.Value, HealthHearts = health, IsMoving = false, TravelMode = TravelMode.Walk, Version = target.Version + 1 };
                    _actors[target.Id] = updated; changedActors[target.Id] = updated;
                }
                var relation = Relationship(pilot.Id, target.Id) - 1; _relationships[(pilot.Id, target.Id)] = relation;
                await _store.SaveRelationshipAsync(Configuration.Id, new RelationshipState(pilot.Id, target.Id, relation), cancellationToken);
                var dialogue = ProbulatorDialogue();
                combat.Add(new CombatEvent(pilot.Id, target.Id, "probulator", pilot.Position, original, true, damage, died, $"{pilot.Name}'s Probulator abducted {target.Name} for {damage:0.##} hearts damage.", health, drop, "Abducted", now.AddSeconds(2), dialogue));
            }
        }
        foreach (var pair in _fireZones.ToArray())
        {
            var zone = pair.Value;
            if (zone.EndsAtUtc <= now) { _fireZones.TryRemove(pair.Key, out _); continue; }
            foreach (var target in _players.Values.Where(target => target.LocationId == zone.LocationId && target.TravelMode != TravelMode.Ufo && !IsFireproof(target) && target.Position.Distance2D(zone.Position) <= zone.RadiusMeters).ToArray())
                _burningTargets.AddOrUpdate(target.Id, _ => new BurningTarget(target.Id, zone.OwnerId, true, now.AddSeconds(10), now), (_, existing) => existing with { OwnerId = zone.OwnerId, EndsAtUtc = now.AddSeconds(10) });
            foreach (var target in ActorsAtLocation(zone.LocationId).Where(target => target.Position.Distance2D(zone.Position) <= zone.RadiusMeters).ToArray())
                _burningTargets.AddOrUpdate(target.Id, _ => new BurningTarget(target.Id, zone.OwnerId, false, now.AddSeconds(10), now), (_, existing) => existing with { OwnerId = zone.OwnerId, EndsAtUtc = now.AddSeconds(10) });
        }
        foreach (var pair in _burningTargets.ToArray())
        {
            var burning = pair.Value;
            if (burning.EndsAtUtc <= now) { _burningTargets.TryRemove(pair.Key, out _); continue; }
            if (now - burning.LastDamageAtUtc < TimeSpan.FromSeconds(1)) continue;
            _burningTargets[pair.Key] = burning with { LastDamageAtUtc = now };
            if (burning.IsPlayer && _players.TryGetValue(burning.TargetId, out var target))
            {
                if (IsFireproof(target)) { _burningTargets.TryRemove(pair.Key, out _); continue; }
                var burnDamage = ShieldReducedDamage(target, 2);
                var health = target.GodMode ? Math.Max(1, target.HealthHearts - burnDamage) : Math.Max(0, target.HealthHearts - burnDamage); var died = health <= 0;
                var updated = died ? ResetPlayer(target with { HealthHearts = 0 }) : target with { HealthHearts = health, Version = target.Version + 1 };
                if (!await SavePlayerAsync(updated, cancellationToken)) continue;
                if (died) _burningTargets.TryRemove(pair.Key, out _); changedPlayers.Add(updated); combat.Add(new CombatEvent(burning.OwnerId, target.Id, "molotovFire", target.Position, target.Position, true, burnDamage, died, $"{target.Name} took {burnDamage:0.##} hearts of fire damage.", updated.HealthHearts, StatusEffect: "Burning", StatusEffectUntilUtc: burning.EndsAtUtc));
            }
            else if (!burning.IsPlayer && _actors.TryGetValue(burning.TargetId, out var actor))
            {
                var health = Math.Max(0, actor.HealthHearts - 2); var died = health <= 0;
                if (_players.TryGetValue(burning.OwnerId, out var owner)) UpdateActorHealth(owner, actor, health, died);
                else if (died) _actors.TryRemove(actor.Id, out _); else _actors[actor.Id] = actor with { HealthHearts = health, Version = actor.Version + 1 };
                if (died) _burningTargets.TryRemove(pair.Key, out _); else changedActors[actor.Id] = actor with { HealthHearts = health, Version = actor.Version + 1 };
                combat.Add(new CombatEvent(burning.OwnerId, actor.Id, "molotovFire", actor.Position, actor.Position, true, 2, died, $"{actor.Name} took 2 hearts of fire damage.", health, StatusEffect: "Burning", StatusEffectUntilUtc: burning.EndsAtUtc));
            }
        }
        foreach (var ufo in _actors.Values.Where(actor => actor.Subtype == "ufo").ToArray())
        foreach (var target in _players.Values.Where(player => player.LocationId == "outdoor").ToArray())
        {
            if (ufo.Position.Distance2D(target.Position) > 8 || !_ufoHits.TryAdd($"{ufo.Id}:{target.Id}", 0)) continue;
            var dropPosition = FindNearbyProbedDrop(target, ufo);
            var probedUntil = now.AddMinutes(5);
            var updatedTarget = target with
            {
                Position = dropPosition,
                Terrain = Navigation.TerrainAt(dropPosition.X, dropPosition.Y),
                TravelMode = TravelMode.Walk,
                SpeedMetersPerSecond = 0,
                ProbedUntilUtc = probedUntil,
                Version = target.Version + 1
            };
            if (!await SavePlayerAsync(updatedTarget, cancellationToken))
            {
                _ufoHits.TryRemove($"{ufo.Id}:{target.Id}", out _);
                continue;
            }
            _lastMovement[target.Id] = now;
            changedPlayers.Add(updatedTarget);
            combat.Add(new CombatEvent(ufo.Id, target.Id, "greenBeam", ufo.Position, target.Position, true, 0, false,
                $"A UFO abducted {target.Name}, dropped them nearby, and left them Probed for five minutes.", updatedTarget.HealthHearts,
                dropPosition, "Probed", probedUntil));
        }
        foreach (var ufo in _actors.Values.Where(actor => actor.Subtype == "ufo").ToArray())
        foreach (var victim in _actors.Values.Where(actor => actor.Subtype != "ufo" && actor.LocationId == "outdoor").ToArray())
        {
            if (ufo.Position.Distance2D(victim.Position) > 8 || !_ufoHits.TryAdd($"{ufo.Id}:{victim.Id}", 0)) continue;
            _actors.TryRemove(victim.Id, out _); _actorRoutes.TryRemove(victim.Id, out _);
            combat.Add(new CombatEvent(ufo.Id, victim.Id, "greenBeam", ufo.Position, victim.Position, true, 10, true, $"A UFO struck {victim.Name} with a green beam for 10 hearts.", 0));
        }
        foreach (var originalPredator in _actors.Values.Where(actor => IsEventPredator(actor.Subtype)).ToArray())
        {
            var predator = originalPredator; PlayerState? playerVictim = null; ActorState? actorVictim = null; var nearest = 60d;
            foreach (var candidate in _players.Values.Where(item => item.LocationId == "outdoor" && item.TravelMode != TravelMode.Ufo)) { var distance = predator.Position.Distance2D(candidate.Position); if (distance <= NpcSightRange(predator, candidate.Position) && distance < nearest) { nearest = distance; playerVictim = candidate; actorVictim = null; } }
            foreach (var candidate in _actors.Values.Where(item => item.Id != predator.Id && item.Subtype != "ufo" && !IsEventPredator(item.Subtype) && item.LocationId == "outdoor")) { var distance = predator.Position.Distance2D(candidate.Position); if (distance <= NpcSightRange(predator, candidate.Position) && distance < nearest) { nearest = distance; actorVictim = candidate; playerVictim = null; } }
            var targetPosition = playerVictim?.Position ?? actorVictim?.Position; if (targetPosition is null) continue;
            var maximumAttackRange = EventPredatorMaximumAttackRange(predator.Subtype);
            if (nearest > maximumAttackRange)
            {
                var step = Math.Min(Math.Max(0, nearest - maximumAttackRange * .82), ActorSpeed(predator.Subtype) * elapsed.TotalSeconds); var dx = (targetPosition.Value.X - predator.Position.X) / nearest; var dy = (targetPosition.Value.Y - predator.Position.Y) / nearest;
                var next = predator.Position with { X = predator.Position.X + dx * step, Y = predator.Position.Y + dy * step };
                if (Navigation.CanTraverse(predator.Position, next, true)) { predator = predator with { Position = next, Facing = Math.Abs(dx) > Math.Abs(dy) ? dx > 0 ? "east" : "west" : dy > 0 ? "north" : "south", IsMoving = true, Version = predator.Version + 1 }; _actors[predator.Id] = predator; changedActors[predator.Id] = predator; }
                continue;
            }
            var victimId = playerVictim?.Id ?? actorVictim!.Id; var cooldownKey = $"{predator.Id}:{victimId}";
            if (_eventAttackCooldowns.TryGetValue(cooldownKey, out var lastAttack) && now - lastAttack < TimeSpan.FromSeconds(1)) continue; _eventAttackCooldowns[cooldownKey] = now;
            var attack = SelectEventPredatorAttack(predator.Subtype, nearest);
            if (playerVictim is not null)
            {
                var defendedDamage = ShieldReducedDamage(playerVictim, attack.Damage);
                var died = !playerVictim.GodMode && playerVictim.HealthHearts <= defendedDamage; var health = playerVictim.GodMode ? Math.Max(1, playerVictim.HealthHearts - defendedDamage) : Math.Max(0, playerVictim.HealthHearts - defendedDamage);
                var updated = died ? ResetPlayer(playerVictim with { HealthHearts = 0, Version = playerVictim.Version + 1 }) : playerVictim with { HealthHearts = health, Version = playerVictim.Version + 1 }; await SavePlayerAsync(updated, cancellationToken); changedPlayers.Add(updated);
                combat.Add(new CombatEvent(predator.Id, playerVictim.Id, attack.Weapon, predator.Position, playerVictim.Position, true, defendedDamage, died, $"{predator.Name} {attack.Description} {playerVictim.Name} for {defendedDamage:0.##} hearts.", updated.HealthHearts));
            }
            else
            {
                var health = Math.Max(0, actorVictim!.HealthHearts - attack.Damage); var died = health <= 0;
                if (died) { _actors.TryRemove(actorVictim.Id, out _); _actorRoutes.TryRemove(actorVictim.Id, out _); }
                else { var updated = actorVictim with { HealthHearts = health, Version = actorVictim.Version + 1 }; _actors[updated.Id] = updated; changedActors[updated.Id] = updated; }
                combat.Add(new CombatEvent(predator.Id, actorVictim.Id, attack.Weapon, predator.Position, actorVictim.Position, true, attack.Damage, died, died ? $"{predator.Name} killed {actorVictim.Name}." : $"{predator.Name} {attack.Description} {actorVictim.Name} for {attack.Damage:0.##} hearts.", health));
            }
        }
        foreach (var player in _players.Values.ToArray())
        {
            _dungeons.TryGetValue(player.LocationId, out var currentDungeon);
            var actors = player.LocationId == "outdoor" ? _actors.Values.ToArray() : currentDungeon?.Actors.ToArray() ?? Array.Empty<ActorState>();
            var carryingQuestDrugs = _quests.Where(pair => pair.Key.Player == player.Id && pair.Value.Kind == "drugDelivery" && pair.Value.Status is "active" or "ready")
                .Any(pair => InventoryQuantity(player.Id, QuestDrugItem(pair.Value)) > 0);
            var target = actors.Select(actor => (Actor: actor, Rating: Relationship(player.Id, actor.Id)))
                .Where(item => (item.Rating < 0 || carryingQuestDrugs && item.Actor.FactionId == $"drug-watch:{player.Id}") &&
                    (item.Actor.Subtype != "policeOfficer" || player.WantedLevel > 0 || carryingQuestDrugs) &&
                    item.Actor.Position.Distance2D(player.Position) <= (player.LocationId == "outdoor" ? NpcSightRange(item.Actor, player.Position) : 45))
                .OrderBy(item => item.Actor.Position.Distance2D(player.Position)).FirstOrDefault();
            if (target.Actor is null) continue;
            var actor = target.Actor; var distance = actor.Position.Distance2D(player.Position); var hostility = Math.Abs(target.Rating);
            var weapon = actor.EquippedWeapon is null or "none" ? "fist" : actor.EquippedWeapon;
            var ranged = weapon is not ("fist" or "knife" or "sword");
            var weaponConfiguration = _itemConfigurations.GetValueOrDefault(weapon);
            var weaponRange = Math.Max(1.35, weaponConfiguration?.RangeMeters ?? 1.35);
            var lineBlocked = ranged && (player.LocationId == "outdoor"
                ? !Navigation.CanTraverse(actor.Position, player.Position)
                : currentDungeon!.Walls.Any(wall => CrossesDungeonWall(actor.Position, player.Position, wall)));
            var engagementRange = ranged && !lineBlocked ? Math.Max(2, weaponRange * .72) : 1.35;
            if (distance > engagementRange)
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
            if (player.TravelMode == TravelMode.Ufo && weapon is "fist" or "knife" or "sword") continue;
            if (ranged && lineBlocked) continue;
            var configuredAccuracy = Math.Clamp(weaponConfiguration?.Accuracy ?? 1, 0, 1);
            var hitChance = Math.Clamp(configuredAccuracy * (1 - Math.Clamp(distance / weaponRange, 0, 1) * .7), .01, 1);
            var hit = RandomNumberGenerator.GetInt32(1_000_000) < hitChance * 1_000_000;
            var shieldDeflected = hit && ShieldDeflects(player, ranged);
            if (shieldDeflected) hit = false;
            var weaponDamage = (weaponConfiguration?.Damage ?? .5) * WeaponQualityMultiplier(actor.WeaponQuality);
            var damage = hit ? ShieldReducedDamage(player, Math.Clamp(weaponDamage + Math.Min(2, hostility * .25), .25, 50)) : 0;
            var health = player.GodMode ? Math.Max(1, player.HealthHearts - damage) : Math.Max(0, player.HealthHearts - damage);
            var died = hit && health <= 0; var updated = died ? ResetPlayer(player with { HealthHearts = 0 }) : player with { HealthHearts = health, Version = player.Version + 1 };
            if (!await SavePlayerAsync(updated, cancellationToken)) continue;
            changedPlayers.Add(updated);
            var message = shieldDeflected ? $"{player.Name}'s shield deflected {actor.Name}'s {DisplayItem(weapon)}."
                : !hit ? $"{actor.Name} missed {player.Name}."
                : died ? $"{actor.Name} defeated {player.Name}." : $"{actor.Name} hit {player.Name} for {damage:0.##} heart{(damage == 1 ? "" : "s")} damage.";
            combat.Add(new CombatEvent(actor.Id, player.Id, weapon, actor.Position, player.Position, hit, damage, died, message, updated.HealthHearts));
        }
        return new HostileTick(changedActors.Values.ToArray(), changedPlayers, combat, removedWorldObjects);
    }

    private WorldPosition FindNearbyProbedDrop(PlayerState target, ActorState ufo)
    {
        var bounds = _loadedBounds ?? Configuration.Area.Bounds;
        var initialAngle = (StableInt($"probed-drop:{ufo.Id}:{target.Id}") & int.MaxValue) / (double)int.MaxValue * Math.PI * 2;
        foreach (var distance in new[] { 8d, 6d, 10d, 4d })
        for (var offset = 0; offset < 8; offset++)
        {
            var angle = initialAngle + offset * Math.PI / 4;
            var requested = bounds.Clamp(target.Position with
            {
                X = target.Position.X + Math.Cos(angle) * distance,
                Y = target.Position.Y + Math.Sin(angle) * distance
            });
            var safe = Navigation.IsBlocked(requested.X, requested.Y) || Navigation.TerrainAt(requested.X, requested.Y) == TerrainType.DeepWater
                ? Navigation.FindNearestWalkable(requested)
                : requested with { Z = Navigation.ElevationAt(requested.X, requested.Y) };
            if (safe.Distance2D(target.Position) >= 2 && safe.Distance2D(target.Position) <= 20 &&
                !Navigation.IsBlocked(safe.X, safe.Y) && Navigation.TerrainAt(safe.X, safe.Y) != TerrainType.DeepWater)
                return safe with { Z = Navigation.ElevationAt(safe.X, safe.Y) };
        }
        var fallback = Navigation.FindNearestWalkable(target.Position);
        return fallback with { Z = Navigation.ElevationAt(fallback.X, fallback.Y) };
    }

    private WorldPosition FindNearbySafeDrop(WorldPosition origin, string key)
    {
        var bounds = _loadedBounds ?? Configuration.Area.Bounds;
        var initialAngle = (StableInt($"safe-drop:{key}") & int.MaxValue) / (double)int.MaxValue * Math.PI * 2;
        foreach (var distance in new[] { 4d, 6d, 8d, 10d, 12d })
        for (var offset = 0; offset < 12; offset++)
        {
            var angle = initialAngle + offset * Math.PI / 6;
            var requested = bounds.Clamp(origin with { X = origin.X + Math.Cos(angle) * distance, Y = origin.Y + Math.Sin(angle) * distance });
            var safe = Navigation.IsBlocked(requested.X, requested.Y) || Navigation.TerrainAt(requested.X, requested.Y) == TerrainType.DeepWater ? Navigation.FindNearestWalkable(requested) : requested;
            if (!Navigation.IsBlocked(safe.X, safe.Y) && Navigation.TerrainAt(safe.X, safe.Y) != TerrainType.DeepWater)
                return safe with { Z = Navigation.ElevationAt(safe.X, safe.Y) };
        }
        var fallback = Navigation.FindNearestWalkable(origin);
        return fallback with { Z = Navigation.ElevationAt(fallback.X, fallback.Y) };
    }

    private static string ProbulatorDialogue()
    {
        if (RandomNumberGenerator.GetInt32(100) == 0) return "butt I poop from there!";
        string[] lines = ["Nooooo!", "Arg!", "Ahhhh!", "Put me down!", "What is happening?!"];
        return lines[RandomNumberGenerator.GetInt32(lines.Length)];
    }

    private static bool IsEventPredator(string subtype) => subtype is "tRex" or "eventBear" or "brontosaurus" or "stegosaurus" or "raptor" or "giant" or "zombie";

    private static double EventPredatorMaximumAttackRange(string subtype) => subtype switch
    {
        "brontosaurus" => 10,
        "tRex" => 7,
        "stegosaurus" => 5,
        "raptor" => 1.8,
        "giant" => 4.5,
        "zombie" => 1.5,
        _ => 2.5
    };

    internal static IReadOnlyList<(string Weapon, double Damage, double RangeMeters)> EventPredatorAttackProfile(string subtype) => subtype switch
    {
        "brontosaurus" => new[] { ("brontosaurusTail", 5d, 10d), ("brontosaurusStomp", 10d, 3.5d) },
        "stegosaurus" => new[] { ("stegosaurusTail", 4d, 5d) },
        "raptor" => new[] { ("raptorBite", 3d, 1.8d) },
        "giant" => new[] { ("giantStomp", 4d, 4.5d) },
        "tRex" => new[] { ("trexBite", 7d, 4.5d), ("trexTail", 3d, 7d) },
        "zombie" => new[] { ("zombieBite", 1d, 1.5d) },
        _ => new[] { ("bite", 10d, 2.5d) }
    };

    private (string Weapon, double Damage, string Description) SelectEventPredatorAttack(string subtype, double distance)
    {
        var attacks = EventPredatorAttackProfile(subtype).Where(attack => distance <= attack.RangeMeters).ToArray();
        var selected = attacks.Length == 1 ? attacks[0] : attacks.Length > 1 ? attacks[NextActorRandom(attacks.Length)] : EventPredatorAttackProfile(subtype)[0];
        var description = selected.Weapon switch
        {
            "brontosaurusTail" => "struck",
            "brontosaurusStomp" => "stomped",
            "stegosaurusTail" => "whipped",
            "trexTail" => "tail-whipped",
            "giantStomp" => "stepped on",
            _ => "bit"
        };
        return (selected.Weapon, selected.Damage, description);
    }

    private double NpcSightRange(ActorState observer, WorldPosition target)
    {
        var sight = Weather.IsDay ? 100d : 14d + Math.Clamp(Weather.MoonIllumination, 0, 1) * 42d;
        if (!Weather.IsDay)
        {
            var serverHour = CurrentServerTime.Hour;
            var lightsOn = _eventConfiguration.StreetLightsOnHour > _eventConfiguration.StreetLightsOffHour
                ? serverHour >= _eventConfiguration.StreetLightsOnHour || serverHour < _eventConfiguration.StreetLightsOffHour
                : serverHour >= _eventConfiguration.StreetLightsOnHour && serverHour < _eventConfiguration.StreetLightsOffHour;
            var nearStreetLight = lightsOn && _baseEntities.Values.Any(entity => entity.Kind == EntityKind.StreetLight && entity.Position.Distance2D(target) <= 24);
            var nearPlayerLight = _players.Values.Any(player => player.LocationId == "outdoor" && player.Position.Distance2D(target) <= 24 && (player.LanternOn || CandleActive(player) || player.FlashlightOn || player.LaserOn));
            if (nearStreetLight || nearPlayerLight) sight = Math.Max(sight, 75);
        }
        var condition = Weather.Condition ?? string.Empty;
        if (condition.Contains("snow", StringComparison.OrdinalIgnoreCase)) sight *= .55;
        else if (condition.Contains("rain", StringComparison.OrdinalIgnoreCase) || Weather.PrecipitationMillimeters > 0) sight *= Math.Clamp(.82 - Weather.PrecipitationMillimeters * .025, .45, .82);
        return Math.Max(8, sight);
    }

    private int NextActorRandom(int maximum)
    {
        lock (_actorRandom) return _actorRandom.Next(maximum);
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
        _loot.TryRemove(lootId, out _); foreach (var item in loot.Items) AddInventory(playerId, item.ItemType, item.Quantity, item.Quality);
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
    private void AddInventory(string playerId, string item, int quantity, string? quality = null)
    {
        var inventory = _inventories.GetOrAdd(playerId, _ => new(StringComparer.OrdinalIgnoreCase));
        lock (inventory) inventory[item] = inventory.GetValueOrDefault(item) + quantity;
        if (InventoryDefinition(item).Category == InventoryCategory.Weapon)
            _weaponQualities.TryAdd((playerId, item), NormalizeWeaponQuality(quality) ?? (item is "fist" or "probulator" ? "Common" : RandomWeaponQuality()));
    }
    private bool RemoveInventory(string playerId, string item, int quantity)
    {
        var inventory = _inventories.GetOrAdd(playerId, _ => new(StringComparer.OrdinalIgnoreCase));
        lock (inventory)
        {
            var current = inventory.GetValueOrDefault(item); if (current < quantity) return false;
            inventory[item] = current - quantity;
            if (inventory[item] <= 0) { inventory.Remove(item); _weaponQualities.TryRemove((playerId, item), out _); }
            return true;
        }
    }

    private ItemConfiguration InventoryDefinition(string itemType) => _itemConfigurations.TryGetValue(itemType, out var definition)
        ? definition
        : new ItemConfiguration(itemType, DisplayItem(itemType), "Uncatalogued item", 0, 0, 0, 0, false, WeightPounds: 1,
            Category: itemType.StartsWith("quest:", StringComparison.OrdinalIgnoreCase) ? InventoryCategory.Quest : InventoryCategory.Other);

    private ItemStack InventoryStack(string itemType, int quantity, string? playerId = null, string? quality = null)
    {
        var definition = InventoryDefinition(itemType);
        if (definition.Category == InventoryCategory.Weapon && playerId is not null) quality = _weaponQualities.GetValueOrDefault((playerId, itemType));
        return new ItemStack(itemType, quantity, definition.Category, definition.WeightPounds, definition.CarriedInBackpack, quality);
    }

    private IReadOnlyList<ItemStack> GetInventoryItems(string playerId)
    {
        if (!_inventories.TryGetValue(playerId, out var inventory)) return Array.Empty<ItemStack>();
        lock (inventory) return inventory.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key).Select(pair => InventoryStack(pair.Key, pair.Value, playerId)).ToArray();
    }

    private static readonly string[] WeaponQualityNames = ["Crude", "Poor", "Worn", "Common", "Fine", "Superior", "Masterwork", "Epic", "Legendary", "Godly"];
    private static string? NormalizeWeaponQuality(string? quality) => WeaponQualityNames.FirstOrDefault(value => value.Equals(quality, StringComparison.OrdinalIgnoreCase));
    private static string RandomWeaponQuality(Random? random = null)
    {
        var roll = random?.Next(1000) ?? RandomNumberGenerator.GetInt32(1000);
        return roll switch { < 100 => "Crude", < 220 => "Poor", < 370 => "Worn", < 600 => "Common", < 760 => "Fine", < 870 => "Superior", < 935 => "Masterwork", < 975 => "Epic", < 995 => "Legendary", _ => "Godly" };
    }
    private static double WeaponQualityMultiplier(string? quality) => NormalizeWeaponQuality(quality) switch
    {
        "Crude" => .55, "Poor" => .7, "Worn" => .85, "Fine" => 1.15, "Superior" => 1.3,
        "Masterwork" => 1.5, "Epic" => 1.75, "Legendary" => 2, "Godly" => 2.5, _ => 1
    };
    private double WeaponDamageFor(string playerId, string weapon, double configuredDamage) => configuredDamage * WeaponQualityMultiplier(_weaponQualities.GetValueOrDefault((playerId, weapon)));

    private InventoryState GetInventoryState(string playerId)
    {
        var items = GetInventoryItems(playerId);
        var carried = items.Where(item => item.CarriedInBackpack).ToArray();
        return new InventoryState(playerId, items,
            Math.Round(carried.Sum(item => item.UnitWeightPounds * item.Quantity), 3), PlayerCarryingCapacity(playerId),
            carried.Count(item => item.Category == InventoryCategory.Weapon), MaximumWeaponSlots,
            carried.Count(item => item.Category == InventoryCategory.Quest), MaximumQuestSlots,
            carried.Count(item => item.Category == InventoryCategory.Other && !item.ItemType.Equals("personalFlag", StringComparison.OrdinalIgnoreCase)), MaximumOtherSlots);
    }

    private bool CanAddToBackpack(string playerId, IEnumerable<ItemStack> additions, out string message)
    {
        var combined = GetInventoryItems(playerId).ToDictionary(item => item.ItemType, item => item.Quantity, StringComparer.OrdinalIgnoreCase);
        foreach (var addition in additions.Where(item => item.Quantity > 0)) combined[addition.ItemType] = combined.GetValueOrDefault(addition.ItemType) + addition.Quantity;
        var items = combined.Select(pair => InventoryStack(pair.Key, pair.Value)).Where(item => item.CarriedInBackpack).ToArray();
        var weaponSlots = items.Count(item => item.Category == InventoryCategory.Weapon);
        var questSlots = items.Count(item => item.Category == InventoryCategory.Quest);
        var otherSlots = items.Count(item => item.Category == InventoryCategory.Other && !item.ItemType.Equals("personalFlag", StringComparison.OrdinalIgnoreCase));
        var weight = items.Sum(item => item.UnitWeightPounds * item.Quantity);
        if (weaponSlots > MaximumWeaponSlots) message = $"Your backpack only has {MaximumWeaponSlots} weapon slots (your fist is always free).";
        else if (questSlots > MaximumQuestSlots) message = $"Your backpack only has {MaximumQuestSlots} quest-item slots.";
        else if (otherSlots > MaximumOtherSlots) message = $"Your backpack only has {MaximumOtherSlots} other-item slots.";
        else
        {
            var capacity = PlayerCarryingCapacity(playerId);
            if (weight > capacity + .0001) message = $"That would make your backpack weigh {weight:0.##} lb; your maximum current carrying capacity is {capacity:0} lb.";
            else { message = string.Empty; return true; }
        }
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

public sealed record HostileTick(IReadOnlyList<ActorState> Actors, IReadOnlyList<PlayerState> Players, IReadOnlyList<CombatEvent> Combat, IReadOnlyList<string>? RemovedWorldObjectIds = null);
public sealed record CombatResult(CombatEvent Event, PlayerState Attacker, PlayerState? TargetPlayer, InventoryState Inventory, RelationshipState? Relationship, DungeonState? Dungeon, IReadOnlyList<CombatEvent>? Consequences = null);
public sealed record ActiveProbulatorBeam(string Id, string PlayerId, double DirectionX, double DirectionY, double RangeMeters, DateTimeOffset StartedAtUtc, DateTimeOffset EndsAtUtc);
public sealed record ActiveFireZone(string Id, string OwnerId, string LocationId, WorldPosition Position, double RadiusMeters, DateTimeOffset EndsAtUtc, DateTimeOffset LastDamageAtUtc);
public sealed record BurningTarget(string TargetId, string OwnerId, bool IsPlayer, DateTimeOffset EndsAtUtc, DateTimeOffset LastDamageAtUtc);
public sealed record ChestOpenResult(PlayerState Player, ChestContentsState Contents, string Message);
public sealed record ChestTakeResult(PlayerState Player, InventoryState Inventory, ChestContentsState? Contents, bool ChestRemoved, string Message);
