using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    // Metadata travels with the item through inventory, storage and persistent loot.
    private readonly ConcurrentDictionary<string, PhotographState> _photographs = new(StringComparer.OrdinalIgnoreCase);

    private static void ValidatePhotographThumbnail(string? thumbnail)
    {
        if (thumbnail is null) return;
        const string prefix = "data:image/png;base64,";
        if (!thumbnail.StartsWith(prefix, StringComparison.Ordinal) || thumbnail.Length > 44000)
            throw new InvalidOperationException("Camera prints must be small PNG thumbnails.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(thumbnail[prefix.Length..]); }
        catch (FormatException) { throw new InvalidOperationException("Invalid camera thumbnail."); }
        if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) != 128 ||
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) != 96)
            throw new InvalidOperationException("Camera thumbnails must be 128 by 96 pixels.");
    }

    private void RestorePhotographs(IEnumerable<ItemStack> items)
    {
        foreach (var item in items)
            if (item.ItemType.StartsWith("photograph:") && item.Photograph is { } photo)
                _photographs[item.ItemType] = photo;
    }

    private ItemConfiguration PhotographDefinition(string itemType, PhotographState photo) =>
        new(itemType, "Photograph: " + photo.Subject, "Submit as matching quest evidence or sell to a vendor. Either consumes the print.",
            0, 0, photo.Kind == "scenery" ? 50 : 250, photo.Kind == "scenery" ? 150 : 750, false, WeightPounds: .01);

    private QuestState RedeemPhotographs(string playerId, QuestState quest)
    {
        var inventory = _inventories[playerId];
        lock (inventory)
        {
            var requiredKind = quest.Kind == "adventure:photo" ? "event" : "inspection";
            var prints = inventory.Where(i => i.Value > 0 && _photographs.TryGetValue(i.Key, out var p) && p.Kind == requiredKind)
                .Select(i => (Item: i.Key, Photo: _photographs[i.Key]))
                .OrderBy(p => p.Photo.TakenAtUtc).ThenBy(p => p.Item)
                .DistinctBy(p => p.Photo.Evidence).Take(3).ToArray();
            if (prints.Length != 3)
                throw new InvalidOperationException(requiredKind == "event"
                    ? "Bring photographs of three different event creatures. Sold, stored or previously submitted prints do not count."
                    : "Bring one photograph each of a dungeon barrier, water crossing and exit.");
            foreach (var print in prints) RemoveInventory(playerId, print.Item, 1);
            return quest with { Progress = 3, ObjectiveActorIds = prints.Select(p => p.Item).ToArray() };
        }
    }
}
