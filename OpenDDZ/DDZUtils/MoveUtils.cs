using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils
{
    /// <summary>
    /// 牌型判定与比较工具类
    /// </summary>
    public static class MoveUtils
    {
        /// <summary>
        /// 判定一次出牌的牌型
        /// </summary>
        public static MoveClassification Detect(Move move, RuleSet rules)
        {
            return AnalyzerCore.Detect(move, rules);
        }

        /// <summary>
        /// 判断 next 是否能压 prev (True if next beats prev)
        /// </summary>
        public static bool CanBeat(Move prev, Move next, RuleSet rules)
        {
            var prevC = Detect(prev, rules);
            var nextC = Detect(next, rules);

            if (prevC.Kind == MoveKind.None)
                return true;
            if (nextC.Kind == MoveKind.Invalid || nextC.Kind == MoveKind.None)
                return false;
            if (prevC.Kind == MoveKind.Invalid || nextC.Kind == MoveKind.Invalid)
                return false;

            // If prev is Bomb:
            if (prevC.Kind == MoveKind.Bomb)
            {
                if (nextC.Kind != MoveKind.Bomb) return false;
                var p1 = rules.GetBombPower(prevC);
                var p2 = rules.GetBombPower(nextC);
                return p2 > p1;
            }
            else
            {
                if (nextC.Kind == MoveKind.Bomb) return true;
                if (prevC.Kind != nextC.Kind) return false;

                switch (prevC.Kind)
                {
                    case MoveKind.Single:
                    case MoveKind.Pair:
                    case MoveKind.Triplet:
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank && nextC.CountPrimary == prevC.CountPrimary;
                    case MoveKind.ThreeWithOne:
                    case MoveKind.ThreeWithPair:
                        if (nextC.AttachKind != prevC.AttachKind || nextC.CountPrimary != prevC.CountPrimary) return false;
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank;
                    case MoveKind.ConsecutivePairs:
                        if (nextC.SequenceLength != prevC.SequenceLength) return false;
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank;
                    case MoveKind.FourWithTwoPairs:
                    case MoveKind.FourWithTwoSingles:
                        if (nextC.AttachKind != prevC.AttachKind || nextC.CountPrimary != prevC.CountPrimary) return false;
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank;
                    case MoveKind.Plane:
                        if (nextC.SequenceLength != prevC.SequenceLength) return false;
                        if (nextC.AttachKind != prevC.AttachKind) return false;
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank;
                    case MoveKind.Straight:
                        if (nextC.SequenceLength != prevC.SequenceLength) return false;
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// 内部判定核心
        /// </summary>
        private static class AnalyzerCore
        {
            public static MoveClassification Detect(Move move, RuleSet rules)
            {
                var cards = move.Cards;
                if (move == null || cards.Count == 0) return new MoveClassification { Kind = MoveKind.None };

                var counts = new Dictionary<Rank, int>();
                foreach (var c in cards)
                {
                    counts.TryGetValue(c.Rank, out int v);
                    counts[c.Rank] = v + 1;
                }

                int total = cards.Count;
                counts.TryGetValue(Rank.JokerSmall, out int smallJ);
                counts.TryGetValue(Rank.JokerBig, out int bigJ);
                int jokerTotal = smallJ + bigJ;

                // 1) Rocket / Joker bomb
                if (jokerTotal >= 2 && jokerTotal == total)
                {
                    var primaryRank = smallJ == total ? Rank.JokerSmall : Rank.JokerBig;
                    return new MoveClassification
                    {
                        Kind = MoveKind.Bomb,
                        CountPrimary = jokerTotal,
                        JokerCount = jokerTotal,
                        PrimaryRank = primaryRank
                    };
                }

                // 2) Bomb (单一牌点且数量足够)
                if (counts.Count == 1)
                {
                    var only = counts.First();
                    if (only.Value >= rules.BombMinimumSize)
                    {
                        return new MoveClassification
                        {
                            Kind = MoveKind.Bomb,
                            PrimaryRank = only.Key,
                            CountPrimary = only.Value,
                            JokerCount = (only.Key == Rank.JokerSmall || only.Key == Rank.JokerBig) ? only.Value : 0
                        };
                    }
                }

                var candidates = new List<MoveClassification>();

                // 3) 飞机
                candidates.AddRange(PlaneHelper.DetectPlanes(counts, total, rules));
                // 4) 四带
                candidates.AddRange(FourAttachHelper.DetectFourWithAttachments(counts, total, rules));
                // 5) 三带
                candidates.AddRange(TripleHelper.DetectTriplesLike(counts, total));
                // 6) 连对
                var consecPairs = PairHelper.DetectConsecutivePairs(counts, total, rules);
                if (consecPairs != null) candidates.Add(consecPairs);
                // 7) 对子/单张
                if (total == 2 && counts.Count == 1)
                    candidates.Add(new MoveClassification { Kind = MoveKind.Pair, PrimaryRank = counts.First().Key, CountPrimary = 2 });
                if (total == 1)
                    candidates.Add(new MoveClassification { Kind = MoveKind.Single, PrimaryRank = cards[0].Rank, CountPrimary = 1 });

                // 9) 顺子
                if (cards.Count >= 5 && StraightHelper.IsStraight(cards))
                    return new MoveClassification { Kind = MoveKind.Straight, PrimaryRank = cards.Max(c => c.Rank), CountPrimary = cards.Count, SequenceLength = cards.Count };

                if (candidates.Count == 0)
                    return new MoveClassification { Kind = MoveKind.Invalid };

                candidates.Sort(CompareCandidatesForChoice);
                return candidates.Last();
            }

            private static int CompareCandidatesForChoice(MoveClassification a, MoveClassification b)
            {
                int Prio(MoveKind k)
                {
                    switch (k)
                    {
                        case MoveKind.Bomb: return 9;
                        case MoveKind.Plane: return 8;
                        case MoveKind.FourWithTwoPairs:
                        case MoveKind.FourWithTwoSingles: return 7;
                        case MoveKind.ThreeWithPair:
                        case MoveKind.ThreeWithOne: return 6;
                        case MoveKind.ConsecutivePairs: return 5;
                        case MoveKind.Triplet: return 4;
                        case MoveKind.Pair: return 3;
                        case MoveKind.Single: return 2;
                        default: return 1;
                    }
                }

                var pa = Prio(a.Kind);
                var pb = Prio(b.Kind);
                if (pa != pb) return pa - pb;

                switch (a.Kind)
                {
                    case MoveKind.Bomb:
                        if (a.CountPrimary != b.CountPrimary) return a.CountPrimary - b.CountPrimary;
                        return ((int)a.PrimaryRank) - ((int)b.PrimaryRank);
                    case MoveKind.Plane:
                        if (a.SequenceLength != b.SequenceLength) return a.SequenceLength - b.SequenceLength;
                        if (a.PrimaryRank != b.PrimaryRank) return ((int)a.PrimaryRank) - ((int)b.PrimaryRank);
                        return ((int)a.AttachKind) - ((int)b.AttachKind);
                    case MoveKind.ConsecutivePairs:
                        if (a.SequenceLength != b.SequenceLength) return a.SequenceLength - b.SequenceLength;
                        return ((int)a.PrimaryRank) - ((int)b.PrimaryRank);
                    case MoveKind.FourWithTwoPairs:
                    case MoveKind.FourWithTwoSingles:
                        return ((int)a.PrimaryRank) - ((int)b.PrimaryRank);
                    default:
                        return ((int)a.PrimaryRank) - ((int)b.PrimaryRank);
                }
            }
        }

        /// <summary>
        /// 顺子相关辅助
        /// </summary>
        private static class StraightHelper
        {
            public static bool IsStraight(List<Card> cards)
            {
                var ordered = cards.OrderBy(c => c.Rank).ToList();
                if (ordered.Any(c => c.Rank == Rank.Two || c.Rank >= Rank.JokerSmall))
                    return false;
                for (int i = 1; i < ordered.Count; i++)
                {
                    if ((int)ordered[i].Rank != (int)ordered[i - 1].Rank + 1)
                        return false;
                }
                return true;
            }
        }

        /// <summary>
        /// 飞机相关辅助
        /// </summary>
        private static class PlaneHelper
        {
            public static List<MoveClassification> DetectPlanes(Dictionary<Rank, int> counts, int total, RuleSet rules)
            {
                var results = new List<MoveClassification>();
                var candidateRanks = counts.Where(kvp => kvp.Value >= 3)
                    .Select(kvp => kvp.Key)
                    .Where(r => rules.AllowSequencesWithTwoOrJoker || (r != Rank.Two && r != Rank.JokerSmall && r != Rank.JokerBig))
                    .OrderBy(r => (int)r)
                    .ToList();
                if (candidateRanks.Count < 2) return results;

                var ordinals = candidateRanks.Select(r => (int)r).ToArray();
                int n = ordinals.Length;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        bool cons = true;
                        for (int k = i; k < j; k++)
                        {
                            if (ordinals[k + 1] != ordinals[k] + 1) { cons = false; break; }
                        }
                        if (!cons) continue;
                        int len = j - i + 1;
                        var seqRanks = ordinals.Skip(i).Take(len).Select(x => (Rank)x).ToList();
                        var leftover = counts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                        bool ok = true;
                        foreach (var r in seqRanks)
                        {
                            leftover[r] -= 3;
                            if (leftover[r] < 0) { ok = false; break; }
                        }
                        if (!ok) continue;

                        int leftoverSum = leftover.Values.Sum();
                        if (leftoverSum == 0)
                        {
                            results.Add(new MoveClassification
                            {
                                Kind = MoveKind.Plane,
                                SequenceLength = len,
                                SequenceRanks = seqRanks,
                                PrimaryRank = seqRanks.Max(),
                                AttachKind = AttachmentKind.None,
                                AttachCount = 0
                            });
                            continue;
                        }
                        if (leftoverSum == len)
                        {
                            results.Add(new MoveClassification
                            {
                                Kind = MoveKind.Plane,
                                SequenceLength = len,
                                SequenceRanks = seqRanks,
                                PrimaryRank = seqRanks.Max(),
                                AttachKind = AttachmentKind.Singles,
                                AttachCount = len
                            });
                            continue;
                        }
                        if (leftoverSum == 2 * len)
                        {
                            int availablePairs = leftover.Values.Sum(v => v / 2);
                            if (availablePairs >= len)
                            {
                                results.Add(new MoveClassification
                                {
                                    Kind = MoveKind.Plane,
                                    SequenceLength = len,
                                    SequenceRanks = seqRanks,
                                    PrimaryRank = seqRanks.Max(),
                                    AttachKind = AttachmentKind.Pairs,
                                    AttachCount = len
                                });
                                continue;
                            }
                        }
                    }
                }
                return results;
            }
        }

        /// <summary>
        /// 四带相关辅助
        /// </summary>
        private static class FourAttachHelper
        {
            public static List<MoveClassification> DetectFourWithAttachments(Dictionary<Rank, int> counts, int total, RuleSet rules)
            {
                var results = new List<MoveClassification>();
                foreach (var kvp in counts)
                {
                    var rank = kvp.Key;
                    var cnt = kvp.Value;
                    if (cnt >= 4)
                    {
                        var leftover = counts.ToDictionary(x => x.Key, x => x.Value);
                        leftover[rank] -= 4;
                        int leftoverSum = leftover.Values.Sum();
                        if (leftoverSum == 2)
                        {
                            results.Add(new MoveClassification
                            {
                                Kind = MoveKind.FourWithTwoSingles,
                                PrimaryRank = rank,
                                CountPrimary = 4,
                                AttachKind = AttachmentKind.Singles,
                                AttachCount = 2
                            });
                        }
                        if (leftoverSum == 4)
                        {
                            int pairs = leftover.Values.Sum(v => v / 2);
                            if (pairs >= 2)
                            {
                                results.Add(new MoveClassification
                                {
                                    Kind = MoveKind.FourWithTwoPairs,
                                    PrimaryRank = rank,
                                    CountPrimary = 4,
                                    AttachKind = AttachmentKind.Pairs,
                                    AttachCount = 2
                                });
                            }
                        }
                    }
                }
                return results;
            }
        }

        /// <summary>
        /// 三带相关辅助
        /// </summary>
        private static class TripleHelper
        {
            public static List<MoveClassification> DetectTriplesLike(Dictionary<Rank, int> counts, int total)
            {
                var res = new List<MoveClassification>();
                foreach (var kvp in counts)
                {
                    if (kvp.Value == 3 && counts.Count == 1)
                    {
                        res.Add(new MoveClassification { Kind = MoveKind.Triplet, PrimaryRank = kvp.Key, CountPrimary = 3 });
                    }
                }
                if (total == 4)
                {
                    foreach (var kvp in counts)
                    {
                        if (kvp.Value == 3)
                        {
                            res.Add(new MoveClassification { Kind = MoveKind.ThreeWithOne, PrimaryRank = kvp.Key, CountPrimary = 3, AttachKind = AttachmentKind.Singles, AttachCount = 1 });
                        }
                    }
                }
                if (total == 5)
                {
                    foreach (var kvp in counts)
                    {
                        if (kvp.Value == 3)
                        {
                            var otherTotal = counts.Where(x => x.Key != kvp.Key).Sum(x => x.Value);
                            if (otherTotal == 2 && counts.Where(x => x.Key != kvp.Key).First().Value == 2)
                            {
                                res.Add(new MoveClassification { Kind = MoveKind.ThreeWithPair, PrimaryRank = kvp.Key, CountPrimary = 3, AttachKind = AttachmentKind.Pairs, AttachCount = 1 });
                            }
                        }
                    }
                }
                return res;
            }
        }

        /// <summary>
        /// 连对相关辅助
        /// </summary>
        private static class PairHelper
        {
            public static MoveClassification DetectConsecutivePairs(Dictionary<Rank, int> counts, int total, RuleSet rules)
            {
                if (total < 6 || total % 2 != 0) return null;
                int neededPairs = total / 2;
                var ranksExactlyTwo = counts.Where(kvp => kvp.Value == 2).Select(kvp => kvp.Key).OrderBy(r => (int)r).ToList();
                if (ranksExactlyTwo.Count != neededPairs) return null;
                var ords = ranksExactlyTwo.Select(r => (int)r).OrderBy(x => x).ToArray();
                for (int i = 0; i < ords.Length - 1; i++)
                {
                    if (ords[i + 1] != ords[i] + 1) return null;
                }
                if (!rules.AllowSequencesWithTwoOrJoker && ranksExactlyTwo.Any(r => r == Rank.Two || r == Rank.JokerSmall || r == Rank.JokerBig))
                    return null;
                return new MoveClassification
                {
                    Kind = MoveKind.ConsecutivePairs,
                    SequenceLength = neededPairs,
                    SequenceRanks = ranksExactlyTwo,
                    PrimaryRank = ranksExactlyTwo.Max()
                };
            }
        }
    }
}