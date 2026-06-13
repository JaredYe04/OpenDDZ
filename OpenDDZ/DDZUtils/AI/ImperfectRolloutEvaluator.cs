using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.AI
{
    public static class ImperfectRolloutEvaluator
    {
        public static double EvaluateWinRate(BotDecisionContext ctx, Move candidate, int rollouts, Random rng)
        {
            if (rollouts <= 0) return 0;
            int wins = 0;
            for (int r = 0; r < rollouts; r++)
            {
                if (SimulateRollout(ctx, candidate, rng))
                    wins++;
            }
            return (double)wins / rollouts;
        }

        public static bool SimulateRollout(BotDecisionContext ctx, Move candidate, Random rng)
        {
            var hands = new List<List<Card>>();
            for (int i = 0; i < ctx.PlayerCount; i++)
                hands.Add(new List<Card>());

            hands[ctx.MyIndex] = new List<Card>(ctx.MyHand);
            var sampled = ctx.CardTracker.SampleOpponentHands(ctx.PlayerCount, ctx.MyIndex, ctx.HandCounts, rng);
            if (sampled == null) return false;

            for (int i = 0; i < ctx.PlayerCount; i++)
            {
                if (i != ctx.MyIndex)
                    hands[i] = sampled[i];
            }

            if (candidate != null && candidate.Cards != null && candidate.Cards.Count > 0)
            {
                foreach (var card in candidate.Cards)
                {
                    var idx = hands[ctx.MyIndex].FindIndex(c => c.Suit == card.Suit && c.Rank == card.Rank);
                    if (idx >= 0) hands[ctx.MyIndex].RemoveAt(idx);
                }
            }

            int nextPlayer = (ctx.MyIndex + 1) % ctx.PlayerCount;
            Move lastMove = candidate;
            int lastPlayer = ctx.MyIndex;

            if (candidate == null || candidate.Cards == null || candidate.Cards.Count == 0)
            {
                lastMove = ctx.EffectiveLastMove;
                lastPlayer = ctx.LastPlayerIndex;
                nextPlayer = (ctx.MyIndex + 1) % ctx.PlayerCount;
            }
            else if (hands[ctx.MyIndex].Count == 0)
            {
                return FastGameSimulator.DidPlayerWin(ctx.MyIndex, ctx.MyIndex, ctx.Mode, ctx.TeamIds);
            }

            var sim = new FastGameSimulator(hands, nextPlayer, lastMove, lastPlayer,
                ctx.LandlordIndex, ctx.Mode, ctx.Rules);
            int winner = sim.RunToEnd(rng);
            return FastGameSimulator.DidPlayerWin(winner, ctx.MyIndex, ctx.Mode, ctx.TeamIds);
        }
    }
}
