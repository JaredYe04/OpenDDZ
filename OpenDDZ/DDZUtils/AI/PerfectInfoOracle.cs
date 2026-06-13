using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.AI
{
    public class OracleEvaluation
    {
        public List<Move> Candidates { get; set; }
        public double[] WinRates { get; set; }
        public int BestIndex { get; set; }
        public Move BestMove => Candidates != null && BestIndex >= 0 && BestIndex < Candidates.Count
            ? Candidates[BestIndex] : null;
    }

    public static class PerfectInfoOracle
    {
        public static OracleEvaluation Evaluate(BotDecisionContext ctx, List<List<Card>> knownHands, int rollouts = 50)
        {
            var candidates = BotCandidateHelper.SelectCandidates(ctx);
            if (candidates.Count == 0)
                return new OracleEvaluation { Candidates = candidates, WinRates = new double[0], BestIndex = -1 };

            var winRates = new double[candidates.Count];
            var rng = new Random(ctx.MyIndex * 997 + candidates.Count);

            for (int i = 0; i < candidates.Count; i++)
                winRates[i] = EvaluateCandidateWinRate(ctx, knownHands, candidates[i], rollouts, rng);

            int bestIdx = 0;
            for (int i = 1; i < winRates.Length; i++)
                if (winRates[i] > winRates[bestIdx]) bestIdx = i;

            return new OracleEvaluation
            {
                Candidates = candidates,
                WinRates = winRates,
                BestIndex = bestIdx
            };
        }

        private static double EvaluateCandidateWinRate(
            BotDecisionContext ctx, List<List<Card>> knownHands, Move candidate, int rollouts, Random rng)
        {
            int wins = 0;
            for (int r = 0; r < rollouts; r++)
            {
                if (SimulatePerfectRollout(ctx, knownHands, candidate, rng))
                    wins++;
            }
            return (double)wins / rollouts;
        }

        private static bool SimulatePerfectRollout(
            BotDecisionContext ctx, List<List<Card>> knownHands, Move candidate, Random rng)
        {
            var hands = knownHands.Select(h => new List<Card>(h)).ToList();
            int p = ctx.MyIndex;

            if (candidate != null && candidate.Cards != null && candidate.Cards.Count > 0)
            {
                foreach (var card in candidate.Cards)
                {
                    var idx = hands[p].FindIndex(c => c.Suit == card.Suit && c.Rank == card.Rank);
                    if (idx >= 0) hands[p].RemoveAt(idx);
                }
            }

            int nextPlayer = (p + 1) % ctx.PlayerCount;
            Move lastMove = candidate;
            int lastPlayer = p;

            if (candidate == null || candidate.Cards == null || candidate.Cards.Count == 0)
            {
                lastMove = ctx.EffectiveLastMove;
                lastPlayer = ctx.LastPlayerIndex;
                nextPlayer = (p + 1) % ctx.PlayerCount;
            }
            else if (hands[p].Count == 0)
            {
                return FastGameSimulator.DidPlayerWin(p, ctx.MyIndex, ctx.Mode, ctx.TeamIds);
            }

            var sim = new FastGameSimulator(hands, nextPlayer, lastMove, lastPlayer,
                ctx.LandlordIndex, ctx.Mode, ctx.Rules);
            int winner = sim.RunToEnd(rng);
            return FastGameSimulator.DidPlayerWin(winner, ctx.MyIndex, ctx.Mode, ctx.TeamIds);
        }

        public static bool HasMeaningfulSpread(double[] winRates, double minSpread = 0.05)
        {
            if (winRates == null || winRates.Length < 2) return false;
            return winRates.Max() - winRates.Min() >= minSpread;
        }
    }
}
