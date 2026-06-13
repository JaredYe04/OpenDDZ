using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using System;
using Terminal.Gui;
using TgView = Terminal.Gui.View;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public enum CardVisualState
    {
        Normal,
        Hover,
        Pressed,
        Selected,
        Hint,
        DragPreview
    }

    public static class CardFaceRenderer
    {
        public const int CardBodyWidth = 5;
        public const int CardBodyHeight = 3;
        public const int CardStepX = 6;
        public const int CardTotalHeight = 5;
        public const int CardInnerWidth = 3;

        public const int MiniWidth = 5;
        public const int MiniHeight = 3;
        public const int MiniStepX = 6;
        public const int MiniInnerWidth = 3;

        public const int BackMiniWidth = 3;
        public const int BackMiniHeight = 3;
        public const int BackMiniStepX = 3;

        public static int ComputeHandWidth(int cardCount)
        {
            if (cardCount <= 0) return 0;
            return CardBodyWidth + (cardCount - 1) * CardStepX;
        }

        public static int ComputeMiniRowWidth(int cardCount)
        {
            if (cardCount <= 0) return 0;
            return MiniWidth + (cardCount - 1) * MiniStepX;
        }

        public static int ComputeBackRowWidth(int cardCount)
        {
            if (cardCount <= 0) return 0;
            int shown = Math.Min(cardCount, 12);
            return BackMiniWidth + (shown - 1) * BackMiniStepX;
        }

        public static void DrawCard(TgView view, int x, int y, Card card, CardVisualState state)
        {
            int yOff = 0;
            if (state == CardVisualState.Selected || state == CardVisualState.Hint)
                yOff = -1;
            else if (state == CardVisualState.Pressed)
                yOff = 1;

            int drawY = y + yOff;
            var (fg, bg, borderFg) = ColorsFor(card, state);
            bool bold = state == CardVisualState.Hover || state == CardVisualState.DragPreview;

            DrawBorderRow(view, x, drawY, CardBodyWidth, bold, borderFg, bg, top: true);
            DrawInnerRow(view, x, drawY + 1, CardBodyWidth, FormatFaceFull(card), fg, bg, borderFg);
            DrawBorderRow(view, x, drawY + 2, CardBodyWidth, bold, borderFg, bg, top: false);
        }

        public static void DrawMiniCard(TgView view, int x, int y, Card card, CardVisualState state)
        {
            var (fg, bg, borderFg) = ColorsFor(card, state);

            DrawBorderRow(view, x, y, MiniWidth, false, borderFg, bg, top: true);
            DrawMiniInnerRow(view, x, y + 1, card, fg, bg, borderFg);
            DrawBorderRow(view, x, y + 2, MiniWidth, false, borderFg, bg, top: false);
        }

        private static void DrawMiniInnerRow(TgView view, int x, int y, Card card, Color fg, Color bg, Color borderFg)
        {
            int right = x + MiniWidth - 1;
            int innerCols = MiniWidth - 2;

            DrawAt(view, x, y, "│", borderFg, bg);
            for (int c = x + 1; c < right; c++)
                DrawAt(view, c, y, " ", fg, bg);
            DrawClippedCentered(view, x + 1, y, innerCols, FormatFaceMini(card), fg, bg);
            DrawAt(view, right, y, "│", borderFg, bg);
        }

        public static void DrawMiniCardBack(TgView view, int x, int y)
        {
            DrawBorderRow(view, x, y, BackMiniWidth, false, Color.White, Color.BrightBlue, top: true);
            DrawInnerRow(view, x, y + 1, BackMiniWidth, "▒", Color.BrightCyan, Color.Blue, Color.White);
            DrawBorderRow(view, x, y + 2, BackMiniWidth, false, Color.White, Color.BrightBlue, top: false);
        }

        public static CardVisualState ResolveState(bool selected, bool hint, bool hover, bool pressed, bool dragPreview = false)
        {
            if (pressed) return CardVisualState.Pressed;
            if (hint) return CardVisualState.Hint;
            if (selected) return CardVisualState.Selected;
            if (dragPreview) return CardVisualState.DragPreview;
            if (hover) return CardVisualState.Hover;
            return CardVisualState.Normal;
        }

        private static string FormatFaceFull(Card card)
        {
            if (card.Rank == Rank.JokerSmall) return "☆";
            if (card.Rank == Rank.JokerBig) return "★";
            return RankLabel(card) + SuitSymbol(card.Suit);
        }

        private static string FormatFaceMini(Card card)
        {
            if (card.Rank == Rank.JokerSmall) return "☆";
            if (card.Rank == Rank.JokerBig) return "★";
            return RankLabel(card) + SuitSymbol(card.Suit);
        }

        private static void DrawBorderRow(TgView view, int x, int y, int width, bool bold, Color borderFg, Color bg, bool top)
        {
            string tl = top ? (bold ? "┏" : "╭") : (bold ? "┗" : "╰");
            string tr = top ? (bold ? "┓" : "╮") : (bold ? "┛" : "╯");
            string hz = bold ? "━" : "─";

            DrawAt(view, x, y, tl, borderFg, bg);
            for (int i = 1; i < width - 1; i++)
                DrawAt(view, x + i, y, hz, borderFg, bg);
            DrawAt(view, x + width - 1, y, tr, borderFg, bg);
        }

        private static void DrawInnerRow(TgView view, int x, int y, int width, string content, Color fg, Color bg, Color borderFg)
        {
            int rightBorder = x + width - 1;
            int innerCols = width - 2;

            DrawAt(view, x, y, "│", borderFg, bg);
            for (int c = x + 1; c < rightBorder; c++)
                DrawAt(view, c, y, " ", fg, bg);
            DrawClippedCentered(view, x + 1, y, innerCols, content, fg, bg);
            DrawAt(view, rightBorder, y, "│", borderFg, bg);
        }

        private static void DrawClippedCentered(TgView view, int x, int y, int maxCols, string content, Color fg, Color bg)
        {
            if (maxCols <= 0) return;

            string text = TruncateDisplayWidth(content, maxCols);
            int textWidth = DisplayWidth(text);
            int start = x + Math.Max(0, (maxCols - textWidth) / 2);

            var driver = Application.Driver;
            driver.SetAttribute(driver.MakeAttribute(fg, bg));
            view.Move(start, y);
            driver.AddStr(text);
        }

        private static string TruncateDisplayWidth(string text, int maxCols)
        {
            if (string.IsNullOrEmpty(text) || maxCols <= 0) return "";
            var sb = new System.Text.StringBuilder();
            int w = 0;
            foreach (var ch in text)
            {
                int cw = Rune.ColumnWidth(ch);
                if (w + cw > maxCols) break;
                sb.Append(ch);
                w += cw;
            }
            return sb.ToString();
        }

        private static int DisplayWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int w = 0;
            foreach (var ch in text)
                w += Rune.ColumnWidth(ch);
            return w;
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

        private static string RankLabel(Card card)
        {
            switch (card.Rank)
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
                default: return "?";
            }
        }

        private static (Color fg, Color bg, Color borderFg) ColorsFor(Card card, CardVisualState state)
        {
            Color fg = IsRedSuit(card) ? Color.BrightRed : Color.Black;
            Color bg = Color.White;
            Color border = Color.Gray;

            switch (state)
            {
                case CardVisualState.Hint:
                case CardVisualState.Selected:
                    fg = Color.White;
                    bg = Color.BrightBlue;
                    border = Color.BrightCyan;
                    break;
                case CardVisualState.DragPreview:
                    border = Color.BrightYellow;
                    bg = Color.BrightYellow;
                    fg = Color.Black;
                    break;
                case CardVisualState.Hover:
                    border = Color.White;
                    bg = Color.Gray;
                    break;
                case CardVisualState.Pressed:
                    fg = IsRedSuit(card) ? Color.Red : Color.Black;
                    bg = Color.Gray;
                    border = Color.DarkGray;
                    break;
            }

            if (card.Rank == Rank.JokerBig || card.Rank == Rank.JokerSmall)
            {
                fg = state == CardVisualState.Selected || state == CardVisualState.Hint
                    ? Color.White
                    : Color.BrightRed;
            }

            return (fg, bg, border);
        }

        private static bool IsRedSuit(Card card)
        {
            return card.Suit == Suit.Heart || card.Suit == Suit.Diamond
                || card.Rank == Rank.JokerBig || card.Rank == Rank.JokerSmall;
        }

        private static void DrawAt(TgView view, int x, int y, string text, Color fg, Color bg)
        {
            if (string.IsNullOrEmpty(text)) return;
            var driver = Application.Driver;
            driver.SetAttribute(driver.MakeAttribute(fg, bg));
            view.Move(x, y);
            driver.AddStr(text);
        }
    }
}
