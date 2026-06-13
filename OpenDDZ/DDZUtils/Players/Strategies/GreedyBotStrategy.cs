using OpenDDZ.DDZUtils.Entities;
using System.Linq;

namespace OpenDDZ.DDZUtils.Players.Strategies
{
    public class GreedyBotStrategy : IBotStrategy
    {
        public Move ChoosePlay(BotDecisionContext ctx)
        {
            return CardUtils.FindGreedyBestMove(ctx.MyHand, ctx.EffectiveLastMove, ctx.Rules);
        }

        public string ChooseBid(BotDecisionContext ctx, string[] options)
        {
            return HeuristicBidStrategy.ChooseBid(ctx, options);
        }

        public Card ChooseDiscard(BotDecisionContext ctx)
        {
            if (ctx.MyHand.Count == 0) return null;
            return ctx.MyHand.OrderBy(c => (int)c.Rank).First();
        }
    }
}
