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
            // ��ʼ���ƶ�
            _deck = CardUtils.CreateDeck();
            ShuffleUtils.RandomShuffle(_deck);

            // ���ƣ�����3�ˣ�ÿ��17�ţ�ʣ3�ŵ��ƣ�
            var initialHands = new Dictionary<IPlayer, List<Card>>();
            for (int i = 0; i < _players.Count; i++)
            {
                var hand = _deck.Skip(i * 17).Take(17).OrderByDescending(c => (int)c.Rank).ThenByDescending(c => (int)c.Suit).ToList();
                DealCards(_players[i], hand);
                initialHands[_players[i]] = new List<Card>(hand);
            }

            // ��ʼ����Ϸ��¼
            CurrentGame = new GameRecord
            {
                StartTime = DateTime.Now,
                Players = _players,
                Dealer = this,
                InitialHands = initialHands
            };

            // ֪ͨ���������Ϸ��ʼ
            Broadcast("��Ϸ��ʼ���ѷ��ƣ�");
            _currentPlayerIndex = 0;
            NotifyCurrentPlayer();
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
                player.OnMessage(new DealerMessage
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
                if (lastMove == null || MoveUtils.CanBeat(lastMove, move, Rules) || LastMove.Item1 == player)
                {
                    // �Ϸ�����
                    LastMove = (player, move, DateTime.Now);
                    RemoveCardsFromHand(player, move.Cards);
                    Broadcast($"{player.Id} ����: {string.Join(",", move.Cards.Select(c => c.ToString()))}");
                    Broadcast($"{player.Id} ʣ������: {player.GetHandCards().Count}");

                    // ��¼���γ���
                    CurrentGame?.Moves.Add((player, move, DateTime.Now));

                    // �ж��Ƿ����
                    if (player.GetHandCards().Count == 0)
                    {
                        Broadcast($"{player.Id} �ѳ��������ƣ���Ϸ������");
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
                    player.OnMessage(new DealerMessage
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
                Broadcast($"{player.Id} ѡ�񲻳���");
                Broadcast($"{player.Id} ʣ������: {player.GetHandCards().Count}");

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
                p.OnMessage(new DealerMessage
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
            current.OnMessage(new DealerMessage
            {
                Type = DealerMessageType.RequestPlay,
                Content = "�����",
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

        // ��¼�Ծ���Ϣ����־
        private void LogGameRecord()
        {
            string json = CurrentGame?.Serialize();
            if (!string.IsNullOrEmpty(json))
            {
                Logger.Instance.Info( "���Ծּ�¼��" + json);
                // ���滻Ϊʵ��logger
            }
        }
    }
}