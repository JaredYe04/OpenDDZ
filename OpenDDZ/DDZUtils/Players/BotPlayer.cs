using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Interfaces;
using OpenDDZ.DDZUtils.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDDZ.DDZUtils.Players
{
    [Serializable]
    public class BotPlayer : IPlayer
    {
        /// <summary>
        /// 辅助函数：获取所有组合
        /// </summary>
        /// <param name="cards"></param>
        /// <param name="comboSize"></param>
        /// <returns></returns>
        private List<List<Card>> GetCombinations(List<Card> cards, int comboSize)
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

        public string Id { get; set; }
        private List<Card> _hand = new List<Card>();
        private IDealer _dealer;
        private RuleSet _rules=RuleSet.Default;

        public BotPlayer()
        {
            Id = Guid.NewGuid().ToString();
        }
        public BotPlayer(string id)
        {
            Id = id;
        }

        public IList<Card> GetHandCards() => _hand;

        public void RequestPlay(Move move)
        {
            _dealer?.HandlePlayRequest(this, move);
        }

        public void OnMessage(DealerMessage message)
        {
            if (message.Type == DealerMessageType.RequestPlay)
            {
                // 自动出牌
                var lastMove = _dealer.LastMove.Item2;
                if (_dealer.LastMove.Item1==this)
                {
                    //说明没人能接自己的牌，出任意牌
                    lastMove = null;
                }
                var myMove = FindBestMove(lastMove);
                RequestPlay(myMove);
                //Console.WriteLine($"[{Id}] 自动出牌: {(myMove == null || myMove.Cards.Count == 0 ? "pass" : CardUtils.FormatCards(myMove.Cards))}");
            }
            else if (message.Type == DealerMessageType.Info)
            {
                //Console.WriteLine($"[{Id}] {message.Content}");
            }
        }

        public void SetDealer(IDealer dealer)
        {
            _dealer = dealer;
        }

        public void ReceiveCards(IEnumerable<Card> cards)
        {
            _hand.AddRange(cards);
        }

        // 贪心算法：找到能压制lastMove的最小牌型
        private Move FindBestMove(Move lastMove)
        {

            // 枚举所有可能的出牌组合
            var allMoves = GenerateAllMoves(_hand);
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
                if (MoveUtils.CanBeat(lastMove, move, _rules))
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

        // 枚举所有可能的出牌组合（只考虑单牌、对子、三张、炸弹、顺子等常见类型）
        private IEnumerable<Move> GenerateAllMoves(List<Card> hand)
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
                    if (MoveUtils.Detect(new Move(seq), _rules).Kind == MoveKind.Straight)
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
                        if (MoveUtils.Detect(new Move(seq), _rules).Kind == MoveKind.Pair)
                            moves.Add(new Move(seq));
                    }
                }
            }
            


            return moves;
        }
    }
}