using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players.Strategies;
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
        private readonly IBotStrategy _strategy;

        public IBotStrategy Strategy => _strategy;

        public BotPlayer() : this(new GreedyBotStrategy()) { }

        public BotPlayer(string name) : this(name, new GreedyBotStrategy()) { }

        public BotPlayer(IBotStrategy strategy)
        {
            Id = Guid.NewGuid().ToString();
            _strategy = strategy ?? new GreedyBotStrategy();
        }

        public BotPlayer(string name, IBotStrategy strategy)
        {
            Id = Guid.NewGuid().ToString();
            Name = name;
            _strategy = strategy ?? new GreedyBotStrategy();
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
            var ctx = BotDecisionContext.From(this, _dealer);

            if (message.Type == DealerMessageType.RequestPlay)
            {
                var myMove = _strategy.ChoosePlay(ctx);
                RequestPlay(myMove);
            }
            else if (message.Type == DealerMessageType.Info)
            {
            }
            else if (message.Type == DealerMessageType.RequestCallLandlord)
            {
                string[] options;
                int highestBid = 0;
                if (message.Data is object[] arr && arr.Length >= 2)
                {
                    options = arr[0] as string[];
                    if (arr[1] is int hb) highestBid = hb;
                }
                else
                {
                    options = message.Data as string[];
                }
                ctx.HighestBid = highestBid;
                ctx.BidOptions = options;
                var choice = _strategy.ChooseBid(ctx, options);
                return new PlayerMessage
                {
                    Type = PlayerMessageType.CallLandlord,
                    Data = choice
                };
            }
            else if (message.Type == DealerMessageType.RequestDiscard)
            {
                var card = _strategy.ChooseDiscard(ctx);
                return new PlayerMessage { Type = PlayerMessageType.Discard, Data = card };
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

        public void ResetHand()
        {
            _hand.Clear();
        }
    }
}
