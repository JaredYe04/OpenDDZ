using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.Players
{
    [Serializable]
    public class BotPlayer : IPlayer
    {

        public string Id { get; set; }
        public string Name { get; set; } = "Bot";
        private List<Card> _hand = new List<Card>();
        private IDealer _dealer;
        private RuleSet _rules=RuleSet.Default;

        public BotPlayer()
        {
            Id = Guid.NewGuid().ToString();
        }
        public BotPlayer(string name)
        {
            Id = Guid.NewGuid().ToString();
            Name = name;
        }

        public IList<Card> GetHandCards() => _hand;

        public void RequestPlay(Move move)
        {
            _dealer.OnPlayerMessage(this, new PlayerMessage
            {
                Type = PlayerMessageType.Play,
                Data = move
            });
        }

        public PlayerMessage OnDealerMessage(DealerMessage message)
        {
            if (message.Type == DealerMessageType.RequestPlay)
            {
                // 自动出牌
                var lastMove = _dealer.LastMove.Item2;
                if (_dealer.LastMove.Item1==this)
                {
                    //说明没人能接自己的牌，出任意牌
                    lastMove = null;
                }
                var myMove = CardUtils.FindGreedyBestMove(_hand,lastMove,_rules);
                RequestPlay(myMove);
                
                //Console.WriteLine($"[{Id}] 自动出牌: {(myMove == null || myMove.Cards.Count == 0 ? "pass" : CardUtils.FormatCards(myMove.Cards))}");
            }
            else if (message.Type == DealerMessageType.Info)
            {
                
                //Console.WriteLine($"[{Id}] {message.Content}");
            }
            else if (message.Type == DealerMessageType.RequestCallLandlord)
            {
                var options = (string[])message.Data;
                //作为AI，这里简单随机叫地主,todo
                var rand = new Random((int)DateTime.Now.Ticks);
                var choice = options[rand.Next(options.Length)];
                return new PlayerMessage
                {
                    Type = PlayerMessageType.CallLandlord,
                    Data = choice
                };
            }

            return new PlayerMessage { Type = PlayerMessageType.Ack };
        }

        public void SetDealer(IDealer dealer)
        {
            _dealer = dealer;
        }

        public void ReceiveCards(IEnumerable<Card> cards)
        {
            _hand.AddRange(cards);
        }


    }
}