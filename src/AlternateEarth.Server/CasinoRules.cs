using System.Security.Cryptography;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

// Stored only on the server. Never serialize this record into a socket response.
public sealed record CasinoSavedRound(string RoundId, string PlayerId, string BuildingId, string Game,
    long WagerCents, string Choice, int Revision, string Phase, int[] Deck, int[] Cards,
    int[] DealerCards, string[] Symbols, string Message, long PayoutCents = 0, string? RewardLootId = null);

public static class CasinoRules
{
    public static readonly CasinoStation[] Stations =
    [new("blackjack", "BLACKJACK", 8), new("poker", "VIDEO POKER", 17.144),
     new("roulette", "ROULETTE", 26.288), new("slots", "SLOT MACHINES", 35.432),
     new("dice", "HIGH / LOW DICE", 44.576)];
    private static readonly HashSet<int> Reds = [1,3,5,7,9,12,14,16,18,19,21,23,25,27,30,32,34,36];

    public static CasinoSavedRound Start(string id, string player, string building, string game, long wager, string? choice)
    {
        if (!Guid.TryParse(id, out _) || wager <= 0 || wager > long.MaxValue / 1000 || !Stations.Any(s => s.Game == game))
            throw new InvalidOperationException("Choose a game and a positive, supported wager.");
        choice ??= "";
        if (game == "roulette" && choice is not ("red" or "black" or "odd" or "even" or "low" or "high") &&
            !(int.TryParse(choice, out var number) && number is >= 0 and <= 36))
            throw new InvalidOperationException("Choose a roulette color, range, parity, or number from 0 to 36.");
        if (game == "dice" && choice is not ("high" or "low")) throw new InvalidOperationException("Choose high or low.");
        var deck = Enumerable.Range(0, 52).ToArray();
        for (var i = deck.Length - 1; i > 0; i--) { var j = RandomNumberGenerator.GetInt32(i + 1); (deck[i], deck[j]) = (deck[j], deck[i]); }
        int[] cards = game == "blackjack" ? deck[..2] : game == "poker" ? deck[..5] : [];
        int[] dealer = game == "blackjack" ? deck[2..4] : [];
        var used = game == "blackjack" ? 4 : game == "poker" ? 5 : 0;
        var phase = game is "blackjack" or "poker" ? "playing" : "resolve";
        if (game == "blackjack" && (BlackjackTotal(cards) == 21 || BlackjackTotal(dealer) == 21)) phase = "resolve";
        return new(id, player, building, game, wager, choice, 0, phase, deck[used..], cards, dealer, [],
            game == "poker" ? "Select cards to hold, then draw once." : game == "blackjack" ? "Hit or stand. Dealer stands on all 17s." : "Wager accepted. Ready to play.");
    }

    public static CasinoSavedRound Act(CasinoSavedRound round, CasinoActionRequest action)
    {
        if (round.Phase == "complete" || action.Revision != round.Revision) return round;
        var next = round with { Revision = round.Revision + 1 };
        if (round.Game == "blackjack")
        {
            if (round.Phase == "playing" && action.Action == "hit")
            {
                next = next with { Cards = [..round.Cards, round.Deck[0]], Deck = round.Deck[1..] };
                var total = BlackjackTotal(next.Cards);
                if (total > 21) return Finish(next, 0, "Bust. The house wins.");
                if (total < 21) return next with { Message = $"Your total: {total}. Hit or stand." };
            }
            else if (!(round.Phase == "playing" && action.Action == "stand") && !(round.Phase == "resolve" && action.Action == "resolve"))
                throw new InvalidOperationException("Choose hit or stand.");
            var player = BlackjackTotal(next.Cards);
            var natural = next.Cards.Length == 2 && player == 21;
            var dealerNatural = next.DealerCards.Length == 2 && BlackjackTotal(next.DealerCards) == 21;
            if (natural || dealerNatural) return Finish(next, natural && dealerNatural ? 1m : natural ? 2.5m : 0,
                natural && dealerNatural ? "Both blackjack. Push." : natural ? "Blackjack! Returns four times your stake." : "Dealer blackjack.");
            while (BlackjackTotal(next.DealerCards) < 17)
                next = next with { DealerCards = [..next.DealerCards, next.Deck[0]], Deck = next.Deck[1..] };
            var house = BlackjackTotal(next.DealerCards);
            return Finish(next, house > 21 || player > house ? 2 : player == house ? 1 : 0,
                house > 21 ? "Dealer busts. You win!" : player > house ? "You beat the dealer!" : player == house ? "Push. Stake returned." : "Dealer wins.");
        }
        if (round.Game == "poker" && action.Action == "draw")
        {
            var holds = action.Holds?.ToArray() ?? [];
            if (holds.Length > 5 || holds.Any(i => i < 0 || i > 4) || holds.Distinct().Count() != holds.Length)
                throw new InvalidOperationException("Hold only cards in your five-card hand.");
            var cards = round.Cards.ToArray(); var cursor = 0;
            for (var i = 0; i < 5; i++) if (!holds.Contains(i)) cards[i] = round.Deck[cursor++];
            var (multiplier, name) = PokerPayout(cards);
            return Finish(next with { Cards = cards, Deck = round.Deck[cursor..] }, multiplier, name);
        }
        if (round.Phase != "resolve" || action.Action != "resolve") throw new InvalidOperationException("That action is unavailable.");
        if (round.Game == "roulette")
        {
            var n = RandomNumberGenerator.GetInt32(37);
            var straight = int.TryParse(round.Choice, out var chosen);
            var win = straight ? n == chosen : n != 0 && (round.Choice switch
            { "red" => Reds.Contains(n), "black" => !Reds.Contains(n), "odd" => n % 2 == 1,
              "even" => n % 2 == 0, "low" => n <= 18, "high" => n >= 19, _ => false });
            return Finish(next with { Symbols = [n.ToString(), n == 0 ? "GREEN" : Reds.Contains(n) ? "RED" : "BLACK"] },
                win ? straight ? 36 : 2 : 0, $"Ball lands on {n}. {(win ? "You win!" : "House wins.")}");
        }
        if (round.Game == "slots")
        {
            string[] symbols = ["CHERRY", "LEMON", "BELL", "SEVEN", "DIAMOND"];
            var reels = Enumerable.Range(0, 3).Select(_ => symbols[RandomNumberGenerator.GetInt32(5)]).ToArray();
            var cherries = reels.Count(s => s == "CHERRY");
            var multiplier = reels.Distinct().Count() == 1 ? reels[0] == "SEVEN" ? 20 : 5 : cherries == 2 ? 3 : cherries == 1 ? 2 : 0;
            return Finish(next with { Symbols = reels }, multiplier, multiplier > 0 ? "Winning reels!" : "No winning combination.");
        }
        var dice = new[] { RandomNumberGenerator.GetInt32(1, 7), RandomNumberGenerator.GetInt32(1, 7) };
        var sum = dice.Sum(); var diceWin = round.Choice == "high" ? sum >= 8 : sum <= 6;
        return Finish(next with { Symbols = dice.Select(n => n.ToString()).ToArray() }, diceWin ? 2.3m : 0,
            $"Rolled {sum}. {(diceWin ? "You win!" : sum == 7 ? "Seven. House wins." : "House wins.")}");
    }

    private static CasinoSavedRound Finish(CasinoSavedRound round, decimal multiplier, string message) =>
        // Arcade casino: double the profit, while preserving losses and blackjack pushes.
        round with { Phase = "complete", PayoutCents = (long)decimal.Floor(round.WagerCents * (multiplier > 1 ? 1 + (multiplier - 1) * 2 : multiplier)), Message = message };

    public static int BlackjackTotal(IEnumerable<int> cards)
    {
        var ranks = cards.Select(c => c % 13 + 2).ToArray();
        var sum = ranks.Sum(r => r == 14 ? 11 : Math.Min(r, 10));
        for (var aces = ranks.Count(r => r == 14); sum > 21 && aces > 0; aces--) sum -= 10;
        return sum;
    }

    public static (int Multiplier, string Name) PokerPayout(int[] cards)
    {
        var ranks = cards.Select(c => c % 13 + 2).Order().ToArray();
        var groups = ranks.GroupBy(r => r).Select(g => (Rank: g.Key, Count: g.Count())).OrderByDescending(g => g.Count).ToArray();
        var flush = cards.Select(c => c / 13).Distinct().Count() == 1;
        var straight = groups.Length == 5 && (ranks[4] - ranks[0] == 4 || ranks.SequenceEqual(new[] { 2,3,4,5,14 }));
        if (straight && flush) return ranks[0] == 10 ? (250, "Royal flush!") : (50, "Straight flush!");
        if (groups[0].Count == 4) return (25, "Four of a kind!");
        if (groups[0].Count == 3 && groups[1].Count == 2) return (9, "Full house!");
        if (flush) return (6, "Flush!");
        if (straight) return (4, "Straight!");
        if (groups[0].Count == 3) return (3, "Three of a kind!");
        if (groups.Count(g => g.Count == 2) == 2) return (2, "Two pair!");
        if (groups.Any(g => g.Count == 2 && g.Rank >= 11)) return (2, "Jacks or better!");
        return (0, "No paying hand.");
    }

    public static CasinoRoundView View(CasinoSavedRound round) => new(round.RoundId, round.Revision,
        round.Game, round.WagerCents, round.Phase, round.Cards.Select(CardName).ToArray(),
        round.DealerCards.Select((c, i) => round.Phase != "complete" && i > 0 ? "?" : CardName(c)).ToArray(),
        round.Symbols, round.Message, round.PayoutCents, round.RewardLootId);
    private static string CardName(int card) => ((card % 13 + 2) switch { 11 => "J", 12 => "Q", 13 => "K", 14 => "A", var rank => rank.ToString() }) + "CDHS"[card / 13];
}
