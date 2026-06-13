using OpenDDZ.DDZUtils;
using OpenDDZ.DDZUtils.Dealers;
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

        private readonly Random _botDelayRng = new Random();

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
            int humanSeat = FindHumanSeatIndex();
            _state.HumanSeatIndex = humanSeat;
            _state.ViewSeatIndex = humanSeat;
        }

        public void SetMode(GameMode mode) => _state.Mode = mode;

        public TuiGameState State => _state;

        public void ShowMessage(string message)
        {
            InvokeUi(() =>
            {
                _state.AddMessage(message);
                SyncHumanHandFromPlayer();
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
            InvokeUiAndWait(() =>
            {
                if (player == null) return;
                SyncHumanHandFromPlayer(player);
                RefreshSnapshot();
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
                SyncHumanHandFromPlayer();
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
                _state.ErrorMessage = "";
                _state.ClearSelection();
                SyncHumanHandFromPlayer(player);
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
                SyncHumanHandFromPlayer(player);
                _state.InputMode = TuiInputMode.WaitBid;
                _state.ActivePlayer = player;
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
                _state.ErrorMessage = "";
                SyncHumanHandFromPlayer(player);
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
            InvokeUiAndWait(() =>
            {
                _state.ErrorMessage = reason;
                _state.InputMode = TuiInputMode.WaitPlay;
                SyncHumanHandFromPlayer(_state.ActivePlayer as RealPlayer);
                RefreshSnapshot();
                _state.NotifyChanged();
            });
        }

        public void BeforeBotPlay(IPlayer player)
        {
            if (player == null || player is RealPlayer)
                return;

            InvokeUiAndWait(() =>
            {
                SyncHumanHandFromPlayer();
                if (_dealer != null && _players.Count > 0)
                    RefreshSnapshot(null);
            });

            int delayMs = _botDelayRng.Next(1000, 5001);
            Thread.Sleep(delayMs);
        }

        private void InvokeUiAndWait(Action action)
        {
            if (Application.MainLoop == null)
            {
                action();
                return;
            }
            using (var done = new ManualResetEventSlim(false))
            {
                Application.MainLoop.Invoke(() =>
                {
                    try { action(); }
                    catch (Exception ex)
                    {
                        _state.ErrorMessage = "UI异常: " + ex.Message;
                        _state.AddMessage("[UI异常] " + ex.Message);
                    }
                    finally { done.Set(); }
                });
                done.Wait(3000);
            }
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

            InvokeUiAndWait(() =>
            {
                if (_state.ActivePlayer is RealPlayer && move?.Cards != null && move.Cards.Count > 0)
                {
                    var remaining = _state.HandCards.ToList();
                    foreach (var played in move.Cards)
                    {
                        int idx = remaining.FindIndex(c => c.Rank == played.Rank && c.Suit == played.Suit);
                        if (idx >= 0) remaining.RemoveAt(idx);
                    }
                    _state.HandCards = remaining;
                    _state.ClearSelection();
                }
                RefreshSnapshot();
                _state.NotifyChanged();
            });
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

        private void RefreshSnapshot(IPlayer perspective = null)
        {
            if (_dealer == null || _players.Count == 0)
            {
                _state.NotifyChanged();
                return;
            }

            int viewSeat = _state.ViewSeatIndex;
            if (viewSeat < 0)
                viewSeat = FindHumanSeatIndex();

            CaptureFourPlayerTeams();

            var lastBySeat = BuildLastMovesBySeat();
            int current = _dealer.GetCurrentPlayerIndex();
            int landlordIdx = _dealer.LandlordIndex;

            _state.Seats = new List<SeatDisplayInfo>();
            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                bool isTeammate = _state.Mode == GameMode.FourPlayer
                    && i != viewSeat
                    && IsSameFourPlayerTeam(viewSeat, i);
                var info = new SeatDisplayInfo
                {
                    SeatIndex = i,
                    PlayerName = p.Name + (p is RealPlayer ? "" : " [Bot]"),
                    HandCount = p.GetHandCards().Count,
                    IsLandlord = _state.Mode != GameMode.FourPlayer && i == landlordIdx,
                    IsCurrentTurn = i == current,
                    IsTeammate = isTeammate
                };

                if (isTeammate)
                    info.VisibleHandCards = CardRender.SortHand(p.GetHandCards().ToList());

                if (lastBySeat.TryGetValue(i, out var seatMove))
                {
                    info.IsPass = seatMove.IsPass;
                    info.LastCards = seatMove.Cards ?? new List<Card>();
                    info.MoveKindLabel = seatMove.KindLabel;
                }

                _state.Seats.Add(info);
            }

            UpdateCardCounter(viewSeat);
            _state.NotifyChanged();
        }

        private void CaptureFourPlayerTeams()
        {
            if (_state.Mode != GameMode.FourPlayer)
                return;

            if (_dealer is IFourPlayerTeamInfo teamInfo)
            {
                if (_state.FourPlayerTeamIds == null || _state.FourPlayerTeamIds.Length != _players.Count)
                    _state.FourPlayerTeamIds = new int[_players.Count];
                for (int i = 0; i < _players.Count; i++)
                    _state.FourPlayerTeamIds[i] = teamInfo.GetTeamId(i);
            }
        }

        private bool IsSameFourPlayerTeam(int a, int b)
        {
            if (_state.FourPlayerTeamIds == null || a < 0 || b < 0
                || a >= _state.FourPlayerTeamIds.Length || b >= _state.FourPlayerTeamIds.Length)
            {
                if (_dealer is IFourPlayerTeamInfo teamInfo)
                    return teamInfo.SameTeam(a, b);
                return false;
            }
            return _state.FourPlayerTeamIds[a] == _state.FourPlayerTeamIds[b];
        }

        private void SyncHumanHandFromPlayer(IPlayer player = null)
        {
            var human = player as RealPlayer ?? _players.FirstOrDefault(p => p is RealPlayer);
            if (human == null) return;
            _state.HandCards = CardRender.SortHand(human.GetHandCards());
        }

        private void UpdateCardCounter(int viewSeat)
        {
            int deckCount = _dealer?.CurrentGame?.Config?.DeckCount ?? 1;
            if (_state.Mode == GameMode.FourPlayer)
                deckCount = Math.Max(2, deckCount);
            _state.DeckCount = deckCount;

            var played = CollectPlayedCards();
            _state.CardCounter = CardCounterHelper.Compute(
                _state.Mode,
                deckCount,
                _state.HandCards,
                played);
        }

        private List<Card> CollectPlayedCards()
        {
            var played = new List<Card>();
            var moves = _dealer?.CurrentGame?.Moves;
            if (moves == null) return played;
            foreach (var entry in moves)
            {
                if (entry.move?.Cards != null)
                    played.AddRange(entry.move.Cards);
            }
            return played;
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
