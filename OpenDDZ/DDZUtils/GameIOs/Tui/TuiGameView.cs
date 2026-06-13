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
        private readonly SeatPanelView _upperSeat = new SeatPanelView();
        private readonly SeatPanelView _lowerSeat = new SeatPanelView();
        private readonly SeatPanelView _topSeat = new SeatPanelView { Visible = false };
        private readonly SeatPanelView _selfSeat = new SeatPanelView();
        private readonly Label _messageLabel = new Label("") { X = 1, Y = 0, Width = Dim.Fill() - 2, Height = 3 };
        private readonly Label _errorLabel;
        private readonly CardCounterView _cardCounter = new CardCounterView();

        private readonly HandCardView _handView = new HandCardView();
        private readonly StyledButton _btnPlay = new StyledButton("出牌");
        private readonly StyledButton _btnHint = new StyledButton("提示");
        private readonly StyledButton _btnPass = new StyledButton("不要");
        private readonly StyledButton _btnCantBeat = new StyledButton("要不起") { Visible = false };

        private readonly View _bidPanel = new View { X = 1, Width = Dim.Fill() - 2, Height = 1, Visible = false };
        private readonly View _discardPanel = new View { X = 1, Width = Dim.Fill() - 2, Height = 1, Visible = false };
        private readonly View _actionBar = new View { X = 1, Width = Dim.Fill() - 2, Height = 1 };

        private readonly StyledButton _btnDiscardSkip = new StyledButton("不弃");
        private readonly StyledButton _btnDiscardConfirm = new StyledButton("弃选中牌");

        private bool _inLayout;
        private bool _bidPanelBuilt;
        private bool _discardPanelBuilt;
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
            var seatScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.White, Color.Green)
            };
            _upperSeat.ColorScheme = seatScheme;
            _lowerSeat.ColorScheme = seatScheme;
            _topSeat.ColorScheme = seatScheme;
            _selfSeat.ColorScheme = seatScheme;
            _messageLabel.ColorScheme = seatScheme;

            _actionBar.Add(_btnPlay, _btnHint, _btnPass, _btnCantBeat);

            Add(_cardCounter, _titleLabel, _statusLabel, _tableFrame,
                _upperSeat, _lowerSeat, _topSeat, _selfSeat, _messageLabel,
                _errorLabel, _actionBar, _bidPanel, _discardPanel, _handView);

            _btnPlay.Clicked += OnPlayClicked;
            _btnHint.Clicked += OnHintClicked;
            _btnPass.Clicked += OnPassClicked;
            _btnCantBeat.Clicked += OnPassClicked;
            _btnDiscardSkip.Clicked += OnDiscardSkipClicked;
            _btnDiscardConfirm.Clicked += OnDiscardConfirmClicked;

            BuildDiscardPanelOnce();

            _handView.SelectionChanged += SyncHandSelectionToState;
            _state.StateChanged += RefreshUi;
            _cardCounter.LayoutChanged += OnCardCounterLayoutChanged;
            RefreshUi();
        }

        public void Detach()
        {
            _state.StateChanged -= RefreshUi;
            _handView.SelectionChanged -= SyncHandSelectionToState;
            _cardCounter.LayoutChanged -= OnCardCounterLayoutChanged;
        }

        private void OnCardCounterLayoutChanged()
        {
            LayoutTable();
            SetNeedsDisplay();
        }

        private void SyncHandSelectionToState()
        {
            _state.SetSelection(_handView.GetSelectedIndices(), _handView.GetHintIndices());
            _handView.MarkSelectionApplied(_state.SelectionVersion);
        }

        private void OnDiscardSkipClicked()
        {
            if (_state.InputMode != TuiInputMode.WaitDiscard) return;
            _state.ErrorMessage = "";
            _io.SubmitDiscard(null);
        }

        private void OnDiscardConfirmClicked()
        {
            if (_state.InputMode != TuiInputMode.WaitDiscard) return;
            SyncHandSelectionToState();
            var sel = _handView.GetSelectedCards();
            if (sel.Count != 1)
            {
                _state.ErrorMessage = "请选择一张要弃的牌";
                SetNeedsDisplay();
                return;
            }
            _state.ErrorMessage = "";
            _io.SubmitDiscard(sel[0]);
        }

        private void BuildDiscardPanelOnce()
        {
            if (_discardPanelBuilt) return;
            _discardPanel.RemoveAll();
            _discardPanel.Add(new Label("第三、第四顺位可弃牌：") { X = 0, Y = 0 });
            _btnDiscardSkip.X = 12;
            _btnDiscardSkip.Y = 0;
            _btnDiscardConfirm.X = 24;
            _btnDiscardConfirm.Y = 0;
            _discardPanel.Add(_btnDiscardSkip, _btnDiscardConfirm);
            _discardPanelBuilt = true;
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
            int human = _state.ViewSeatIndex;
            int n = Math.Max(1, _state.PlayerCount);
            var seats = _state.Seats ?? new List<SeatDisplayInfo>();

            SeatDisplayInfo GetRel(int offset)
            {
                int idx = (human + offset) % n;
                return seats.FirstOrDefault(s => s.SeatIndex == idx);
            }

            string SeatLabel(string baseLabel, SeatDisplayInfo info)
            {
                if (info != null && info.IsTeammate && baseLabel != "我")
                    return baseLabel + "·队友";
                return baseLabel;
            }

            // 逆时针：上家 = 上一个出牌 (human-1)，下家 = 下一个 (human+1)，对家 = human+2
            _upperSeat.SetSeat(SeatLabel("上家", GetRel(n - 1)), GetRel(n - 1));
            _lowerSeat.SetSeat(SeatLabel("下家", GetRel(1)), GetRel(1));
            _selfSeat.SetSeat("我", GetRel(0), hideHandBacks: true);
            _topSeat.Visible = n >= 4;
            if (n >= 4)
                _topSeat.SetSeat(SeatLabel("对家", GetRel(2)), GetRel(2));

            var current = seats.FirstOrDefault(s => s.IsCurrentTurn);
            _statusLabel.Text = current != null
                ? $"当前回合：{current.PlayerName}"
                  + (_state.Mode != GameMode.FourPlayer && current.IsLandlord ? " [地主]" : "")
                : "等待中…";

            _messageLabel.Text = _state.Messages.Count > 0
                ? string.Join("\n", _state.Messages.Skip(Math.Max(0, _state.Messages.Count - 3)))
                : "";

            _errorLabel.Text = string.IsNullOrEmpty(_state.ErrorMessage) ? "" : "! " + _state.ErrorMessage;

            _cardCounter.SetCounts(_state.CardCounter);

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
            if (waitDiscard)
                BuildDiscardPanelOnce();

            _handView.SingleSelectMode = waitDiscard;
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
            int topRows = Math.Max(2, _cardCounter.LayoutHeight);
            _tableFrame.Y = topRows;
            int tableH = Math.Max(8, viewH - topRows - bottomReserve);
            if (_lastTableHeight != tableH)
            {
                _lastTableHeight = tableH;
                _tableFrame.Height = tableH;
            }

            int tableW = _tableFrame.Frame.Width - 2;
            if (tableW <= 0) tableW = Math.Max(40, viewW - 4);
            int tableLeft = _tableFrame.Frame.X + 1;
            int tableTop = _tableFrame.Frame.Y + 1;

            int n = Math.Max(1, _state.PlayerCount);
            bool fourPlayer = n >= 4;

            int minSideW = CardFaceRenderer.ComputeMiniRowWidth(4) + 2;
            int maxSideW = Math.Max(minSideW, (tableW - 4) / 2);
            if (fourPlayer && _state.Seats != null)
            {
                foreach (var seat in _state.Seats)
                {
                    if (!seat.IsTeammate || seat.VisibleHandCards == null || seat.VisibleHandCards.Count == 0)
                        continue;
                    int need = CardFaceRenderer.ComputeMiniRowWidth(seat.VisibleHandCards.Count) + 2;
                    maxSideW = Math.Max(maxSideW, Math.Min(need, tableW - 4));
                }
            }
            _upperSeat.ApplyLayoutWidth(maxSideW);
            _lowerSeat.ApplyLayoutWidth(maxSideW);

            int sideSeatY = ComputeSideSeatY(tableH, fourPlayer);

            _upperSeat.X = tableLeft;
            _upperSeat.Y = tableTop + sideSeatY;

            _lowerSeat.X = tableLeft + Math.Max(0, tableW - _lowerSeat.LayoutWidth - 2);
            _lowerSeat.Y = tableTop + sideSeatY;

            _topSeat.X = tableLeft + Math.Max(0, (tableW - _topSeat.PanelWidth) / 2);
            _topSeat.Y = tableTop;

            _selfSeat.X = tableLeft + Math.Max(0, (tableW - _selfSeat.PanelWidth) / 2);
            int selfSeatY = fourPlayer
                ? Math.Min(Math.Max(sideSeatY + 8, 6), tableH - 6)
                : Math.Max(4, tableH - 12);
            _selfSeat.Y = tableTop + selfSeatY;

            _messageLabel.X = tableLeft + 1;
            _messageLabel.Y = tableTop + (fourPlayer
                ? Math.Min(tableH - 3, selfSeatY + 6)
                : Math.Max(7, tableH - 4));
            _messageLabel.Width = tableW - 2;

            _cardCounter.X = 0;
            _cardCounter.Y = 0;
            int headerX = _cardCounter.LayoutWidth + 1;
            if (headerX < 12) headerX = 12;
            _titleLabel.X = headerX;
            _titleLabel.Y = 0;
            _statusLabel.X = headerX;
            _statusLabel.Y = 1;
            _statusLabel.Width = Math.Max(10, viewW - headerX - 2);

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

            BringSubviewToFront(_upperSeat);
            BringSubviewToFront(_lowerSeat);
            BringSubviewToFront(_topSeat);
            BringSubviewToFront(_selfSeat);
            BringSubviewToFront(_messageLabel);
            BringSubviewToFront(_cardCounter);
            BringSubviewToFront(_handView);
            if (_state.InputMode == TuiInputMode.WaitPlay) BringSubviewToFront(_actionBar);
            if (_state.InputMode == TuiInputMode.WaitBid) BringSubviewToFront(_bidPanel);
            if (_state.InputMode == TuiInputMode.WaitDiscard) BringSubviewToFront(_discardPanel);
        }

        private static int ComputeSideSeatY(int tableH, bool fourPlayer)
        {
            if (!fourPlayer)
                return 1;

            // 对家：标签(0) + 手牌(2-4) + 出牌(6-8) + 牌型(9) ≈ 10 行
            const int belowTopSeat = 10;
            int maxY = Math.Max(1, tableH - 13);
            return Math.Min(belowTopSeat, maxY);
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

        private void RebuildDiscardPanel() => BuildDiscardPanelOnce();

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
            if (_state.InputMode == TuiInputMode.WaitDiscard)
            {
                if (kb.Key == Key.Enter)
                {
                    OnDiscardConfirmClicked();
                    return true;
                }
                if (kb.Key == Key.Esc)
                {
                    OnDiscardSkipClicked();
                    return true;
                }
            }
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
