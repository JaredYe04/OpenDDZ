using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenDDZ.DDZUtils.AI
{
    /// <summary>
    /// Compact card/move encoding for JSONL traces and replay.
    /// Card: rank char + suit char (S/H/C/D/J). Move: card strings joined by comma, empty = pass.
    /// </summary>
    public static class CardCodec
    {
        public static string EncodeCard(Card c)
        {
            char rank = RankChar(c.Rank);
            char suit = c.Suit == Suit.Joker ? 'J' : SuitChar(c.Suit);
            return $"{rank}{suit}";
        }

        public static Card DecodeCard(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 2)
                throw new ArgumentException($"Invalid card: {s}");
            var rank = CharRank(s[0]);
            var suit = s[1] == 'J' ? Suit.Joker : CharSuit(s[1]);
            return new Card(rank, suit);
        }

        public static string EncodeMove(Move move)
        {
            if (move == null || move.Cards == null || move.Cards.Count == 0)
                return "";
            return string.Join(",", move.Cards.Select(EncodeCard));
        }

        public static Move DecodeMove(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var cards = s.Split(',').Where(x => !string.IsNullOrEmpty(x)).Select(DecodeCard).ToList();
            return cards.Count == 0 ? null : new Move(cards);
        }

        public static List<Card> DecodeHand(IEnumerable<string> encoded)
        {
            return encoded?.Select(DecodeCard).ToList() ?? new List<Card>();
        }

        private static char RankChar(Rank r)
        {
            switch (r)
            {
                case Rank.Ten: return 'T';
                case Rank.J: return 'J';
                case Rank.Q: return 'Q';
                case Rank.K: return 'K';
                case Rank.A: return 'A';
                case Rank.Two: return '2';
                case Rank.JokerSmall: return 'X';
                case Rank.JokerBig: return 'Y';
                default: return ((int)r).ToString()[0];
            }
        }

        private static Rank CharRank(char c)
        {
            switch (c)
            {
                case 'T': return Rank.Ten;
                case 'J': return Rank.J;
                case 'Q': return Rank.Q;
                case 'K': return Rank.K;
                case 'A': return Rank.A;
                case '2': return Rank.Two;
                case 'X': return Rank.JokerSmall;
                case 'Y': return Rank.JokerBig;
                default:
                    if (c >= '3' && c <= '9') return (Rank)(c - '0');
                    throw new ArgumentException($"Invalid rank char: {c}");
            }
        }

        private static char SuitChar(Suit s)
        {
            switch (s)
            {
                case Suit.Spade: return 'S';
                case Suit.Heart: return 'H';
                case Suit.Club: return 'C';
                case Suit.Diamond: return 'D';
                default: return 'J';
            }
        }

        private static Suit CharSuit(char c)
        {
            switch (c)
            {
                case 'S': return Suit.Spade;
                case 'H': return Suit.Heart;
                case 'C': return Suit.Club;
                case 'D': return Suit.Diamond;
                case 'J': return Suit.Joker;
                default: throw new ArgumentException($"Invalid suit char: {c}");
            }
        }
    }

    /// <summary>
    /// Rebuild BotDecisionContext from serialized game trace at a decision point.
    /// </summary>
    public static class GameStateRebuilder
    {
        public class TraceMove
        {
            public int Seat { get; set; }
            public string Move { get; set; }
        }

        public class GameTrace
        {
            public int GameId { get; set; }
            public int Seed { get; set; }
            public List<List<string>> InitialHands { get; set; }
            public List<TraceMove> Moves { get; set; }
            public int Winner { get; set; } = -1;
            public int Landlord { get; set; } = -1;
            public string Mode { get; set; } = "Normal";
        }

        /// <summary>
        /// Build context at decision index (0 = before first move). Returns context and known hands copy.
        /// </summary>
        public static BotDecisionContext BuildContext(
            GameTrace trace, int decisionIndex, int playerCount = 3, RuleSet rules = null)
        {
            rules = rules ?? RuleSet.Default;
            var mode = trace.Mode != null && trace.Mode.Equals("FourPlayer", StringComparison.OrdinalIgnoreCase)
                ? GameMode.FourPlayer : GameMode.Normal;
            int deckCount = mode == GameMode.FourPlayer ? 2 : 1;

            var hands = trace.InitialHands
                .Select(h => CardCodec.DecodeHand(h))
                .ToList();

            Move lastMove = null;
            int lastPlayerIndex = -1;
            var history = new List<(IPlayer player, Move move, DateTime timestamp)>();

            for (int i = 0; i < decisionIndex && i < trace.Moves.Count; i++)
            {
                var tm = trace.Moves[i];
                var move = CardCodec.DecodeMove(tm.Move);
                ApplyMove(hands, tm.Seat, move);
                history.Add((null, move, DateTime.MinValue));
                if (move != null && move.Cards.Count > 0)
                {
                    lastMove = move;
                    lastPlayerIndex = tm.Seat;
                }
            }

            if (decisionIndex >= trace.Moves.Count)
                throw new ArgumentOutOfRangeException(nameof(decisionIndex));

            int myIndex = trace.Moves[decisionIndex].Seat;
            var myHand = hands[myIndex];

            Move effectiveLast = lastMove;
            if (lastPlayerIndex == myIndex)
                effectiveLast = null;

            var played = new List<Card>();
            foreach (var entry in history)
            {
                if (entry.move?.Cards != null)
                    played.AddRange(entry.move.Cards);
            }

            var tracker = CardTracker.Create(mode, deckCount, myHand, played);

            return new BotDecisionContext
            {
                Self = null,
                MyIndex = myIndex,
                MyHand = myHand,
                LastMove = lastMove,
                LastPlayerIndex = lastPlayerIndex,
                EffectiveLastMove = effectiveLast,
                PlayerCount = playerCount,
                HandCounts = hands.Select(h => h.Count).ToArray(),
                LandlordIndex = trace.Landlord,
                MoveHistory = history,
                Rules = rules,
                DeckCount = deckCount,
                Mode = mode,
                CardTracker = tracker
            };
        }

        public static List<List<Card>> GetKnownHands(GameTrace trace, int decisionIndex)
        {
            var hands = trace.InitialHands.Select(h => CardCodec.DecodeHand(h)).ToList();
            for (int i = 0; i < decisionIndex && i < trace.Moves.Count; i++)
            {
                var tm = trace.Moves[i];
                ApplyMove(hands, tm.Seat, CardCodec.DecodeMove(tm.Move));
            }
            return hands;
        }

        private static void ApplyMove(List<List<Card>> hands, int seat, Move move)
        {
            if (move == null || move.Cards == null || move.Cards.Count == 0)
                return;

            foreach (var card in move.Cards)
            {
                var hand = hands[seat];
                var idx = hand.FindIndex(c => c.Suit == card.Suit && c.Rank == card.Rank);
                if (idx >= 0) hand.RemoveAt(idx);
            }
        }
    }
}
