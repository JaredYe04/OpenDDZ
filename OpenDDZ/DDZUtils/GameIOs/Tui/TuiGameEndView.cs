using OpenDDZ.DDZUtils;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using System.Linq;
using System.Text;
using Terminal.Gui;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public class TuiGameEndView : View
    {
        public event System.Action BackToMenu;

        public TuiGameEndView(TuiGameState state)
        {
            Width = Dim.Fill();
            Height = Dim.Fill();

            var sb = new StringBuilder();
            sb.AppendLine("══════ 本局结束 ══════");
            if (state.Winner != null)
                sb.AppendLine($"胜者：{state.Winner.Name}");
            if (state.Landlord != null)
                sb.AppendLine($"地主：{state.Landlord.Name}");
            sb.AppendLine();
            sb.AppendLine("各玩家剩余手牌：");

            foreach (var p in state.AllPlayers ?? Enumerable.Empty<IPlayer>())
            {
                var hand = p.GetHandCards();
                string cards = hand != null && hand.Count > 0
                    ? CardUtils.FormatCards(hand)
                    : "（已出完）";
                sb.AppendLine($"  {p.Name}: {cards}");
            }

            Add(new Label(sb.ToString())
            {
                X = 2,
                Y = 2,
                Width = Dim.Fill() - 4,
                Height = Dim.Fill() - 6
            });

            var btn = new StyledButton("返回主菜单")
            {
                X = Pos.Center(),
                Y = Pos.AnchorEnd(2)
            };
            btn.Clicked += () => BackToMenu?.Invoke();
            Add(btn);
        }
    }
}
