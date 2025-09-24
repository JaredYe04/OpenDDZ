using OpenDDZ.DDZUtils.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Interfaces
{
    /// <summary>
    /// 表示斗地主游戏中的玩家接口。
    /// </summary>
    public interface IPlayer
    {
        /// <summary>
        /// 获取玩家的唯一标识符。
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// 获取玩家当前手牌列表。
        /// </summary>
        /// <returns>玩家手中的牌列表，只读。</returns>
        IList<Card> GetHandCards();

        /// <summary>
        /// 请求玩家出牌。
        /// </summary>
        /// <param name="move">玩家要出的牌型。</param>
        void RequestPlay(Move move);

        /// <summary>
        /// 处理来自发牌者的消息。
        /// </summary>
        /// <param name="message">发牌者发送的消息。</param>
        void OnMessage(DealerMessage message);

        /// <summary>
        /// 设置发牌者实例。
        /// </summary>
        /// <param name="dealer">发牌者对象。</param>
        void SetDealer(IDealer dealer);
    }
}
