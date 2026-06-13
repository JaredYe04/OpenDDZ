using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public class TuiGameView : View
    {
        private readonly TuiGameIO _io;
        private readonly TuiGameState _state;

        private readonly Label _titleLabel = new Label("斗地主") { X = 1, Y = 0 };
        private readonly Label _statusLabel = new Label("") { X = 1, Y = 1, Width = Dim.Fill() - 2 };
        private readonly FrameView _tableFrame = new FrameView("牌桌")
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill() - (CardFaceRenderer.CardTotalHeight + 5)
        };
        private readonly View _tableInterior = new View { Width = Dim.Fill() - 2, Height = Dim.Fill() - 2 };
        private readonly SeatPanelView _upperSeat = new SeatPanelView();
        private readonly SeatPanelView _lowerSeat = new SeatPanelView();
        private readonly SeatPanelView _topSeat = new SeatPanelView { Visible = false };
        private readonly Label _messageLabel = new Label("") { X = 1, Y = 0, Width = Dim.Fill() - 2, Height = 3 };
        private readonly Label _errorLabel;

        private readonly HandCardView _handView = new HandCardView();
        private readonly StyledButton _btnPlay = new StyledButton("出牌");
        private readonly StyledButton _btnHint = new StyledButton("提示");
        private readonly StyledButton _btnPass = new StyledButton("不要");
        private readonly StyledButton _btnCantBeat = new StyledButton("要不起") { Visible = false };

        private readonly View _bidPanel = new View { X = 1, Width = Dim.Fill() - 2, Height = 1, Visible = false };
        private readonly View _discardPanel = new View { X = 1, Width = Dim.Fill() - 2, Height = 1, Visible = false };
        private readonly View _actionBar = new View { X = 1, Width = Dim.Fill() - 2, Height = 1 };

        private bool _inLayout;
        private bool _bidPanelBuilt;
        private int _lastTableHeight = -1;

        public TuiGameView(TuiGameIO io)
        {
            _io = io;
            _state = io.State;
            Width = Dim.Fill();
            Height = Dim.Fill();

            _errorLabel = new Label("") { X = 1, Width = Dim.Fill() - 2 };
            _errorLabel.ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightRed, Color.Black)
            };

            _tableFrame.ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.White, Color.Green)
            };
            _tableInterior.ColorScheme = _tableFrame.ColorScheme;
            _tableFrame.Add(_tableInterior);
            _tableInterior.Add(_upperSeat, _lowerSeat, _topSeat, _messageLabel);

            _actionBar.Add(_btnPlay, _btnHint, _btnPass, _btnCantBeat);

            Add(_titleLabel, _statusLabel, _tableFrame, _errorLabel, _actionBar, _bidPanel, _discardPanel, _handView);

            _btnPlay.Clicked += OnPlayClicked;
            _btnHint.Clicked += OnHintClicked;
            _btnPass.Clicked += OnPassClicked;
            _btnCantBeat.Clicked += OnPassClicked;

            _handView.SelectionChanged += SyncHandSelectionToState;
            _state.StateChanged += RefreshUi;
            RefreshUi();
        }

        public void Detach()
        {
            _state.StateChanged -= RefreshUi;
            _handView.SelectionChanged -= SyncHandSelectionToState;
        }

        private void SyncHandSelectionToState()
        {
            _state.SetSelection(_handView.GetSelectedIndices(), _handView.GetHintIndices());
            _handView.MarkSelectionApplied(_state.SelectionVersion);
        }

        private void OnPlayClicked()
        {
            if (_state.InputMode != TuiInputMode.WaitPlay) return;
            var cards = _handView.GetSelectedCards();
            if (cards.Count == 0)
            {
                _state.ErrorMessage = "请先选择要出的牌";
                RefreshUi();
                return;
            }
            _io.SubmitPlay(new Move(cards));
        }

        private void OnPassClicked()
        {
            if (_state.InputMode != TuiInputMode.WaitPlay) return;
            _io.SubmitPlay(null);
        }

        private void OnHintClicked()
        {
            if (_state.InputMode != TuiInputMode.WaitPlay || _state.ActivePlayer == null) return;
            var hint = _io.ComputeGreedyHint(_state.ActivePlayer);
            if (hint == null || hint.Cards == null || hint.Cards.Count == 0)
            {
                _io.SubmitPlay(null);
                return;
            }

            var sortedHand = CardRender.SortHand(_state.HandCards);
            var indices = new List<int>();
            var remaining = hint.Cards.ToList();
            for (int i = 0; i < sortedHand.Count; i++)
            {
                var c = sortedHand[i];
                int found = remaining.FindIndex(r => r.Rank == c.Rank && r.Suit == c.Suit);
                if (found >= 0)
                {
                    indices.Add(i);
                    remaining.RemoveAt(found);
                }
            }

            _state.SetSelection(null, indices);
            _handView.ApplySelectionFromState(_state.SelectionVersion, _state.SelectedIndices, _state.HintIndices);
        }

        private void RefreshUi()
        {
            if (Application.MainLoop == null) return;

            try
            {
                RefreshUiCore();
            }
            catch (Exception ex)
            {
                _errorLabel.Text = "! UI: " + ex.Message;
            }
        }

        private void RefreshUiCore()
        {
            int human = _state.HumanSeatIndex;
            int n = Math.Max(1, _state.PlayerCount);
            var seats = _state.Seats ?? new List<SeatDisplayInfo>();

            SeatDisplayInfo GetRel(int offset)
            {
                int idx = (human + offset) % n;
                return seats.FirstOrDefault(s => s.SeatIndex == idx);
            }

            // 逆时针：上家 = 上一个出牌 (human-1)，下家 = 下一个 (human+1)，对家 = human+2
            _upperSeat.SetSeat("上家", GetRel(n - 1));
            _lowerSeat.SetSeat("下家", GetRel(1));
            _topSeat.Visible = n >= 4;
            if (n >= 4)
                _topSeat.SetSeat("对家", GetRel(2));

            var current = seats.FirstOrDefault(s => s.IsCurrentTurn);
            _statusLabel.Text = current != null
                ? $"当前回合：{current.PlayerName}" + (current.IsLandlord ? " [地主]" : "")
                : "等待中…";

            _messageLabel.Text = _state.Messages.Count > 0
                ? string.Join("\n", _state.Messages.Skip(Math.Max(0, _state.Messages.Count - 3)))
                : "";

            _errorLabel.Text = string.IsNullOrEmpty(_state.ErrorMessage) ? "" : "! " + _state.ErrorMessage;

            bool waitPlay = _state.InputMode == TuiInputMode.WaitPlay;
            bool waitBid = _state.InputMode == TuiInputMode.WaitBid;
            bool waitDiscard = _state.InputMode == TuiInputMode.WaitDiscard;

            _btnPlay.Visible = waitPlay && _state.CanBeat;
            _btnHint.Visible = waitPlay && _state.CanBeat;
            _btnPass.Visible = waitPlay && _state.CanBeat;
            _btnCantBeat.Visible = waitPlay && !_state.CanBeat;

            _bidPanel.Visible = waitBid;
            _discardPanel.Visible = waitDiscard;
            _actionBar.Visible = waitPlay;

            if (waitBid)
            {
                if (!_bidPanelBuilt)
                    RebuildBidPanel();
            }
            else
            {
                _bidPanelBuilt = false;
            }
            if (waitDiscard) RebuildDiscardPanel();

            _handView.CanSelect = waitPlay || waitDiscard;
            _handView.UpdateHand(_state.HandCards);
            if (waitPlay || waitDiscard)
            {
                if (_state.SelectionVersion != _handView.GetAppliedSelectionVersion())
                    _handView.ApplySelectionEpoch(_state.SelectionVersion, _state.SelectedIndices, _state.HintIndices);
            }
            else
                _handView.ClearSelectionVisual();
            LayoutTable();
            SetNeedsDisplay();
        }

        private void LayoutTable()
        {
            if (_inLayout)
                return;
            _inLayout = true;
            try
            {
                LayoutTableCore();
            }
            finally
            {
                _inLayout = false;
            }
        }

        private void LayoutTableCore()
        {
            int viewW = Frame.Width;
            if (viewW <= 0) viewW = 80;
            int viewH = Frame.Height;
            if (viewH <= 0) viewH = 24;

            const int bottomReserve = CardFaceRenderer.CardTotalHeight + 5;
            int tableH = Math.Max(8, viewH - 2 - bottomReserve);
            if (_lastTableHeight != tableH)
            {
                _lastTableHeight = tableH;
                _tableFrame.Height = tableH;
            }

            int tableW = _tableInterior.Frame.Width;
            if (tableW <= 0) tableW = Math.Max(40, viewW - 4);

            _upperSeat.X = 0;
            _upperSeat.Y = 1;

            _lowerSeat.X = Math.Max(0, tableW - _lowerSeat.PanelWidth);
            _lowerSeat.Y = 1;

            _topSeat.X = Math.Max(0, (tableW - _topSeat.PanelWidth) / 2);
            _topSeat.Y = 0;

            _messageLabel.X = 1;
            _messageLabel.Y = Math.Max(7, tableH - 4);
            _messageLabel.Width = tableW - 2;

            int handW = _handView.ComputeRequiredWidth();
            _handView.X = Math.Max(1, (viewW - handW) / 2);
            _handView.Y = Math.Max(0, viewH - CardFaceRenderer.CardTotalHeight - 1);
            _handView.Width = Math.Max(handW + 2, 20);
            _handView.Height = CardFaceRenderer.CardTotalHeight;

            _actionBar.Y = _handView.Y - 2;
            _btnPlay.X = Math.Max(1, (viewW - 36) / 2);
            _btnHint.X = _btnPlay.X + 10;
            _btnPass.X = _btnHint.X + 10;
            _btnCantBeat.X = _btnHint.X;

            _errorLabel.Y = _actionBar.Y - 1;

            _bidPanel.Y = _actionBar.Y;
            _discardPanel.Y = _actionBar.Y;

            BringSubviewToFront(_actionBar);
            BringSubviewToFront(_bidPanel);
            BringSubviewToFront(_discardPanel);
            BringSubviewToFront(_handView);
        }

        private void RebuildBidPanel()
        {
            _bidPanel.RemoveAll();
            var opts = _state.BidOptions ?? new[] { "1分", "2分", "3分", "不叫" };
            int x = 0;
            _bidPanel.Add(new Label("请叫地主：") { X = x, Y = 0 });
            x += 12;
            foreach (var opt in opts)
            {
                var captured = opt;
                var btn = new StyledButton(opt) { X = x, Y = 0 };
                btn.Clicked += () => _io.SubmitBid(captured);
                _bidPanel.Add(btn);
                x += opt.Length + 4;
            }
            _bidPanelBuilt = true;
        }

        private void RebuildDiscardPanel()
        {
            _discardPanel.RemoveAll();
            _discardPanel.Add(new Label("后手弃牌：") { X = 0, Y = 0 });
            var skip = new StyledButton("不弃") { X = 12, Y = 0 };
            skip.Clicked += () => _io.SubmitDiscard(null);
            _discardPanel.Add(skip);

            var discard = new StyledButton("弃选中牌") { X = 24, Y = 0 };
            discard.Clicked += () =>
            {
                var sel = _handView.GetSelectedCards();
                if (sel.Count != 1)
                {
                    _state.ErrorMessage = "请选择一张要弃的牌";
                    RefreshUi();
                    return;
                }
                _io.SubmitDiscard(sel[0]);
            };
            _discardPanel.Add(discard);
        }

        public void OnTerminalResize()
        {
            _lastTableHeight = -1;
            LayoutTable();
            SetNeedsDisplay();
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            if (!_inLayout)
                LayoutTable();
        }

        public override bool ProcessKey(KeyEvent kb)
        {
            if (_state.InputMode == TuiInputMode.WaitPlay)
            {
                if (kb.Key == Key.Enter)
                {
                    OnPlayClicked();
                    return true;
                }
                if (kb.Key == Key.Esc)
                {
                    OnPassClicked();
                    return true;
                }
                if (kb.Key == (Key)'h' || kb.Key == (Key)'H')
                {
                    OnHintClicked();
                    return true;
                }
            }
            return base.ProcessKey(kb);
        }
    }
}
