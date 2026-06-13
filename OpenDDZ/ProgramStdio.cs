using Newtonsoft.Json.Linq;
using OpenDDZ.DDZUtils.Controllers;
using OpenDDZ.DDZUtils.Dealers;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.GameIOs;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players;
using OpenDDZ.DDZUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenDDZ
{
    /// <summary>
    /// Stdio mode entry: --stdio then read "start" JSON, run game loop.
    /// </summary>
    public static class ProgramStdio
    {
        private static string SafeExceptionString(Exception ex)
        {
            try
            {
                var typeName = ex?.GetType()?.FullName ?? "Unknown";
                var msg = "";
                try { msg = ex?.Message ?? ""; } catch { msg = "(Message get failed)"; }
                return typeName + ": " + msg;
            }
            catch
            {
                return "Exception (safe string failed)";
            }
        }

        public static void RunStdioMode()
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;
            }
            catch { }

            try
            {
                var io = new StdioGameIO(Console.Out, Console.In);
                io.EmitReady();

                string startLine = null;
                try
                {
                    startLine = Console.In.ReadLine();
                }
                catch (Exception ex)
                {
                    io.EmitError(SafeExceptionString(ex));
                    return;
                }

                if (string.IsNullOrWhiteSpace(startLine))
                {
                    io.EmitError("Missing start command (engine received EOF or empty line)");
                    return;
                }

                JObject startJson;
                try
                {
                    startJson = JObject.Parse(startLine);
                }
                catch (Exception ex)
                {
                    io.EmitError("Invalid start JSON: " + SafeExceptionString(ex));
                    return;
                }

                if (startJson["cmd"]?.ToString() != "start")
                {
                    io.EmitError("Expected start command");
                    return;
                }

                var mode = startJson["mode"]?.ToString() ?? "Normal";
                var deckCount = startJson["deckCount"]?.Value<int>() ?? 1;
                var playerCount = startJson["playerCount"]?.Value<int>() ?? 3;
                int seed;
                try
                {
                    var seedToken = startJson["seed"];
                    if (seedToken == null)
                        seed = (int)(DateTime.Now.Ticks % int.MaxValue);
                    else
                    {
                        var seedLong = seedToken.Value<long>();
                        seed = (int)((ulong)seedLong % (ulong)int.MaxValue);
                        if (seed < 0) seed = -seed;
                    }
                }
                catch (OverflowException)
                {
                    seed = (int)(DateTime.Now.Ticks % int.MaxValue);
                    if (seed < 0) seed = -seed;
                }
                catch
                {
                    seed = (int)(DateTime.Now.Ticks % int.MaxValue);
                    if (seed < 0) seed = -seed;
                }
                var playersConfig = startJson["players"] as JArray;

                if (playerCount != 3 && playerCount != 4)
                {
                    io.EmitError("playerCount must be 3 or 4");
                    return;
                }

                if (mode == "FourPlayer" && playerCount != 4)
                {
                    playerCount = 4;
                }

                var config = BuildConfig(io, mode, deckCount, playerCount, seed, playersConfig);
                if (config == null)
                {
                    io.EmitError("Failed to build config");
                    return;
                }

                io.SetPlayerIndexMap(config.Players);
                io.StartStdinReader();

                var controller = new GameController(config, io);
                controller.StartGame();
                controller.RunGameLoop();

                io.Stop();
            }
            catch (Exception ex)
            {
                var safeEx = SafeExceptionString(ex);
                try
                {
                    Console.Error.WriteLine("Engine error: " + safeEx);
                    Console.Error.Flush();
                }
                catch { }
                try
                {
                    var io = new StdioGameIO(Console.Out, Console.In);
                    io.EmitError("Engine exception: " + safeEx);
                }
                catch { }
            }
        }

        private static GameConfig BuildConfig(StdioGameIO io, string mode, int deckCount, int playerCount, int seed, JArray playersConfig)
        {
            IDealer dealer = (mode == "FourPlayer")
                ? (IDealer)new FourPlayerDealer(RuleSet.Default)
                : new BasicDealer(RuleSet.Default);
            var players = new List<IPlayer>();
            for (int i = 0; i < playerCount; i++)
            {
                bool human = true;
                if (playersConfig != null && i < playersConfig.Count)
                {
                    var p = playersConfig[i] as JObject;
                    if (p != null && p["human"] != null)
                        human = p["human"].Value<bool>();
                }
                if (human)
                    players.Add(new ConsoleRealPlayer("P" + i, io));
                else
                    players.Add(new BotPlayer("Bot" + i));
            }

            if (mode == "FourPlayer")
            {
                deckCount = Math.Max(2, deckCount);
                playerCount = 4;
            }
            return new GameConfig
            {
                DeckCount = deckCount,
                Seed = seed,
                Dealer = dealer,
                Players = players,
                ShuffleMethod = list => { ShuffleUtils.WeakShuffle(list, seed); return list; },
                EnableLandlord = true,
                Mode = mode == "FourPlayer" ? GameMode.FourPlayer : GameMode.Normal
            };
        }
    }
}
