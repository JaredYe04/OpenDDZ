using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using OpenDDZ.DDZUtils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils
{
    internal class CardUtils
    {
        public static string ShowHand(IPlayer player)
        {
            var hand = player.GetHandCards().OrderByDescending(c => (int)c.Rank).ThenByDescending(c => (int)c.Suit).ToList();
            return $"你的手牌：{FormatCards(hand)}";
        }

        public static string FormatCards(IEnumerable<Card> cards)
        {
            return string.Join(" ", cards.OrderByDescending(c => (int)c.Rank).ThenByDescending(c => (int)c.Suit).Select(CardToString));
        }

        public static string CardToString(Card card)
        {
            if (card.Rank == Rank.JokerSmall) return "小王";
            if (card.Rank == Rank.JokerBig) return "大王";
            string suitStr = "";
            switch (card.Suit)
            {
                case Suit.Heart:
                    suitStr = "红桃";
                    break;
                case Suit.Spade:
                    suitStr = "黑桃";
                    break;
                case Suit.Diamond:
                    suitStr = "方片";
                    break;
                case Suit.Club:
                    suitStr = "梅花";
                    break;
                default:
                    suitStr = "王牌";
                    break;
            }
            string rankStr = "";
            switch (card.Rank)
            {
                case Rank.A:
                    rankStr = "A";
                    break;
                case Rank.K:
                    rankStr = "K";
                    break;
                case Rank.Q:
                    rankStr = "Q";
                    break;
                case Rank.J:
                    rankStr = "J";
                    break;
                case Rank.Ten:
                    rankStr = "10";
                    break;
                case Rank.Nine:
                    rankStr = "9";
                    break;
                case Rank.Eight:
                    rankStr = "8";
                    break;
                case Rank.Seven:
                    rankStr = "7";
                    break;
                case Rank.Six:
                    rankStr = "6";
                    break;
                case Rank.Five:
                    rankStr = "5";
                    break;
                case Rank.Four:
                    rankStr = "4";
                    break;
                case Rank.Three:
                    rankStr = "3";
                    break;
                case Rank.Two:
                    rankStr = "2";
                    break;
                default:
                    rankStr = ((int)card.Rank).ToString();
                    break;
            }
            return $"{suitStr}{rankStr}";
        }

        public static List<Card> CreateDeck()
        {
            var deck = new List<Card>();
            foreach (Rank r in Enum.GetValues(typeof(Rank)))
            {
                if (r == Rank.JokerSmall || r == Rank.JokerBig) continue;// 跳过大小王，后面单独添加
                foreach (Suit s in Enum.GetValues(typeof(Suit)))
                {
                    if (s == Suit.Joker) continue;// 跳过 Joker 花色
                    deck.Add(new Card(r, s));
                }

            }
            deck.Add(new Card(Rank.JokerSmall, Suit.Joker));
            deck.Add(new Card(Rank.JokerBig, Suit.Joker));
            return deck;
        }
    }
}
