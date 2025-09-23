using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZAnalyzer
{
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
                if (jokerCount == 2)
                {
                    // greater than all 4炸 (400) but less than 5炸 (500)
                    if (mainRank == Rank.JokerSmall)
                        return 5 * 100.0 - 1 - 0.5;//小王炸稍微比大王炸低一点
                    else
                        return 5 * 100.0 - 1;
                }
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

}
