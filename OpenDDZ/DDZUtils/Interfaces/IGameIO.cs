using OpenDDZ.DDZUtils.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Interfaces
{
    /// <summary>
    /// 通用IO接口
    /// </summary>
    public interface IGameIO
    {
        void ShowMessage(string message);
        void ShowHand(IPlayer player);
        void ShowLastMove(IPlayer player, Move move, IPlayer lastPlayer);
        Move GetMoveInput(IPlayer player);// 出牌输入
        string GetBidInput(IPlayer player);// 叫地主输入
        void ShowError(string message);
        void ShowGameEnd(IPlayer winner);
        /// <summary> 后手弃牌：返回一张牌或 null 表示不弃。四人模式用。 </summary>
        Card GetDiscardInput(IPlayer player);
        /// <summary> 出牌不合法时通知客户端重试，不消耗队列中的下一条输入。 </summary>
        void EmitPlayRejected(string reason);
    }
}
