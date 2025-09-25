using OpenDDZ.DDZUtils.Controllers;
using OpenDDZ.DDZUtils.Dealers;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.GameIOs;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Tests
{
    internal class ConsoleGameTest
    {

        public static void Main(string[] args)
        {
            int seed = (int)DateTime.Now.Ticks;
            //var config = new GameConfig
            //{
            //    Dealer = new BasicDealer(RuleSet.Default),
            //    Players = new List<IPlayer> { new ConsoleRealPlayer("玩家1"), new BotPlayer("Bot1"), new BotPlayer("Bot2"), new ConsoleRealPlayer("玩家2") },
            //    Seed=seed,
            //    ShuffleMethod = list => { ShuffleUtils.RandomShuffle(list, seed); return list; },
            //    DeckCount = 2
            //};
            var config = new GameConfig
            {
                Dealer = new BasicDealer(RuleSet.Default),
                Players = new List<IPlayer> { new ConsoleRealPlayer("玩家1"), new BotPlayer("Bot1"), new BotPlayer("Bot2")},
                Seed = seed,
                ShuffleMethod = list => { ShuffleUtils.WeakShuffle(list, seed); return list; },
                DeckCount = 1
            };
            var io = new ConsoleIO();
            var controller = new GameController(config, io);
            controller.StartGame();
            controller.RunGameLoop();
        }
    }
}
