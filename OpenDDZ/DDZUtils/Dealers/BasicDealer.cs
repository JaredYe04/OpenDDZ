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
        private IPlayer landLord = null; // �������
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
            Broadcast("����Ϸ��ʼ��");
            // ��ʼ���ƶ�
            _config = config;

            _deck = CardUtils.CreateDeck();
            int deckCount = config.DeckCount;
            while (deckCount > 1)
            {
                _deck.AddRange(CardUtils.CreateDeck());
                deckCount--;
            }
            _config.ShuffleMethod?.Invoke(_deck);

            // ���ƣ������n���ƣ�����3n�ŵ���
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
                //�ڽе���֮ǰ���ȸ�ÿ����ҿ��Լ�������
                foreach (var player in _players)
                {
                    player.OnDealerMessage(new DealerMessage
                    {
                        Type = DealerMessageType.Info,
                        Content = $"���������: {CardUtils.ShowHand(initialHands[player])}"
                    });
                }
                CallLandlord(bottomCards);
            }
            else
            {
                //������е������Ͱѵ���ƽ������
                int idx = 0;
                foreach (var player in _players)
                {
                    var hand = initialHands[player];
                    var cardsToAdd = bottomCards.Skip(idx * 3 * config.DeckCount).Take(3 * config.DeckCount).ToList();
                    hand.AddRange(cardsToAdd);
                    idx++;
                }
            }

            // ��ʼ����Ϸ��¼
            CurrentGame = new GameRecord
            {
                StartTime = DateTime.Now,
                Players = _players,
                Dealer = this,
                InitialHands = initialHands,
                Config = config
            };

            // ֪ͨ���������Ϸ��ʼ
            Broadcast("��Ϸ��ʼ���ѷ��ƣ�");
            _currentPlayerIndex = 0;
            NotifyCurrentPlayer();
        }
        private void CallLandlord(List<Card> bottomCards)
        {
            // �����ַ�ʽȷ��������һ��ֱ�ӽ�3�ֵĵ��������������ҵ��У��з�����ߵ��������������Ҷ����е��������һλ˵����Ϊ������

            //���ƣ�һ����54�ţ�һ��17�ţ���3�������ƣ���ȷ������֮ǰ��Ҳ��ܿ�����4��
            //���ƣ����ư����Ƶ�˳���������У�ÿ��ֻ�ܽ�һ�Ρ�����ʱ���ԽС�1�֡�����2�֡�����3�֡��������С����������ֻ�ܽб�ǰ����Ҹߵķֻ��߲��С�
            //ȷ�������������ַ�ʽȷ��������һ��ֱ�ӽ�3�ֵĵ��������������ҵ��У��з�����ߵ��������������Ҷ����е��������һλ˵����Ϊ������
            //������Ȩ���������ߵ��ƣ����ҵ����ȳ��ơ�

            //���ѡȡһ�������Ϊ��ʼ��ң����не���
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
                    //�����һ�������Ϊ����
                }
                curentPlayer = _players[currentIndex++ % _players.Count];//���������ת
                Broadcast($"{curentPlayer.Name} ��ʼ�е���");
                string[] callOptions = { "1��", "2��", "3��", "����" };

                while (true)
                {
                    var result = curentPlayer.OnDealerMessage(new DealerMessage
                    {
                        Type = DealerMessageType.RequestCallLandlord,
                        Content = "��ѡ��з�",
                        Data = callOptions
                    });
                    string choice = result.Data as string;
                    //����зֽ���������3�֣�ֱ�ӷ�������������2�֣���¼��߷֣������1�֣���¼��߷֣�������У�������һ�����
                    if (result.Type != PlayerMessageType.CallLandlord || result.Data == null || choice == "����")
                    {
                        //���û����ȷ��Ӧ�е���������Ϊ����
                        Broadcast($"{curentPlayer.Name} ѡ�񲻽�");
                        break;
                    }
                    else if (choice == "1��")
                    {
                        if (highestBid >= 1)
                        {
                            //��Ч�з�
                            curentPlayer.OnDealerMessage(new DealerMessage
                            {
                                Type = DealerMessageType.Error,
                                Content = $"�кŲ����Ϲ�����ѡ�񲻽У����߽б�{highestBid}�ߵķ�"
                            });
                            continue;
                        }

                        highestBid = 1;
                        candidate = curentPlayer;
                        _scoreTimes += highestBid;
                        Broadcast($"���{curentPlayer.Name}����{choice}����߷�Ϊ{highestBid}");
                        break;
                    }
                    else if (choice == "2��")
                    {
                        if (highestBid >= 2)
                        {
                            //��Ч�з�
                            curentPlayer.OnDealerMessage(new DealerMessage
                            {
                                Type = DealerMessageType.Error,
                                Content = $"�кŲ����Ϲ�����ѡ�񲻽У����߽б�{highestBid}�ߵķ�"
                            });
                            continue;
                        }
                        highestBid = 2;
                        candidate = curentPlayer;
                        _scoreTimes += highestBid;
                        Broadcast($"���{curentPlayer.Name}����{choice}����߷�Ϊ{highestBid}");
                        break;
                    }
                    else if (choice == "3��")
                    {


                        highestBid = 3;
                        candidate = curentPlayer;
                        _scoreTimes += highestBid;
                        Broadcast($"���{curentPlayer.Name}����{choice}����߷�Ϊ{highestBid}");
                        break;
                    }
                    //ֻ��ѡ�񲻽У������Ǳ�highestBid�ߵķ֣��������·�
                }
            }
        }
        private void AllocateLandLord(IPlayer landlord, IEnumerable<Card> bottomCards)
        {
            Broadcast($"{landlord.Name} ��Ϊ�˵�����");
            Broadcast($"����չʾ��{CardUtils.ShowHand(bottomCards)}");
            //�ѵ��Ʒ�������
            var hand = landlord.GetHandCards() as List<Card>;
            hand.AddRange(bottomCards);
            landlord.OnDealerMessage(new DealerMessage
            {
                Type = DealerMessageType.Info,
                Content = $"�����˵���: {CardUtils.ShowHand(bottomCards)}"
            });

        }
        public void DealCards(IPlayer player, IEnumerable<Card> cards)
        {
            // ͨ�������Լ������ ReceiveCards
            var receiveMethod = player.GetType().GetMethod("ReceiveCards");
            receiveMethod?.Invoke(player, new object[] { cards });
        }

        public bool HandlePlayRequest(IPlayer player, Move move)
        {
            // �ж��Ƿ�ǰ���
            if (_players[_currentPlayerIndex] != player)
            {
                player.OnDealerMessage(new DealerMessage
                {
                    Type = DealerMessageType.Error,
                    Content = "������Ļغϣ�"
                });
                return false;
            }

            // �жϳ����Ƿ�Ϸ�
            if (move != null && move.Cards.Count > 0)
            {
                var lastMove = LastMove.Item2;
                if (LastMove.Item1 == player)
                {
                    //������
                    lastMove = null;
                }
                if (MoveUtils.CanBeat(lastMove, move, Rules))
                {
                    // �Ϸ�����
                    LastMove = (player, move, DateTime.Now);
                    RemoveCardsFromHand(player, move.Cards);
                    Broadcast($"{player.Name} ����: {string.Join(",", move.Cards.Select(c => c.ToString()))}");
                    Broadcast($"{player.Name} ʣ������: {player.GetHandCards().Count}");

                    // ��¼���γ���
                    CurrentGame?.Moves.Add((player, move, DateTime.Now));

                    // �ж��Ƿ����
                    if (player.GetHandCards().Count == 0)
                    {
                        Broadcast($"{player.Name} �ѳ��������ƣ���Ϸ������");
                        CurrentGame.EndTime = DateTime.Now;
                        CalculateScores();
                        LogGameRecord();
                        return true;
                    }

                    // �ֵ���һ�����
                    _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                    NotifyCurrentPlayer();
                    return true;
                }
                else
                {
                    player.OnDealerMessage(new DealerMessage
                    {
                        Type = DealerMessageType.Error,
                        Content = "���Ʋ��Ϸ���"
                    });
                    return false;
                }
            }
            else
            {
                // ѡ�񲻳�
                Broadcast($"{player.Name} ѡ�񲻳���");
                Broadcast($"{player.Name} ʣ������: {player.GetHandCards().Count}");

                // ��¼���β�����pass��
                CurrentGame?.Moves.Add((player, null, DateTime.Now));

                // �ֵ���һ�����
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
            // ��ʵ�֣�Ӯ�ҵ÷�+1������-1
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
            Broadcast("���ֽ�����ϣ�");
        }

        private void NotifyCurrentPlayer()
        {
            var current = _players[_currentPlayerIndex];
            current.OnDealerMessage(new DealerMessage
            {
                Type = DealerMessageType.RequestPlay,
                Content = "�����",
            });
        }

        private void RemoveCardsFromHand(IPlayer player, IEnumerable<Card> cards)
        {
            var hand = player.GetHandCards() as List<Card>;
            foreach (var card in cards)
            {
                //����card���´����Ķ���������Ҫ���ݻ�ɫ�͵������Ƴ�
                var toRemove = hand.FirstOrDefault(c => c.Suit == card.Suit && c.Rank == card.Rank);
                if (toRemove != null)
                    hand.Remove(toRemove);
                else
                {
                    throw new Exception("��ͼ�Ƴ�������в����ڵ��ƣ�");
                }
            }
        }

        public int GetCurrentPlayerIndex()
        {
            return _currentPlayerIndex;
        }

        // ��¼�Ծ���Ϣ����־
        private void LogGameRecord()
        {
            string json = CurrentGame?.Serialize();
            if (!string.IsNullOrEmpty(json))
            {
                Logger.Instance.Info("���Ծֽ�����");
                Recorder.Instance.Record(json);
            }
        }

        public void OnPlayerMessage(IPlayer player, PlayerMessage message)
        {
            // �¼�������ƣ�����PlayerMessageType����
            // ���磺���ơ��з֡�pass��
            if (message.Type == PlayerMessageType.Play)
            {
                var move = message.Data as Move;
                HandlePlayRequest(player, move);
            }
            else if (message.Type == PlayerMessageType.Pass)
            {
                HandlePlayRequest(player, null);
            }
            // ��������ͬ��
        }
    }
}