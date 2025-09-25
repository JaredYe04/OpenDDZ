using OpenDDZ.DDZUtils.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Interfaces
{
    public interface IDealer
    {
        /// <summary>
        /// 当前游戏记录
        /// </summary>
        GameRecord CurrentGame { get; }
        /// <summary>
        /// 上一个出牌的玩家、出牌内容和时间戳，会自动跳过null的情况，只返回实际出过牌的记录
        /// </summary>
        (IPlayer, Move, DateTime) LastMove { get; }
        
        /// <summary>
        /// 注册一局的玩家（斗地主是3个玩家）
        /// </summary>
        void RegisterPlayers(IEnumerable<IPlayer> players);

        /// <summary>
        /// 开始新的一局，负责发牌、确定地主、初始化状态
        /// </summary>
        void StartGame(GameConfig config);

        /// <summary>
        /// 给指定玩家发牌
        /// </summary>
        void DealCards(IPlayer player, IEnumerable<Card> cards);

        /// <summary>
        /// 处理玩家的出牌请求，move 为 null 表示玩家选择不出
        /// </summary>
        bool HandlePlayRequest(IPlayer player, Move move);

        /// <summary>
        /// 向所有玩家广播消息
        /// </summary>
        void Broadcast(string message);

        /// <summary>
        /// 分数计算逻辑（不同庄家可能不同）
        /// </summary>
        void CalculateScores();

        /// <summary>
        /// 是否需要读秒（如人机/多人联机对战）
        /// AI 训练模式可禁用
        /// </summary>
        bool EnableTimer { get; }


        /// <summary>
        /// 规则集，例如是否允许炸弹、火箭等
        /// </summary>
        RuleSet Rules {get;}

        /// <summary>
        /// 获取当前出牌玩家的索引
        /// </summary>
        /// <returns></returns>
        int GetCurrentPlayerIndex();
    }

}
