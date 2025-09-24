using OpenDDZ.DDZUtils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Entities
{
    //包含完整的对一个Move的分类结果
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



}
