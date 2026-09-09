namespace AlternateEarth.Shared;

public sealed record AdventureDefinition(string Id, string Title, string Story, string Subject, string[] Choices, string? Supply = null, bool Escort = false);
public static class AdventureCatalog
{
    public static readonly IReadOnlyList<AdventureDefinition> All = new AdventureDefinition[]
    {
        new("goose", "The Goose Is Loose", "A goose stole the merchant's keys and wallet. Chase it, or offer food and lure it home.", "Goose with suspicious wallet", ["Offer food", "Catch goose"], "food", true),
        new("mimic", "Definitely Not a Mimic", "Deliver a chest that keeps growing legs. Bribe it with coins, or subdue it. Keep it close on the way home.", "Emotionally distressed chest", ["Offer coins", "Subdue chest"], Escort: true),
        new("deliveries", "Employee of the Apocalypse", "Make three deliveries inside an active inversion. The customer's home being crushed is no excuse for cold fries.", "Extremely demanding customer", ["Deliver order"]),
        new("scream", "A Quiet Place to Scream", "Escort this easily startled customer to meditation class. Their screams attract nearby enemies. Offer food to calm them.", "Meditation enthusiast", ["Begin escort", "Offer food"], "food", true),
        new("refund", "Return Policy", "Retrieve a defective weapon from a bandit. It loudly announces the owner's location before firing.", "Dissatisfied armed customer", ["Negotiate refund", "Exchange weapon", "Fight bandit"]),
        new("gnomes", "Lawn and Order", "Follow the footprints, question a neighbor, then decide whether to return the stolen garden gnomes or fund their army.", "Gnome general", ["Inspect footprints", "Question neighbor", "Return gnomes", "Support gnome army"]),
        new("roll", "The Last Roll", "Three survivors want one package of toilet paper. Choose who gets it, share it, or negotiate a trade.", "Desperate survivor", ["Give supplies", "Split supplies", "Trade supplies"]),
        new("ufo", "My Other Car Is a UFO", "Follow three increasingly useful clues to the owner's missing vehicle. The last clue: it has no wheels.", "Forgetful UFO owner", ["Ask for clue", "Inspect vehicle"]),
        new("ghost", "Unfinished Business", "An ancient grudge turns out to concern a borrowed lawn mower. Return it, replace it, or persuade the ghost to let go.", "Ghost with a lawn problem", ["Return mower", "Buy replacement", "Persuade ghost"]),
        new("bait", "Please Do Not Feed the Boss", "Offer food to lure the monster away from the neighborhood. It follows whoever feeds it; lead it to the marked refuge.", "Oversized hungry monster", ["Offer food"], "food", true),
        new("photo", "Department of Unnatural Disasters", "Equip a camera and photograph three different event creatures. Without a receipt, that apocalypse is a personal expense.", "Unflappable disaster clerk", ["Submit photographs"]),
        new("insurance", "Absolutely Legitimate Insurance", "Escort an insured shipment through the marked route. The adjuster refuses payment if its carrier is defeated.", "Suspicious insurance adjuster", ["Begin escort"], Escort: true),
        new("rescue", "This Is Fine", "Retrieve a survivor's favorite mug, then escort them out of an active inversion. Apparently the mug is irreplaceable.", "Stubborn mug enthusiast", ["Collect favorite mug", "Begin escort"], Escort: true),
        new("inspection", "Dungeon Inspection Services", "Photograph a dungeon barrier, a water crossing, and the exit. The boss claims everything is up to code.", "Dungeon safety inspector", ["Submit inspection"]),
        new("lost", "Lost and Profound", "Investigate three lost-property markers. Return recovered possessions or keep them for a smaller cash reward and a worse reputation.", "Lost-property clerk", ["Search for property", "Return property", "Keep property"]),
        new("race", "The Grand Inconvenience", "Race through four marked checkpoints using walk, skateboard, raft, and swim. The course loans the required equipment.", "Overconfident rival", ["Start race", "Check checkpoint"]),
        new("watch", "Neighborhood Watch, Literally", "Sneak between the floating eyeball's sweeping gaze to inspect three suspicious markers. Being spotted sends you back one clue.", "Giant floating eyeball", ["Inspect clue"])
    };
}
