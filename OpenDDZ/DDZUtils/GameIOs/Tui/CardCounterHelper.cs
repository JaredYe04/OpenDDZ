using OpenDDZ.DDZUtils.AI;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public struct CardCounterEntry
    {
        public string Label;
        public int Count;
    }

    internal static class CardCounterHelper
    {
        public static List<CardCounterEntry> Compute(
            GameMode mode,
            int deckCount,
            IList<Card> myHand,
            IList<Card> played)
        {
            var hand = myHand?.ToList() ?? new List<Card>();
            var playedList = played?.ToList() ?? new List<Card>();
            var tracker = CardTracker.Create(mode, deckCount, hand, playedList);
            var byRank = tracker.RemainingCards
                .GroupBy(c => c.Rank)
                .ToDictionary(g => g.Key, g => g.Count());

            var result = new List<CardCounterEntry>();
            foreach (var rank in RankOrder(mode))
            {
                byRank.TryGetValue(rank, out int count);
                result.Add(new CardCounterEntry { Label = RankLabel(rank), Count = count });
            }
            return result;
        }

        public static string FormatTableRow(IReadOnlyList<CardCounterEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return "（无数据）";

            var cells = new List<string>();
            foreach (var e in entries)
                cells.Add($"{e.Label}:{e.Count}");
            return string.Join(" | ", cells);
        }

        private static IEnumerable<Rank> RankOrder(GameMode mode)
        {
            yield return Rank.JokerBig;
            yield return Rank.JokerSmall;
            yield return Rank.Two;
            yield return Rank.A;
            yield return Rank.K;
            yield return Rank.Q;
            yield return Rank.J;
            yield return Rank.Ten;
            yield return Rank.Nine;
            yield return Rank.Eight;
            yield return Rank.Seven;
            yield return Rank.Six;
            if (mode != GameMode.FourPlayer)
            {
                yield return Rank.Five;
                yield return Rank.Four;
                yield return Rank.Three;
            }
        }

        private static string RankLabel(Rank rank)
        {
            switch (rank)
            {
                case Rank.JokerBig: return "大";
                case Rank.JokerSmall: return "小";
                case Rank.Two: return "2";
                case Rank.A: return "A";
                case Rank.K: return "K";
                case Rank.Q: return "Q";
                case Rank.J: return "J";
                case Rank.Ten: return "10";
                case Rank.Nine: return "9";
                case Rank.Eight: return "8";
                case Rank.Seven: return "7";
                case Rank.Six: return "6";
                case Rank.Five: return "5";
                case Rank.Four: return "4";
                case Rank.Three: return "3";
                default: return rank.ToString();
            }
        }
    }
}
