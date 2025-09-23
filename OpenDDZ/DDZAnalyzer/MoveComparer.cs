using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZAnalyzer
{
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
                        if (nextC.SequenceLength != prevC.SequenceLength) return false;//不一样长度，不能压
                        if (nextC.AttachKind != prevC.AttachKind) return false;
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank;
                    case MoveKind.Straight:
                        // same length, higher primary rank
                        if (nextC.SequenceLength != prevC.SequenceLength) return false;//不一样长度，不能压
                        return (int)nextC.PrimaryRank > (int)prevC.PrimaryRank;
                    default:
                        return false;
                }
            }
        }
    }


}
