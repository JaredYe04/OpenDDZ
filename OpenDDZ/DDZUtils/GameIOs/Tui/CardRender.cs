using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terminal.Gui;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public static class CardRender
    {
        public const int CardWidth = 4;

        public static List<Card> SortHand(IEnumerable<Card> cards)
        {
            return cards
                .OrderByDescending(c => (int)c.Rank)
                .ThenByDescending(c => (int)c.Suit)
                .ToList();
        }

        public static string CompactLabel(Card card)
        {
            if (card.Rank == Rank.JokerSmall) return "小";
            if (card.Rank == Rank.JokerBig) return "大";
            string rank = RankLabel(card.Rank);
            string suit = SuitSymbol(card.Suit);
            return suit + rank;
        }

        public static string FormatCardLine(IEnumerable<Card> cards)
        {
            if (cards == null) return "";
            var list = SortHand(cards);
            if (list.Count == 0) return "—";
            return string.Join(" ", list.Select(c => PadCard(CompactLabel(c))));
        }

        public static string PadCard(string label)
        {
            if (label.Length >= CardWidth) return label.Substring(0, CardWidth);
            return label.PadRight(CardWidth);
        }

        public static Terminal.Gui.Attribute CardAttr(Card card, bool selected, bool hint)
        {
            if (hint)
                return new Terminal.Gui.Attribute(Color.White, Color.BrightBlue);
            if (selected)
                return new Terminal.Gui.Attribute(Color.White, Color.BrightBlue);

            if (card.Rank == Rank.JokerBig || card.Rank == Rank.JokerSmall)
                return new Terminal.Gui.Attribute(Color.BrightRed, Color.Black);

            switch (card.Suit)
            {
                case Suit.Heart:
                case Suit.Diamond:
                    return new Terminal.Gui.Attribute(Color.BrightRed, Color.Black);
                default:
                    return new Terminal.Gui.Attribute(Color.White, Color.Black);
            }
        }

        private static string SuitSymbol(Suit suit)
        {
            switch (suit)
            {
                case Suit.Heart: return "♥";
                case Suit.Diamond: return "♦";
                case Suit.Spade: return "♠";
                case Suit.Club: return "♣";
                default: return "★";
            }
        }

        private static string RankLabel(Rank rank)
        {
            switch (rank)
            {
                case Rank.Three: return "3";
                case Rank.Four: return "4";
                case Rank.Five: return "5";
                case Rank.Six: return "6";
                case Rank.Seven: return "7";
                case Rank.Eight: return "8";
                case Rank.Nine: return "9";
                case Rank.Ten: return "T";
                case Rank.J: return "J";
                case Rank.Q: return "Q";
                case Rank.K: return "K";
                case Rank.A: return "A";
                case Rank.Two: return "2";
                default: return "";
            }
        }
    }
}
