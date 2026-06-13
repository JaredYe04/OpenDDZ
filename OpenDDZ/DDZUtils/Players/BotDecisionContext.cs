using OpenDDZ.DDZUtils.AI;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.Players
{
    public class BotDecisionContext
    {
        public IPlayer Self { get; set; }
        public int MyIndex { get; set; }
        public List<Card> MyHand { get; set; }
        public Move LastMove { get; set; }
        public int LastPlayerIndex { get; set; } = -1;
        public Move EffectiveLastMove { get; set; }
        public int PlayerCount { get; set; }
        public int[] HandCounts { get; set; }
        public int LandlordIndex { get; set; } = -1;
        public bool IsLandlord => LandlordIndex == MyIndex;
        public bool IsTeammate(int index)
        {
            if (index == MyIndex) return true;
            if (Mode == GameMode.FourPlayer)
                return (MyIndex % 2) == (index % 2);
            if (LandlordIndex < 0) return false;
            return index != LandlordIndex && !IsLandlord;
        }
        public bool IsOpponent(int index) => index != MyIndex && !IsTeammate(index);
        public List<(IPlayer player, Move move, DateTime timestamp)> MoveHistory { get; set; }
        public RuleSet Rules { get; set; }
        public int DeckCount { get; set; }
        public GameMode Mode { get; set; }
        public int HighestBid { get; set; }
        public string[] BidOptions { get; set; }
        public CardTracker CardTracker { get; set; }

        public static BotDecisionContext From(IPlayer self, IDealer dealer)
        {
            var players = dealer.Players;
            int myIndex = dealer.GetPlayerIndex(self);
            var hand = (self.GetHandCards() as List<Card>) ?? self.GetHandCards().ToList();
            var lastMoveTuple = dealer.LastMove;
            int lastPlayerIndex = lastMoveTuple.Item1 != null ? dealer.GetPlayerIndex(lastMoveTuple.Item1) : -1;
            Move lastMove = lastMoveTuple.Item2;
            Move effectiveLastMove = lastMove;
            if (lastMoveTuple.Item1 == self)
                effectiveLastMove = null;

            var config = dealer.CurrentGame?.Config;
            var mode = config?.Mode ?? GameMode.Normal;
            int deckCount = config?.DeckCount ?? 1;

            var history = dealer.CurrentGame?.Moves ?? new List<(IPlayer, Move, DateTime)>();
            var played = new List<Card>();
            foreach (var entry in history)
            {
                if (entry.move?.Cards != null)
                    played.AddRange(entry.move.Cards);
            }

            var tracker = CardTracker.Create(mode, deckCount, hand, played);

            return new BotDecisionContext
            {
                Self = self,
                MyIndex = myIndex,
                MyHand = hand,
                LastMove = lastMove,
                LastPlayerIndex = lastPlayerIndex,
                EffectiveLastMove = effectiveLastMove,
                PlayerCount = players.Count,
                HandCounts = players.Select(p => p.GetHandCards().Count).ToArray(),
                LandlordIndex = dealer.LandlordIndex,
                MoveHistory = history,
                Rules = dealer.Rules,
                DeckCount = deckCount,
                Mode = mode,
                CardTracker = tracker
            };
        }
    }
}
