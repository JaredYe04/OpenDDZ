using OpenDDZ.DDZUtils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace OpenDDZ.DDZUtils.Entities
{
    // 纸牌类
    public class Card
    {
        public Rank Rank { get; set; }
        public Suit Suit { get; set; } // 使用 Suit 常量表示花色
        public Card() { }
        /// <summary>
        /// 完整构造函数：指定点数和花色
        /// </summary>
        public Card(Rank rank, Suit suit)
        {
            if ((rank == Rank.JokerSmall || rank == Rank.JokerBig) && suit != Suit.Joker)
                throw new ArgumentException("Joker 必须使用 Suit.Joker");

            if (!(rank == Rank.JokerSmall || rank == Rank.JokerBig) && suit == Suit.Joker)
                throw new ArgumentException("非 Joker 不能使用 Suit.Joker");

            Rank = rank;
            Suit = suit;
        }

        /// <summary>
        /// 兼容旧版本：只指定点数，花色默认 Club
        /// </summary>
        public Card(Rank rank) : this(rank, 
            rank == Rank.JokerSmall || rank == Rank.JokerBig ? Suit.Joker : Suit.Club
            ) { }

        public override string ToString()
        {
            if (Suit == Suit.Joker)
                return Rank == Rank.JokerSmall ? "小王" : "大王";

            string suitStr;
            switch (Suit)
            {
                case Suit.Spade:
                    suitStr = "黑桃";
                    break;
                case Suit.Heart:
                    suitStr = "红桃";
                    break;
                case Suit.Club:
                    suitStr = "梅花";
                    break;
                case Suit.Diamond:
                    suitStr = "方片";
                    break;
                default:
                    suitStr = "";
                    break;
            }
            return string.Format("{0}{1}", suitStr, RankToString(Rank));
        }

        private static string RankToString(Rank rank)
        {
            switch (rank)
            {
                case Rank.J: return "J";
                case Rank.Q: return "Q";
                case Rank.K: return "K";
                case Rank.A: return "A";
                case Rank.Two: return "2";
                case Rank.Three: return "3";
                case Rank.Four: return "4";
                case Rank.Five: return "5";
                case Rank.Six: return "6";
                case Rank.Seven: return "7";
                case Rank.Eight: return "8";
                case Rank.Nine: return "9";
                case Rank.Ten: return "10";
                case Rank.JokerSmall: return "JokerS";
                case Rank.JokerBig: return "JokerB";
                default: return ((int)rank).ToString();
            }
        }
    }
}
