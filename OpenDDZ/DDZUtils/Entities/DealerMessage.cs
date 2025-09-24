using OpenDDZ.DDZUtils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Entities
{


    public class DealerMessage
    {
        public DealerMessage() { }
        public DealerMessageType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public object Data { get; set; }  // 可携带 Move, Card, Player 等信息
    }

}
