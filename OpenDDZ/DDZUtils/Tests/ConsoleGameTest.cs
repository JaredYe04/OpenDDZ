using OpenDDZ;
using OpenDDZ.DDZUtils.Controllers;
using OpenDDZ.DDZUtils.GameIOs;
using OpenDDZ.DDZUtils.GameIOs.Tui;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Linq;
using System.Text;

namespace OpenDDZ.DDZUtils.Tests
{
    internal class ConsoleGameTest
    {
        public static void Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;
            }
            catch { /* 部分终端不支持 */ }
            if (args != null && args.Any(a => a == "--stdio"))
            {
                ProgramStdio.RunStdioMode();
                return;
            }
            if (args != null && args.Any(a => a == "--benchmark"))
            {
                BotBenchmarkTest.Run(args);
                return;
            }
            if (args != null && args.Any(a => a == "--generate-dataset" || a == "--merge-shards"))
            {
                TrainingDataCli.Run(args);
                return;
            }
            if (args != null && args.Any(a => a == "--self-play"))
            {
                SelfPlayCli.Run(args);
                return;
            }
            if (args != null && args.Any(a => a == "--mine-selfplay"))
            {
                SelfPlayMinerCli.Run(args);
                return;
            }
            if (args != null && args.Any(a => a == "--classic"))
            {
                RunClassicMode();
                return;
            }

            TuiApp.Run();
        }

        private static void RunClassicMode()
        {
            var setup = new ConsoleGameSetup();
            if (!setup.RunInteractive())
            {
                Console.WriteLine("已取消。");
                return;
            }

            var io = new ConsoleIO();
            var config = setup.BuildConfig(io);
            var controller = new GameController(config, io);
            controller.StartGame();
            controller.RunGameLoop();
        }
    }
}
