using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


/*
 单文件实现：
 - Rank / Card / Move
 - MoveKind enum
 - MoveClassification
 - RuleSet (可配置炸弹比较规则等)
 - MoveAnalyzer: DetectMoveType(move, rules)
 - MoveComparer: CanBeat(prev, next, rules)
 - TestSuite: 一套覆盖常见和corner-case的自动化测试
 - Main: 运行测试并打印结果
*/

namespace OpenDDZ.DDZAnalyzer
{
    // 点数定义（数值用于比较）
    public enum Rank
    {
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        J = 11,
        Q = 12,
        K = 13,
        A = 14,
        Two = 15,
        JokerSmall = 16,
        JokerBig = 17
    }

    // 简单 Card（这里只用 Rank 来判定）
    public class Card
    {
        public Rank Rank { get; }
        public Card(Rank r) { Rank = r; }
        public override string ToString() => Rank.ToString();
    }

    // 一次出牌（move）
    public class Move
    {
        public List<Card> Cards { get; } = new List<Card>();
        public Move(IEnumerable<Card> cards) { Cards.AddRange(cards); }
        public Move(params Rank[] ranks) { foreach (var r in ranks) Cards.Add(new Card(r)); }
        public override string ToString() => string.Join(",", Cards.Select(c => c.Rank.ToString()));
    }

    public enum MoveKind
    {
        Invalid,
        Single,
        Pair,
        Triplet,
        ThreeWithOne,
        ThreeWithPair,
        Straight,      // (not required by user but provided for completeness) - single sequence
        ConsecutivePairs,
        FourWithTwoSingles,
        FourWithTwoPairs,
        Plane,         // 飞机（包含带牌情况）
        Bomb            // 炸弹（任意大小）或王炸（用 classification 的 JokerCount 标记）
    }

    public enum AttachmentKind
    {
        None,
        Singles,
        Pairs
    }

    // 判定结果
    public class MoveClassification
    {
        public MoveKind Kind { get; set; } = MoveKind.Invalid;
        public Rank PrimaryRank { get; set; } = 0; // e.g., for pair/triplet/bomb: the rank of the main set
        public int CountPrimary { get; set; } = 0; // how many cards in main set (e.g., bomb size)
        public int SequenceLength { get; set; } = 0; // 连对长度 or 飞机 n
        public AttachmentKind AttachKind { get; set; } = AttachmentKind.None;
        public int AttachCount { get; set; } = 0; // number of attach units (n for n飞)
        public int JokerCount { get; set; } = 0; // how many jokers used (for bomb)
        public List<Rank> SequenceRanks { get; set; } = new List<Rank>(); // ranks in sequence/plane
        public override string ToString()
        {
            if (Kind == MoveKind.Bomb)
            {
                if (JokerCount > 0) return $"Bomb(Jokers:{JokerCount})";
                return $"Bomb(size={CountPrimary},rank={PrimaryRank})";
            }
            if (Kind == MoveKind.Plane)
            {
                return $"Plane(n={SequenceLength}, mainMax={PrimaryRank}, attach={AttachKind}/{AttachCount})";
            }
            if (Kind == MoveKind.ConsecutivePairs)
                return $"ConsecPairs(len={SequenceLength}, mainMax={PrimaryRank})";
            return $"{Kind} (rank={PrimaryRank},count={CountPrimary})";
        }
    }

    // 规则集：包含炸弹比较方法与序列允许范围等
    public class RuleSet
    {
        // 异常玩法可修改
        public int BombMinimumSize { get; set; } = 4;
        public bool AllowSequencesWithTwoOrJoker { get; set; } = false;

        // 炸弹权重函数：根据炸弹的大小和jokerCount返回一个数值权重用于比较
        // 默认实现：遵循用户给定的特殊排序说明（示例中的规则）
        // - 四王炸（jokerCount==4） -> 全场最大
        // - 双王炸（jokerCount==2） -> 大于所有4炸，小于5炸
        // - 三王炸（jokerCount==3） -> 在6炸和7炸之间（作为示例）
        public Func<int, Rank, int, double> BombPowerFunc { get; set; }

        public RuleSet()
        {
            BombPowerFunc = DefaultBombPowerFunc;
        }

        // 默认炸弹权重函数（可替换）
        private double DefaultBombPowerFunc(int bombSize, Rank mainRank, int jokerCount)
        {
            // base by bomb size
            double basePower = bombSize * 100.0 + (int)mainRank / 100.0;

            // special handling for joker bombs (use total joker count)
            if (jokerCount > 0)
            {
                // follow the user's mapping roughly:
                // 4 jokers -> absolute max
                if (jokerCount >= 4) return 1_000_000; // 四王炸最高
                if (jokerCount == 3) return 6.5 * 100.0; // between 6炸(600) and 7炸(700)
                if (jokerCount == 2) return 5 * 100.0 - 1; // greater than all 4炸 (400) but less than 5炸 (500)
                // single joker bombs unlikely, but give high power
                return 4.5 * 100.0;
            }

            // otherwise normal bomb power
            return basePower;
        }

        // convenience:
        public double GetBombPower(MoveClassification c)
        {
            return BombPowerFunc(c.CountPrimary, c.PrimaryRank, c.JokerCount);
        }

        // Default RuleSet for quick use
        public static RuleSet Default => new RuleSet();
    }

    // 核心判定器
    public static class MoveAnalyzer
    {
        // 主判定函数：给出 MoveClassification（如果无效则 Kind==Invalid）
        public static MoveClassification Detect(Move move, RuleSet rules)
        {
            if (move == null || move.Cards.Count == 0) return new MoveClassification { Kind = MoveKind.Invalid };

            // Build counts per rank
            var counts = new Dictionary<Rank, int>();
            foreach (var c in move.Cards)
            {
                counts.TryGetValue(c.Rank, out int v);
                counts[c.Rank] = v + 1;
            }

            int total = move.Cards.Count;

            // Count jokers total
            counts.TryGetValue(Rank.JokerSmall, out int smallJ);
            counts.TryGetValue(Rank.JokerBig, out int bigJ);
            int jokerTotal = smallJ + bigJ;

            // Helper: deep-clone dictionary
            Dictionary<Rank, int> CloneCounts() => counts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 1) Rocket / Joker bomb: if all cards are jokers (some number)
            if (jokerTotal > 0 && jokerTotal == total)
            {
                return new MoveClassification
                {
                    Kind = MoveKind.Bomb,
                    CountPrimary = jokerTotal,
                    JokerCount = jokerTotal,
                    PrimaryRank = Rank.JokerBig // arbitrary
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
                AddCandidate(new MoveClassification { Kind = MoveKind.Single, PrimaryRank = move.Cards[0].Rank, CountPrimary = 1 });
            }

            // 8) Bombs that are not single-rank (for example if there are multiple ranks but combined equal a bomb of jokers)
            // (handled earlier: all-joker-case) - otherwise bombs must be all of same rank, already handled.

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
                return k switch
                {
                    MoveKind.Bomb => 9,
                    MoveKind.Plane => 8,
                    MoveKind.FourWithTwoPairs => 7,
                    MoveKind.FourWithTwoSingles => 7,
                    MoveKind.ThreeWithPair => 6,
                    MoveKind.ThreeWithOne => 6,
                    MoveKind.ConsecutivePairs => 5,
                    MoveKind.Triplet => 4,
                    MoveKind.Pair => 3,
                    MoveKind.Single => 2,
                    _ => 1
                };
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

    // 比较器
    public static class MoveComparer
    {
        // 判断 next 是否能压 prev (True if next beats prev)
        public static bool CanBeat(Move prev, Move next, RuleSet rules)
        {
            var prevC = MoveAnalyzer.Detect(prev, rules);
            var nextC = MoveAnalyzer.Detect(next, rules);

            if (prevC.Kind == MoveKind.Invalid || nextC.Kind == MoveKind.Invalid) return false;

            // If prev is Bomb:
            if (prevC.Kind == MoveKind.Bomb)
            {
                if (nextC.Kind != MoveKind.Bomb) return false;
                // both bombs: compare power
                var p1 = rules.GetBombPower(prevC);
                var p2 = rules.GetBombPower(nextC);
                return p2 > p1;
            }
            else
            {
                // if next is bomb -> it beats any non-bomb
                if (nextC.Kind == MoveKind.Bomb) return true;

                // same kind?
                if (prevC.Kind != nextC.Kind) return false;

                // handle per-kind comparison rules:
                switch (prevC.Kind)
                {
                    case MoveKind.Single:
                    case MoveKind.Pair:
                    case MoveKind.Triplet:
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank && nextC.CountPrimary == prevC.CountPrimary;
                    case MoveKind.ThreeWithOne:
                    case MoveKind.ThreeWithPair:
                        // must be same AttachKind and same CountPrimary (3)
                        if (nextC.AttachKind != prevC.AttachKind || nextC.CountPrimary != prevC.CountPrimary) return false;
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank;
                    case MoveKind.ConsecutivePairs:
                        // same length and higher primary rank
                        if (nextC.SequenceLength != prevC.SequenceLength) return false;
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank;
                    case MoveKind.FourWithTwoPairs:
                    case MoveKind.FourWithTwoSingles:
                        // same attachment kind and same 4 size:
                        if (nextC.AttachKind != prevC.AttachKind || nextC.CountPrimary != prevC.CountPrimary) return false;
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank;
                    case MoveKind.Plane:
                        // same sequence length, same attach kind, compare highest primary
                        if (nextC.SequenceLength != prevC.SequenceLength) return false;
                        if (nextC.AttachKind != prevC.AttachKind) return false;
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank;
                    default:
                        return false;
                }
            }
        }
    }


}
