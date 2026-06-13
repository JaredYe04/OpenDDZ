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

    /// 四人斗地主：2v2，牌堆无3/4/5，至少2副牌。

    /// 相邻组队（随机 0+1 vs 2+3 或 3+0 vs 1+2）；

    /// 先手及下一家不弃牌，第三、第四顺位可弃一张或不弃。

    /// </summary>

    public class FourPlayerDealer : IDealer, IFourPlayerTeamInfo

    {

        public GameRecord CurrentGame { get; private set; }

        public (IPlayer, Move, DateTime) LastMove { get; private set; }

        public RuleSet Rules { get; private set; }

        public bool EnableTimer => false;



        private List<IPlayer> _players = new List<IPlayer>();

        private int _currentPlayerIndex = 0;

        private List<Card> _deck = new List<Card>();

        private GameConfig _config;

        private int _firstPlayerIndex;

        private int[] _teamIds = new int[4];



        public FourPlayerDealer(RuleSet rules)

        {

            Rules = rules;

        }



        public IReadOnlyList<IPlayer> Players => _players;

        public int LandlordIndex => -1;



        public int GetPlayerIndex(IPlayer player) => _players.IndexOf(player);



        public int GetTeamId(int seat) => _teamIds[seat];



        public int GetTeammateIndex(int seat) => FourPlayerTeamHelper.GetTeammateIndex(seat, _teamIds);



        public bool SameTeam(int a, int b) => _teamIds[a] == _teamIds[b];



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



            FourPlayerTeamHelper.AssignTeams(_config.Seed, out _teamIds, out _);



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



            _firstPlayerIndex = new Random(_config.Seed).Next(4);

            Broadcast(_players[_firstPlayerIndex].Name + " 先手");



            CurrentGame = new GameRecord

            {

                StartTime = DateTime.Now,

                Players = _players,

                Dealer = this,

                InitialHands = initialHands,

                Config = config,

                Landlord = null

            };



            RunDiscardPhase();

            Broadcast("游戏开始，已发牌！");

            _currentPlayerIndex = _firstPlayerIndex;

            NotifyCurrentPlayer();

        }



        private void RunDiscardPhase()

        {

            for (int offset = 2; offset <= 3; offset++)

            {

                int i = (_firstPlayerIndex + offset) % 4;

                var p = _players[i];

                var result = p.OnDealerMessage(new DealerMessage

                {

                    Type = DealerMessageType.RequestDiscard,

                    Content = "第三、第四顺位可弃一张牌或不弃"

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

            int winnerTeam = _teamIds[winnerIdx];

            foreach (var p in _players)

            {

                var coinsProp = p.GetType().GetProperty("Coins");

                if (coinsProp != null)

                {

                    int idx = _players.IndexOf(p);

                    bool onWinnerTeam = _teamIds[idx] == winnerTeam;

                    int coins = (int)coinsProp.GetValue(p);

                    coinsProp.SetValue(p, coins + (onWinnerTeam ? 1 : -1));

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



