using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenDDZ.Utils;
using System.Drawing.Text;

namespace OpenDDZ.DDZUtils.Dealers
{
    public class BasicDealer : IDealer
    {
        public GameRecord CurrentGame { get; private set; }
        public (IPlayer, Move, DateTime) LastMove { get; private set; }
        public RuleSet Rules { get; private set; }
        public bool EnableTimer => false;

        private List<IPlayer> _players = new List<IPlayer>();
        private IPlayer landLord = null; // 地主玩家
        private int _currentPlayerIndex = 0;
        private List<Card> _deck = new List<Card>();
        private int _scoreTimes = 0;
        private GameConfig _config;
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


        public void StartGame(GameConfig config)
        {
            Broadcast("新游戏开始！");
            // 初始化牌堆
            _config = config;

            _deck = CardUtils.CreateDeck();
            int deckCount = config.DeckCount;
            while (deckCount > 1)
            {
                _deck.AddRange(CardUtils.CreateDeck());
                deckCount--;
            }
            _config.ShuffleMethod?.Invoke(_deck);

            // 发牌，如果是n副牌，则留3n张底牌
            int cardsPerPlayer = (_deck.Count - 3 * config.DeckCount) / _players.Count;
            var initialHands = new Dictionary<IPlayer, List<Card>>();
            foreach (var player in _players)
            {
                var hand = _deck.Take(cardsPerPlayer).ToList();
                initialHands[player] = hand;
                DealCards(player, hand);
                _deck.RemoveRange(0, cardsPerPlayer);
            }
            var bottomCards = _deck.Take(3 * config.DeckCount).ToList();
            if (config.EnableLandlord)
            {
                //在叫地主之前，先给每个玩家看自己的手牌
                foreach (var player in _players)
                {
                    player.OnDealerMessage(new DealerMessage
                    {
                        Type = DealerMessageType.Info,
                        Content = $"你的手牌是: {CardUtils.ShowHand(initialHands[player])}"
                    });
                }
                CallLandlord(bottomCards);
            }
            else
            {
                //如果不叫地主，就把底牌平均发完
                int idx = 0;
                foreach (var player in _players)
                {
                    var hand = initialHands[player];
                    var cardsToAdd = bottomCards.Skip(idx * 3 * config.DeckCount).Take(3 * config.DeckCount).ToList();
                    hand.AddRange(cardsToAdd);
                    idx++;
                }
            }

            // 初始化游戏记录
            CurrentGame = new GameRecord
            {
                StartTime = DateTime.Now,
                Players = _players,
                Dealer = this,
                InitialHands = initialHands,
                Config = config
            };

            // 通知所有玩家游戏开始
            Broadcast("游戏开始，已发牌！");
            _currentPlayerIndex = 0;
            NotifyCurrentPlayer();
        }
        private void CallLandlord(List<Card> bottomCards)
        {
            // 有三种方式确定地主：一是直接叫3分的当地主，二是三家当中，叫分最高者当地主；三是三家都不叫地主，则第一位说话者为地主。

            //发牌：一副牌54张，一人17张，留3张做底牌，在确定地主之前玩家不能看底牌4。
            //叫牌：叫牌按出牌的顺序轮流进行，每人只能叫一次。叫牌时可以叫“1分”，“2分”，“3分”，“不叫”。后叫牌者只能叫比前面玩家高的分或者不叫。
            //确定地主：有三种方式确定地主：一是直接叫3分的当地主，二是三家当中，叫分最高者当地主；三是三家都不叫地主，则第一位说话者为地主。
            //地主特权：地主拿走底牌，并且地主先出牌。

            //随机选取一个玩家作为开始玩家，进行叫地主
            int startIndex = new Random(_config.Seed).Next(_players.Count);
            int currentIndex = startIndex;
            IPlayer curentPlayer;
            IPlayer candidate = _players[startIndex];
            int highestBid = 0;
            int cnt = 0;
            while (true)
            {
                ++cnt;
                if (cnt > _players.Count || highestBid >= 3)
                {
                    _scoreTimes = Math.Max(1, _scoreTimes);
                    AllocateLandLord(candidate, bottomCards);
                    break;
                    //分配第一个玩家作为地主
                }
                curentPlayer = _players[currentIndex++ % _players.Count];//从玩家中轮转
                Broadcast($"{curentPlayer.Name} 开始叫地主");
                string[] callOptions = { "1分", "2分", "3分", "不叫" };

                while (true)
                {
                    var result = curentPlayer.OnDealerMessage(new DealerMessage
                    {
                        Type = DealerMessageType.RequestCallLandlord,
                        Content = "请选择叫分",
                        Data = callOptions
                    });
                    string choice = result.Data as string;
                    //处理叫分结果，如果叫3分，直接分配地主，如果叫2分，记录最高分，如果叫1分，记录最高分，如果不叫，继续下一个玩家
                    if (result.Type != PlayerMessageType.CallLandlord || result.Data == null || choice == "不叫")
                    {
                        //玩家没有正确响应叫地主请求，视为不叫
                        Broadcast($"{curentPlayer.Name} 选择不叫");
                        break;
                    }
                    else if (choice == "1分")
                    {
                        if (highestBid >= 1)
                        {
                            //无效叫分
                            curentPlayer.OnDealerMessage(new DealerMessage
                            {
                                Type = DealerMessageType.Error,
                                Content = $"叫号不符合规则，请选择不叫，或者叫比{highestBid}高的分"
                            });
                            continue;
                        }

                        highestBid = 1;
                        candidate = curentPlayer;
                        _scoreTimes += highestBid;
                        Broadcast($"玩家{curentPlayer.Name}叫了{choice}，最高分为{highestBid}");
                        break;
                    }
                    else if (choice == "2分")
                    {
                        if (highestBid >= 2)
                        {
                            //无效叫分
                            curentPlayer.OnDealerMessage(new DealerMessage
                            {
                                Type = DealerMessageType.Error,
                                Content = $"叫号不符合规则，请选择不叫，或者叫比{highestBid}高的分"
                            });
                            continue;
                        }
                        highestBid = 2;
                        candidate = curentPlayer;
                        _scoreTimes += highestBid;
                        Broadcast($"玩家{curentPlayer.Name}叫了{choice}，最高分为{highestBid}");
                        break;
                    }
                    else if (choice == "3分")
                    {


                        highestBid = 3;
                        candidate = curentPlayer;
                        _scoreTimes += highestBid;
                        Broadcast($"玩家{curentPlayer.Name}叫了{choice}，最高分为{highestBid}");
                        break;
                    }
                    //只能选择不叫，或者是比highestBid高的分，否则重新发
                }
            }
        }
        private void AllocateLandLord(IPlayer landlord, IEnumerable<Card> bottomCards)
        {
            Broadcast($"{landlord.Name} 成为了地主！");
            Broadcast($"底牌展示：{CardUtils.ShowHand(bottomCards)}");
            //把底牌发给地主
            var hand = landlord.GetHandCards() as List<Card>;
            hand.AddRange(bottomCards);
            landlord.OnDealerMessage(new DealerMessage
            {
                Type = DealerMessageType.Info,
                Content = $"你获得了底牌: {CardUtils.ShowHand(bottomCards)}"
            });

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
                player.OnDealerMessage(new DealerMessage
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
                if (LastMove.Item1 == player)
                {
                    //任意牌
                    lastMove = null;
                }
                if (MoveUtils.CanBeat(lastMove, move, Rules))
                {
                    // 合法出牌
                    LastMove = (player, move, DateTime.Now);
                    RemoveCardsFromHand(player, move.Cards);
                    Broadcast($"{player.Name} 出牌: {string.Join(",", move.Cards.Select(c => c.ToString()))}");
                    Broadcast($"{player.Name} 剩余牌数: {player.GetHandCards().Count}");

                    // 记录本次出牌
                    CurrentGame?.Moves.Add((player, move, DateTime.Now));

                    // 判断是否结束
                    if (player.GetHandCards().Count == 0)
                    {
                        Broadcast($"{player.Name} 已出完所有牌，游戏结束！");
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
                    player.OnDealerMessage(new DealerMessage
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
                Broadcast($"{player.Name} 选择不出牌");
                Broadcast($"{player.Name} 剩余牌数: {player.GetHandCards().Count}");

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
                p.OnDealerMessage(new DealerMessage
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
            current.OnDealerMessage(new DealerMessage
            {
                Type = DealerMessageType.RequestPlay,
                Content = "请出牌",
            });
        }

        private void RemoveCardsFromHand(IPlayer player, IEnumerable<Card> cards)
        {
            var hand = player.GetHandCards() as List<Card>;
            foreach (var card in cards)
            {
                //由于card是新创建的对象，所以需要根据花色和点数来移除
                var toRemove = hand.FirstOrDefault(c => c.Suit == card.Suit && c.Rank == card.Rank);
                if (toRemove != null)
                    hand.Remove(toRemove);
                else
                {
                    throw new Exception("试图移除玩家手中不存在的牌！");
                }
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
                Logger.Instance.Info("【对局结束】");
                Recorder.Instance.Record(json);
            }
        }

        public void OnPlayerMessage(IPlayer player, PlayerMessage message)
        {
            // 事件处理机制，根据PlayerMessageType处理
            // 例如：出牌、叫分、pass等
            if (message.Type == PlayerMessageType.Play)
            {
                var move = message.Data as Move;
                HandlePlayRequest(player, move);
            }
            else if (message.Type == PlayerMessageType.Pass)
            {
                HandlePlayRequest(player, null);
            }
            // 其他类型同理
        }
    }
}