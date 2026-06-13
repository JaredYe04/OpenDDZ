using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;

namespace OpenDDZ.DDZUtils.GameIOs
{
    public class NullGameIO : IGameIO
    {
        public IPlayer LastWinner { get; private set; }

        public void ShowMessage(string message) { }
        public void ShowHand(IPlayer player) { }
        public void ShowLastMove(IPlayer player, Move move, IPlayer lastPlayer) { }
        public Move GetMoveInput(IPlayer player) => null;
        public string GetBidInput(IPlayer player) => "不叫";
        public void ShowError(string message) { }
        public void ShowGameEnd(IPlayer winner) { LastWinner = winner; }
        public Card GetDiscardInput(IPlayer player) => null;
        public void EmitPlayRejected(string reason) { }
        public void BeforeBotPlay(IPlayer player) { }
    }
}
