// OpenDDZ\DDZUtils\Entities\PlayerMessage.cs
using OpenDDZ.DDZUtils.Enums;

namespace OpenDDZ.DDZUtils.Entities
{
    public class PlayerMessage
    {
        public PlayerMessageType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public object Data { get; set; } // 可携带 Move, 叫分, 等
    }
}