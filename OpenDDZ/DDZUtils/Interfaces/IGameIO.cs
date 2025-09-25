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

    }
}
