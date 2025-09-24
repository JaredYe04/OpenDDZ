using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Enums
{
    public enum MoveKind
    {
        Invalid,// 无效
        None,// 空
        Single,// 单张
        Pair,// 对子
        Triplet,// 三张
        ThreeWithOne,// 三带一
        ThreeWithPair,// 三带一对
        Straight,// 顺子（至少5张连续单牌）
        ConsecutivePairs,// 连对（至少3对连续）
        FourWithTwoSingles,// 4带2单
        FourWithTwoPairs,// 4带2对
        Plane,         // 飞机（包含带牌情况）
        Bomb            // 炸弹（任意大小）或王炸（用 classification 的 JokerCount 标记）
    }

}
