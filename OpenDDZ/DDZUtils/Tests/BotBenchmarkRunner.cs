using OpenDDZ.DDZUtils.Controllers;
using OpenDDZ.DDZUtils.Dealers;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.GameIOs;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players;
using OpenDDZ.DDZUtils.Players.Strategies;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OpenDDZ.DDZUtils.Tests
{
    public class BotBenchmarkConfig
    {
        public string Matchup { get; set; } = "greedy_all";
        public GameMode Mode { get; set; } = GameMode.Normal;
        public int GameCount { get; set; } = 100;
        public int BaseSeed { get; set; } = 42;
        public int DeckCount { get; set; } = 1;
        public int RolloutCount { get; set; } = 20;
        public bool WriteJsonReport { get; set; } = true;
        public string ReportDirectory { get; set; }
    }

    public static class BotBenchmarkRunner
    {
        public static BotMatchStats Run(BotBenchmarkConfig config)
        {
            int playerCount = config.Mode == GameMode.FourPlayer ? 4 : 3;
            var stats = new BotMatchStats
            {
                Matchup = config.Matchup,
                Mode = config.Mode.ToString(),
                GameCount = config.GameCount,
                BaseSeed = config.BaseSeed,
                SeatWins = new int[playerCount]
            };

            var totalSw = Stopwatch.StartNew();

            for (int g = 0; g < config.GameCount; g++)
            {
                int seed = config.BaseSeed + g;
                var seatStrategies = ResolveMatchup(config.Matchup, playerCount, config.RolloutCount, seed);
                var timedStrategies = seatStrategies.Select(s => new TimingBotStrategy(s)).ToArray();
                var players = new List<IPlayer>();
                for (int i = 0; i < playerCount; i++)
                    players.Add(new BotPlayer($"Bot{i}", timedStrategies[i]));

                IDealer dealer = config.Mode == GameMode.FourPlayer
                    ? (IDealer)new FourPlayerDealer(RuleSet.Default)
                    : new BasicDealer(RuleSet.Default);

                int deckCount = config.Mode == GameMode.FourPlayer ? Math.Max(2, config.DeckCount) : config.DeckCount;

                var gameConfig = new GameConfig
                {
                    Dealer = dealer,
                    Players = players,
                    Seed = seed,
                    DeckCount = deckCount,
                    Mode = config.Mode,
                    EnableLandlord = true,
                    ShuffleMethod = list => { ShuffleUtils.WeakShuffle(list, seed); return list; }
                };

                var io = new NullGameIO();
                var controller = new GameController(gameConfig, io);
                controller.StartGame();
                controller.RunGameLoop();

                var winner = io.LastWinner ?? players.FirstOrDefault(p => p.GetHandCards().Count == 0);
                if (winner == null) continue;

                int winnerIdx = players.IndexOf(winner);
                stats.SeatWins[winnerIdx]++;

                string stratName = timedStrategies[winnerIdx].InnerName;
                if (!stats.StrategySeatWins.ContainsKey(stratName))
                    stats.StrategySeatWins[stratName] = 0;
                stats.StrategySeatWins[stratName]++;

                if (config.Mode == GameMode.Normal && dealer.LandlordIndex >= 0)
                {
                    if (winnerIdx == dealer.LandlordIndex) stats.LandlordWins++;
                    else stats.FarmerWins++;
                }
                if (config.Mode == GameMode.FourPlayer && dealer is IFourPlayerTeamInfo teamInfo)
                {
                    if (teamInfo.GetTeamId(winnerIdx) == 0) stats.Team0Wins++;
                    else stats.Team1Wins++;
                }

                foreach (var ts in timedStrategies)
                {
                    stats.TotalDecisionMs += ts.TotalDecisionMs;
                    stats.TotalDecisions += ts.DecisionCount;
                }
            }

            totalSw.Stop();
            stats.TotalElapsedMs = totalSw.ElapsedMilliseconds;

            if (config.WriteJsonReport)
                WriteReport(stats, config);

            return stats;
        }

        private static IBotStrategy[] ResolveMatchup(string matchup, int playerCount, int rolloutCount, int seed = 0)
        {
            var greedy = new GreedyBotStrategy();
            var mc = new MonteCarloBotStrategy { RolloutCount = rolloutCount, ParallelRollouts = true };

            var strategies = new IBotStrategy[playerCount];
            for (int i = 0; i < playerCount; i++) strategies[i] = greedy;

            switch (matchup?.ToLowerInvariant())
            {
                case "mc_vs_greedy":
                    strategies[0] = mc;
                    break;
                case "mc_team_vs_greedy":
                    if (playerCount >= 4)
                    {
                        FourPlayerTeamHelper.AssignTeams(seed, out int[] teamIds, out _);
                        for (int i = 0; i < playerCount; i++)
                        {
                            if (teamIds[i] == teamIds[0])
                                strategies[i] = mc;
                        }
                    }
                    break;
                case "ml_vs_greedy":
                    strategies[0] = new MLBotStrategy(PlayerFactory.ResolveModelPath(null));
                    break;
                case "ml_vs_mc":
                    strategies[0] = new MLBotStrategy(PlayerFactory.ResolveModelPath(null));
                    strategies[1] = mc;
                    if (playerCount > 2) strategies[2] = mc;
                    break;
                case "ml_all":
                    for (int i = 0; i < playerCount; i++)
                        strategies[i] = new MLBotStrategy(PlayerFactory.ResolveModelPath(null));
                    break;
                case "mc_all":
                    for (int i = 0; i < playerCount; i++) strategies[i] = mc;
                    break;
                case "greedy_all":
                default:
                    break;
            }
            return strategies;
        }

        private static void WriteReport(BotMatchStats stats, BotBenchmarkConfig config)
        {
            string dir = config.ReportDirectory;
            if (string.IsNullOrEmpty(dir))
            {
                dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "benchmarks");
            }
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"benchmark_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(stats, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(path, json);
            Console.WriteLine($"Report written: {path}");
        }

        private class TimingBotStrategy : IBotStrategy
        {
            private readonly IBotStrategy _inner;
            public long TotalDecisionMs { get; private set; }
            public int DecisionCount { get; private set; }
            public string InnerName => _inner.GetType().Name;

            public TimingBotStrategy(IBotStrategy inner) { _inner = inner; }

            public Move ChoosePlay(BotDecisionContext ctx)
            {
                var sw = Stopwatch.StartNew();
                var move = _inner.ChoosePlay(ctx);
                sw.Stop();
                TotalDecisionMs += sw.ElapsedMilliseconds;
                DecisionCount++;
                return move;
            }

            public string ChooseBid(BotDecisionContext ctx, string[] options) => _inner.ChooseBid(ctx, options);
            public Card ChooseDiscard(BotDecisionContext ctx) => _inner.ChooseDiscard(ctx);
        }
    }
}
