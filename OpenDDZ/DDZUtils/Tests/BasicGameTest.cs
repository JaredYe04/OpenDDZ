using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players;
using OpenDDZ.DDZUtils.Dealers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.Tests
{
    internal class BasicGameTest
    {
        public static void Run()
        {
            // 规则
            var rules = RuleSet.Default;

            // 创建庄家
            var dealer = new BasicDealer(rules);

            // 创建玩家
            var realPlayer = new ConsoleRealPlayer("你", "player@local");
            var bot1 = new BotPlayer("Bot1");
            var bot2 = new BotPlayer("Bot2");

            var players = new List<IPlayer> { realPlayer, bot1, bot2 };
            dealer.RegisterPlayers(players);

            // 开始游戏
            dealer.StartGame();

            // 主循环
            while (true)
            {
                if (dealer.CurrentGame == null)
                    break;

                // 检查是否有玩家出完牌
                var winner = players.FirstOrDefault(p => p.GetHandCards().Count == 0);
                if (winner != null)
                {
                    Console.WriteLine($"游戏结束，胜者：{GetPlayerName(winner)}");
                    break;
                }

                // 让当前玩家行动
                var currentPlayer = players[dealer.GetCurrentPlayerIndex()];
                if (currentPlayer is ConsoleRealPlayer)
                {
                    // 显示手牌
                    Console.WriteLine(CardUtils.ShowHand(currentPlayer));

                    // 显示上一手x
                    var lastMove = dealer.LastMove.Item2;
                    if (lastMove != null && lastMove.Cards.Count > 0 && dealer.LastMove.Item1 != currentPlayer)
                    {
                        Console.WriteLine($"上一手：{GetPlayerName(dealer.LastMove.Item1)} 出牌 {CardUtils.FormatCards(lastMove.Cards)}");
                    }
                    else
                    {
                        Console.WriteLine("你是首家，请出牌。");
                    }

                    // 输入出牌
                    while (true)
                    {
                        Console.WriteLine("请输入你要出的牌（如：红桃A 黑桃K 小王），或按回车跳过：");
                        var input = Console.ReadLine();
                        if (input.Trim().ToLower() == "pass")
                        {
                            currentPlayer.RequestPlay(null);
                            break;
                        }
                        var move = ParseMove(input, currentPlayer.GetHandCards().ToList());
                        if (move == null)
                        {
                            Console.WriteLine("输入格式错误或牌不在手牌中，请重新输入。");
                            continue;
                        }
                        // 合法性由庄家判断
                        currentPlayer.RequestPlay(move);
                        break;
                    }
                }
                else
                {
                    // Bot自动出牌
                    // BotPlayer会自动响应RequestPlay消息
                }
            }
        }

        // 控制台真人玩家
        public class ConsoleRealPlayer : RealPlayer
        {
            public ConsoleRealPlayer(string username, string email) : base(username, email) { }

            public override void OnMessage(DealerMessage message)
            {
                // 控制台输出
                switch (message.Type)
                {
                    case DealerMessageType.Info:
                        Console.WriteLine($"[系统] {message.Content}");
                        break;
                    case DealerMessageType.Error:
                        Console.WriteLine($"[错误] {message.Content}");
                        break;
                    case DealerMessageType.RequestPlay:
                        // 由主循环处理
                        break;
                    default:
                        Console.WriteLine($"[消息] {message.Content}");
                        break;
                }
            }
        }


        // 工具方法

        private static string GetPlayerName(IPlayer player)
        {
            if (player is ConsoleRealPlayer rp) return rp.Username;
            return player.Id;
        }

        // 解析用户输入为Move
        private static Move ParseMove(string input, List<Card> hand)
        {
            var parts = input.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var selected = new List<Card>();
            foreach (var part in parts)
            {
                var card = ParseCard(part.Trim(), hand);
                if (card == null) return null;
                selected.Add(card);
            }
            // 检查是否都在手牌
            foreach (var card in selected)
            {
                if (!hand.Any(h => h.Suit == card.Suit && h.Rank == card.Rank))
                    return null;
            }
            return new Move(selected);
        }

        // 解析单张牌
        private static Card ParseCard(string str, List<Card> hand)
        {
            str = str.Replace(" ", "").Replace("红桃", "H").Replace("黑桃", "S").Replace("方片", "D").Replace("梅花", "C");
            if (str == "小王") return hand.FirstOrDefault(c => c.Rank == Rank.JokerSmall);
            if (str == "大王") return hand.FirstOrDefault(c => c.Rank == Rank.JokerBig);

            Suit? suit = null;
            Rank? rank = null;

            if (str.StartsWith("H")) suit = Suit.Heart;
            else if (str.StartsWith("S")) suit = Suit.Spade;
            else if (str.StartsWith("D")) suit = Suit.Diamond;
            else if (str.StartsWith("C")) suit = Suit.Club;

            var rankStr = str.Substring(1).ToUpper();
            switch (rankStr)
            {
                case "A": rank = Rank.A; break;
                case "K": rank = Rank.K; break;
                case "Q": rank = Rank.Q; break;
                case "J": rank = Rank.J; break;
                case "10": rank = Rank.Ten; break;
                case "9": rank = Rank.Nine; break;
                case "8": rank = Rank.Eight; break;
                case "7": rank = Rank.Seven; break;
                case "6": rank = Rank.Six; break;
                case "5": rank = Rank.Five; break;
                case "4": rank = Rank.Four; break;
                case "3": rank = Rank.Three; break;
                case "2": rank = Rank.Two; break;
                default: return null;
            }
            return hand.FirstOrDefault(c => c.Suit == suit && c.Rank == rank);
        }

        public static void Main(string[] args)
        {
            Run();
        }
    }
}
