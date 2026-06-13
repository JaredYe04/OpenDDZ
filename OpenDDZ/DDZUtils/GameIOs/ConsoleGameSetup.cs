using OpenDDZ.DDZUtils;
using OpenDDZ.DDZUtils.Dealers;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;

namespace OpenDDZ.DDZUtils.GameIOs
{
    public enum ShuffleKind
    {
        Random,
        Weak
    }

    public class ConsoleGameSetup
    {
        public GameMode Mode { get; private set; } = GameMode.Normal;
        public int Seed { get; private set; }
        public int DeckCount { get; private set; } = 1;
        public int McRollouts { get; private set; } = 20;
        public ShuffleKind Shuffle { get; private set; } = ShuffleKind.Weak;
        public List<PlayerKind> SeatKinds { get; } = new List<PlayerKind>();

        public static int DefaultDeckCount(GameMode mode) =>
            mode == GameMode.FourPlayer ? 2 : 1;

        /// <summary>0 表示每次开局使用当前时间随机。</summary>
        public int ResolveGameSeed() =>
            Seed != 0 ? Seed : (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);

        public void ApplySettings(
            GameMode mode,
            int seed,
            int mcRollouts,
            IList<PlayerKind> seatKinds,
            int deckCount,
            ShuffleKind shuffle)
        {
            Mode = mode;
            Seed = seed;
            McRollouts = mcRollouts > 0 ? mcRollouts : 20;
            Shuffle = shuffle;

            SeatKinds.Clear();
            if (seatKinds != null)
                SeatKinds.AddRange(seatKinds);

            DeckCount = deckCount > 0 ? deckCount : DefaultDeckCount(mode);
        }

        public bool RunInteractive()
        {
            Console.WriteLine("=== OpenDDZ 游戏设置 ===");
            Console.Write("1. 模式 [1=3人斗地主 2=4人2v2] (默认1): ");
            var modeInput = Console.ReadLine()?.Trim();
            Mode = modeInput == "2" ? GameMode.FourPlayer : GameMode.Normal;

            int playerCount = Mode == GameMode.FourPlayer ? 4 : 3;
            DeckCount = DefaultDeckCount(Mode);

            for (int i = 0; i < playerCount; i++)
            {
                Console.Write($"2.{i} 座位{i} [1=真人 2=贪心 3=蒙特卡洛 4=机器学习] (默认3): ");
                SeatKinds.Add(ParseKind(Console.ReadLine(), defaultKind: i == 0 ? PlayerKind.Human : PlayerKind.MonteCarlo));
            }

            Console.Write("3. 随机种子 [回车=随机]: ");
            var seedInput = Console.ReadLine()?.Trim();
            Seed = string.IsNullOrEmpty(seedInput) ? 0 : int.Parse(seedInput);

            Console.Write("4. MC rollout 次数 [回车=20]: ");
            var mcInput = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(mcInput) && int.TryParse(mcInput, out int mc))
                McRollouts = mc;

            Console.WriteLine($"ML 模型：{PlayerFactory.DefaultModelPath}");

            Console.Write("确认并开始？(Y/n): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            return confirm != "n" && confirm != "no";
        }

        public GameConfig BuildConfig(IGameIO io)
        {
            var players = new List<IPlayer>();
            for (int i = 0; i < SeatKinds.Count; i++)
            {
                string name = SeatKinds[i] == PlayerKind.Human ? $"玩家{i}" : $"Bot{i}";
                players.Add(PlayerFactory.Create(SeatKinds[i], name, io, null, McRollouts));
            }

            IDealer dealer = Mode == GameMode.FourPlayer
                ? (IDealer)new FourPlayerDealer(RuleSet.Default)
                : new BasicDealer(RuleSet.Default);

            int seed = ResolveGameSeed();
            Func<IList<Card>, IList<Card>> shuffleMethod = Shuffle == ShuffleKind.Random
                ? (Func<IList<Card>, IList<Card>>)(list => { ShuffleUtils.RandomShuffle(list, seed); return list; })
                : (Func<IList<Card>, IList<Card>>)(list => { ShuffleUtils.WeakShuffle(list, seed); return list; });

            return new GameConfig
            {
                Dealer = dealer,
                Players = players,
                Seed = seed,
                DeckCount = DeckCount,
                Mode = Mode,
                EnableLandlord = true,
                ShuffleMethod = shuffleMethod
            };
        }

        private static PlayerKind ParseKind(string input, PlayerKind defaultKind)
        {
            switch (input?.Trim())
            {
                case "1": return PlayerKind.Human;
                case "2": return PlayerKind.Greedy;
                case "3": return PlayerKind.MonteCarlo;
                case "4": return PlayerKind.MachineLearning;
                case "": return defaultKind;
                default: return defaultKind;
            }
        }
    }
}
