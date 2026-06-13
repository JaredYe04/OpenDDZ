using Newtonsoft.Json;
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OpenDDZ.DDZUtils.GameIOs
{
    /// <summary>
    /// Stdio protocol IGameIO: outputs JSON lines to stdout, blocks on pending response for current requestId.
    /// Only accepts stdin responses that match the current requestId to avoid wrong/duplicate consumption.
    /// </summary>
    public class StdioGameIO : IGameIO
    {
        private readonly TextWriter _out;
        private readonly TextReader _in;
        private readonly Thread _stdinThread;
        private volatile bool _running = true;
        private readonly object _writeLock = new object();

        private string _pendingPlayRequestId;
        private Move _pendingPlayResult;
        private bool _pendingPlayDone;
        private readonly object _pendingPlayLock = new object();

        private string _pendingBidRequestId;
        private string _pendingBidResult;
        private bool _pendingBidDone;
        private readonly object _pendingBidLock = new object();

        private string _pendingDiscardRequestId;
        private Card _pendingDiscardResult;
        private bool _pendingDiscardDone;
        private readonly object _pendingDiscardLock = new object();

        private string _lastPlayRequestId;

        public StdioGameIO(TextWriter stdout = null, TextReader stdin = null)
        {
            _out = stdout ?? Console.Out;
            _in = stdin ?? Console.In;
            _stdinThread = new Thread(ReadStdinLoop) { IsBackground = true };
        }

        /// <summary> Call after parsing "start" so stdin is used only for play/pass/call. </summary>
        public void StartStdinReader()
        {
            _stdinThread.Start();
        }

        public void Stop()
        {
            _running = false;
            lock (_pendingPlayLock) { _pendingPlayDone = true; System.Threading.Monitor.PulseAll(_pendingPlayLock); }
            lock (_pendingBidLock) { _pendingBidDone = true; System.Threading.Monitor.PulseAll(_pendingBidLock); }
            lock (_pendingDiscardLock) { _pendingDiscardDone = true; System.Threading.Monitor.PulseAll(_pendingDiscardLock); }
        }

        private void WriteLine(string json)
        {
            lock (_writeLock)
            {
                _out.WriteLine(json);
                _out.Flush();
            }
        }

        private void Emit(object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            WriteLine(json);
        }

        public void EmitReady()
        {
            Emit(new { type = "ready" });
        }

        public void EmitError(string message)
        {
            Emit(new { type = "error", content = message });
        }

        public void ShowMessage(string message)
        {
            Emit(new { type = "message", content = message });
        }

        public void ShowHand(IPlayer player)
        {
            var hand = player.GetHandCards().OrderByDescending(c => (int)c.Rank).ThenByDescending(c => (int)c.Suit).ToList();
            var cards = hand.Select(MoveUtils.CardToProtocolString).ToList();
            Emit(new { type = "state", playerIndex = GetPlayerIndex(player), hand = cards, handCount = cards.Count });
        }

        public void ShowLastMove(IPlayer player, Move move, IPlayer lastPlayer)
        {
            var lastCards = move != null && move.Cards.Count > 0
                ? move.Cards.Select(MoveUtils.CardToProtocolString).ToList()
                : (IList<string>)new List<string>();
            Emit(new
            {
                type = "last_move",
                lastPlayerIndex = lastPlayer != null ? GetPlayerIndex(lastPlayer) : (int?)null,
                lastCards,
                isFirstHand = move == null || move.Cards.Count == 0
            });
        }

        public void ShowError(string message)
        {
            Emit(new { type = "error", content = message });
        }

        public void ShowGameEnd(IPlayer winner)
        {
            Emit(new { type = "game_end", winnerIndex = GetPlayerIndex(winner), winnerName = winner?.Name });
        }

        public Move GetMoveInput(IPlayer player)
        {
            var requestId = Guid.NewGuid().ToString("N");
            _lastPlayRequestId = requestId;
            lock (_pendingPlayLock)
            {
                _pendingPlayRequestId = requestId;
                _pendingPlayDone = false;
                _pendingPlayResult = null;
            }
            Emit(new { type = "request_play", requestId, playerIndex = GetPlayerIndex(player), deadlineSec = 30 });
            lock (_pendingPlayLock)
            {
                while (!_pendingPlayDone && _running)
                    System.Threading.Monitor.Wait(_pendingPlayLock);
            }
            return _pendingPlayResult;
        }

        public void EmitPlayRejected(string reason)
        {
            var rid = _lastPlayRequestId;
            if (string.IsNullOrEmpty(rid)) return;
            Emit(new { type = "play_rejected", requestId = rid, reason });
        }

        public void BeforeBotPlay(IPlayer player) { }

        public string GetBidInput(IPlayer player)
        {
            var requestId = Guid.NewGuid().ToString("N");
            lock (_pendingBidLock)
            {
                _pendingBidRequestId = requestId;
                _pendingBidDone = false;
                _pendingBidResult = "不叫";
            }
            Emit(new { type = "request_call", requestId, playerIndex = GetPlayerIndex(player), options = new[] { "1分", "2分", "3分", "不叫" } });
            lock (_pendingBidLock)
            {
                while (!_pendingBidDone && _running)
                    System.Threading.Monitor.Wait(_pendingBidLock);
            }
            return _pendingBidResult ?? "不叫";
        }

        public Card GetDiscardInput(IPlayer player)
        {
            var requestId = Guid.NewGuid().ToString("N");
            lock (_pendingDiscardLock)
            {
                _pendingDiscardRequestId = requestId;
                _pendingDiscardDone = false;
                _pendingDiscardResult = null;
            }
            Emit(new { type = "request_discard", requestId, playerIndex = GetPlayerIndex(player) });
            lock (_pendingDiscardLock)
            {
                while (!_pendingDiscardDone && _running)
                    System.Threading.Monitor.Wait(_pendingDiscardLock);
            }
            return _pendingDiscardResult;
        }

        private int GetPlayerIndex(IPlayer player)
        {
            return _playerIndexMap != null && _playerIndexMap.ContainsKey(player) ? _playerIndexMap[player] : -1;
        }

        private Dictionary<IPlayer, int> _playerIndexMap;

        public void SetPlayerIndexMap(IList<OpenDDZ.DDZUtils.Interfaces.IPlayer> players)
        {
            _playerIndexMap = new Dictionary<IPlayer, int>();
            for (int i = 0; i < players.Count; i++)
                _playerIndexMap[players[i]] = i;
        }

        private void ReadStdinLoop()
        {
            while (_running)
            {
                try
                {
                    var line = _in.ReadLine();
                    if (line == null) break;
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    var jobj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(line);
                    var cmd = jobj["cmd"]?.ToString();
                    if (string.IsNullOrEmpty(cmd)) continue;
                    var requestId = jobj["requestId"]?.ToString();

                    switch (cmd)
                    {
                        case "play":
                            Move moveResult = null;
                            var cardsToken = jobj["cards"];
                            if (cardsToken != null && cardsToken is Newtonsoft.Json.Linq.JArray arr && arr.Count > 0)
                            {
                                var cardStrs = arr.Select(t => t.ToString()).ToList();
                                try
                                {
                                    moveResult = MoveUtils.ParseMoveFromProtocolStrings(cardStrs);
                                }
                                catch (Exception ex)
                                {
                                    Emit(new { type = "error", content = ex.Message });
                                }
                            }
                            CompletePlayRequest(requestId, moveResult);
                            break;
                        case "pass":
                            CompletePlayRequest(requestId, null);
                            break;
                        case "call":
                            var choice = jobj["choice"]?.ToString() ?? "不叫";
                            CompleteBidRequest(requestId, choice);
                            break;
                        case "discard":
                            Card cardResult = null;
                            var dcStr = jobj["card"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(dcStr))
                            {
                                try
                                {
                                    cardResult = MoveUtils.ParseCardFromProtocolString(dcStr.Trim());
                                }
                                catch (Exception ex)
                                {
                                    Emit(new { type = "error", content = ex.Message });
                                }
                            }
                            CompleteDiscardRequest(requestId, cardResult);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    try { Emit(new { type = "error", content = ex.Message }); } catch { }
                }
            }
        }

        private void CompletePlayRequest(string requestId, Move move)
        {
            lock (_pendingPlayLock)
            {
                if (!string.IsNullOrEmpty(requestId) && requestId != _pendingPlayRequestId)
                    return;
                _pendingPlayResult = move;
                _pendingPlayDone = true;
                System.Threading.Monitor.PulseAll(_pendingPlayLock);
            }
        }

        private void CompleteBidRequest(string requestId, string choice)
        {
            lock (_pendingBidLock)
            {
                if (!string.IsNullOrEmpty(requestId) && requestId != _pendingBidRequestId)
                    return;
                _pendingBidResult = choice;
                _pendingBidDone = true;
                System.Threading.Monitor.PulseAll(_pendingBidLock);
            }
        }

        private void CompleteDiscardRequest(string requestId, Card card)
        {
            lock (_pendingDiscardLock)
            {
                if (!string.IsNullOrEmpty(requestId) && requestId != _pendingDiscardRequestId)
                    return;
                _pendingDiscardResult = card;
                _pendingDiscardDone = true;
                System.Threading.Monitor.PulseAll(_pendingDiscardLock);
            }
        }
    }
}
