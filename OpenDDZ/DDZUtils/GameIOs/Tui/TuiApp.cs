using OpenDDZ.DDZUtils.Controllers;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Terminal.Gui;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public static class TuiApp
    {
        private enum Screen
        {
            Menu,
            Settings,
            Game,
            GameEnd
        }

        public static void Run()
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            Application.Init();

            var setup = new ConsoleGameSetup();
            TuiSettingsStore.Load(setup);
            EnsureDefaultSeats(setup);

            var state = new TuiGameState();
            var io = new TuiGameIO(state);

            var win = new Window("OpenDDZ")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var container = new View { Width = Dim.Fill(), Height = Dim.Fill() };
            win.Add(container);
            Application.Top.Add(win);

            Screen current = Screen.Menu;
            View activeView = null;
            Thread gameThread = null;

            void ShowScreen(Screen screen)
            {
                if (activeView is TuiGameView oldGame)
                    oldGame.Detach();

                current = screen;
                container.RemoveAll();
                activeView = null;

                switch (screen)
                {
                    case Screen.Menu:
                        var menu = new TuiMenuView();
                        menu.StartGame += () => StartGame();
                        menu.OpenSettings += () => ShowScreen(Screen.Settings);
                        menu.ExitApp += () =>
                        {
                            Application.RequestStop();
                        };
                        activeView = menu;
                        break;

                    case Screen.Settings:
                        var settings = new TuiSettingsView(setup);
                        settings.Saved += () => ShowScreen(Screen.Menu);
                        settings.Cancelled += () => ShowScreen(Screen.Menu);
                        activeView = settings;
                        break;

                    case Screen.Game:
                        activeView = new TuiGameView(io);
                        break;

                    case Screen.GameEnd:
                        var end = new TuiGameEndView(state);
                        end.BackToMenu += () =>
                        {
                            state.InputMode = TuiInputMode.None;
                            state.Messages.Clear();
                            state.ErrorMessage = "";
                            state.Winner = null;
                            ShowScreen(Screen.Menu);
                        };
                        activeView = end;
                        break;
                }

                if (activeView != null)
                {
                    container.Add(activeView);
                    activeView.Width = Dim.Fill();
                    activeView.Height = Dim.Fill();
                }
                Application.Refresh();
            }

            void StartGame()
            {
                if (gameThread != null && gameThread.IsAlive)
                    return;

                state.InputMode = TuiInputMode.None;
                state.Messages.Clear();
                state.ErrorMessage = "";
                state.Winner = null;

                var config = setup.BuildConfig(io);
                io.SetDealer(config.Dealer);
                io.SetPlayers(config.Players);
                io.SetMode(config.Mode);
                state.PlayerCount = config.Players.Count;
                state.HumanSeatIndex = FindHumanSeat(config.Players);
                state.Mode = config.Mode;

                ShowScreen(Screen.Game);

                gameThread = new Thread(() =>
                {
                    try
                    {
                        var controller = new GameController(config, io);
                        controller.StartGame();
                        controller.RunGameLoop();
                    }
                    catch (Exception ex)
                    {
                        io.ShowError("游戏异常: " + ex.Message);
                        state.AddMessage("[异常] " + ex.Message);
                    }
                    finally
                    {
                        Application.MainLoop?.Invoke(() =>
                        {
                            if ((state.InputMode == TuiInputMode.GameEnded || state.Winner != null)
                                && current != Screen.GameEnd)
                                ShowScreen(Screen.GameEnd);
                        });
                    }
                })
                {
                    IsBackground = true,
                    Name = "OpenDDZ-GameThread"
                };
                gameThread.Start();
            }

            state.StateChanged += () =>
            {
                if (state.InputMode == TuiInputMode.GameEnded && current == Screen.Game)
                    Application.MainLoop?.Invoke(() => ShowScreen(Screen.GameEnd));
            };

            Application.Resized += _ =>
            {
                if (activeView != null)
                {
                    activeView.Width = Dim.Fill();
                    activeView.Height = Dim.Fill();
                }
                if (activeView is TuiGameView gameView)
                    gameView.OnTerminalResize();
                Application.Refresh();
            };

            ShowScreen(Screen.Menu);
            Application.Run();
            Application.Shutdown();
        }

        private static void EnsureDefaultSeats(ConsoleGameSetup setup)
        {
            if (setup.SeatKinds.Count == 0)
            {
                setup.SeatKinds.Add(PlayerKind.Human);
                setup.SeatKinds.Add(PlayerKind.MonteCarlo);
                setup.SeatKinds.Add(PlayerKind.MonteCarlo);
            }
        }

        private static int FindHumanSeat(IList<IPlayer> players)
        {
            for (int i = 0; i < players.Count; i++)
                if (players[i] is RealPlayer)
                    return i;
            return 0;
        }
    }
}
