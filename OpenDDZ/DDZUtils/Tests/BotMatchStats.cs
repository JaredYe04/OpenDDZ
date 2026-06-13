using OpenDDZ.DDZUtils.Entities;
using System;
using System.Collections.Generic;

namespace OpenDDZ.DDZUtils.Tests
{
    public class BotMatchStats
    {
        public string Matchup { get; set; }
        public string Mode { get; set; }
        public int GameCount { get; set; }
        public int BaseSeed { get; set; }
        public long TotalElapsedMs { get; set; }
        public int[] SeatWins { get; set; }
        public int LandlordWins { get; set; }
        public int FarmerWins { get; set; }
        public int Team0Wins { get; set; }
        public int Team1Wins { get; set; }
        public long TotalDecisionMs { get; set; }
        public int TotalDecisions { get; set; }
        public Dictionary<string, int> StrategySeatWins { get; set; } = new Dictionary<string, int>();

        public double SeatWinRate(int seat) => GameCount > 0 ? (double)SeatWins[seat] / GameCount : 0;
        public double AvgDecisionMs => TotalDecisions > 0 ? (double)TotalDecisionMs / TotalDecisions : 0;
        public double GamesPerSecond => TotalElapsedMs > 0 ? GameCount * 1000.0 / TotalElapsedMs : 0;

        public string Summary()
        {
            var lines = new List<string>
            {
                $"Matchup: {Matchup}, Mode: {Mode}, Games: {GameCount}, Seed: {BaseSeed}",
                $"Total: {TotalElapsedMs}ms, {GamesPerSecond:F2} games/s, Avg decision: {AvgDecisionMs:F2}ms"
            };
            if (SeatWins != null)
            {
                for (int i = 0; i < SeatWins.Length; i++)
                    lines.Add($"  Seat {i} win rate: {SeatWinRate(i):P1} ({SeatWins[i]}/{GameCount})");
            }
            if (Mode == "Normal")
                lines.Add($"  Landlord wins: {LandlordWins}, Farmer wins: {FarmerWins}");
            if (Mode == "FourPlayer")
                lines.Add($"  Team0(0+2) wins: {Team0Wins}, Team1(1+3) wins: {Team1Wins}");
            foreach (var kv in StrategySeatWins)
                lines.Add($"  Strategy {kv.Key} wins: {kv.Value}");
            return string.Join(Environment.NewLine, lines);
        }
    }
}
