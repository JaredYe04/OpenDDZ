using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using OpenDDZ.DDZUtils.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils
{
    internal class CardUtils
    {

        public static string ShowHand(IPlayer player)
        {
            var hand = player.GetHandCards().OrderByDescending(c => (int)c.Rank).ThenByDescending(c => (int)c.Suit).ToList();
            return $"{player.Name}的手牌：{FormatCards(hand)}";
        }
        public static string ShowHand(IEnumerable<Card> cards)
        {
            var hand = cards.OrderByDescending(c => (int)c.Rank).ThenByDescending(c => (int)c.Suit).ToList();
            return $"{FormatCards(hand)}";
        }
        public static string FormatCards(IEnumerable<Card> cards)
        {
            return string.Join(" ", cards.OrderByDescending(c => (int)c.Rank).ThenByDescending(c => (int)c.Suit).Select(CardToString));
        }

        public static string CardToString(Card card)
        {
            if (card.Rank == Rank.JokerSmall) return "小王";
            if (card.Rank == Rank.JokerBig) return "大王";
            string suitStr = "";
            switch (card.Suit)
            {
                case Suit.Heart:
                    suitStr = "红桃";
                    break;
                case Suit.Spade:
                    suitStr = "黑桃";
                    break;
                case Suit.Diamond:
                    suitStr = "方片";
                    break;
                case Suit.Club:
                    suitStr = "梅花";
                    break;
                default:
                    suitStr = "王牌";
                    break;
            }
            string rankStr = "";
            switch (card.Rank)
            {
                case Rank.A:
                    rankStr = "A";
                    break;
                case Rank.K:
                    rankStr = "K";
                    break;
                case Rank.Q:
                    rankStr = "Q";
                    break;
                case Rank.J:
                    rankStr = "J";
                    break;
                case Rank.Ten:
                    rankStr = "10";
                    break;
                case Rank.Nine:
                    rankStr = "9";
                    break;
                case Rank.Eight:
                    rankStr = "8";
                    break;
                case Rank.Seven:
                    rankStr = "7";
                    break;
                case Rank.Six:
                    rankStr = "6";
                    break;
                case Rank.Five:
                    rankStr = "5";
                    break;
                case Rank.Four:
                    rankStr = "4";
                    break;
                case Rank.Three:
                    rankStr = "3";
                    break;
                case Rank.Two:
                    rankStr = "2";
                    break;
                default:
                    rankStr = ((int)card.Rank).ToString();
                    break;
            }
            return $"{suitStr}{rankStr}";
        }

        public static List<Card> CreateDeck()
        {
            var deck = new List<Card>();
            foreach (Rank r in Enum.GetValues(typeof(Rank)))
            {
                if (r == Rank.JokerSmall || r == Rank.JokerBig) continue;// 跳过大小王，后面单独添加
                foreach (Suit s in Enum.GetValues(typeof(Suit)))
                {
                    if (s == Suit.Joker) continue;// 跳过 Joker 花色
                    deck.Add(new Card(r, s));
                }

            }
            deck.Add(new Card(Rank.JokerSmall, Suit.Joker));
            deck.Add(new Card(Rank.JokerBig, Suit.Joker));
            return deck;
        }

        /// <summary>
        ///  贪心算法：找到当前手牌中，能压制lastMove的最小牌型
        /// </summary>
        /// <param name="hand"></param>
        /// <param name="lastMove"></param>
        /// <param name="rules"></param>
        /// <returns></returns>
        public static Move FindGreedyBestMove(List<Card> hand, Move lastMove, RuleSet rules)
        {

            // 枚举所有可能的出牌组合
            var allMoves = GenerateAllMoves(hand,rules);
            Move bestMove = null;

            // 如果没有lastMove，选择牌数最多的最小牌
            if (lastMove == null || lastMove.Cards.Count == 0)
            {
                allMoves.Aggregate((m1, m2) =>
                {
                    if (bestMove == null ||
                        m1.Cards.Count > bestMove.Cards.Count ||
                        (m1.Cards.Count == bestMove.Cards.Count && m1.Cards.Min(c => c.Rank) < bestMove.Cards.Min(c => c.Rank)))
                    {
                        bestMove = m1;
                    }
                    if (m2.Cards.Count > bestMove.Cards.Count ||
                        (m2.Cards.Count == bestMove.Cards.Count && m2.Cards.Min(c => c.Rank) < bestMove.Cards.Min(c => c.Rank)))
                    {
                        bestMove = m2;
                    }
                    return bestMove;
                });
                return bestMove;
            }


            foreach (var move in allMoves.OrderBy(m => m.Cards.Count).ThenBy(m => m.Cards.Min(c => c.Rank)))
            {
                if (MoveUtils.CanBeat(lastMove, move, rules))
                {
                    if (bestMove == null ||
                        move.Cards.Count < bestMove.Cards.Count ||
                        (move.Cards.Count == bestMove.Cards.Count && move.Cards.Min(c => c.Rank) < bestMove.Cards.Min(c => c.Rank)))
                    {
                        bestMove = move;
                    }
                }
            }

            // 如果没有可压制的牌，则不出牌，即返回null
            return bestMove;
        }

        /// <summary>
        ///  枚举所有可能的出牌组合
        /// </summary>
        /// <param name="hand"></param>
        /// <returns></returns>
        public static IEnumerable<Move> GenerateAllMoves(List<Card> hand,RuleSet rules)
        {
            var moves = new List<Move>();

            // 单牌
            foreach (var card in hand)
                moves.Add(new Move(new List<Card> { card }));

            // 对子
            var pairs = hand.GroupBy(c => c.Rank).Where(g => g.Count() >= 2);
            foreach (var pair in pairs)
                moves.Add(new Move(pair.Take(2).ToList()));

            // 三张
            var triples = hand.GroupBy(c => c.Rank).Where(g => g.Count() >= 3);
            foreach (var triple in triples)
                moves.Add(new Move(triple.Take(3).ToList()));

            //三带一(在三张的基础上带一张单牌)
            foreach (var triple in triples)
            {
                var remainingCards = hand.Except(triple).ToList();
                foreach (var card in remainingCards)
                {
                    var moveCards = triple.Take(3).ToList();
                    moveCards.Add(card);
                    moves.Add(new Move(moveCards));
                }
            }
            //三带二(在三张的基础上带一对)
            foreach (var triple in triples)
            {
                var remainingCards = hand.Except(triple).ToList();
                var remainingPairs = remainingCards.GroupBy(c => c.Rank).Where(g => g.Count() >= 2);
                foreach (var pair in remainingPairs)
                {
                    var moveCards = triple.Take(3).ToList();
                    moveCards.AddRange(pair.Take(2));
                    moves.Add(new Move(moveCards));
                }
            }
            // 四带二(在四张的基础上带两张单牌或一对)
            var quads = hand.GroupBy(c => c.Rank).Where(g => g.Count() >= 4);
            foreach (var triple in quads)
            {
                var remainingCards = hand.Except(triple).ToList();
                // 带两张单牌
                var singleCombinations = GetCombinations(remainingCards, 2);
                foreach (var combo in singleCombinations)
                {
                    var moveCards = triple.Take(4).ToList();
                    moveCards.AddRange(combo);
                    moves.Add(new Move(moveCards));
                }
                // 带一对
                var remainingPairs = remainingCards.GroupBy(c => c.Rank).Where(g => g.Count() >= 2);
                foreach (var pair in remainingPairs)
                {
                    var moveCards = triple.Take(4).ToList();
                    moveCards.AddRange(pair.Take(2));
                    moves.Add(new Move(moveCards));
                }
            }

            // 炸弹
            var bombs = hand.GroupBy(c => c.Rank).Where(g => g.Count() >= 4);
            foreach (var bomb in bombs)
                moves.Add(new Move(bomb.Take(4).ToList()));

            // 王炸
            var jokers = hand.Where(c => c.Rank == Rank.JokerSmall || c.Rank == Rank.JokerBig).ToList();
            if (jokers.Count == 2)
                moves.Add(new Move(jokers));

            // 顺子（长度5及以上）
            var ordered = hand.OrderBy(c => c.Rank).ToList();
            for (int len = 5; len <= ordered.Count; len++)
            {
                for (int i = 0; i <= ordered.Count - len; i++)
                {
                    var seq = ordered.Skip(i).Take(len).ToList();
                    if (MoveUtils.Detect(new Move(seq), rules).Kind == MoveKind.Straight)
                        moves.Add(new Move(seq));
                }
            }

            // 连对（长度3及以上）
            var pairGroups = hand.GroupBy(c => c.Rank).Where(g => g.Count() >= 2).Select(g => g.Take(2).ToList()).ToList();
            foreach (var pair in pairGroups)
            {
                for (int len = 3; len <= pairGroups.Count; len++)
                {
                    for (int i = 0; i <= pairGroups.Count - len; i++)
                    {
                        var seq = pairGroups.Skip(i).Take(len).SelectMany(p => p).ToList();
                        if (MoveUtils.Detect(new Move(seq), rules).Kind == MoveKind.Pair)
                            moves.Add(new Move(seq));
                    }
                }
            }



            return moves;
        }
        /// <summary>
        /// 辅助函数：获取所有组合
        /// </summary>
        /// <param name="cards"></param>
        /// <param name="comboSize"></param>
        /// <returns></returns>
        public static List<List<Card>> GetCombinations(List<Card> cards, int comboSize)
        {
            var result = new List<List<Card>>();
            int n = cards.Count;
            if (comboSize > n) return result;
            void Backtrack(int start, List<Card> current)
            {
                if (current.Count == comboSize)
                {
                    result.Add(new List<Card>(current));
                    return;
                }
                for (int i = start; i < n; i++)
                {
                    current.Add(cards[i]);
                    Backtrack(i + 1, current);
                    current.RemoveAt(current.Count - 1);
                }
            }
            Backtrack(0, new List<Card>());
            return result;
        }

    }
}
