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
        string GetMoveInput(IPlayer player);
        void ShowError(string message);
        void ShowGameEnd(IPlayer winner);
    }
}
