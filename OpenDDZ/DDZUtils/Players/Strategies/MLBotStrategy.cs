using OpenDDZ.DDZUtils.AI;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Players;
using OpenDDZ.DDZUtils.Players.Strategies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace OpenDDZ.DDZUtils.Players.Strategies
{
    /// <summary>
    /// ML 策略：树模型筛选候选，再对 Top-K 交给 MC 做 rollout 精化。
    /// </summary>
    public class MLBotStrategy : IBotStrategy
    {
        private readonly GreedyBotStrategy _fallback = new GreedyBotStrategy();
        private readonly MonteCarloBotStrategy _mcRefiner = new MonteCarloBotStrategy();
        private readonly Dictionary<string, double> _legacyWeights = new Dictionary<string, double>();
        private TreeEnsemblePredictor _predictor;
        private bool _legacyValid;

        public double Alpha { get; set; } = 0.5;
        public string ModelPath { get; private set; }
        public int MlFilterTopK { get; set; } = 4;
        public int McRefineRollouts { get; set; } = 90;

        public MLBotStrategy() { ConfigureRefiner(); }

        public MLBotStrategy(string modelPath)
        {
            ModelPath = modelPath;
            LoadModel(modelPath);
            ConfigureRefiner();
        }

        private void ConfigureRefiner()
        {
            _mcRefiner.RolloutCount = McRefineRollouts;
            _mcRefiner.ParallelRollouts = true;
            _mcRefiner.MaxCandidates = MlFilterTopK;
        }

        public void LoadModel(string modelPath)
        {
            ModelPath = modelPath;
            _predictor = TreeEnsemblePredictor.Load(modelPath);
            _legacyValid = false;
            _legacyWeights.Clear();

            if (_predictor != null) return;
            if (!File.Exists(modelPath)) return;

            try
            {
                var json = JObject.Parse(File.ReadAllText(modelPath));
                if ((json["version"]?.Value<int>() ?? 1) >= 2) return;

                foreach (var prop in json["weights"]?.Children<JProperty>() ?? Enumerable.Empty<JProperty>())
                {
                    double v = prop.Value.Value<double>();
                    if (!double.IsNaN(v) && !double.IsInfinity(v))
                        _legacyWeights[prop.Name] = v;
                }
                _legacyValid = _legacyWeights.Count > 0;
                if (json["alpha"] != null)
                    Alpha = json["alpha"].Value<double>();
            }
            catch { _legacyValid = false; }
        }

        public Move ChoosePlay(BotDecisionContext ctx)
        {
            var candidates = BotCandidateHelper.SelectCandidates(ctx);
            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            if (_predictor != null)
            {
                _mcRefiner.RolloutCount = McRefineRollouts;
                var filtered = FilterByModel(ctx, candidates, MlFilterTopK);
                return _mcRefiner.ChoosePlayFromCandidates(ctx, filtered);
            }

            if (_legacyValid)
                return ChooseLegacy(ctx, candidates);

            return _fallback.ChoosePlay(ctx);
        }

        private List<Move> FilterByModel(BotDecisionContext ctx, List<Move> candidates, int topK)
        {
            var greedy = _fallback.ChoosePlay(ctx);
            var scored = candidates
                .Select(m => (move: m, score: ScoreMove(ctx, m)))
                .OrderByDescending(x => x.score)
                .Take(Math.Max(2, topK))
                .Select(x => x.move)
                .ToList();
            if (greedy != null && !scored.Any(m => BotCandidateHelper.MovesEqual(m, greedy)))
                scored.Add(greedy);
            return scored.Count > 0 ? scored : candidates;
        }

        private double ScoreMove(BotDecisionContext ctx, Move move)
        {
            double ml = _predictor.Predict(BotFeatureExtractor.Extract(ctx, move));
            return IsFinite(ml) ? ml : 0;
        }

        private Move ChooseLegacy(BotDecisionContext ctx, List<Move> candidates)
        {
            Move best = null;
            double bestScore = double.MinValue;
            foreach (var move in candidates)
            {
                double mlScore = ScoreLegacy(ctx, move);
                double greedyScore = move == null ? 0 : move.Cards.Count;
                double score = Alpha * mlScore + (1 - Alpha) * greedyScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = move;
                }
            }
            return best ?? _fallback.ChoosePlay(ctx);
        }

        private double ScoreLegacy(BotDecisionContext ctx, Move move)
        {
            double score = _legacyWeights.ContainsKey("bias") ? _legacyWeights["bias"] : 0;
            score += (_legacyWeights.ContainsKey("hand_count") ? _legacyWeights["hand_count"] : 0) * ctx.MyHand.Count;
            score += (_legacyWeights.ContainsKey("is_landlord") ? _legacyWeights["is_landlord"] : 0) * (ctx.IsLandlord ? 1 : 0);
            score += (_legacyWeights.ContainsKey("move_cards") ? _legacyWeights["move_cards"] : 0) * (move?.Cards?.Count ?? 0);
            if (move?.Cards != null)
            {
                foreach (var c in move.Cards)
                {
                    string key = "rank_" + (int)c.Rank;
                    if (_legacyWeights.ContainsKey(key))
                        score += _legacyWeights[key];
                }
            }
            return score;
        }

        public string ChooseBid(BotDecisionContext ctx, string[] options)
        {
            return HeuristicBidStrategy.ChooseBid(ctx, options);
        }

        public Card ChooseDiscard(BotDecisionContext ctx)
        {
            return _fallback.ChooseDiscard(ctx);
        }

        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
