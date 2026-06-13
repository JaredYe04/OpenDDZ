using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenDDZ.DDZUtils.AI;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Tests
{
    public class SelfPlayMinerConfig
    {
        public string InputPath { get; set; } = "datasets/selfplay_traces.jsonl";
        public string OutputPath { get; set; } = "datasets/selfplay_corrections.jsonl";
        public int OracleRollouts { get; set; } = 50;
        public double MinWinRateSpread { get; set; } = 0.05;
        public double HighSpreadThreshold { get; set; } = 0.15;
        public int Parallelism { get; set; } = Environment.ProcessorCount;
    }

    public static class SelfPlayMiner
    {
        public static void Run(SelfPlayMinerConfig config)
        {
            if (!File.Exists(config.InputPath))
            {
                Console.WriteLine($"Input not found: {config.InputPath}");
                return;
            }

            var traces = File.ReadAllLines(config.InputPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(ParseTrace)
                .Where(t => t != null)
                .ToList();

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(config.OutputPath)) ?? ".");
            int parallelism = Math.Max(1, config.Parallelism);
            var shardPaths = new string[parallelism];
            var locks = new object[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                shardPaths[i] = config.OutputPath + $".part{i:D3}";
                locks[i] = new object();
                if (File.Exists(shardPaths[i])) File.Delete(shardPaths[i]);
            }

            int totalSamples = 0;
            var progressLock = new object();

            Parallel.For(0, traces.Count, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, ti =>
            {
                var trace = traces[ti];
                int shard = ti % parallelism;
                int added = MineTrace(trace, config, shardPaths[shard], locks[shard]);
                lock (progressLock)
                {
                    totalSamples += added;
                    if ((ti + 1) % 50 == 0)
                        Console.WriteLine($"  mined {ti + 1}/{traces.Count}, corrections {totalSamples}");
                }
            });

            MergeShards(config.OutputPath, shardPaths);
            Console.WriteLine($"Corrections written: {config.OutputPath} ({totalSamples} samples from {traces.Count} games)");
        }

        private static void MergeShards(string outputPath, string[] shardPaths)
        {
            using (var writer = new StreamWriter(outputPath, false))
            {
                foreach (var shard in shardPaths)
                {
                    if (!File.Exists(shard)) continue;
                    foreach (var line in File.ReadLines(shard))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            writer.WriteLine(line);
                    }
                    File.Delete(shard);
                }
            }
        }

        private static int MineTrace(GameStateRebuilder.GameTrace trace, SelfPlayMinerConfig config,
            string shardPath, object shardLock)
        {
            int count = 0;
            int playerCount = trace.InitialHands?.Count ?? 3;

            for (int mi = 0; mi < trace.Moves.Count; mi++)
            {
                BotDecisionContext ctx;
                List<List<Card>> knownHands;
                try
                {
                    ctx = GameStateRebuilder.BuildContext(trace, mi, playerCount);
                    knownHands = GameStateRebuilder.GetKnownHands(trace, mi);
                }
                catch
                {
                    continue;
                }

                var actualMove = CardCodec.DecodeMove(trace.Moves[mi].Move);
                var eval = PerfectInfoOracle.Evaluate(ctx, knownHands, config.OracleRollouts);

                if (eval.Candidates.Count < 2 ||
                    !PerfectInfoOracle.HasMeaningfulSpread(eval.WinRates, config.MinWinRateSpread))
                    continue;

                int actualIndex = FindMoveIndex(eval.Candidates, actualMove);
                if (actualIndex < 0 || actualIndex == eval.BestIndex)
                    continue;

                double spread = eval.WinRates.Max() - eval.WinRates.Min();
                double weight = 2.0;
                int seat = trace.Moves[mi].Seat;
                if (trace.Winner >= 0 && trace.Winner != seat && spread >= config.HighSpreadThreshold)
                    weight = 3.0;

                var candFeatures = eval.Candidates
                    .Select(m => BotFeatureExtractor.Extract(ctx, m).ToList())
                    .ToList();

                var row = new
                {
                    game_id = trace.GameId * 10000 + mi,
                    mode = trace.Mode,
                    player_index = ctx.MyIndex,
                    best_index = eval.BestIndex,
                    win_rates = eval.WinRates,
                    candidates = candFeatures,
                    source = "selfplay",
                    weight = weight,
                    actual_index = actualIndex
                };

                lock (shardLock)
                {
                    using (var writer = new StreamWriter(shardPath, true))
                        writer.WriteLine(JsonConvert.SerializeObject(row));
                }
                count++;
            }

            return count;
        }

        private static int FindMoveIndex(List<Move> candidates, Move actual)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (BotCandidateHelper.MovesEqual(candidates[i], actual))
                    return i;
            }
            return -1;
        }

        private static GameStateRebuilder.GameTrace ParseTrace(string line)
        {
            try
            {
                var jo = JObject.Parse(line);
                var trace = new GameStateRebuilder.GameTrace
                {
                    GameId = jo["game_id"]?.Value<int>() ?? 0,
                    Seed = jo["seed"]?.Value<int>() ?? 0,
                    Mode = jo["mode"]?.Value<string>() ?? "Normal",
                    Winner = jo["winner"]?.Value<int>() ?? -1,
                    Landlord = jo["landlord"]?.Value<int>() ?? -1,
                    InitialHands = jo["initial_hands"]?.ToObject<List<List<string>>>() ?? new List<List<string>>(),
                    Moves = new List<GameStateRebuilder.TraceMove>()
                };

                foreach (var m in jo["moves"] ?? new JArray())
                {
                    trace.Moves.Add(new GameStateRebuilder.TraceMove
                    {
                        Seat = m["seat"]?.Value<int>() ?? 0,
                        Move = m["move"]?.Value<string>() ?? ""
                    });
                }
                return trace;
            }
            catch
            {
                return null;
            }
        }
    }

    internal class SelfPlayMinerCli
    {
        public static void Run(string[] args)
        {
            var config = new SelfPlayMinerConfig();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--input" && i + 1 < args.Length) config.InputPath = args[++i];
                else if (args[i] == "--output" && i + 1 < args.Length) config.OutputPath = args[++i];
                else if (args[i] == "--oracle-rollouts" && i + 1 < args.Length) config.OracleRollouts = int.Parse(args[++i]);
                else if (args[i] == "--parallel" && i + 1 < args.Length) config.Parallelism = int.Parse(args[++i]);
            }

            Console.WriteLine($"Mining self-play: input={config.InputPath}, rollouts={config.OracleRollouts}, parallel={config.Parallelism}");
            SelfPlayMiner.Run(config);
        }
    }
}
