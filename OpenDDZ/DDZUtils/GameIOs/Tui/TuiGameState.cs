using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.GameIOs.Tui
{
    public enum TuiInputMode
    {
        None,
        WaitPlay,
        WaitBid,
        WaitDiscard,
        GameEnded
    }

    public class SeatDisplayInfo
    {
        public int SeatIndex { get; set; }
        public string PlayerName { get; set; }
        public int HandCount { get; set; }
        public bool IsLandlord { get; set; }
        public bool IsCurrentTurn { get; set; }
        public bool IsPass { get; set; }
        public List<Card> LastCards { get; set; } = new List<Card>();
        public string MoveKindLabel { get; set; } = "";
    }

    public class TuiGameState
    {
        public TuiInputMode InputMode { get; set; } = TuiInputMode.None;
        public IPlayer ActivePlayer { get; set; }
        public int HumanSeatIndex { get; set; }
        public int PlayerCount { get; set; } = 3;
        public GameMode Mode { get; set; } = GameMode.Normal;

        public List<Card> HandCards { get; set; } = new List<Card>();
        public HashSet<int> SelectedIndices { get; } = new HashSet<int>();
        public HashSet<int> HintIndices { get; } = new HashSet<int>();
        public int SelectionVersion { get; private set; }

        public Move EffectiveLastMove { get; set; }
        public IPlayer LastMovePlayer { get; set; }
        public bool IsFirstHand { get; set; }
        public bool CanBeat { get; set; } = true;

        public string[] BidOptions { get; set; } = { "1分", "2分", "3分", "不叫" };
        public int HighestBid { get; set; }

        public List<SeatDisplayInfo> Seats { get; set; } = new List<SeatDisplayInfo>();
        public List<string> Messages { get; } = new List<string>();
        public string ErrorMessage { get; set; } = "";

        public IPlayer Winner { get; set; }
        public IPlayer Landlord { get; set; }
        public List<IPlayer> AllPlayers { get; set; } = new List<IPlayer>();

        public int Version { get; private set; }

        public void BumpVersion() => Version++;

        public void AddMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            Messages.Add(message);
            while (Messages.Count > 8)
                Messages.RemoveAt(0);
            BumpVersion();
        }

        public void ClearSelection() => SetSelection(null, null);

        public void SetSelection(IEnumerable<int> selected, IEnumerable<int> hint)
        {
            var newSelected = selected?.ToList() ?? new List<int>();
            var newHint = (newSelected.Count == 0 ? hint?.ToList() : new List<int>()) ?? new List<int>();

            if (SelectedIndices.SetEquals(newSelected) && HintIndices.SetEquals(newHint))
                return;

            SelectedIndices.Clear();
            HintIndices.Clear();
            foreach (var i in newSelected)
                SelectedIndices.Add(i);
            foreach (var i in newHint)
                HintIndices.Add(i);
            SelectionVersion++;
        }

        public event Action StateChanged;

        public void NotifyChanged()
        {
            BumpVersion();
            StateChanged?.Invoke();
        }
    }
}
