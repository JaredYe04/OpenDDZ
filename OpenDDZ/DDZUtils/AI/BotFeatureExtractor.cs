using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.AI
{
    public static class BotFeatureExtractor
    {
        public const int FeatureDim = 90;
        public const int RankFeatureCount = 15;
        public const int MoveKindCount = 12;

        private static readonly Rank[] RankOrder =
        {
            Rank.Three, Rank.Four, Rank.Five, Rank.Six, Rank.Seven, Rank.Eight,
            Rank.Nine, Rank.Ten, Rank.J, Rank.Q, Rank.K, Rank.A, Rank.Two,
            Rank.JokerSmall, Rank.JokerBig
        };

        public static readonly string[] FeatureNames = BuildFeatureNames();

        public static float[] Extract(BotDecisionContext ctx, Move candidate)
        {
            var f = new float[FeatureDim];
            int i = 0;

            i = WriteRankCounts(f, i, ctx.MyHand, RankFeatureCount);
            i = WriteRankCounts(f, i, GetPlayedCards(ctx), RankFeatureCount);
            i = WriteRankCounts(f, i, ctx.CardTracker?.RemainingCards ?? new List<Card>(), RankFeatureCount);

            for (int p = 0; p < 3; p++)
            {
                if (p < ctx.PlayerCount && p != ctx.MyIndex)
                    f[i++] = ctx.HandCounts[p];
                else
                    f[i++] = 0;
            }

            f[i++] = ctx.IsLandlord ? 1f : 0f;
            f[i++] = (!ctx.IsLandlord && ctx.LandlordIndex >= 0) ? 1f : 0f;
            f[i++] = ctx.Mode == GameMode.FourPlayer ? (ctx.MyIndex % 2 == 0 ? 1f : 0f) : 0f;

            bool following = ctx.EffectiveLastMove != null && ctx.EffectiveLastMove.Cards?.Count > 0;
            f[i++] = following ? 1f : 0f;

            WriteMoveKindOneHot(f, ref i, ctx.EffectiveLastMove, ctx.Rules);
            f[i++] = following ? NormRank(GetPrimaryRank(ctx.EffectiveLastMove, ctx.Rules)) : 0f;

            f[i++] = candidate?.Cards?.Count ?? 0;
            WriteMoveKindOneHot(f, ref i, candidate, ctx.Rules);
            f[i++] = NormRank(GetPrimaryRank(candidate, ctx.Rules));

            f[i++] = ctx.PlayerCount > 0 ? (float)ctx.MyIndex / ctx.PlayerCount : 0f;
            if (ctx.LandlordIndex >= 0 && ctx.PlayerCount > 0)
                f[i++] = (float)((ctx.MyIndex - ctx.LandlordIndex + ctx.PlayerCount) % ctx.PlayerCount) / ctx.PlayerCount;
            else
                f[i++] = 0f;

            f[i++] = ctx.MyHand.Count;
            f[i++] = ctx.DeckCount;

            while (i < FeatureDim) f[i++] = 0f;
            return f;
        }

        private static int WriteRankCounts(float[] f, int start, IEnumerable<Card> cards, int count)
        {
            var tallies = new int[count];
            foreach (var c in cards)
            {
                int idx = RankToIndex(c.Rank);
                if (idx >= 0 && idx < count) tallies[idx]++;
            }
            for (int j = 0; j < count; j++)
                f[start + j] = tallies[j];
            return start + count;
        }

        private static void WriteMoveKindOneHot(float[] f, ref int i, Move move, RuleSet rules)
        {
            var kind = MoveKind.None;
            if (move != null && move.Cards != null && move.Cards.Count > 0)
                kind = MoveUtils.Detect(move, rules).Kind;

            int kindIdx = KindToIndex(kind);
            for (int k = 0; k < MoveKindCount; k++)
                f[i++] = k == kindIdx ? 1f : 0f;
        }

        private static List<Card> GetPlayedCards(BotDecisionContext ctx)
        {
            var played = new List<Card>();
            if (ctx.MoveHistory == null) return played;
            foreach (var entry in ctx.MoveHistory)
            {
                if (entry.move?.Cards != null)
                    played.AddRange(entry.move.Cards);
            }
            return played;
        }

        private static Rank GetPrimaryRank(Move move, RuleSet rules)
        {
            if (move == null || move.Cards == null || move.Cards.Count == 0)
                return Rank.Three;
            return MoveUtils.Detect(move, rules).PrimaryRank;
        }

        private static float NormRank(Rank r) => (float)((int)r) / 17f;

        private static int RankToIndex(Rank r)
        {
            for (int i = 0; i < RankOrder.Length; i++)
                if (RankOrder[i] == r) return i;
            return -1;
        }

        private static int KindToIndex(MoveKind kind)
        {
            switch (kind)
            {
                case MoveKind.Single: return 0;
                case MoveKind.Pair: return 1;
                case MoveKind.Triplet: return 2;
                case MoveKind.ThreeWithOne: return 3;
                case MoveKind.ThreeWithPair: return 4;
                case MoveKind.Straight: return 5;
                case MoveKind.ConsecutivePairs: return 6;
                case MoveKind.Plane: return 7;
                case MoveKind.FourWithTwoSingles: return 8;
                case MoveKind.FourWithTwoPairs: return 9;
                case MoveKind.Bomb: return 10;
                case MoveKind.None: return 11;
                default: return 11;
            }
        }

        private static string[] BuildFeatureNames()
        {
            var names = new List<string>();
            foreach (var prefix in new[] { "hand", "played", "remain" })
                foreach (var r in RankOrder)
                    names.Add($"{prefix}_rank_{(int)r}");
            names.Add("opp0_cards"); names.Add("opp1_cards"); names.Add("opp2_cards");
            names.Add("is_landlord"); names.Add("is_farmer"); names.Add("team0_four");
            names.Add("following");
            names.AddRange(Enumerable.Range(0, MoveKindCount).Select(k => $"last_kind_{k}"));
            names.Add("last_primary_rank");
            names.Add("cand_card_count");
            names.AddRange(Enumerable.Range(0, MoveKindCount).Select(k => $"cand_kind_{k}"));
            names.Add("cand_primary_rank");
            names.Add("seat_norm"); names.Add("landlord_dist_norm");
            names.Add("my_hand_count"); names.Add("deck_count");
            while (names.Count < FeatureDim) names.Add($"pad_{names.Count}");
            return names.ToArray();
        }
    }
}
