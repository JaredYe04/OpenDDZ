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

        public void SetSeat(string label, SeatDisplayInfo info)
        {
            _label = label ?? "";
            _info = info;
            PanelWidth = 22;

            int backW = info != null ? CardFaceRenderer.ComputeBackRowWidth(info.HandCount) : 0;
            int playW = info?.LastCards != null && info.LastCards.Count > 0
                ? CardFaceRenderer.ComputeMiniRowWidth(info.LastCards.Count)
                : 0;

            PanelWidth = Math.Max(22, Math.Max(backW, playW) + 2);
            Width = PanelWidth;
            SetNeedsDisplay();
        }

        public override void Redraw(Rect bounds)
        {
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

            DrawHandBacks();

            if (_info.IsPass)
            {
                Driver.SetAttribute(Application.Driver.MakeAttribute(Color.Gray, Color.Green));
                Move(0, 7);
                Driver.AddStr("不出");
                if (!string.IsNullOrEmpty(_info.MoveKindLabel))
                {
                    Move(0, 8);
                    Driver.AddStr($"[{_info.MoveKindLabel}]");
                }
                return;
            }

            var cards = _info.LastCards ?? new List<Card>();
            if (cards.Count == 0) return;

            int x = 0;
            foreach (var card in cards)
            {
                CardFaceRenderer.DrawMiniCard(this, x, 6, card, CardVisualState.Normal);
                x += CardFaceRenderer.MiniStepX;
            }

            Driver.SetAttribute(Application.Driver.MakeAttribute(Color.BrightCyan, Color.Green));
            Move(0, 9);
            Driver.AddStr($"[{_info.MoveKindLabel}]");
        }

        private void DrawHandBacks()
        {
            int count = _info?.HandCount ?? 0;
            if (count <= 0) return;

            int shown = Math.Min(count, 12);
            int x = 0;
            for (int i = 0; i < shown; i++)
            {
                CardFaceRenderer.DrawMiniCardBack(this, x, 2);
                x += CardFaceRenderer.BackMiniStepX;
            }

            Driver.SetAttribute(Application.Driver.MakeAttribute(Color.Gray, Color.Green));
            Move(x + 1, 2);
            if (count > shown)
                Driver.AddStr($"+{count - shown}");
            else
                Driver.AddStr($"×{count}");
        }
    }
}
