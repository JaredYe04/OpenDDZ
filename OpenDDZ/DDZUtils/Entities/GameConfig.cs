using Newtonsoft.Json;
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

    [Serializable]
    public class GameConfig
    {
        public int DeckCount { get; set; } = 1;
        public int Seed { get; set; } = 114514;
        [JsonIgnore]
        public IDealer Dealer { get; set; }
        [JsonIgnore]
        public Func<IList<Card>, IList<Card>> ShuffleMethod { get; set; }
        public List<IPlayer> Players { get; set; } = new List<IPlayer>();
        [JsonIgnore]
        public RuleSet Rules { get; set; } = RuleSet.Default;

        public bool EnableLandlord { get; set; } = true;//启用叫地主模式

        /// <summary> 游戏模式：Normal=3人斗地主，FourPlayer=4人2v2 </summary>
        public GameMode Mode { get; set; } = GameMode.Normal;

        /// <summary> 发牌完成后、叫地主/弃牌前调用，用于向客户端推送手牌等。 </summary>
        [JsonIgnore]
        public Action AfterDeal { get; set; }
    }

    public enum GameMode
    {
        Normal = 0,
        FourPlayer = 1
    }
}
