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

        public string GetMoveInput(IPlayer player)
        {
            Console.WriteLine("请输入你要出的牌（如：红桃A 黑桃K 小王），或按回车跳过：");
            return Console.ReadLine();
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
