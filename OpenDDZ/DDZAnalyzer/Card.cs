using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZAnalyzer
{
    public class Card
    {
        public Rank Rank { get; }
        public Card(Rank r) { Rank = r; }
        public override string ToString() => Rank.ToString();
    }

}
