using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZAnalyzer
{
    // 一次出牌（move）
    public class Move
    {
        public List<Card> Cards { get; } = new List<Card>();
        public Move(IEnumerable<Card> cards) { Cards.AddRange(cards); }
        public Move(params Rank[] ranks) { foreach (var r in ranks) Cards.Add(new Card(r)); }
        public override string ToString() => string.Join(",", Cards.Select(c => c.Rank.ToString()));


        /// <summary>
        /// 将字符串解析为 Move
        /// 例如 "34567" -> Move(3,4,5,6,7)
        /// "XXY" -> Move(JokerSmall, JokerSmall, JokerBig)
        /// </summary>
        public Move(string s)
        {

            foreach (char c in s)
            {
                switch (c)
                {
                    case '3': Cards.Add(new Card(Rank.Three)); break;
                    case '4': Cards.Add(new Card(Rank.Four)); break;
                    case '5': Cards.Add(new Card(Rank.Five)); break;
                    case '6': Cards.Add(new Card(Rank.Six)); break;
                    case '7': Cards.Add(new Card(Rank.Seven)); break;
                    case '8': Cards.Add(new Card(Rank.Eight)); break;
                    case '9': Cards.Add(new Card(Rank.Nine)); break;
                    case 'T': Cards.Add(new Card(Rank.Ten)); break;
                    case 'J': Cards.Add(new Card(Rank.J)); break;
                    case 'Q': Cards.Add(new Card(Rank.Q)); break;
                    case 'K': Cards.Add(new Card(Rank.K)); break;
                    case 'A': Cards.Add(new Card(Rank.A)); break;
                    case '2': Cards.Add(new Card(Rank.Two)); break;
                    case 'X': Cards.Add(new Card(Rank.JokerSmall)); break;
                    case 'Y': Cards.Add(new Card(Rank.JokerBig)); break;
                    default:
                        throw new ArgumentException($"Invalid card character: {c}");
                }
            }
        }
    }
}
