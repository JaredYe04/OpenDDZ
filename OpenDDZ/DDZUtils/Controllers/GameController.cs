using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
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
            _config.AfterDeal = () =>
            {
                foreach (var p in _players)
                    if (p is RealPlayer)
                        _io.ShowHand(p);
            };
            _dealer.StartGame(_config);
        }

        public void RunGameLoop()
        {
            // 开局后立即向所有人类玩家发送手牌，避免客户端进入游戏后手牌区为空
            foreach (var p in _players)
            {
                if (p is RealPlayer)
                    _io.ShowHand(p);
            }

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
                if (currentPlayer is BotPlayer)
                {
                    _io.BeforeBotPlay(currentPlayer);
                    currentPlayer.OnDealerMessage(new DealerMessage { Type = DealerMessageType.RequestPlay, Content = "请出牌" });
                    continue;
                }
                if (currentPlayer is RealPlayer)
                {
                    _io.ShowHand(currentPlayer);
                    _io.ShowLastMove(currentPlayer, _dealer.LastMove.Item2, _dealer.LastMove.Item1);

                    int playAttempts = 0;
                    bool playAccepted = false;
                    while (playAttempts++ < 30)
                    {
                        var move = _io.GetMoveInput(currentPlayer);

                        if (!MoveUtils.ValidateMove(move, currentPlayer.GetHandCards()))
                        {
                            _io.ShowError("输入格式错误或牌不在手牌中，请重新输入。");
                            _io.EmitPlayRejected("输入格式错误或牌不在手牌中，请重新输入。");
                            continue;
                        }
                        int handBefore = currentPlayer.GetHandCards().Count;
                        currentPlayer.RequestPlay(move);
                        if (currentPlayer.GetHandCards().Count == handBefore && move != null && move.Cards.Count > 0)
                        {
                            _io.EmitPlayRejected("出牌不符合规则，无法压过上家");
                            continue;
                        }
                        playAccepted = true;
                        if (currentPlayer is RealPlayer)
                            _io.ShowHand(currentPlayer);
                        break;
                    }
                    if (!playAccepted)
                        currentPlayer.RequestPlay(null);
                }
                // BotPlayer等AI玩家会自动响应RequestPlay消息
            }
        }
    }
}
