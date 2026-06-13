using NStack;
using Terminal.Gui;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public class TuiMenuView : View
    {
        public event System.Action StartGame;
        public event System.Action OpenSettings;
        public event System.Action ExitApp;

        public TuiMenuView()
        {
            Width = Dim.Fill();
            Height = Dim.Fill();

            Add(new Label("OpenDDZ 斗地主")
            {
                X = Pos.Center(),
                Y = 2
            });

            Add(new Label("终端图形界面 · 支持鼠标选牌")
            {
                X = Pos.Center(),
                Y = 4
            });

            var btnStart = new StyledButton("开始游戏")
            {
                X = Pos.Center(),
                Y = 8
            };
            btnStart.Clicked += () => StartGame?.Invoke();

            var btnSettings = new StyledButton("参数设置")
            {
                X = Pos.Center(),
                Y = 10
            };
            btnSettings.Clicked += () => OpenSettings?.Invoke();

            var btnExit = new StyledButton("退出")
            {
                X = Pos.Center(),
                Y = 12
            };
            btnExit.Clicked += () => ExitApp?.Invoke();

            Add(btnStart, btnSettings, btnExit);
        }
    }
}
