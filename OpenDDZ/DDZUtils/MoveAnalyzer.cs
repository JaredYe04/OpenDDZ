using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace OpenDDZ.DDZUtils
{
    // 核心判定器
    public static class MoveAnalyzer
    {
        private static bool IsStraight(List<Card> cards)
        {
            // 排序
            var ordered = cards.OrderBy(c => c.Rank).ToList();
            // 含2或王，直接不合法
            if (ordered.Any(c => c.Rank == Rank.Two || c.Rank >= Rank.JokerSmall))
                return false;

            for (int i = 1; i < ordered.Count; i++)
            {
                if ((int)ordered[i].Rank != (int)ordered[i - 1].Rank + 1)
                    return false;
            }
            return true;
        }
        // 主判定函数：给出 MoveClassification（如果无效则 Kind==Invalid）
        public static MoveClassification Detect(Move move, RuleSet rules)
        {
            var cards = move.Cards;
            if (move == null || cards.Count == 0) return new MoveClassification { Kind = MoveKind.None };

            // Build counts per rank
            var counts = new Dictionary<Rank, int>();
            foreach (var c in cards)
            {
                counts.TryGetValue(c.Rank, out int v);
                counts[c.Rank] = v + 1;
            }

            int total = cards.Count;

            // Count jokers total
            counts.TryGetValue(Rank.JokerSmall, out int smallJ);
            counts.TryGetValue(Rank.JokerBig, out int bigJ);
            int jokerTotal = smallJ + bigJ;

            // Helper: deep-clone dictionary
            Dictionary<Rank, int> CloneCounts() => counts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 1) Rocket / Joker bomb: if all cards are jokers (some number)
            if (jokerTotal >=2 && jokerTotal == total)//只有王牌个数大于等于2且等于总数才是王炸
            {
                var primaryRank = Rank.JokerBig;
                //如果两张都是小王，则PrimaryRank为小王
                if (smallJ == total) primaryRank = Rank.JokerSmall;
                else primaryRank = Rank.JokerBig;
                //全王炸的情况，但是要注意双小王和双大王的情况，PrimaryRank需要根据实际情况定
                return new MoveClassification
                    {
                        Kind = MoveKind.Bomb,
                        CountPrimary = jokerTotal,
                        JokerCount = jokerTotal,
                        PrimaryRank = primaryRank
                    };
            }

            // 2) Bomb (single rank and count >= BombMinimum)
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

            // We'll accumulate possible classifications and then pick the best by priority/strength
            var candidates = new List<MoveClassification>();

            // Helper: add candidate if valid
            void AddCandidate(MoveClassification c) => candidates.Add(c);

            // 3) Plane (飞机): enumerate all consecutive sequences of ranks with count >= 3
            var planeCandidates = DetectPlanes(counts, total, rules);
            candidates.AddRange(planeCandidates);

            // 4) Four with attachments (4带2单 / 4带两对)
            var fourCandidates = DetectFourWithAttachments(counts, total, rules);
            candidates.AddRange(fourCandidates);

            // 5) Three types: triplet, three+1, three+pair
            var threeCandidates = DetectTriplesLike(counts, total);
            candidates.AddRange(threeCandidates);

            // 6) Consecutive pairs (连对) - require even cards and >=3 pairs
            var consecPairs = DetectConsecutivePairs(counts, total, rules);
            if (consecPairs != null) candidates.Add(consecPairs);

            // 7) Pair & Single
            if (total == 2)
            {
                if (counts.Count == 1)
                    AddCandidate(new MoveClassification { Kind = MoveKind.Pair, PrimaryRank = counts.First().Key, CountPrimary = 2 });
            }
            if (total == 1)
            {
                AddCandidate(new MoveClassification { Kind = MoveKind.Single, PrimaryRank = cards[0].Rank, CountPrimary = 1 });
            }

            // 8) Bombs that are not single-rank (for example if there are multiple ranks but combined equal a bomb of jokers)
            // (handled earlier: all-joker-case) - otherwise bombs must be all of same rank, already handled.

            // 9) Straights (not required by user, so we skip implementing this unless needed)
            if (cards.Count >= 5 && IsStraight(cards))
                return new MoveClassification { Kind = MoveKind.Straight, PrimaryRank = cards.Max(c => c.Rank), CountPrimary = cards.Count };

            // Choose best candidate by a priority comparator
            if (candidates.Count == 0)
            {
                return new MoveClassification { Kind = MoveKind.Invalid };
            }

            // Pick best: higher priority by (1) special: Bomb (but bombs already handled) (2) Plane with larger n (3) FourWithTwo... (4) ThreeWithPair/ThreeWithOne (5) ConsecutivePairs longer first (6) Triplet/Pair/Single
            candidates.Sort(CompareCandidatesForChoice);
            return candidates.Last(); // last is best due to comparator (we'll make comparator return -1 for lower)
        }

        // comparator: return <0 if a weaker than b
        private static int CompareCandidatesForChoice(MoveClassification a, MoveClassification b)
        {
            int Prio(MoveKind k)
            {
                switch (k)
                {
                    case MoveKind.Bomb:
                        return 9;
                    case MoveKind.Plane:
                        return 8;
                    case MoveKind.FourWithTwoPairs:
                        return 7;
                    case MoveKind.FourWithTwoSingles:
                        return 7;
                    case MoveKind.ThreeWithPair:
                        return 6;
                    case MoveKind.ThreeWithOne:
                        return 6;
                    case MoveKind.ConsecutivePairs:
                        return 5;
                    case MoveKind.Triplet:
                        return 4;
                    case MoveKind.Pair:
                        return 3;
                    case MoveKind.Single:
                        return 2;
                    default:
                        return 1;
                }
            }

            var pa = Prio(a.Kind);
            var pb = Prio(b.Kind);
            if (pa != pb) return pa - pb;

            // same priority: break tie by more "powerful" attributes:
            switch (a.Kind)
            {
                case MoveKind.Bomb:
                    // compare bomb size then main rank
                    if (a.CountPrimary != b.CountPrimary) return a.CountPrimary - b.CountPrimary;
                    return ((int)a.PrimaryRank) - ((int)b.PrimaryRank);
                case MoveKind.Plane:
                    // longer plane better, then higher primary (max rank)
                    if (a.SequenceLength != b.SequenceLength) return a.SequenceLength - b.SequenceLength;
                    if (a.PrimaryRank != b.PrimaryRank) return ((int)a.PrimaryRank) - ((int)b.PrimaryRank);
                    // prefer pairs attachments over single attachments (arbitrary but reasonable)
                    return ((int)a.AttachKind) - ((int)b.AttachKind);
                case MoveKind.ConsecutivePairs:
                    if (a.SequenceLength != b.SequenceLength) return a.SequenceLength - b.SequenceLength;
                    return ((int)a.PrimaryRank) - ((int)b.PrimaryRank);
                case MoveKind.FourWithTwoPairs:
                case MoveKind.FourWithTwoSingles:
                    // prefer larger main rank
                    return ((int)a.PrimaryRank) - ((int)b.PrimaryRank);
                default:
                    // triplet/pair/single: compare rank
                    return ((int)a.PrimaryRank) - ((int)b.PrimaryRank);
            }
        }

        // Detect planes (飞机) that use ALL cards in move (i.e., partitioned correctly)
        private static List<MoveClassification> DetectPlanes(Dictionary<Rank, int> counts, int total, RuleSet rules)
        {
            var results = new List<MoveClassification>();
            // build array of ranks that can be used as triplet (count>=3)
            var candidateRanks = counts.Where(kvp => kvp.Value >= 3)
                                       .Select(kvp => kvp.Key)
                                       .Where(r => rules.AllowSequencesWithTwoOrJoker || (r != Rank.Two && r != Rank.JokerSmall && r != Rank.JokerBig))
                                       .OrderBy(r => (int)r)
                                       .ToList();
            if (candidateRanks.Count < 2) return results; // at least 2-fly

            // Enumerate all contiguous sequences of length >=2 up to candidateRanks.Count
            // We'll treat ordinals as ints and find contiguous runs
            var ordinals = candidateRanks.Select(r => (int)r).ToArray();
            // find runs in ordinals
            int n = ordinals.Length;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    // check if ordinals[i..j] are consecutive
                    bool cons = true;
                    for (int k = i; k < j; k++)
                    {
                        if (ordinals[k + 1] != ordinals[k] + 1) { cons = false; break; }
                    }
                    if (!cons) continue;
                    int len = j - i + 1; // sequence length n (飞n)
                    // try to build plane using these ranks
                    var seqRanks = ordinals.Skip(i).Take(len).Select(x => (Rank)x).ToList();
                    // create leftover after removing three each
                    var leftover = counts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    bool ok = true;
                    foreach (var r in seqRanks)
                    {
                        leftover[r] -= 3;
                        if (leftover[r] < 0) { ok = false; break; }
                    }
                    if (!ok) continue;

                    int leftoverSum = leftover.Values.Sum();
                    // option1: plane without attachments (leftoverSum == 0)
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
                    // option2: attachments as singles: need leftoverSum == len
                    if (leftoverSum == len)
                    {
                        // can take any singles from leftover -> feasible
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
                    // option3: attachments as pairs: need leftoverSum == 2*len and have at least len pairs
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

        // Detect 四带2(单)/四带2(对)
        private static List<MoveClassification> DetectFourWithAttachments(Dictionary<Rank, int> counts, int total, RuleSet rules)
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
                    // four带两单 (total 6)
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
                    // four带两对 (total 8)
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

        // Triples and three带
        private static List<MoveClassification> DetectTriplesLike(Dictionary<Rank, int> counts, int total)
        {
            var res = new List<MoveClassification>();
            // triplet alone
            foreach (var kvp in counts)
            {
                if (kvp.Value == 3 && counts.Count == 1)
                {
                    res.Add(new MoveClassification { Kind = MoveKind.Triplet, PrimaryRank = kvp.Key, CountPrimary = 3 });
                }
            }
            // 三带一 (4 cards)
            if (total == 4)
            {
                foreach (var kvp in counts)
                {
                    if (kvp.Value == 3)
                    {
                        // the other card can be anything single
                        res.Add(new MoveClassification { Kind = MoveKind.ThreeWithOne, PrimaryRank = kvp.Key, CountPrimary = 3, AttachKind = AttachmentKind.Singles, AttachCount = 1 });
                    }
                }
            }
            // 三带一对 (5 cards)
            if (total == 5)
            {
                foreach (var kvp in counts)
                {
                    if (kvp.Value == 3)
                    {
                        // other two must form a pair
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

        // 连对 detection (strict: require exactly pairs for consecutive ranks, length >= 3)
        private static MoveClassification DetectConsecutivePairs(Dictionary<Rank, int> counts, int total, RuleSet rules)
        {
            if (total < 6 || total % 2 != 0) return null;
            int neededPairs = total / 2;
            // gather ranks with count==2 OR count==? In standard ddz, each rank used must supply exactly one pair; extra cards make leftover invalid
            // So require there are exactly neededPairs distinct ranks and each rank has count == 2 (or possibly 4 but then leftover cannot be zero).
            var ranksExactlyTwo = counts.Where(kvp => kvp.Value == 2).Select(kvp => kvp.Key).OrderBy(r => (int)r).ToList();
            if (ranksExactlyTwo.Count != neededPairs) return null;
            // check consecutive
            var ords = ranksExactlyTwo.Select(r => (int)r).OrderBy(x => x).ToArray();
            for (int i = 0; i < ords.Length - 1; i++)
            {
                if (ords[i + 1] != ords[i] + 1) return null;
            }
            // also ensure none of ranks are 2 or jokers (unless rule allows)
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
