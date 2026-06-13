using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using OpenDDZ.DDZUtils.Players;
using OpenDDZ.DDZUtils.Players.Strategies;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.AI
{
    public static class BotCandidateHelper
    {
        public const int DefaultMaxCandidates = 12;

        private static readonly GreedyBotStrategy Greedy = new GreedyBotStrategy();

        public static List<Move> SelectCandidates(BotDecisionContext ctx, int maxCandidates = DefaultMaxCandidates)
        {
            var candidates = new List<Move>();
            var greedyPick = Greedy.ChoosePlay(ctx);
            if (greedyPick != null) candidates.Add(greedyPick);

            bool following = ctx.EffectiveLastMove != null && ctx.EffectiveLastMove.Cards?.Count > 0;

            if (following)
            {
                foreach (var c in ctx.MyHand.OrderBy(x => (int)x.Rank))
                {
                    if (candidates.Count >= maxCandidates) break;
                    var m = new Move(new List<Card> { c });
                    if (MoveUtils.CanBeat(ctx.EffectiveLastMove, m, ctx.Rules) && !ContainsMove(candidates, m))
                        candidates.Add(m);
                }
                foreach (var g in ctx.MyHand.GroupBy(c => c.Rank).Where(x => x.Count() >= ctx.Rules.BombMinimumSize))
                {
                    if (candidates.Count >= maxCandidates) break;
                    var m = new Move(g.Take(ctx.Rules.BombMinimumSize).ToList());
                    if (MoveUtils.CanBeat(ctx.EffectiveLastMove, m, ctx.Rules) && !ContainsMove(candidates, m))
                        candidates.Add(m);
                }
                if (!ContainsMove(candidates, null))
                    candidates.Add(null);
            }
            else
            {
                foreach (var g in ctx.MyHand.GroupBy(c => c.Rank).Where(x => x.Count() >= 2).OrderBy(x => x.Key))
                {
                    if (candidates.Count >= maxCandidates) break;
                    var m = new Move(g.Take(2).ToList());
                    if (!ContainsMove(candidates, m)) candidates.Add(m);
                }
                foreach (var c in ctx.MyHand.OrderBy(x => (int)x.Rank))
                {
                    if (candidates.Count >= maxCandidates) break;
                    var m = new Move(new List<Card> { c });
                    if (!ContainsMove(candidates, m)) candidates.Add(m);
                }
            }

            return candidates;
        }

        public static bool MovesEqual(Move a, Move b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Cards.Count != b.Cards.Count) return false;
            var sa = a.Cards.OrderBy(c => ((int)c.Rank << 4) + (int)c.Suit).ToList();
            var sb = b.Cards.OrderBy(c => ((int)c.Rank << 4) + (int)c.Suit).ToList();
            for (int i = 0; i < sa.Count; i++)
                if (sa[i].Rank != sb[i].Rank || sa[i].Suit != sb[i].Suit) return false;
            return true;
        }

        private static bool ContainsMove(List<Move> list, Move m)
        {
            return list.Any(x => MovesEqual(x, m));
        }
    }
}
