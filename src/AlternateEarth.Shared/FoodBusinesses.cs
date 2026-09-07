namespace AlternateEarth.Shared;

public static class FoodBusinesses
{
    public static bool OffersDelivery(IReadOnlyDictionary<string, string> tags) =>
        tags.GetValueOrDefault("amenity") is "restaurant" or "fast_food" or "food_court" or "cafe" or "ice_cream" ||
        tags.GetValueOrDefault("shop") is "deli" or "bakery" ||
        tags.GetValueOrDefault("takeaway") is "yes" or "only" || tags.GetValueOrDefault("delivery") == "yes";
}
