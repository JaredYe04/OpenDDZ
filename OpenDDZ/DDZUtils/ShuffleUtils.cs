using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils
{
    public static class ShuffleUtils
    {

        /// <summary>
        /// 标准洗牌算法（Fisher-Yates 洗牌）
        /// </summary>
        public static void RandomShuffle<T>(IList<T> list,int seed)
        {
            var rng = new Random(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// WeakShuffle算法：大范围分组交换，提升大牌组合概率
        /// </summary>
        public static void WeakShuffle<T>(IList<T> list, int seed)
        {
            var rng = new Random(seed);
            int minBlock = 4;
            int maxBlock = 15;
            int swapTimes = 10;

            int n = list.Count;
            for (int t = 0; t < swapTimes; t++)
            {
                // 随机选择两个区块
                int blockSize = rng.Next(minBlock, Math.Min(maxBlock, n / 2) + 1);
                int idx1 = rng.Next(0, n - blockSize + 1);
                int idx2 = rng.Next(0, n - blockSize + 1);
                if (Math.Abs(idx1 - idx2) < blockSize) continue; // 避免重叠

                // 交换两个区块
                for (int k = 0; k < blockSize; k++)
                {
                    (list[idx1 + k], list[idx2 + k]) = (list[idx2 + k], list[idx1 + k]);
                }
            }
            // 最后可选做一次小范围洗牌，避免完全可预测
            for (int i = 0; i < n - 1; i++)
            {
                if (rng.NextDouble() < 0.15) // 15%概率微调
                {
                    int j = rng.Next(i, Math.Min(i + minBlock, n));
                    (list[i], list[j]) = (list[j], list[i]);
                }
            }
        }
    }

}
