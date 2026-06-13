using OpenDDZ.DDZUtils.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.AI
{
    public class CardTracker
    {
        public List<Card> RemainingCards { get; private set; }

        public static CardTracker Create(GameMode mode, int deckCount, List<Card> myHand, List<Card> playedCards)
        {
            var fullDeck = BuildFullDeck(mode, deckCount);
            var remaining = new List<Card>(fullDeck);
            RemoveCards(remaining, myHand);
            RemoveCards(remaining, playedCards);
            return new CardTracker { RemainingCards = remaining };
        }

        public static List<Card> BuildFullDeck(GameMode mode, int deckCount)
        {
            var deck = new List<Card>();
            int count = mode == GameMode.FourPlayer ? Math.Max(2, deckCount) : deckCount;
            for (int i = 0; i < count; i++)
            {
                if (mode == GameMode.FourPlayer)
                    deck.AddRange(CardUtils.CreateDeckWithout345());
                else
                    deck.AddRange(CardUtils.CreateDeck());
            }
            return deck;
        }

        /// <summary>
        /// 将剩余牌随机分配给对手（不含 selfIndex），handSizes 长度等于 playerCount。
        /// </summary>
        public List<List<Card>> SampleOpponentHands(int playerCount, int selfIndex, int[] handSizes, Random rng)
        {
            var result = new List<List<Card>>();
            for (int i = 0; i < playerCount; i++)
                result.Add(new List<Card>());

            int needed = 0;
            for (int i = 0; i < playerCount; i++)
            {
                if (i != selfIndex)
                    needed += handSizes[i];
            }

            if (needed != RemainingCards.Count)
                return null;

            var pool = new List<Card>(RemainingCards);
            Shuffle(pool, rng);
            int offset = 0;
            for (int i = 0; i < playerCount; i++)
            {
                if (i == selfIndex) continue;
                int size = handSizes[i];
                result[i].AddRange(pool.Skip(offset).Take(size));
                offset += size;
            }
            return result;
        }

        private static void RemoveCards(List<Card> pool, IEnumerable<Card> toRemove)
        {
            foreach (var card in toRemove)
            {
                var match = pool.FirstOrDefault(c => c.Suit == card.Suit && c.Rank == card.Rank);
                if (match != null)
                    pool.Remove(match);
            }
        }

        private static void Shuffle(List<Card> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
