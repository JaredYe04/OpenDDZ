using Newtonsoft.Json;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    internal static class TuiSettingsStore
    {
        private static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenDDZ",
                "settings.json");

        public static void Load(ConsoleGameSetup setup)
        {
            try
            {
                var path = SettingsPath;
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<TuiSettingsData>(json);
                if (data == null) return;

                setup.ApplySettings(
                    data.Mode,
                    data.Seed,
                    data.McRollouts,
                    data.SeatKinds ?? new List<PlayerKind>(),
                    data.DeckCount,
                    data.Shuffle);
            }
            catch
            {
                // ignore corrupt settings
            }
        }

        public static void Save(ConsoleGameSetup setup)
        {
            try
            {
                var path = SettingsPath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new TuiSettingsData
                {
                    Mode = setup.Mode,
                    Seed = setup.Seed,
                    McRollouts = setup.McRollouts,
                    DeckCount = setup.DeckCount,
                    Shuffle = setup.Shuffle,
                    SeatKinds = new List<PlayerKind>(setup.SeatKinds)
                };

                File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch
            {
                // ignore write failures
            }
        }

        private class TuiSettingsData
        {
            public GameMode Mode { get; set; } = GameMode.Normal;
            public int Seed { get; set; }
            public int McRollouts { get; set; } = 20;
            public int DeckCount { get; set; } = 1;
            public ShuffleKind Shuffle { get; set; } = ShuffleKind.Weak;
            public List<PlayerKind> SeatKinds { get; set; }
        }
    }
}
