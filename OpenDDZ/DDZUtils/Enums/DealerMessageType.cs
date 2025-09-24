using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Enums
{
    public enum DealerMessageType
    {
        GameStart,
        GameEnd,
        DealCards,
        TurnChanged,
        PlayAccepted,
        PlayRejected,
        PlayerPassed,
        Broadcast,//广播消息
        Error,
        Info,
        RequestPlay//请求当前玩家出牌
    }
}
