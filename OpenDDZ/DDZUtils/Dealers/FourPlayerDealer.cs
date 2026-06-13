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
    /// <summary>
    /// 四人斗地主：2v2，牌堆无3/4/5，至少2副牌，后手开局前可弃一张牌。
    /// 队伍：0-2 一队，1-3 一队。
    /// </summary>
    public class FourPlayerDealer : IDealer
    {
        public GameRecord CurrentGame { get; private set; }
        public (IPlayer, Move, DateTime) LastMove { get; private set; }
        public RuleSet Rules { get; private set; }
        public bool EnableTimer => false;

        private List<IPlayer> _players = new List<IPlayer>();
        private int _currentPlayerIndex = 0;
        private List<Card> _deck = new List<Card>();
        private int _scoreTimes = 0;
        private GameConfig _config;
        private int _landlordIndex = -1;

        public FourPlayerDealer(RuleSet rules)
        {
            Rules = rules;
        }

        public IReadOnlyList<IPlayer> Players => _players;
        public int LandlordIndex => _landlordIndex;

        public int GetPlayerIndex(IPlayer player) => _players.IndexOf(player);

        public void RegisterPlayers(IEnumerable<IPlayer> players)
        {
            _players = players.ToList();
            if (_players.Count != 4)
                throw new InvalidOperationException("FourPlayerDealer requires exactly 4 players.");
            foreach (var p in _players)
                p.SetDealer(this);
        }

        public void StartGame(GameConfig config)
        {
            Broadcast("新游戏开始！四人斗地主。");
            _config = config;
            int deckCount = Math.Max(2, config.DeckCount);

            _deck = new List<Card>();
            for (int i = 0; i < deckCount; i++)
                _deck.AddRange(CardUtils.CreateDeckWithout345());
            _config.ShuffleMethod?.Invoke(_deck);

            const int bottomCount = 8;
            int cardsPerPlayer = (_deck.Count - bottomCount) / 4;
            var initialHands = new Dictionary<IPlayer, List<Card>>();
            foreach (var player in _players)
            {
                var hand = _deck.Take(cardsPerPlayer).ToList();
                initialHands[player] = hand;
                DealCards(player, hand);
                _deck.RemoveRange(0, cardsPerPlayer);
            }
            var bottomCards = _deck.Take(bottomCount).ToList();

            config.AfterDeal?.Invoke();
            foreach (var player in _players)
            {
                player.OnDealerMessage(new DealerMessage
                {
                    Type = DealerMessageType.Info,
                    Content = "你的手牌: " + CardUtils.ShowHand(initialHands[player])
                });
            }
            CallLandlord(bottomCards);

            CurrentGame = new GameRecord
            {
                StartTime = DateTime.Now,
                Players = _players,
                Dealer = this,
                InitialHands = initialHands,
                Config = config,
                Landlord = _landlordIndex >= 0 ? _players[_landlordIndex] : null
            };

            RunDiscardPhase();
            Broadcast("游戏开始，已发牌！");
            _currentPlayerIndex = _landlordIndex;
            NotifyCurrentPlayer();
        }

        private void CallLandlord(List<Card> bottomCards)
        {
            int startIndex = new Random(_config.Seed).Next(4);
            int currentIndex = startIndex;
            IPlayer candidate = _players[startIndex];
            int highestBid = 0;
            int cnt = 0;
            string[] callOptions = { "1分", "2分", "3分", "不叫" };

            while (true)
            {
                cnt++;
                if (cnt > 4 || highestBid >= 3)
                {
                    _scoreTimes = Math.Max(1, _scoreTimes);
                    AllocateLandLord(candidate, bottomCards);
                    break;
                }
                var cur = _players[currentIndex++ % 4];
                Broadcast(cur.Name + " 开始叫地主");
                while (true)
                {
                    var result = cur.OnDealerMessage(new DealerMessage
                    {
                        Type = DealerMessageType.RequestCallLandlord,
                        Content = "请选择叫分",
                        Data = new object[] { callOptions, highestBid }
                    });
                    string choice = result.Data as string;
                    if (result.Type != PlayerMessageType.CallLandlord || choice == "不叫")
                    {
                        Broadcast(cur.Name + " 选择不叫");
                        break;
                    }
                    if (choice == "1分" && highestBid >= 1) { cur.OnDealerMessage(new DealerMessage { Type = DealerMessageType.Error, Content = "请选择不叫或更高分" }); continue; }
                    if (choice == "2分" && highestBid >= 2) { cur.OnDealerMessage(new DealerMessage { Type = DealerMessageType.Error, Content = "请选择不叫或更高分" }); continue; }
                    highestBid = choice == "1分" ? 1 : choice == "2分" ? 2 : 3;
                    candidate = cur;
                    Broadcast(cur.Name + " 叫了 " + choice);
                    break;
                }
            }
        }

        private void AllocateLandLord(IPlayer landlord, List<Card> bottomCards)
        {
            _landlordIndex = _players.IndexOf(landlord);
            Broadcast(landlord.Name + " 成为了地主！");
            Broadcast("底牌: " + CardUtils.ShowHand(bottomCards));
            DealCards(landlord, bottomCards);
            landlord.OnDealerMessage(new DealerMessage
            {
                Type = DealerMessageType.Info,
                Content = "你获得了底牌: " + CardUtils.ShowHand(bottomCards)
            });
        }

        private void RunDiscardPhase()
        {
            int[] secondHandIndices = (_landlordIndex == 0 || _landlordIndex == 2) ? new[] { 1, 3 } : new[] { 0, 2 };
            foreach (int i in secondHandIndices)
            {
                var p = _players[i];
                var result = p.OnDealerMessage(new DealerMessage
                {
                    Type = DealerMessageType.RequestDiscard,
                    Content = "后手可弃一张牌或不弃"
                });
                if (result.Type == PlayerMessageType.Discard && result.Data is Card card)
                {
                    RemoveCardsFromHand(p, new[] { card });
                    Broadcast(p.Name + " 弃掉一张牌");
                }
            }
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
                player.OnDealerMessage(new DealerMessage { Type = DealerMessageType.Error, Content = "不是你的回合！" });
                return false;
            }
            if (move != null && move.Cards.Count > 0)
            {
                var lastMove = LastMove.Item2;
                if (LastMove.Item1 == player) lastMove = null;
                if (MoveUtils.CanBeat(lastMove, move, Rules))
                {
                    LastMove = (player, move, DateTime.Now);
                    RemoveCardsFromHand(player, move.Cards);
                    Broadcast(player.Name + " 出牌: " + string.Join(",", move.Cards.Select(c => c.ToString())));
                    Broadcast(player.Name + " 剩余: " + player.GetHandCards().Count);
                    CurrentGame?.Moves.Add((player, move, DateTime.Now));
                    if (player.GetHandCards().Count == 0)
                    {
                        Broadcast(player.Name + " 出完，游戏结束！");
                        CurrentGame.EndTime = DateTime.Now;
                        CalculateScores();
                        return true;
                    }
                    _currentPlayerIndex = (_currentPlayerIndex + 1) % 4;
                    NotifyCurrentPlayer();
                    return true;
                }
                player.OnDealerMessage(new DealerMessage { Type = DealerMessageType.Error, Content = "出牌不合法！" });
                return false;
            }
            Broadcast(player.Name + " 选择不出");
            CurrentGame?.Moves.Add((player, null, DateTime.Now));
            _currentPlayerIndex = (_currentPlayerIndex + 1) % 4;
            NotifyCurrentPlayer();
            return true;
        }

        public void Broadcast(string message)
        {
            foreach (var p in _players)
                p.OnDealerMessage(new DealerMessage { Type = DealerMessageType.Info, Content = message });
        }

        public void CalculateScores()
        {
            var winner = LastMove.Item1;
            int winnerIdx = _players.IndexOf(winner);
            bool team0Wins = (winnerIdx == 0 || winnerIdx == 2);
            foreach (var p in _players)
            {
                var coinsProp = p.GetType().GetProperty("Coins");
                if (coinsProp != null)
                {
                    int idx = _players.IndexOf(p);
                    bool onTeam0 = (idx == 0 || idx == 2);
                    int coins = (int)coinsProp.GetValue(p);
                    coinsProp.SetValue(p, coins + (team0Wins == onTeam0 ? 1 : -1));
                }
            }
            Broadcast("本局结束！");
        }

        /// <summary> No-op: 出牌由 GameController 驱动，避免全 Bot 时同步链导致 StackOverflow。 </summary>
        private void NotifyCurrentPlayer()
        {
        }

        private void RemoveCardsFromHand(IPlayer player, IEnumerable<Card> cards)
        {
            var hand = player.GetHandCards() as List<Card>;
            foreach (var card in cards)
            {
                var toRemove = hand.FirstOrDefault(c => c.Suit == card.Suit && c.Rank == card.Rank);
                if (toRemove != null) hand.Remove(toRemove);
                else throw new Exception("手牌中不存在该牌");
            }
        }

        public int GetCurrentPlayerIndex() => _currentPlayerIndex;

        public void OnPlayerMessage(IPlayer player, PlayerMessage message)
        {
            if (message.Type == PlayerMessageType.Play)
                HandlePlayRequest(player, message.Data as Move);
            else if (message.Type == PlayerMessageType.Pass)
                HandlePlayRequest(player, null);
        }
    }
}
