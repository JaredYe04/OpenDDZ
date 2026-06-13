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

namespace OpenDDZ.DDZUtils.Tests
{
    public class SelfPlayConfig
    {
        public int GameCount { get; set; } = 2000;
        public int BaseSeed { get; set; } = 42;
        public string ModelPath { get; set; }
        public string OutputPath { get; set; } = "datasets/selfplay_traces.jsonl";
        public GameMode Mode { get; set; } = GameMode.Normal;
    }

    public static class SelfPlayRunner
    {
        public static void Run(SelfPlayConfig config)
        {
            string modelPath = config.ModelPath ?? PlayerFactory.ResolveModelPath(null);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(config.OutputPath)) ?? ".");

            int playerCount = config.Mode == GameMode.FourPlayer ? 4 : 3;
            int written = 0;

            using (var writer = new StreamWriter(config.OutputPath, false))
            {
                for (int g = 0; g < config.GameCount; g++)
                {
                    int seed = config.BaseSeed + g;
                    var trace = RunSingleGame(g, seed, modelPath, playerCount, config.Mode);
                    if (trace != null)
                    {
                        writer.WriteLine(JsonConvert.SerializeObject(trace));
                        written++;
                    }

                    if ((g + 1) % 100 == 0)
                        Console.WriteLine($"  self-play {g + 1}/{config.GameCount}, traces {written}");
                }
            }

            Console.WriteLine($"Self-play traces written: {config.OutputPath} ({written} games)");
        }

        private static object RunSingleGame(int gameId, int seed, string modelPath, int playerCount, GameMode mode)
        {
            var ml = new MLBotStrategy(modelPath);
            var players = new List<IPlayer>();
            for (int i = 0; i < playerCount; i++)
                players.Add(new BotPlayer($"ML{i}", ml));

            IDealer dealer = mode == GameMode.FourPlayer
                ? (IDealer)new FourPlayerDealer(RuleSet.Default)
                : new BasicDealer(RuleSet.Default);

            int deckCount = mode == GameMode.FourPlayer ? 2 : 1;
            var gameConfig = new GameConfig
            {
                Dealer = dealer,
                Players = players,
                Seed = seed,
                DeckCount = deckCount,
                Mode = mode,
                EnableLandlord = true,
                ShuffleMethod = list => { ShuffleUtils.WeakShuffle(list, seed); return list; }
            };

            var io = new NullGameIO();
            var controller = new GameController(gameConfig, io);
            controller.StartGame();

            var initialHands = players
                .Select(p => p.GetHandCards().Select(CardCodec.EncodeCard).ToList())
                .ToList();

            controller.RunGameLoop();

            var winner = io.LastWinner ?? players.FirstOrDefault(p => p.GetHandCards().Count == 0);
            if (winner == null || dealer.CurrentGame == null)
                return null;

            var moves = dealer.CurrentGame.Moves
                .Select(m => new
                {
                    seat = players.IndexOf(m.player),
                    move = CardCodec.EncodeMove(m.move)
                })
                .Where(m => m.seat >= 0)
                .ToList();

            return new
            {
                game_id = gameId,
                seed = seed,
                mode = mode.ToString(),
                initial_hands = initialHands,
                moves = moves,
                winner = players.IndexOf(winner),
                landlord = dealer.LandlordIndex
            };
        }
    }

    internal class SelfPlayCli
    {
        public static void Run(string[] args)
        {
            var config = new SelfPlayConfig();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--games" && i + 1 < args.Length) config.GameCount = int.Parse(args[++i]);
                else if (args[i] == "--seed" && i + 1 < args.Length) config.BaseSeed = int.Parse(args[++i]);
                else if (args[i] == "--model" && i + 1 < args.Length) config.ModelPath = args[++i];
                else if (args[i] == "--output" && i + 1 < args.Length) config.OutputPath = args[++i];
                else if (args[i] == "--mode" && i + 1 < args.Length)
                    config.Mode = args[++i].Equals("FourPlayer", StringComparison.OrdinalIgnoreCase)
                        ? GameMode.FourPlayer : GameMode.Normal;
            }

            Console.WriteLine($"Self-play: games={config.GameCount}, model={config.ModelPath ?? PlayerFactory.ResolveModelPath(null)}");
            SelfPlayRunner.Run(config);
        }
    }
}
