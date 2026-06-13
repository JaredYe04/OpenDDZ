using System;
using System.Collections.Generic;
using Terminal.Gui;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public class CardCounterView : View
    {
        private bool _expanded = true;
        private bool _toggleArmed;
        private List<CardCounterEntry> _entries = new List<CardCounterEntry>();
        private string _tableRow = "";

        public int LayoutHeight { get; private set; } = 2;
        public int LayoutWidth { get; private set; } = 10;

        public event Action LayoutChanged;

        public CardCounterView()
        {
            CanFocus = false;
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.White, Color.Green),
                Focus = Application.Driver.MakeAttribute(Color.Black, Color.BrightGreen)
            };
            ApplyLayout();
        }

        public void SetCounts(IReadOnlyList<CardCounterEntry> entries)
        {
            _entries = entries != null ? new List<CardCounterEntry>(entries) : new List<CardCounterEntry>();
            _tableRow = CardCounterHelper.FormatTableRow(_entries);
            ApplyLayout();
            SetNeedsDisplay();
        }

        public override bool OnMouseEvent(MouseEvent me)
        {
            bool released = (me.Flags & MouseFlags.Button1Released) != 0;
            bool pressed = (me.Flags & MouseFlags.Button1Pressed) != 0;
            bool inToggle = me.Y == 0 && me.X >= 6 && me.X <= 9;

            if (pressed && inToggle)
            {
                _toggleArmed = true;
                return true;
            }

            if (released)
            {
                bool wasArmed = _toggleArmed;
                _toggleArmed = false;
                if (wasArmed && inToggle)
                {
                    _expanded = !_expanded;
                    ApplyLayout();
                    SetNeedsDisplay();
                    SuperView?.SetNeedsDisplay();
                }
                return wasArmed || inToggle;
            }

            if (_toggleArmed)
                return true;

            return base.OnMouseEvent(me);
        }

        public override void Redraw(Rect bounds)
        {
            var normal = ColorScheme?.Normal ?? Application.Driver.MakeAttribute(Color.White, Color.Green);
            Driver.SetAttribute(normal);
            Clear();

            Driver.SetAttribute(Application.Driver.MakeAttribute(Color.BrightYellow, Color.Green));
            Move(0, 0);
            Driver.AddStr("记牌");

            string toggle = _expanded ? "[▼]" : "[▶]";
            Driver.SetAttribute(Application.Driver.MakeAttribute(Color.White, Color.BrightGreen));
            Move(6, 0);
            Driver.AddStr(toggle);

            if (!_expanded)
                return;

            Driver.SetAttribute(Application.Driver.MakeAttribute(Color.Gray, Color.Green));
            Move(0, 1);
            Driver.AddStr(new string('─', Math.Max(0, bounds.Width)));

            Driver.SetAttribute(normal);
            Move(0, 2);
            string row = _tableRow;
            if (string.IsNullOrEmpty(row))
                row = "（无数据）";
            if (row.Length > bounds.Width)
                row = row.Substring(0, Math.Max(0, bounds.Width));
            Driver.AddStr(row);
        }

        private void ApplyLayout()
        {
            if (!_expanded)
            {
                LayoutWidth = 10;
                LayoutHeight = 1;
                Width = LayoutWidth;
                Height = LayoutHeight;
                LayoutChanged?.Invoke();
                return;
            }

            LayoutWidth = Math.Max(40, Math.Min(120, _tableRow.Length + 4));
            LayoutHeight = 3;
            Width = LayoutWidth;
            Height = LayoutHeight;
            LayoutChanged?.Invoke();
        }
    }
}
