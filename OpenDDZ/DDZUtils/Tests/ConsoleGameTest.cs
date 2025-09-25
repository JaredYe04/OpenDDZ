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

            var config = new GameConfig
            {
                Dealer = new BasicDealer(RuleSet.Default),
                Players = new List<IPlayer> { new ConsoleRealPlayer("你", "player@local"), new BotPlayer("Bot1"), new BotPlayer("Bot2") },
                ShuffleMethod = list => { ShuffleUtils.RandomShuffle(list); return list; },
                DeckCount = 2
            };
            var io = new ConsoleIO();
            var controller = new GameController(config, io);
            controller.StartGame();
            controller.RunGameLoop();
        }
    }
}
