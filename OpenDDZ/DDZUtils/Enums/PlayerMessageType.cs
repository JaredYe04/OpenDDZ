// OpenDDZ\DDZUtils\Enums\PlayerMessageType.cs
namespace OpenDDZ.DDZUtils.Enums
{
    public enum PlayerMessageType
    {
        Ack,          // 确认收到
        Play,         // 出牌
        Pass,         // 不出
        CallLandlord, // 叫地主，叫分等
        Other         // 其他
    }
}