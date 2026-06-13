using OpenDDZ.DDZUtils;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Terminal.Gui;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public class TuiGameIO : IGameIO
    {
        private readonly TuiGameState _state;
        private IDealer _dealer;
        private List<IPlayer> _players = new List<IPlayer>();
        private RuleSet _rules = RuleSet.Default;

        private readonly object _playLock = new object();
        private Move _pendingMove;
        private bool _playDone;

        private readonly object _bidLock = new object();
        private string _pendingBid;
        private bool _bidDone;

        private readonly object _discardLock = new object();
        private Card _pendingDiscard;
        private bool _discardDone;

        public TuiGameIO(TuiGameState state)
        {
            _state = state;
        }

        public void SetDealer(IDealer dealer)
        {
            _dealer = dealer;
            _rules = dealer?.Rules ?? RuleSet.Default;
        }

        public void SetPlayers(IList<IPlayer> players)
        {
            _players = players?.ToList() ?? new List<IPlayer>();
            _state.AllPlayers = _players;
            _state.PlayerCount = _players.Count;
            _state.HumanSeatIndex = FindHumanSeatIndex();
        }

        public void SetMode(GameMode mode) => _state.Mode = mode;

        public TuiGameState State => _state;

        public void ShowMessage(string message)
        {
            InvokeUi(() =>
            {
                _state.AddMessage(message);
                RefreshSnapshot(null);
            });
        }

        private void ReleaseAllWaits()
        {
            lock (_playLock)
            {
                _playDone = true;
                _pendingMove = null;
                Monitor.PulseAll(_playLock);
            }
            lock (_bidLock)
            {
                _bidDone = true;
                Monitor.PulseAll(_bidLock);
            }
            lock (_discardLock)
            {
                _discardDone = true;
                Monitor.PulseAll(_discardLock);
            }
        }

        public void ShowHand(IPlayer player)
        {
            InvokeUi(() =>
            {
                if (player == null) return;
                _state.HandCards = CardRender.SortHand(player.GetHandCards());
                RefreshSnapshot(player);
            });
        }

        public void ShowLastMove(IPlayer player, Move move, IPlayer lastPlayer)
        {
            InvokeUi(() =>
            {
                _state.IsFirstHand = move == null || move.Cards == null || move.Cards.Count == 0
                    || lastPlayer == player;
                _state.EffectiveLastMove = _state.IsFirstHand ? null : move;
                _state.LastMovePlayer = lastPlayer;
                UpdateCanBeat(player);
                RefreshSnapshot(player);
            });
        }

        public Move GetMoveInput(IPlayer player)
        {
            lock (_playLock)
            {
                _playDone = false;
                _pendingMove = null;
            }

            InvokeUi(() =>
            {
                _state.InputMode = TuiInputMode.WaitPlay;
                _state.ActivePlayer = player;
                _state.HumanSeatIndex = _dealer?.GetPlayerIndex(player) ?? _state.HumanSeatIndex;
                _state.ErrorMessage = "";
                _state.ClearSelection();
                _state.HandCards = CardRender.SortHand(player.GetHandCards());
                UpdateCanBeat(player);
                RefreshSnapshot(player);
            });

            WaitPlayInput();
            Move result;
            lock (_playLock)
            {
                result = _pendingMove;
                _playDone = false;
            }

            InvokeUi(() =>
            {
                if (_state.InputMode != TuiInputMode.GameEnded)
                {
                    _state.InputMode = TuiInputMode.None;
                    _state.NotifyChanged();
                }
            });
            return result;
        }

        public string GetBidInput(IPlayer player)
        {
            lock (_bidLock)
            {
                _bidDone = false;
                _pendingBid = "不叫";
            }

            InvokeUi(() =>
            {
                _state.HandCards = CardRender.SortHand(player.GetHandCards());
                _state.InputMode = TuiInputMode.WaitBid;
                _state.ActivePlayer = player;
                _state.HumanSeatIndex = _dealer?.GetPlayerIndex(player) ?? _state.HumanSeatIndex;
                _state.ErrorMessage = "";
                RefreshSnapshot(player);
            });

            WaitBidInput();
            string result;
            lock (_bidLock)
            {
                result = _pendingBid ?? "不叫";
                _bidDone = false;
            }

            InvokeUi(() =>
            {
                if (_state.InputMode != TuiInputMode.GameEnded)
                {
                    _state.InputMode = TuiInputMode.None;
                    _state.NotifyChanged();
                }
            });
            return result;
        }

        public Card GetDiscardInput(IPlayer player)
        {
            lock (_discardLock)
            {
                _discardDone = false;
                _pendingDiscard = null;
            }

            InvokeUi(() =>
            {
                _state.InputMode = TuiInputMode.WaitDiscard;
                _state.ActivePlayer = player;
                _state.HandCards = CardRender.SortHand(player.GetHandCards());
                _state.ClearSelection();
                RefreshSnapshot(player);
            });

            WaitDiscardInput();
            Card result;
            lock (_discardLock)
            {
                result = _pendingDiscard;
                _discardDone = false;
            }

            InvokeUi(() =>
            {
                if (_state.InputMode != TuiInputMode.GameEnded)
                {
                    _state.InputMode = TuiInputMode.None;
                    _state.NotifyChanged();
                }
            });
            return result;
        }

        public void ShowError(string message)
        {
            InvokeUi(() =>
            {
                _state.ErrorMessage = message;
                _state.AddMessage("[错误] " + message);
                _state.NotifyChanged();
            });
        }

        public void ShowGameEnd(IPlayer winner)
        {
            ReleaseAllWaits();
            InvokeUi(() =>
            {
                try
                {
                    _state.Winner = winner;
                    _state.Landlord = _dealer?.CurrentGame?.Landlord;
                    _state.InputMode = TuiInputMode.GameEnded;
                    _state.AllPlayers = _players;
                    if (_dealer != null && _players.Count > 0)
                        RefreshSnapshot(null);
                    _state.NotifyChanged();
                }
                catch (Exception ex)
                {
                    _state.ErrorMessage = "结算异常: " + ex.Message;
                    _state.NotifyChanged();
                }
            });
        }

        public void EmitPlayRejected(string reason)
        {
            InvokeUi(() =>
            {
                _state.ErrorMessage = reason;
                _state.InputMode = TuiInputMode.WaitPlay;
                _state.NotifyChanged();
            });
        }

        public void SubmitPlay(Move move)
        {
            lock (_playLock)
            {
                if (_playDone) return;
                _pendingMove = move;
                _playDone = true;
                Monitor.PulseAll(_playLock);
            }
        }

        public void SubmitBid(string bid)
        {
            lock (_bidLock)
            {
                if (_bidDone) return;
                _pendingBid = bid ?? "不叫";
                _bidDone = true;
                Monitor.PulseAll(_bidLock);
            }
        }

        public void SubmitDiscard(Card card)
        {
            lock (_discardLock)
            {
                if (_discardDone) return;
                _pendingDiscard = card;
                _discardDone = true;
                Monitor.PulseAll(_discardLock);
            }
        }

        public Move ComputeGreedyHint(IPlayer player)
        {
            if (player == null || _dealer == null) return null;
            var hand = player.GetHandCards().ToList();
            Move last = _state.EffectiveLastMove;
            if (_state.IsFirstHand) last = null;
            return CardUtils.FindGreedyBestMove(hand, last, _rules);
        }

        private void WaitPlayInput()
        {
            lock (_playLock)
            {
                while (!_playDone)
                    Monitor.Wait(_playLock);
            }
        }

        private void WaitBidInput()
        {
            lock (_bidLock)
            {
                while (!_bidDone)
                    Monitor.Wait(_bidLock);
            }
        }

        private void WaitDiscardInput()
        {
            lock (_discardLock)
            {
                while (!_discardDone)
                    Monitor.Wait(_discardLock);
            }
        }

        private void UpdateCanBeat(IPlayer player)
        {
            var hint = ComputeGreedyHint(player);
            _state.CanBeat = _state.IsFirstHand || hint != null;
        }

        private void RefreshSnapshot(IPlayer perspective)
        {
            if (_dealer == null || _players.Count == 0)
            {
                _state.NotifyChanged();
                return;
            }

            int viewSeat = perspective != null
                ? _dealer.GetPlayerIndex(perspective)
                : _state.HumanSeatIndex;
            if (viewSeat < 0) viewSeat = _state.HumanSeatIndex;

            var lastBySeat = BuildLastMovesBySeat();
            int current = _dealer.GetCurrentPlayerIndex();
            int landlordIdx = _dealer.LandlordIndex;

            _state.Seats = new List<SeatDisplayInfo>();
            for (int i = 0; i < _players.Count; i++)
            {
                int rel = RelativeSeat(viewSeat, i, _players.Count);
                var p = _players[i];
                var info = new SeatDisplayInfo
                {
                    SeatIndex = i,
                    PlayerName = p.Name + (p is RealPlayer ? "" : " [Bot]"),
                    HandCount = p.GetHandCards().Count,
                    IsLandlord = i == landlordIdx,
                    IsCurrentTurn = i == current
                };

                if (lastBySeat.TryGetValue(i, out var seatMove))
                {
                    info.IsPass = seatMove.IsPass;
                    info.LastCards = seatMove.Cards ?? new List<Card>();
                    info.MoveKindLabel = seatMove.KindLabel;
                }

                _state.Seats.Add(info);
            }

            _state.NotifyChanged();
        }

        private Dictionary<int, (bool IsPass, List<Card> Cards, string KindLabel)> BuildLastMovesBySeat()
        {
            var result = new Dictionary<int, (bool, List<Card>, string)>();
            var moves = _dealer?.CurrentGame?.Moves;
            if (moves == null) return result;

            foreach (var entry in moves)
            {
                int idx = _dealer.GetPlayerIndex(entry.player);
                if (idx < 0) continue;
                if (entry.move == null || entry.move.Cards == null || entry.move.Cards.Count == 0)
                    result[idx] = (true, new List<Card>(), "不出");
                else
                    result[idx] = (false, entry.move.Cards.ToList(),
                        MoveKindFormatter.Format(entry.move, _rules));
            }
            return result;
        }

        private static int RelativeSeat(int mySeat, int seat, int n)
        {
            return (seat - mySeat + n) % n;
        }

        private int FindHumanSeatIndex()
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i] is RealPlayer)
                    return i;
            return 0;
        }

        private void InvokeUi(Action action)
        {
            if (Application.MainLoop == null)
                return;
            Application.MainLoop.Invoke(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _state.ErrorMessage = "UI异常: " + ex.Message;
                    _state.AddMessage("[UI异常] " + ex.Message);
                }
            });
        }
    }
}
