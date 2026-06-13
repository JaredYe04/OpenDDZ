namespace OpenDDZ.DDZUtils.Interfaces
{
    /// <summary>
    /// 四人 2v2 相邻组队信息（由 FourPlayerDealer 提供）。
    /// </summary>
    public interface IFourPlayerTeamInfo
    {
        int GetTeamId(int seat);
        int GetTeammateIndex(int seat);
        bool SameTeam(int a, int b);
    }
}
