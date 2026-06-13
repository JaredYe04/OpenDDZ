using OpenDDZ.DDZUtils.AI;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Players.Strategies
{
    public class MonteCarloBotStrategy : IBotStrategy
    {
        public int RolloutCount { get; set; } = 20;
        public bool ParallelRollouts { get; set; } = true;
        public int MaxCandidates { get; set; } = 12;

        private readonly GreedyBotStrategy _candidateRanker = new GreedyBotStrategy();
        private readonly Random _rng = new Random();

        public Move ChoosePlay(BotDecisionContext ctx)
        {
            var candidates = BotCandidateHelper.SelectCandidates(ctx, MaxCandidates);
            return ChoosePlayFromCandidates(ctx, candidates);
        }

        public Move ChoosePlayFromCandidates(BotDecisionContext ctx, List<Move> candidates)
        {
            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            var scores = new double[candidates.Count];

            if (ParallelRollouts && RolloutCount >= 10 && candidates.Count >= 2)
            {
                Parallel.For(0, candidates.Count, i =>
                {
                    int seed;
                    lock (_rng) { seed = _rng.Next(); }
                    scores[i] = EvaluateMove(ctx, candidates[i], new Random(seed));
                });
            }
            else
            {
                for (int i = 0; i < candidates.Count; i++)
                    scores[i] = EvaluateMove(ctx, candidates[i], _rng);
            }

            int bestIdx = 0;
            for (int i = 1; i < candidates.Count; i++)
            {
                if (scores[i] > scores[bestIdx])
                    bestIdx = i;
            }

            return candidates[bestIdx];
        }

        private double EvaluateMove(BotDecisionContext ctx, Move candidate, Random rng)
        {
            return ImperfectRolloutEvaluator.EvaluateWinRate(ctx, candidate, RolloutCount, rng);
        }

        private bool SimulateRollout(BotDecisionContext ctx, Move candidate, Random rng)
        {
            return ImperfectRolloutEvaluator.SimulateRollout(ctx, candidate, rng);
        }

        public string ChooseBid(BotDecisionContext ctx, string[] options)
        {
            return HeuristicBidStrategy.ChooseBid(ctx, options);
        }

        public Card ChooseDiscard(BotDecisionContext ctx)
        {
            if (ctx.MyHand.Count == 0) return null;
            return ctx.MyHand.OrderBy(c => (int)c.Rank).First();
        }
    }
}
