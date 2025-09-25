using Newtonsoft.Json;
using OpenDDZ.DDZUtils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Entities
{
    [Serializable]
    public class GameRecord :ISerializable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public GameConfig Config { get; set; }
        public string Name { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public List<IPlayer> Players { get; set; } = new List<IPlayer>();
        public IPlayer Landlord { get; set; }//地主

        public Dictionary<IPlayer,List<Card>> InitialHands { get; set; }
            = new Dictionary<IPlayer, List<Card>>();


        [JsonIgnore]
        public IDealer Dealer { get; set; }
        public List<(IPlayer player, Move move, DateTime timestamp)> Moves { get; set; }
            = new List<(IPlayer, Move, DateTime)>();

        public void Deserialize(string data)
        {
            Newtonsoft.Json.JsonConvert.PopulateObject(data, this);
        }

        public string Serialize()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }
    }

}
