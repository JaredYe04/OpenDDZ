using OpenDDZ.DDZUtils.Entities;
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

        public RealPlayer(string name, string email="")
        {
            Id = Guid.NewGuid().ToString();
            Name = name;
            Email = email;
            Coins = 1000; // 初始金币
            RegisterTime = DateTime.UtcNow;
        }

        public IList<Card> GetHandCards() => _hand;

        public void RequestPlay(Move move)
        {
            _dealer?.HandlePlayRequest(this, move);
        }

        public virtual void OnMessage(DealerMessage message)
        {
            Console.WriteLine($"[RealPlayer {Name}] 收到消息: {message.Type} - {message.Content}");
            //todo：这里可以触发UI更新等逻辑，目前仅打印日志
        }

        public void SetDealer(IDealer dealer) => _dealer = dealer;

        public void ReceiveCards(IEnumerable<Card> cards)
        {
            _hand.AddRange(cards);
        }
    }

}
