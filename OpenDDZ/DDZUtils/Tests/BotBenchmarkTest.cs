using OpenDDZ.DDZUtils.Entities;
using System;

namespace OpenDDZ.DDZUtils.Tests
{
    internal class BotBenchmarkTest
    {
        public static void Run(string[] args)
        {
            var config = ParseArgs(args);
            Console.WriteLine("OpenDDZ Bot Benchmark");
            Console.WriteLine($"Matchup={config.Matchup}, Mode={config.Mode}, Games={config.GameCount}, Seed={config.BaseSeed}, Rollouts={config.RolloutCount}");
            Console.WriteLine();

            var stats = BotBenchmarkRunner.Run(config);
            Console.WriteLine(stats.Summary());
        }

        private static BotBenchmarkConfig ParseArgs(string[] args)
        {
            var config = new BotBenchmarkConfig();
            if (args == null) return config;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--matchup" && i + 1 < args.Length) config.Matchup = args[++i];
                else if (a == "--mode" && i + 1 < args.Length)
                    config.Mode = args[++i].Equals("FourPlayer", StringComparison.OrdinalIgnoreCase) ? GameMode.FourPlayer : GameMode.Normal;
                else if (a == "--games" && i + 1 < args.Length) config.GameCount = int.Parse(args[++i]);
                else if (a == "--seed" && i + 1 < args.Length) config.BaseSeed = int.Parse(args[++i]);
                else if (a == "--deck" && i + 1 < args.Length) config.DeckCount = int.Parse(args[++i]);
                else if (a == "--rollouts" && i + 1 < args.Length) config.RolloutCount = int.Parse(args[++i]);
            }
            return config;
        }
    }
}
