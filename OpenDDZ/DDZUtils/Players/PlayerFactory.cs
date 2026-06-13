using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players.Strategies;
using System;
using System.IO;

namespace OpenDDZ.DDZUtils.Players
{
    public enum PlayerKind
    {
        Human = 1,
        Greedy = 2,
        MonteCarlo = 3,
        MachineLearning = 4
    }

    public static class PlayerFactory
    {
        /// <summary>exe 同目录 models/bot_model.bin</summary>
        public static string DefaultModelPath => ResolveModelPath(null);

        public static string ResolveModelPath(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return Path.GetFullPath(path);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string binModel = Path.Combine(baseDir, "models", "bot_model.bin");
            string binJson = Path.Combine(baseDir, "models", "bot_model.json");

            if (File.Exists(binModel)) return binModel;
            if (File.Exists(binJson)) return binJson;

            return binModel;
        }

        public static IPlayer Create(PlayerKind kind, string name, IGameIO io, string modelPath = null, int mcRollouts = 20)
        {
            switch (kind)
            {
                case PlayerKind.Human:
                    return new ConsoleRealPlayer(name, io);
                case PlayerKind.Greedy:
                    return new BotPlayer(name, new GreedyBotStrategy());
                case PlayerKind.MonteCarlo:
                    return new BotPlayer(name, new MonteCarloBotStrategy
                    {
                        RolloutCount = mcRollouts,
                        ParallelRollouts = true
                    });
                case PlayerKind.MachineLearning:
                    return new BotPlayer(name, new MLBotStrategy(ResolveModelPath(modelPath)));
                default:
                    return new BotPlayer(name, new GreedyBotStrategy());
            }
        }
    }
}
