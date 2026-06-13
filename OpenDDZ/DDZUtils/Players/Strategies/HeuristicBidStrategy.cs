using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using System.Linq;

namespace OpenDDZ.DDZUtils.Players.Strategies
{
    internal static class HeuristicBidStrategy
    {
        public static string ChooseBid(BotDecisionContext ctx, string[] options)
        {
            int strength = EvaluateHandStrength(ctx.MyHand);
            int highest = ctx.HighestBid;

            string passOpt = options.FirstOrDefault(o => o.Contains("不") || o.Contains("??")) ?? options[options.Length - 1];
            if (strength < 3) return passOpt;

            int desired = strength >= 8 ? 3 : strength >= 5 ? 2 : 1;
            if (desired <= highest) return passOpt;

            foreach (int bid in new[] { 3, 2, 1 })
            {
                if (bid <= highest || bid > desired) continue;
                string opt = options.FirstOrDefault(o => o.Contains(bid.ToString()));
                if (opt != null) return opt;
            }

            if (desired > highest)
            {
                string opt = options.FirstOrDefault(o => o.Contains(desired.ToString()));
                if (opt != null) return opt;
            }
            return passOpt;
        }

        private static int EvaluateHandStrength(System.Collections.Generic.List<Card> hand)
        {
            int score = 0;
            var groups = hand.GroupBy(c => c.Rank).ToList();
            foreach (var g in groups)
            {
                if (g.Count() >= 4) score += 4;
                else if (g.Count() == 3) score += 1;
                if (g.Key == Rank.Two) score += 2;
                if (g.Key == Rank.JokerSmall || g.Key == Rank.JokerBig) score += 2;
                if (g.Key == Rank.A) score += 1;
            }
            if (hand.Any(c => c.Rank == Rank.JokerSmall) && hand.Any(c => c.Rank == Rank.JokerBig))
                score += 3;
            return score;
        }
    }
}
