using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Enums;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenDDZ.Utils;

namespace OpenDDZ.DDZUtils.Dealers
{
    public class BasicDealer : IDealer
    {
        private static readonly string[] CallOptions = { "1分", "2分", "3分", "不叫" };
        private const string PassChoice = "不叫";

        public GameRecord CurrentGame { get; private set; }
        public (IPlayer, Move, DateTime) LastMove { get; private set; }
        public RuleSet Rules { get; private set; }
        public bool EnableTimer => false;

        private List<IPlayer> _players = new List<IPlayer>();
        private IPlayer landLord = null;
        private int _landlordIndex = -1;
        private int _currentPlayerIndex = 0;
        private List<Card> _deck = new List<Card>();
        private int _scoreTimes = 0;
        private GameConfig _config;

        public BasicDealer(RuleSet rules)
        {
            Rules = rules;
        }

        public IReadOnlyList<IPlayer> Players => _players;
        public int LandlordIndex => _landlordIndex;

        public int GetPlayerIndex(IPlayer player) => _players.IndexOf(player);

        public void RegisterPlayers(IEnumerable<IPlayer> players)
        {
            _players = players.ToList();
            foreach (var p in _players)
                p.SetDealer(this);
        }

        public void StartGame(GameConfig config)
        {
            Broadcast("游戏开始");
            _config = config;

            _deck = CardUtils.CreateDeck();
            int deckCount = config.DeckCount;
            while (deckCount > 1)
            {
                _deck.AddRange(CardUtils.CreateDeck());
                deckCount--;
            }
            _config.ShuffleMethod?.Invoke(_deck);

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
            config.AfterDeal?.Invoke();

            if (config.EnableLandlord)
            {
                foreach (var player in _players)
                {
                    player.OnDealerMessage(new DealerMessage
                    {
                        Type = DealerMessageType.Info,
                        Content = $"初始手牌: {CardUtils.ShowHand(initialHands[player])}"
                    });
                }
                CallLandlord(bottomCards);
            }
            else
            {
                int idx = 0;
                foreach (var player in _players)
                {
                    var hand = initialHands[player];
                    var cardsToAdd = bottomCards.Skip(idx * 3 * config.DeckCount).Take(3 * config.DeckCount).ToList();
                    hand.AddRange(cardsToAdd);
                    idx++;
                }
            }

            CurrentGame = new GameRecord
            {
                StartTime = DateTime.Now,
                Players = _players,
                Dealer = this,
                InitialHands = initialHands,
                Config = config,
                Landlord = _landlordIndex >= 0 ? _players[_landlordIndex] : null
            };

            Broadcast("出牌阶段开始");
            _currentPlayerIndex = _landlordIndex >= 0 ? _landlordIndex : 0;
            NotifyCurrentPlayer();
        }

        private void CallLandlord(List<Card> bottomCards)
        {
            Broadcast("叫地主阶段开始");
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
                }

                curentPlayer = _players[currentIndex++ % _players.Count];
                Broadcast($"{curentPlayer.Name} 叫地主中");

                while (true)
                {
                    var result = curentPlayer.OnDealerMessage(new DealerMessage
                    {
                        Type = DealerMessageType.RequestCallLandlord,
                        Content = "请叫地主",
                        Data = new object[] { CallOptions, highestBid }
                    });
                    string choice = result.Data as string;

                    if (result.Type != PlayerMessageType.CallLandlord || result.Data == null || choice == PassChoice)
                    {
                        Broadcast($"{curentPlayer.Name} 选择不叫");
                        break;
                    }

                    int bid = ParseBid(choice);
                    if (bid <= 0)
                    {
                        Broadcast($"{curentPlayer.Name} 选择不叫");
                        break;
                    }

                    if (bid <= highestBid)
                    {
                        curentPlayer.OnDealerMessage(new DealerMessage
                        {
                            Type = DealerMessageType.Error,
                            Content = $"叫分必须高于当前最高分 {highestBid} 分"
                        });
                        continue;
                    }

                    highestBid = bid;
                    candidate = curentPlayer;
                    _scoreTimes += highestBid;
                    Broadcast($"{curentPlayer.Name} 叫 {choice}，当前最高 {highestBid} 分");
                    break;
                }
            }
        }

        private static int ParseBid(string choice)
        {
            if (string.IsNullOrEmpty(choice) || choice == PassChoice) return 0;
            if (choice.StartsWith("1")) return 1;
            if (choice.StartsWith("2")) return 2;
            if (choice.StartsWith("3")) return 3;
            return 0;
        }

        private void AllocateLandLord(IPlayer landlord, IEnumerable<Card> bottomCards)
        {
            landLord = landlord;
            _landlordIndex = _players.IndexOf(landlord);
            Broadcast($"{landlord.Name} 成为地主");
            Broadcast($"底牌: {CardUtils.ShowHand(bottomCards)}");

            var bottomList = bottomCards.ToList();
            DealCards(landlord, bottomList);
            landlord.OnDealerMessage(new DealerMessage
            {
                Type = DealerMessageType.Info,
                Content = $"获得底牌: {CardUtils.ShowHand(bottomCards)}"
            });
        }

        public void DealCards(IPlayer player, IEnumerable<Card> cards)
        {
            var receiveMethod = player.GetType().GetMethod("ReceiveCards");
            receiveMethod?.Invoke(player, new object[] { cards });
        }

        public bool HandlePlayRequest(IPlayer player, Move move)
        {
            if (_players[_currentPlayerIndex] != player)
            {
                player.OnDealerMessage(new DealerMessage
                {
                    Type = DealerMessageType.Error,
                    Content = "不是你的回合"
                });
                return false;
            }

            if (move != null && move.Cards.Count > 0)
            {
                var lastMove = LastMove.Item2;
                if (LastMove.Item1 == player)
                    lastMove = null;

                if (MoveUtils.CanBeat(lastMove, move, Rules))
                {
                    LastMove = (player, move, DateTime.Now);
                    RemoveCardsFromHand(player, move.Cards);
                    Broadcast($"{player.Name} 出牌: {string.Join(",", move.Cards.Select(c => c.ToString()))}");
                    Broadcast($"{player.Name} 剩余: {player.GetHandCards().Count}");

                    CurrentGame?.Moves.Add((player, move, DateTime.Now));

                    if (player.GetHandCards().Count == 0)
                    {
                        Broadcast($"{player.Name} 出完牌，获胜！");
                        CurrentGame.EndTime = DateTime.Now;
                        CalculateScores();
                        LogGameRecord();
                        return true;
                    }

                    _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                    NotifyCurrentPlayer();
                    return true;
                }

                player.OnDealerMessage(new DealerMessage
                {
                    Type = DealerMessageType.Error,
                    Content = "出牌不符合规则，无法压过上家"
                });
                return false;
            }

            Broadcast($"{player.Name} 不出");
            Broadcast($"{player.Name} 剩余: {player.GetHandCards().Count}");
            CurrentGame?.Moves.Add((player, null, DateTime.Now));
            _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
            NotifyCurrentPlayer();
            return true;
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
            Broadcast("本局结束");
        }

        private void NotifyCurrentPlayer()
        {
        }

        private void RemoveCardsFromHand(IPlayer player, IEnumerable<Card> cards)
        {
            var hand = player.GetHandCards() as List<Card>;
            foreach (var card in cards)
            {
                var toRemove = hand.FirstOrDefault(c => c.Suit == card.Suit && c.Rank == card.Rank);
                if (toRemove != null)
                    hand.Remove(toRemove);
                else
                    throw new Exception("出牌不在手牌中");
            }
        }

        public int GetCurrentPlayerIndex()
        {
            return _currentPlayerIndex;
        }

        private void LogGameRecord()
        {
            string json = CurrentGame?.Serialize();
            if (!string.IsNullOrEmpty(json))
            {
                Logger.Instance.Info("记录对局");
                Recorder.Instance.Record(json);
            }
        }

        public void OnPlayerMessage(IPlayer player, PlayerMessage message)
        {
            if (message.Type == PlayerMessageType.Play)
            {
                var move = message.Data as Move;
                HandlePlayRequest(player, move);
            }
            else if (message.Type == PlayerMessageType.Pass)
            {
                HandlePlayRequest(player, null);
            }
        }
    }
}
