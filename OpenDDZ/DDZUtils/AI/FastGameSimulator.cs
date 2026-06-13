using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.AI
{
    public class FastGameSimulator
    {
        public List<List<Card>> Hands { get; private set; }
        public int CurrentPlayer { get; private set; }
        public Move LastMove { get; private set; }
        public int LastPlayer { get; private set; } = -1;
        public int LandlordIndex { get; private set; }
        public GameMode Mode { get; private set; }
        public RuleSet Rules { get; private set; }
        public bool IsFinished { get; private set; }
        public int WinnerIndex { get; private set; } = -1;

        public int PlayerCount => Hands?.Count ?? 0;

        public FastGameSimulator(List<List<Card>> hands, int currentPlayer, Move lastMove, int lastPlayer,
            int landlordIndex, GameMode mode, RuleSet rules)
        {
            Hands = hands.Select(h => new List<Card>(h)).ToList();
            CurrentPlayer = currentPlayer;
            LastMove = lastMove;
            LastPlayer = lastPlayer;
            LandlordIndex = landlordIndex;
            Mode = mode;
            Rules = rules ?? RuleSet.Default;
        }

        public void ApplyMove(int playerIndex, Move move)
        {
            if (IsFinished) return;

            if (move == null || move.Cards == null || move.Cards.Count == 0)
            {
                CurrentPlayer = (playerIndex + 1) % PlayerCount;
                return;
            }

            var hand = Hands[playerIndex];
            foreach (var card in move.Cards)
            {
                var idx = hand.FindIndex(c => c.Suit == card.Suit && c.Rank == card.Rank);
                if (idx >= 0) hand.RemoveAt(idx);
            }

            LastMove = move;
            LastPlayer = playerIndex;

            if (hand.Count == 0)
            {
                IsFinished = true;
                WinnerIndex = playerIndex;
                return;
            }

            CurrentPlayer = (playerIndex + 1) % PlayerCount;
        }

        public int RunToEnd(Random rng)
        {
            int guard = 0;
            while (!IsFinished && guard++ < 800)
            {
                int p = CurrentPlayer;
                Move effectiveLast = LastPlayer == p ? null : LastMove;
                var move = PickFastMove(Hands[p], effectiveLast, Rules, rng);
                ApplyMove(p, move);
            }
            return WinnerIndex;
        }

        /// <summary> 推演用轻量策略：仅单牌/炸弹/pass，避免 GenerateAllMoves </summary>
        internal static Move PickFastMove(List<Card> hand, Move lastMove, RuleSet rules, Random rng)
        {
            bool following = lastMove != null && lastMove.Cards != null && lastMove.Cards.Count > 0;

            if (following)
            {
                foreach (var c in hand.OrderBy(x => (int)x.Rank))
                {
                    var m = new Move(new List<Card> { c });
                    if (MoveUtils.CanBeat(lastMove, m, rules)) return m;
                }
                foreach (var g in hand.GroupBy(c => c.Rank).Where(x => x.Count() >= rules.BombMinimumSize))
                {
                    var m = new Move(g.Take(rules.BombMinimumSize).ToList());
                    if (MoveUtils.CanBeat(lastMove, m, rules)) return m;
                }
                var jokers = hand.Where(c => c.Rank == Rank.JokerSmall || c.Rank == Rank.JokerBig).ToList();
                if (jokers.Count == 2)
                {
                    var m = new Move(jokers);
                    if (MoveUtils.CanBeat(lastMove, m, rules)) return m;
                }
                return null;
            }

            var pairs = hand.GroupBy(c => c.Rank).Where(g => g.Count() >= 2).OrderBy(g => g.Key).ToList();
            if (pairs.Count > 0 && rng.Next(3) == 0)
                return new Move(pairs[0].Take(2).ToList());
            return new Move(new List<Card> { hand.OrderBy(c => (int)c.Rank).First() });
        }

        public static bool DidPlayerWin(int winnerIndex, int playerIndex, GameMode mode, int[] teamIds = null)
        {
            if (winnerIndex < 0) return false;
            if (mode == GameMode.FourPlayer)
            {
                if (teamIds != null && winnerIndex < teamIds.Length && playerIndex < teamIds.Length)
                    return teamIds[winnerIndex] == teamIds[playerIndex];
                return (winnerIndex % 2) == (playerIndex % 2);
            }
            return winnerIndex == playerIndex;
        }
    }
}
