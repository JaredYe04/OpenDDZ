using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.GameIOs
{
    /// <summary>
    /// 控制台IO实现
    /// </summary>
    public class ConsoleIO : IGameIO
    {
        public void ShowMessage(string message)
        {
            Console.WriteLine(message);
        }

        public void ShowHand(IPlayer player)
        {
            Console.WriteLine(CardUtils.ShowHand(player));
        }

        public void ShowLastMove(IPlayer player, Move move, IPlayer lastPlayer)
        {
            if (move != null && move.Cards.Count > 0 && lastPlayer != player)
                Console.WriteLine($"上一手：{GetPlayerName(lastPlayer)} 出牌 {CardUtils.FormatCards(move.Cards)}");
            else
                Console.WriteLine("你是首家，请出牌。");
        }

        public Move GetMoveInput(IPlayer player)
        {
            Console.WriteLine($"{player.Name}，请输入你要出的牌（如：红桃A 黑桃K 小王），或按回车跳过：");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || input.Trim().ToLower() == "pass")
            {
                return null;
            }
            try
            {
                var move = MoveUtils.ParseMove(input);
                return move;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                ShowHand(player);
                return GetMoveInput(player);
            }
        }
        public string GetBidInput(IPlayer player)
        {
            ShowHand(player);
            Console.WriteLine($"{player.Name}，请输入叫分（0=不叫, 1/2/3 或 1分/2分/3分/不叫），回车=不叫：");
            string input = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(input) || input == "0") return "不叫";
            while (true)
            {
                switch (input)
                {
                    case "1": case "1分": return "1分";
                    case "2": case "2分": return "2分";
                    case "3": case "3分": return "3分";
                    case "0": case "不叫": return "不叫";
                    default:
                        Console.WriteLine("请输入 1/2/3 或 不叫：");
                        input = Console.ReadLine()?.Trim() ?? "";
                        if (string.IsNullOrEmpty(input)) return "不叫";
                        break;
                }
            }
        }

        public void ShowError(string message)
        {
            Console.WriteLine("[错误] " + message);
        }

        public void ShowGameEnd(IPlayer winner)
        {
            Console.WriteLine($"游戏结束，胜者：{GetPlayerName(winner)}");
        }

        public Card GetDiscardInput(IPlayer player)
        {
            return null;
        }

        public void EmitPlayRejected(string reason)
        {
            ShowError(reason);
        }

        public void BeforeBotPlay(IPlayer player) { }

        private string GetPlayerName(IPlayer player)
        {
            return player.Name;
        }
    }
}
