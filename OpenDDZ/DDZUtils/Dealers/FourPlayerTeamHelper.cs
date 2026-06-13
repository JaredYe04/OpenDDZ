using System;

namespace OpenDDZ.DDZUtils.Dealers
{
    /// <summary>
    /// 四人相邻组队：随机 0+1 vs 2+3，或 3+0 vs 1+2。
    /// </summary>
    public static class FourPlayerTeamHelper
    {
        public static void AssignTeams(int seed, out int[] teamIds, out bool altLayout)
        {
            altLayout = new Random(seed ^ 0x5A17).Next(2) == 0;
            teamIds = new int[4];
            if (!altLayout)
            {
                teamIds[0] = 0;
                teamIds[1] = 0;
                teamIds[2] = 1;
                teamIds[3] = 1;
            }
            else
            {
                teamIds[0] = 0;
                teamIds[3] = 0;
                teamIds[1] = 1;
                teamIds[2] = 1;
            }
        }

        public static int GetTeammateIndex(int seat, int[] teamIds)
        {
            int team = teamIds[seat];
            for (int i = 0; i < 4; i++)
            {
                if (i != seat && teamIds[i] == team)
                    return i;
            }
            return (seat + 2) % 4;
        }
    }
}
