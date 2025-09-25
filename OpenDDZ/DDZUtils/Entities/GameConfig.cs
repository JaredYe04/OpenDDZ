using OpenDDZ.DDZUtils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Entities
{
    /// <summary>
    /// 游戏配置
    /// </summary>
    public class GameConfig
    {
        public int DeckCount { get; set; } = 1;
        public IDealer Dealer { get; set; }
        public Func<IList<Card>, IList<Card>> ShuffleMethod { get; set; }
        public List<IPlayer> Players { get; set; } = new List<IPlayer>();
        public RuleSet Rules { get; set; } = RuleSet.Default;
    }

}
