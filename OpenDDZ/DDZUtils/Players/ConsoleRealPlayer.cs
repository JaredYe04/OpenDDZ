
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using System;

namespace OpenDDZ.DDZUtils.Players
{
    public class ConsoleRealPlayer : RealPlayer
    {
        public ConsoleRealPlayer(string name, string email) : base(name, email) { }

        public override void OnMessage(DealerMessage message)
        {
            switch (message.Type)
            {
                case DealerMessageType.Info:
                    Console.WriteLine($"[系统] {message.Content}");
                    break;
                case DealerMessageType.Error:
                    Console.WriteLine($"[错误] {message.Content}");
                    break;
                case DealerMessageType.RequestPlay:
                    // 由GameController主循环处理
                    break;
                default:
                    Console.WriteLine($"[消息] {message.Content}");
                    break;
            }
        }
    }
}