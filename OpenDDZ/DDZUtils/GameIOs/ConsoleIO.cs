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
            Console.WriteLine($"{player.Name}，请输入你的叫分（0-3），或按回车跳过：");
            string input= Console.ReadLine();
            //进行parse,0是不叫，1是1分
            while(true)
            {
                switch (input)
                {
                    case "1": case "1分": return "1分";
                    case "2": case "2分": return "2分";
                    case "3": case "3分": return "3分";
                    case "0": case "不叫": return "不叫";
                    default:
                        Console.WriteLine("请输入正确的叫分:");
                        input = Console.ReadLine();
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

        private string GetPlayerName(IPlayer player)
        {
            return player.Name;
        }
    }
}
