namespace AlternateEarth.Server;

public static class NpcDialogueFarewells
{
    private static readonly string[] Friendly =
    [
        "Good bye!",
        "Take care. Try not to become somebody's side quest.",
        "See you around! May your snacks outlast your problems.",
        "Until next time. Keep your shoes dry and your adventures questionable.",
        "Happy trails! If you find my common sense, send it home.",
        "Catch you later. Stay weird, but stay in one piece."
    ];
    private static readonly string[] SlightlyHostile =
    [
        "Buzz off.",
        "Go on, then. You've used up today's supply of my patience.",
        "Take a hike. Preferably one that starts somewhere else.",
        "Let's continue this conversation never-ish.",
        "Off you go. I'm expecting a very important silence.",
        "Please see yourself out of my immediate vicinity."
    ];
    private static readonly string[] Hostile =
    [
        "Go drink a bottle of lemonade found at truck stops.",
        "Go ask a vending machine for emotional support.",
        "May every shopping cart you touch have one crooked wheel. Goodbye.",
        "Go argue with a parking meter. You two deserve each other.",
        "Go audition for the role of somebody else's problem.",
        "May your socks always find the puddle. We're done here."
    ];

    public static string Choose(double relationship)
    {
        var choices = relationship >= 0 ? Friendly : relationship > -2 ? SlightlyHostile : Hostile;
        return choices[Random.Shared.Next(choices.Length)];
    }
}
