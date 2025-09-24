using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenDDZ.Utils;

namespace OpenDDZ.DDZUtils.Dealers
{
    public class BasicDealer : IDealer
    {
        public GameRecord CurrentGame { get; private set; }
        public (IPlayer, Move, DateTime) LastMove { get; private set; }
        public RuleSet Rules { get; private set; }
        public bool EnableTimer => false;

        private List<IPlayer> _players = new List<IPlayer>();
        private int _currentPlayerIndex = 0;
        private List<Card> _deck = new List<Card>();

        public BasicDealer(RuleSet rules)
        {
            Rules = rules;
        }

        public void RegisterPlayers(IEnumerable<IPlayer> players)
        {
            _players = players.ToList();
            foreach (var p in _players)
                p.SetDealer(this);
        }

        public void StartGame()
        {
            // 初始化牌堆
            _deck = CardUtils.CreateDeck();
            ShuffleUtils.RandomShuffle(_deck);

            // 发牌（假设3人，每人17张，剩3张底牌）
            var initialHands = new Dictionary<IPlayer, List<Card>>();
            for (int i = 0; i < _players.Count; i++)
            {
                var hand = _deck.Skip(i * 17).Take(17).OrderByDescending(c => (int)c.Rank).ThenByDescending(c => (int)c.Suit).ToList();
                DealCards(_players[i], hand);
                initialHands[_players[i]] = new List<Card>(hand);
            }

            // 初始化游戏记录
            CurrentGame = new GameRecord
            {
                StartTime = DateTime.Now,
                Players = _players,
                Dealer = this,
                InitialHands = initialHands
            };

            // 通知所有玩家游戏开始
            Broadcast("游戏开始，已发牌！");
            _currentPlayerIndex = 0;
            NotifyCurrentPlayer();
        }

        public void DealCards(IPlayer player, IEnumerable<Card> cards)
        {
            // 通过反射或约定调用 ReceiveCards
            var receiveMethod = player.GetType().GetMethod("ReceiveCards");
            receiveMethod?.Invoke(player, new object[] { cards });
        }

        public bool HandlePlayRequest(IPlayer player, Move move)
        {
            // 判断是否当前玩家
            if (_players[_currentPlayerIndex] != player)
            {
                player.OnMessage(new DealerMessage
                {
                    Type = DealerMessageType.Error,
                    Content = "不是你的回合！"
                });
                return false;
            }

            // 判断出牌是否合法
            if (move != null && move.Cards.Count > 0)
            {
                var lastMove = LastMove.Item2;
                if (lastMove == null || MoveUtils.CanBeat(lastMove, move, Rules) || LastMove.Item1 == player)
                {
                    // 合法出牌
                    LastMove = (player, move, DateTime.Now);
                    RemoveCardsFromHand(player, move.Cards);
                    Broadcast($"{player.Id} 出牌: {string.Join(",", move.Cards.Select(c => c.ToString()))}");
                    Broadcast($"{player.Id} 剩余牌数: {player.GetHandCards().Count}");

                    // 记录本次出牌
                    CurrentGame?.Moves.Add((player, move, DateTime.Now));

                    // 判断是否结束
                    if (player.GetHandCards().Count == 0)
                    {
                        Broadcast($"{player.Id} 已出完所有牌，游戏结束！");
                        CurrentGame.EndTime = DateTime.Now;
                        CalculateScores();
                        LogGameRecord();
                        return true;
                    }

                    // 轮到下一个玩家
                    _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                    NotifyCurrentPlayer();
                    return true;
                }
                else
                {
                    player.OnMessage(new DealerMessage
                    {
                        Type = DealerMessageType.Error,
                        Content = "出牌不合法！"
                    });
                    return false;
                }
            }
            else
            {
                // 选择不出
                Broadcast($"{player.Id} 选择不出牌");
                Broadcast($"{player.Id} 剩余牌数: {player.GetHandCards().Count}");

                // 记录本次操作（pass）
                CurrentGame?.Moves.Add((player, null, DateTime.Now));

                // 轮到下一个玩家
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                NotifyCurrentPlayer();
                return true;
            }
        }

        public void Broadcast(string message)
        {
            foreach (var p in _players)
            {
                p.OnMessage(new DealerMessage
                {
                    Type = DealerMessageType.Info,
                    Content = message,
                });
            }
        }

        public void CalculateScores()
        {
            // 简单实现：赢家得分+1，其他-1
            var winner = LastMove.Item1;
            foreach (var p in _players)
            {
                var coinsProp = p.GetType().GetProperty("Coins");
                if (coinsProp != null)
                {
                    int coins = (int)coinsProp.GetValue(p);
                    if (p == winner)
                        coinsProp.SetValue(p, coins + 1);
                    else
                        coinsProp.SetValue(p, coins - 1);
                }
            }
            Broadcast("本局结算完毕！");
        }

        private void NotifyCurrentPlayer()
        {
            var current = _players[_currentPlayerIndex];
            current.OnMessage(new DealerMessage
            {
                Type = DealerMessageType.RequestPlay,
                Content = "请出牌",
            });
        }

        private void RemoveCardsFromHand(IPlayer player, IEnumerable<Card> cards)
        {
            var hand = player.GetHandCards() as List<Card>;
            if (hand == null)
            {
                var field = player.GetType().GetField("_hand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var handList = field.GetValue(player) as List<Card>;
                    foreach (var card in cards)
                        handList?.Remove(card);
                }
            }
            else
            {
                foreach (var card in cards)
                    hand.Remove(card);
            }
        }

        public int GetCurrentPlayerIndex()
        {
            return _currentPlayerIndex;
        }

        // 记录对局信息到日志
        private void LogGameRecord()
        {
            string json = CurrentGame?.Serialize();
            if (!string.IsNullOrEmpty(json))
            {
                Logger.Instance.Info( "【对局记录】" + json);
                // 可替换为实际logger
            }
        }
    }
}