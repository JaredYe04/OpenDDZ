
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
                    Console.WriteLine($"[ϵͳ] {message.Content}");
                    break;
                case DealerMessageType.Error:
                    Console.WriteLine($"[����] {message.Content}");
                    break;
                case DealerMessageType.RequestPlay:
                    // ��GameController��ѭ������
                    break;
                default:
                    Console.WriteLine($"[��Ϣ] {message.Content}");
                    break;
            }
        }
    }
}