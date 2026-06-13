using Terminal.Gui;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public class StyledButton : Button
    {
        private ColorScheme _baseScheme;

        public StyledButton(string text) : base(text)
        {
            ApplyScheme(false);
        }

        private void ApplyScheme(bool pressed)
        {
            _baseScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.White, pressed ? Color.Green : Color.BrightGreen),
                Focus = Application.Driver.MakeAttribute(Color.Black, Color.BrightGreen),
                HotNormal = Application.Driver.MakeAttribute(Color.White, Color.BrightGreen),
                HotFocus = Application.Driver.MakeAttribute(Color.Black, Color.BrightYellow),
                Disabled = Application.Driver.MakeAttribute(Color.Gray, Color.DarkGray)
            };
            ColorScheme = _baseScheme;
        }

        public override bool OnMouseEvent(MouseEvent me)
        {
            if ((me.Flags & MouseFlags.Button1Pressed) != 0)
            {
                ApplyScheme(true);
                SetNeedsDisplay();
            }
            else if ((me.Flags & MouseFlags.Button1Released) != 0)
            {
                ApplyScheme(false);
                SetNeedsDisplay();
            }
            return base.OnMouseEvent(me);
        }
    }
}
