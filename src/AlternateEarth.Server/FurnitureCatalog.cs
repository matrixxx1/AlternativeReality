using AlternateEarth.Shared;

namespace AlternateEarth.Server;

internal sealed record FurnitureDefinition(
    string Type,
    string DisplayName,
    double WidthMeters,
    double DepthMeters,
    long MinimumPriceCents,
    long MaximumPriceCents,
    string ImageKey,
    string[] Colors,
    string[] Patterns);

internal static class FurnitureCatalog
{
    public static readonly FurnitureDefinition[] All =
    [
        new("bed", "Bed", 2.1, 1.5, 30_000, 250_000, "bed", ["walnut", "white", "black", "navy", "sage"], ["solid", "striped", "plaid", "floral"]),
        new("bunkBed", "Bunk bed", 2.1, 1.2, 55_000, 220_000, "bed", ["oak", "white", "black", "blue"], ["solid", "striped"]),
        new("dresser", "Dresser", 1.5, .55, 25_000, 180_000, "dresser", ["oak", "walnut", "white", "black", "teal"], ["solid", "woodgrain"]),
        new("wardrobe", "Wardrobe", 1.5, .65, 50_000, 350_000, "wardrobe", ["oak", "walnut", "white", "black", "sage"], ["solid", "woodgrain", "paneled"]),
        new("nightstand", "Nightstand", .65, .55, 8_000, 50_000, "nightstand", ["oak", "walnut", "white", "black", "navy"], ["solid", "woodgrain"]),
        new("diningChair", "Dining chair", .55, .55, 8_000, 80_000, "chair", ["oak", "walnut", "white", "black", "red"], ["solid", "woodgrain", "woven"]),
        new("armchair", "Armchair", 1.0, .95, 35_000, 200_000, "armchair", ["tan", "navy", "burgundy", "sage", "gray"], ["solid", "striped", "plaid", "floral"]),
        new("recliner", "Recliner", 1.05, 1.05, 45_000, 250_000, "armchair", ["tan", "brown", "black", "navy", "gray"], ["solid", "leather"]),
        new("sofa", "Sofa", 2.2, .95, 60_000, 400_000, "sofa", ["tan", "navy", "burgundy", "sage", "gray"], ["solid", "striped", "plaid", "floral"]),
        new("loveseat", "Loveseat", 1.55, .9, 45_000, 280_000, "sofa", ["tan", "navy", "burgundy", "sage", "gray"], ["solid", "striped", "plaid"]),
        new("ottoman", "Ottoman", .8, .65, 8_000, 75_000, "ottoman", ["tan", "navy", "burgundy", "sage", "gray"], ["solid", "striped", "plaid", "floral"]),
        new("diningTable", "Dining table", 1.8, 1.0, 30_000, 300_000, "table", ["oak", "walnut", "white", "black"], ["solid", "woodgrain", "marble"]),
        new("coffeeTable", "Coffee table", 1.2, .65, 15_000, 120_000, "table", ["oak", "walnut", "white", "black", "glass"], ["solid", "woodgrain", "marble"]),
        new("sideTable", "Side table", .65, .65, 8_000, 65_000, "table", ["oak", "walnut", "white", "black"], ["solid", "woodgrain", "marble"]),
        new("desk", "Desk", 1.5, .7, 25_000, 200_000, "desk", ["oak", "walnut", "white", "black"], ["solid", "woodgrain"]),
        new("bookshelf", "Bookshelf", 1.2, .4, 15_000, 150_000, "bookshelf", ["oak", "walnut", "white", "black", "red"], ["solid", "woodgrain"]),
        new("barstool", "Barstool", .5, .5, 7_500, 50_000, "barstool", ["oak", "walnut", "white", "black", "red"], ["solid", "woven", "leather"]),
        new("bench", "Bench", 1.4, .55, 12_000, 90_000, "bench", ["oak", "walnut", "white", "black", "green"], ["solid", "woodgrain", "woven"]),
        new("cabinet", "Cabinet", 1.1, .5, 20_000, 180_000, "cabinet", ["oak", "walnut", "white", "black", "sage"], ["solid", "woodgrain", "paneled"]),
        new("storageChest", "Storage chest", 1.25, .65, 25_000, 150_000, "storageChest", ["oak", "walnut", "black"], ["solid", "woodgrain", "ironbound"]),
        new("vanity", "Vanity", 1.2, .55, 20_000, 175_000, "vanity", ["oak", "walnut", "white", "black", "rose"], ["solid", "woodgrain"]),
        new("floorLamp", "Floor lamp", .5, .5, 6_000, 50_000, "lamp", ["brass", "black", "white", "silver"], ["solid"]),
        new("tableLamp", "Table lamp", .35, .35, 3_000, 30_000, "lamp", ["brass", "black", "white", "blue", "rose"], ["solid", "striped", "floral"]),
        new("rug", "Area rug", 2.4, 1.7, 10_000, 200_000, "rug", ["red", "navy", "gold", "sage", "gray"], ["striped", "plaid", "floral", "geometric"]),
        new("fireplace", "Fireplace", 1.8, .65, 150_000, 800_000, "fireplace", ["brick", "stone", "white", "black"], ["masonry", "paneled"]),
        new("plant", "House plant", .55, .55, 2_500, 25_000, "plant", ["terracotta", "white", "black", "blue"], ["solid", "striped"]),
        new("grandfatherClock", "Grandfather clock", .7, .45, 40_000, 300_000, "clock", ["oak", "walnut", "black"], ["solid", "woodgrain"]),
        new("piano", "Upright piano", 1.5, .7, 80_000, 900_000, "piano", ["black", "white", "walnut"], ["solid", "woodgrain"]),
        new("recordCabinet", "Record cabinet", 1.0, .5, 15_000, 120_000, "cabinet", ["oak", "walnut", "white", "black"], ["solid", "woodgrain"])
        ,new("homeShopCounter", "Home shop counter", 1.8, .75, 75_000, 300_000, "shop", ["oak", "walnut", "white", "black", "red"], ["solid", "woodgrain", "striped"])
    ];

    private static readonly Dictionary<string, FurnitureDefinition> ByType = All.ToDictionary(item => item.Type, StringComparer.OrdinalIgnoreCase);

    public static string OfferId(FurnitureDefinition item, string color, string pattern) => $"furniture:{item.Type}:{color}:{pattern}";

    public static bool TryParse(string itemType, out FurnitureDefinition definition, out string color, out string pattern)
    {
        definition = null!; color = "natural"; pattern = "solid";
        var parts = itemType.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !parts[0].Equals("furniture", StringComparison.OrdinalIgnoreCase) || !ByType.TryGetValue(parts[1], out definition!)) return false;
        color = definition.Colors.Contains(parts[2], StringComparer.OrdinalIgnoreCase) ? parts[2] : definition.Colors[0];
        pattern = definition.Patterns.Contains(parts[3], StringComparer.OrdinalIgnoreCase) ? parts[3] : definition.Patterns[0];
        return true;
    }

    public static MerchantOffer CreateOffer(FurnitureDefinition item, Random random)
    {
        var color = item.Colors[random.Next(item.Colors.Length)];
        var pattern = item.Patterns[random.Next(item.Patterns.Length)];
        var properties = new Dictionary<string, string>
        {
            ["furnitureType"] = item.Type,
            ["color"] = color,
            ["pattern"] = pattern,
            ["widthMeters"] = item.WidthMeters.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["depthMeters"] = item.DepthMeters.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["description"] = $"{item.WidthMeters:0.##} m x {item.DepthMeters:0.##} m Home furniture"
        };
        return new MerchantOffer(OfferId(item, color, pattern), 1,
            random.NextInt64(item.MinimumPriceCents, item.MaximumPriceCents + 1),
            $"{Title(color)} {Title(pattern)} {item.DisplayName}", item.ImageKey, properties);
    }

    public static string Title(string value) => string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character) ? $" {char.ToLowerInvariant(character)}" : character.ToString())).Trim();
}
