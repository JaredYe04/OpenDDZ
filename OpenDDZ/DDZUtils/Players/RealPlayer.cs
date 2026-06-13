using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using OpenDDZ.DDZUtils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Players
{
    [Serializable]
    public abstract class RealPlayer : IPlayer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int Coins { get; set; }
        public DateTime RegisterTime { get; set; }
        public List<GameRecord> GameHistory { get; set; } = new List<GameRecord>();

        private List<Card> _hand = new List<Card>();
        private IDealer _dealer;

        protected IGameIO GameIO { get; set; }// 用于与玩家交互的接口，因为可能玩家是用不同的方式连接的
        public RealPlayer(string name, IGameIO gameIO)
        {
            Id = Guid.NewGuid().ToString();
            Name = name;
            GameIO = gameIO;
            Coins = 1000; // 初始金币
            RegisterTime = DateTime.UtcNow;
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
            switch (message.Type)
            {
                case DealerMessageType.Info:
                    GameIO.ShowMessage(message.Content);
                    break;
                case DealerMessageType.Error:
                    GameIO.ShowError(message.Content);
                    break;
                case DealerMessageType.RequestPlay:
                    // 由GameController主循环处理
                    break;
                case DealerMessageType.RequestCallLandlord:
                    string[] callOptions = null;
                    int highestBid = 0;
                    if (message.Data is object[] arr && arr.Length >= 2)
                    {
                        callOptions = arr[0] as string[];
                        if (arr[1] is int hb) highestBid = hb;
                    }
                    else
                    {
                        callOptions = message.Data as string[];
                    }
                    var optsText = callOptions != null ? string.Join(" / ", callOptions) : "1分 / 2分 / 3分 / 不叫";
                    GameIO.ShowHand(this);
                    GameIO.ShowMessage($"请叫地主（当前最高: {highestBid} 分，可选: {optsText}）");
                    var call = GameIO.GetBidInput(this);
                    return new PlayerMessage
                    {
                        Type = PlayerMessageType.CallLandlord,
                        Data = call
                    };
                case DealerMessageType.RequestDiscard:
                    var card = GameIO.GetDiscardInput(this);
                    return new PlayerMessage
                    {
                        Type = PlayerMessageType.Discard,
                        Data = card
                    };
                default:
                    GameIO.ShowError(message.Content);
                    break;
            }
            return new PlayerMessage { Type = PlayerMessageType.Ack };
        }
        public void SetDealer(IDealer dealer) => _dealer = dealer;

        public void ReceiveCards(IEnumerable<Card> cards)
        {
            _hand.AddRange(cards);
        }

    }

}
