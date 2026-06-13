using OpenDDZ.DDZUtils;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public static class MoveKindFormatter
    {
        public static string Format(Move move, RuleSet rules)
        {
            if (move == null || move.Cards == null || move.Cards.Count == 0)
                return "不出";

            var c = MoveUtils.Detect(move, rules);
            return FormatClassification(c);
        }

        public static string FormatClassification(MoveClassification c)
        {
            if (c == null || c.Kind == MoveKind.Invalid)
                return "无效";
            if (c.Kind == MoveKind.None)
                return "不出";

            switch (c.Kind)
            {
                case MoveKind.Single:
                    return "单牌";
                case MoveKind.Pair:
                    return "对子";
                case MoveKind.Triplet:
                    return "三张";
                case MoveKind.ThreeWithOne:
                    return "三带一";
                case MoveKind.ThreeWithPair:
                    return "三带一对";
                case MoveKind.Straight:
                    return "顺子";
                case MoveKind.ConsecutivePairs:
                    return c.SequenceLength > 0 ? $"连对×{c.SequenceLength}" : "连对";
                case MoveKind.FourWithTwoSingles:
                    return "四带二";
                case MoveKind.FourWithTwoPairs:
                    return "四带两对";
                case MoveKind.Plane:
                    return FormatPlane(c);
                case MoveKind.Bomb:
                    return FormatBomb(c);
                default:
                    return c.Kind.ToString();
            }
        }

        private static string FormatPlane(MoveClassification c)
        {
            int n = c.SequenceLength > 0 ? c.SequenceLength : c.AttachCount;
            if (n <= 0) n = 2;
            string baseName = n >= 2 ? $"{n}飞" : "飞机";
            if (c.AttachKind == AttachmentKind.Singles && c.AttachCount > 0)
                return $"{baseName}带单";
            if (c.AttachKind == AttachmentKind.Pairs && c.AttachCount > 0)
                return $"{baseName}带对";
            return baseName;
        }

        private static string FormatBomb(MoveClassification c)
        {
            if (c.JokerCount >= 4)
                return "四王炸";
            if (c.JokerCount == 2 && c.CountPrimary <= 2)
                return "王炸";
            if (c.CountPrimary >= 5)
                return $"{c.CountPrimary}炸";
            return "炸弹";
        }
    }
}
