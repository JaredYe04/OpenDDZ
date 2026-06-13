using OpenDDZ.DDZUtils.Entities;

using System;

using System.Collections.Generic;

using System.Linq;

using Terminal.Gui;



namespace OpenDDZ.DDZUtils.GameIOs.Tui

{

    public class SeatPanelView : View

    {

        private SeatDisplayInfo _info;

        private string _label = "";

        private bool _hideHandBacks;



        public SeatPanelView()

        {

            CanFocus = false;

            Width = 22;

            Height = 11;

            ColorScheme = new ColorScheme

            {

                Normal = Application.Driver.MakeAttribute(Color.White, Color.Green)

            };

        }



        public int PanelWidth { get; private set; } = 22;



        private int _maxLayoutWidth;



        public int LayoutWidth

        {

            get

            {

                int cap = _maxLayoutWidth > 0 ? _maxLayoutWidth : PanelWidth;

                int w = Math.Max(22, Math.Min(PanelWidth, cap > 0 ? cap : PanelWidth));

                int handNeed = HandRowLayoutWidth();

                int playNeed = PlayRowLayoutWidth();

                w = Math.Max(w, Math.Max(handNeed, playNeed));

                if (cap > 0)

                    w = Math.Min(w, cap);

                return w;

            }

        }



        private int PlayRowLayoutWidth()

        {

            var cards = _info?.LastCards;

            if (cards == null || cards.Count == 0) return 0;

            return CardFaceRenderer.ComputeMiniRowWidth(cards.Count) + 2;

        }



        private int HandRowLayoutWidth()

        {

            var visible = _info?.VisibleHandCards;

            if (visible != null && visible.Count > 0)

                return CardFaceRenderer.ComputeMiniRowWidth(visible.Count) + 2;

            int count = _info?.HandCount ?? 0;

            if (count <= 0) return 0;

            return CardFaceRenderer.ComputeBackRowWidth(count) + 6;

        }



        public void ApplyLayoutWidth(int maxWidth)

        {

            _maxLayoutWidth = maxWidth > 0 ? maxWidth : 0;

            Width = LayoutWidth;

        }



        public void SetSeat(string label, SeatDisplayInfo info, bool hideHandBacks = false)

        {

            _label = label ?? "";

            _info = info;

            _hideHandBacks = hideHandBacks;

            PanelWidth = 22;



            int handW = HandRowLayoutWidth();

            int playW = info?.LastCards != null && info.LastCards.Count > 0

                ? CardFaceRenderer.ComputeMiniRowWidth(info.LastCards.Count)

                : 0;



            PanelWidth = Math.Max(22, Math.Max(handW, playW) + 2);

            Width = PanelWidth;

            SetNeedsDisplay();

        }



        public override void Redraw(Rect bounds)

        {

            int panelW = LayoutWidth;

            var normal = ColorScheme?.Normal ?? Application.Driver.MakeAttribute(Color.White, Color.Green);

            Driver.SetAttribute(normal);

            Clear();



            if (_info == null)

            {

                Move(0, 0);

                Driver.AddStr(_label + ": —");

                return;

            }



            string mark = _info.IsLandlord ? "★" : "";

            string turn = _info.IsCurrentTurn ? " ◀" : "";

            Driver.SetAttribute(Application.Driver.MakeAttribute(Color.BrightYellow, Color.Green));

            Move(0, 0);

            Driver.AddStr($"{_label} {_info.PlayerName}{mark}{turn}");



            DrawHandRow();



            int playY = _hideHandBacks ? 2 : 6;

            int labelY = _hideHandBacks ? 5 : 9;

            int passY = _hideHandBacks ? 2 : 7;

            int passLabelY = _hideHandBacks ? 3 : 8;



            if (_info.IsPass)

            {

                Driver.SetAttribute(Application.Driver.MakeAttribute(Color.Gray, Color.Green));

                Move(0, passY);

                Driver.AddStr("不出");

                if (!string.IsNullOrEmpty(_info.MoveKindLabel))

                {

                    Move(0, passLabelY);

                    Driver.AddStr($"[{_info.MoveKindLabel}]");

                }

                return;

            }



            var cards = _info.LastCards ?? new List<Card>();

            if (cards.Count == 0) return;



            int x = 0;

            foreach (var card in cards)

            {

                if (x + CardFaceRenderer.MiniWidth > panelW)

                    break;

                CardFaceRenderer.DrawMiniCard(this, x, playY, card, CardVisualState.Normal);

                x += CardFaceRenderer.MiniStepX;

            }



            Driver.SetAttribute(Application.Driver.MakeAttribute(Color.BrightCyan, Color.Green));

            Move(0, labelY);

            Driver.AddStr($"[{_info.MoveKindLabel}]");

        }



        private void DrawHandRow()

        {

            if (_hideHandBacks) return;



            var visible = _info?.VisibleHandCards;

            if (visible != null && visible.Count > 0)

            {

                int x = 0;

                int shown = 0;

                foreach (var card in visible)

                {

                    if (x + CardFaceRenderer.MiniWidth > LayoutWidth)

                        break;

                    CardFaceRenderer.DrawMiniCard(this, x, 2, card, CardVisualState.Normal);

                    x += CardFaceRenderer.MiniStepX;

                    shown++;

                }

                if (visible.Count > shown)

                {

                    Driver.SetAttribute(Application.Driver.MakeAttribute(Color.Gray, Color.Green));

                    Move(x + 1, 2);

                    Driver.AddStr($"+{visible.Count - shown}");

                }

                return;

            }



            int count = _info?.HandCount ?? 0;

            if (count <= 0) return;



            int avail = Math.Max(CardFaceRenderer.BackMiniWidth, LayoutWidth - 6);

            int maxShown = Math.Max(1, (avail - CardFaceRenderer.BackMiniWidth) / CardFaceRenderer.BackMiniStepX + 1);

            int shownBacks = Math.Min(count, Math.Min(12, maxShown));

            int xBack = 0;

            for (int i = 0; i < shownBacks; i++)

            {

                CardFaceRenderer.DrawMiniCardBack(this, xBack, 2);

                xBack += CardFaceRenderer.BackMiniStepX;

            }



            Driver.SetAttribute(Application.Driver.MakeAttribute(Color.Gray, Color.Green));

            Move(xBack + 1, 2);

            if (count > shownBacks)

                Driver.AddStr($"+{count - shownBacks}");

            else

                Driver.AddStr($"×{count}");

        }

    }

}



