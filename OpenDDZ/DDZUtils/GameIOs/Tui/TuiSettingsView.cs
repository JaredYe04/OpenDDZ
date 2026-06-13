using NStack;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using Terminal.Gui;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public class TuiSettingsView : View
    {
        private readonly ConsoleGameSetup _setup;
        private readonly List<RadioGroup> _seatGroups = new List<RadioGroup>();

        public event Action Saved;
        public event Action Cancelled;

        private readonly RadioGroup _modeGroup;
        private readonly TextField _seedField;
        private readonly TextField _mcField;
        private readonly TextField _deckField;
        private readonly RadioGroup _shuffleGroup;
        private readonly View _seatPanel;
        private bool _deckCountManual;

        private static ustring[] UStrings(params string[] items)
        {
            var arr = new ustring[items.Length];
            for (int i = 0; i < items.Length; i++)
                arr[i] = (ustring)items[i];
            return arr;
        }

        public TuiSettingsView(ConsoleGameSetup setup)
        {
            _setup = setup;
            Width = Dim.Fill();
            Height = Dim.Fill();

            Add(new Label("游戏参数设置") { X = 2, Y = 1 });

            Add(new Label("模式：") { X = 2, Y = 3 });
            _modeGroup = new RadioGroup(UStrings("3人斗地主", "4人2v2"))
            {
                X = 10,
                Y = 3,
                SelectedItem = setup.Mode == GameMode.FourPlayer ? 1 : 0
            };
            _modeGroup.SelectedItemChanged += _ => OnModeChanged();
            Add(_modeGroup);

            _seatPanel = new View { X = 2, Y = 5, Width = Dim.Fill() - 4, Height = 6 };
            Add(_seatPanel);
            RebuildSeatPanel();

            Add(new Label("牌副数：") { X = 2, Y = 12 });
            _deckField = new TextField(setup.DeckCount.ToString()) { X = 10, Y = 12, Width = 6 };
            _deckField.TextChanged += _ => _deckCountManual = true;
            Add(_deckField);

            Add(new Label("洗牌规则：") { X = 2, Y = 14 });
            _shuffleGroup = new RadioGroup(UStrings("标准随机", "弱洗牌(大牌组合)"))
            {
                X = 12,
                Y = 14,
                SelectedItem = setup.Shuffle == ShuffleKind.Weak ? 1 : 0
            };
            Add(_shuffleGroup);

            Add(new Label("随机种子（空=随机）：") { X = 2, Y = 16 });
            _seedField = new TextField("") { X = 24, Y = 16, Width = 20 };
            if (setup.Seed != 0) _seedField.Text = setup.Seed.ToString();
            Add(_seedField);

            Add(new Label("MC rollout：") { X = 2, Y = 18 });
            _mcField = new TextField(setup.McRollouts.ToString()) { X = 16, Y = 18, Width = 8 };
            Add(_mcField);

            var btnSave = new StyledButton("保存") { X = 2, Y = 20 };
            btnSave.Clicked += SaveAndClose;
            var btnCancel = new StyledButton("返回") { X = 14, Y = 20 };
            btnCancel.Clicked += () => Cancelled?.Invoke();
            Add(btnSave, btnCancel);
        }

        private void OnModeChanged()
        {
            RebuildSeatPanel();
            if (!_deckCountManual)
            {
                var mode = _modeGroup.SelectedItem == 1 ? GameMode.FourPlayer : GameMode.Normal;
                _deckField.Text = ConsoleGameSetup.DefaultDeckCount(mode).ToString();
            }
        }

        private void RebuildSeatPanel()
        {
            _seatPanel.RemoveAll();
            _seatGroups.Clear();
            int count = _modeGroup.SelectedItem == 1 ? 4 : 3;
            EnsureSeatKinds(count);

            for (int i = 0; i < count; i++)
            {
                _seatPanel.Add(new Label($"座位{i}：") { X = 0, Y = i, Width = 8 });
                int selected = (int)_setup.SeatKinds[i] - 1;
                if (selected < 0) selected = 0;
                var rg = new RadioGroup(UStrings("真人", "贪心", "蒙特卡洛", "机器学习"))
                {
                    X = 8,
                    Y = i,
                    SelectedItem = selected
                };
                _seatGroups.Add(rg);
                _seatPanel.Add(rg);
            }
        }

        private void EnsureSeatKinds(int count)
        {
            while (_setup.SeatKinds.Count < count)
                _setup.SeatKinds.Add(_setup.SeatKinds.Count == 0 ? PlayerKind.Human : PlayerKind.MonteCarlo);
            while (_setup.SeatKinds.Count > count)
                _setup.SeatKinds.RemoveAt(_setup.SeatKinds.Count - 1);
        }

        private void SaveAndClose()
        {
            var mode = _modeGroup.SelectedItem == 1 ? GameMode.FourPlayer : GameMode.Normal;
            int count = mode == GameMode.FourPlayer ? 4 : 3;
            EnsureSeatKinds(count);

            var seats = new List<PlayerKind>();
            for (int i = 0; i < count; i++)
            {
                int sel = _seatGroups.Count > i ? _seatGroups[i].SelectedItem : 2;
                seats.Add((PlayerKind)(sel + 1));
            }

            var seedText = _seedField.Text?.ToString()?.Trim();
            int seed = 0;
            if (!string.IsNullOrEmpty(seedText) && int.TryParse(seedText, out int s))
                seed = s;

            int mc = int.TryParse(_mcField.Text?.ToString(), out int m) ? m : 20;

            int deck = int.TryParse(_deckField.Text?.ToString(), out int d) && d > 0
                ? d
                : ConsoleGameSetup.DefaultDeckCount(mode);

            var shuffle = _shuffleGroup.SelectedItem == 1 ? ShuffleKind.Weak : ShuffleKind.Random;

            _setup.ApplySettings(mode, seed, mc, seats, deck, shuffle);
            TuiSettingsStore.Save(_setup);
            Saved?.Invoke();
        }
    }
}
