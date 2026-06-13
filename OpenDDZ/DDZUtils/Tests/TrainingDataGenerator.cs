using Newtonsoft.Json;
using OpenDDZ.DDZUtils.AI;
using OpenDDZ.DDZUtils.Controllers;
using OpenDDZ.DDZUtils.Dealers;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.GameIOs;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players;
using OpenDDZ.DDZUtils.Players.Strategies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Tests
{
    public class TrainingDataConfig
    {
        public int GameCount { get; set; } = 10000;
        public int BaseSeed { get; set; } = 42;
        public int OracleRollouts { get; set; } = 50;
        public string OutputPath { get; set; } = "datasets/train.jsonl";
        public GameMode Mode { get; set; } = GameMode.Normal;
        public double MinWinRateSpread { get; set; } = 0.05;
        public int Parallelism { get; set; } = Math.Min(Environment.ProcessorCount, 8);
    }

    internal sealed class ShardWriterPool : IDisposable
    {
        private readonly string _outputPath;
        private readonly StreamWriter[] _writers;
        private readonly object[] _locks;

        public ShardWriterPool(string outputPath, int parallelism)
        {
            _outputPath = outputPath;
            _writers = new StreamWriter[parallelism];
            _locks = new object[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                _locks[i] = new object();
                string path = ShardPath(i);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        public string ShardPath(int shard) => _outputPath + $".part{shard:D3}";

        public void WriteLine(int shard, string line)
        {
            lock (_locks[shard])
            {
                if (_writers[shard] == null)
                    _writers[shard] = new StreamWriter(ShardPath(shard), true, new UTF8Encoding(false), 65536);
                _writers[shard].WriteLine(line);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _writers.Length; i++)
            {
                lock (_locks[i])
                {
                    _writers[i]?.Flush();
                    _writers[i]?.Dispose();
                    _writers[i] = null;
                }
            }
        }
    }

    internal static class TrainingDataGenerator
    {
        public static void Run(TrainingDataConfig config)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(config.OutputPath)) ?? ".");
            int parallelism = Math.Max(1, Math.Min(config.Parallelism, 16));

            int samples = 0;
            int games = 0;
            int errors = 0;
            var progressLock = new object();

            using (var pool = new ShardWriterPool(config.OutputPath, parallelism))
            {
                try
                {
                    Parallel.For(0, config.GameCount,
                        new ParallelOptions { MaxDegreeOfParallelism = parallelism },
                        g =>
                        {
                            try
                            {
                                int shard = g % parallelism;
                                int seed = config.BaseSeed + g;
                                int added = GenerateGameSamples(pool, shard, g, seed, config);

                                lock (progressLock)
                                {
                                    if (added > 0) games++;
                                    samples += added;
                                    if ((g + 1) % 100 == 0)
                                        Console.WriteLine($"  games {g + 1}/{config.GameCount}, samples {samples}, errors {errors}");
                                }
                            }
                            catch (Exception ex)
                            {
                                lock (progressLock)
                                {
                                    errors++;
                                    if (errors <= 5)
                                        Console.WriteLine($"  [warn] game {g} failed: {ex.GetType().Name}: {ex.Message}");
                                }
                            }
                        });
                }
                catch (AggregateException ae)
                {
                    foreach (var ex in ae.Flatten().InnerExceptions.Take(3))
                        Console.WriteLine($"  [error] {ex.GetType().Name}: {ex.Message}");
                    throw;
                }
            }

            MergeShards(config.OutputPath, parallelism);
            Console.WriteLine($"Dataset written: {config.OutputPath}");
            Console.WriteLine($"Games with samples: {games}, total samples: {samples}, parallel={parallelism}, errors={errors}");
        }

        private static void MergeShards(string outputPath, int parallelism)
        {
            MergeShardsOnly(outputPath, parallelism);
        }

        public static void MergeShardsOnly(string outputPath, int parallelism)
        {
            long lines = 0;
            using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false), 65536))
            {
                for (int i = 0; i < parallelism; i++)
                {
                    string shard = outputPath + $".part{i:D3}";
                    if (!File.Exists(shard)) continue;
                    foreach (var line in File.ReadLines(shard))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            writer.WriteLine(line);
                            lines++;
                        }
                    }
                    try { File.Delete(shard); } catch { }
                }
            }
            Console.WriteLine($"Merged shards -> {outputPath} ({lines} lines)");
        }

        private static int GenerateGameSamples(ShardWriterPool pool, int shard, int gameId, int seed, TrainingDataConfig config)
        {
            if (config.Mode != GameMode.Normal)
                return GenerateFourPlayerSamples(pool, shard, gameId, seed, config);

            var players = new List<IPlayer>
            {
                new BotPlayer("P0", new GreedyBotStrategy()),
                new BotPlayer("P1", new GreedyBotStrategy()),
                new BotPlayer("P2", new GreedyBotStrategy())
            };

            var dealer = new BasicDealer(RuleSet.Default);
            var gameConfig = new GameConfig
            {
                Dealer = dealer,
                Players = players,
                Seed = seed,
                DeckCount = 1,
                Mode = GameMode.Normal,
                EnableLandlord = true,
                ShuffleMethod = list => { ShuffleUtils.WeakShuffle(list, seed); return list; }
            };

            var controller = new GameController(gameConfig, new NullGameIO());
            var recorder = new TrainingGameRecorder(dealer, players, gameId, config, pool, shard);
            controller.StartGame();
            return recorder.RunRecordedGameLoop();
        }

        private static int GenerateFourPlayerSamples(ShardWriterPool pool, int shard, int gameId, int seed, TrainingDataConfig config)
        {
            var players = new List<IPlayer>
            {
                new BotPlayer("P0", new GreedyBotStrategy()),
                new BotPlayer("P1", new GreedyBotStrategy()),
                new BotPlayer("P2", new GreedyBotStrategy()),
                new BotPlayer("P3", new GreedyBotStrategy())
            };

            var dealer = new FourPlayerDealer(RuleSet.Default);
            var gameConfig = new GameConfig
            {
                Dealer = dealer,
                Players = players,
                Seed = seed,
                DeckCount = 2,
                Mode = GameMode.FourPlayer,
                EnableLandlord = true,
                ShuffleMethod = list => { ShuffleUtils.WeakShuffle(list, seed); return list; }
            };

            var controller = new GameController(gameConfig, new NullGameIO());
            var recorder = new TrainingGameRecorder(dealer, players, gameId, config, pool, shard);
            controller.StartGame();
            return recorder.RunRecordedGameLoop();
        }

        private class TrainingGameRecorder
        {
            private readonly IDealer _dealer;
            private readonly List<IPlayer> _players;
            private readonly int _gameId;
            private readonly TrainingDataConfig _config;
            private readonly ShardWriterPool _pool;
            private readonly int _shard;
            private readonly GreedyBotStrategy _greedy = new GreedyBotStrategy();
            private readonly List<List<Card>> _knownHands = new List<List<Card>>();

            public TrainingGameRecorder(IDealer dealer, List<IPlayer> players, int gameId,
                TrainingDataConfig config, ShardWriterPool pool, int shard)
            {
                _dealer = dealer;
                _players = players;
                _gameId = gameId;
                _config = config;
                _pool = pool;
                _shard = shard;
                for (int i = 0; i < players.Count; i++)
                    _knownHands.Add(new List<Card>());
            }

            public int RunRecordedGameLoop()
            {
                int count = 0;
                int guard = 0;
                while (guard++ < 500)
                {
                    if (_dealer.CurrentGame == null) break;

                    var winner = _players.FirstOrDefault(p => p.GetHandCards().Count == 0);
                    if (winner != null) break;

                    int idx = _dealer.GetCurrentPlayerIndex();
                    var player = _players[idx];
                    SyncKnownHands();

                    var ctx = BotDecisionContext.From(player, _dealer);
                    var eval = PerfectInfoOracle.Evaluate(ctx, _knownHands, _config.OracleRollouts);

                    if (eval.Candidates.Count >= 2 &&
                        PerfectInfoOracle.HasMeaningfulSpread(eval.WinRates, _config.MinWinRateSpread))
                    {
                        WriteSample(eval, ctx);
                        count++;
                    }

                    var move = eval.BestMove ?? _greedy.ChoosePlay(ctx);
                    player.RequestPlay(move);
                }
                return count;
            }

            private void SyncKnownHands()
            {
                for (int i = 0; i < _players.Count; i++)
                    _knownHands[i] = _players[i].GetHandCards().ToList();
            }

            private void WriteSample(OracleEvaluation eval, BotDecisionContext ctx)
            {
                var candFeatures = eval.Candidates
                    .Select(m => BotFeatureExtractor.Extract(ctx, m).ToList())
                    .ToList();

                var row = new
                {
                    game_id = _gameId,
                    mode = ctx.Mode.ToString(),
                    player_index = ctx.MyIndex,
                    best_index = eval.BestIndex,
                    win_rates = eval.WinRates,
                    candidates = candFeatures
                };

                _pool.WriteLine(_shard, JsonConvert.SerializeObject(row));
            }
        }
    }

    internal class TrainingDataCli
    {
        public static void Run(string[] args)
        {
            var config = new TrainingDataConfig();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--games" && i + 1 < args.Length) config.GameCount = int.Parse(args[++i]);
                else if (args[i] == "--seed" && i + 1 < args.Length) config.BaseSeed = int.Parse(args[++i]);
                else if (args[i] == "--output" && i + 1 < args.Length) config.OutputPath = args[++i];
                else if (args[i] == "--oracle-rollouts" && i + 1 < args.Length) config.OracleRollouts = int.Parse(args[++i]);
                else if (args[i] == "--parallel" && i + 1 < args.Length) config.Parallelism = int.Parse(args[++i]);
                else if (args[i] == "--merge-shards")
                {
                    int p = Math.Max(1, Math.Min(config.Parallelism, 16));
                    TrainingDataGenerator.MergeShardsOnly(config.OutputPath, p);
                    return;
                }
                else if (args[i] == "--mode" && i + 1 < args.Length)
                    config.Mode = args[++i].Equals("FourPlayer", StringComparison.OrdinalIgnoreCase)
                        ? GameMode.FourPlayer : GameMode.Normal;
            }

            int parallelism = Math.Max(1, Math.Min(config.Parallelism, 16));
            Console.WriteLine($"Generating dataset: games={config.GameCount}, rollouts={config.OracleRollouts}, parallel={parallelism}, mode={config.Mode}");

            try
            {
                config.Parallelism = parallelism;
                TrainingDataGenerator.Run(config);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Dataset generation failed: " + ex.GetType().Name + ": " + ex.Message);
                Environment.Exit(1);
            }
        }
    }
}
