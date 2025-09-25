using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players;
using OpenDDZ.DDZUtils.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Controllers
{
    /// <summary>
    /// 游戏控制器
    /// </summary>
    public class GameController
    {
        private readonly GameConfig _config;
        private readonly IGameIO _io;
        private IDealer _dealer;
        private List<IPlayer> _players;

        public GameController(GameConfig config, IGameIO io)
        {
            _config = config;
            _io = io;
            _dealer = config.Dealer;
            _players = config.Players;
        }

        public void StartGame()
        {
            _dealer.RegisterPlayers(_players);
            _dealer.StartGame(_config);
        }

        public void RunGameLoop()
        {
            while (true)
            {
                if (_dealer.CurrentGame == null)
                    break;

                var winner = _players.FirstOrDefault(p => p.GetHandCards().Count == 0);
                if (winner != null)
                {
                    _io.ShowGameEnd(winner);
                    break;
                }

                var currentPlayer = _players[_dealer.GetCurrentPlayerIndex()];
                if (currentPlayer is RealPlayer)
                {
                    _io.ShowHand(currentPlayer);
                    _io.ShowLastMove(currentPlayer, _dealer.LastMove.Item2, _dealer.LastMove.Item1);

                    while (true)
                    {
                        var input = _io.GetMoveInput(currentPlayer);
                        if (string.IsNullOrWhiteSpace(input) || input.Trim().ToLower() == "pass")
                        {
                            currentPlayer.RequestPlay(null);
                            break;
                        }
                        var move = MoveUtils.ParseMove(input, currentPlayer.GetHandCards());
                        if (move == null)
                        {
                            _io.ShowError("输入格式错误或牌不在手牌中，请重新输入。");
                            continue;
                        }
                        currentPlayer.RequestPlay(move);
                        break;
                    }
                }
                // BotPlayer等AI玩家会自动响应RequestPlay消息
            }
        }
    }
}
