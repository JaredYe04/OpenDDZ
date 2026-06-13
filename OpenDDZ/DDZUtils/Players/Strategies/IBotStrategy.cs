using OpenDDZ.DDZUtils.Entities;

namespace OpenDDZ.DDZUtils.Players.Strategies
{
    public interface IBotStrategy
    {
        Move ChoosePlay(BotDecisionContext ctx);
        string ChooseBid(BotDecisionContext ctx, string[] options);
        Card ChooseDiscard(BotDecisionContext ctx);
    }
}
