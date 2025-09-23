using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZAnalyzer
{
    internal class DDZAnalyzerTest
    {
        public static class TestSuite
        {
            static int tests = 0;
            static int fails = 0;
            static List<string> failMessages = new List<string>();

            static void Assert(bool cond, string msg)
            {
                tests++;
                if (!cond)
                {
                    fails++;
                    failMessages.Add(msg);
                    Console.WriteLine("[FAIL] " + msg);
                }
            }

            public static void RunAll()
            {
                var rules = RuleSet.Default;

                // basic single/pair/triple
                AssertClassification(new Move(Rank.Three), MoveKind.Single, Rank.Three, rules);
                AssertClassification(new Move(Rank.Four, Rank.Four), MoveKind.Pair, Rank.Four, rules);
                AssertClassification(new Move(Rank.Seven, Rank.Seven, Rank.Seven), MoveKind.Triplet, Rank.Seven, rules);

                // three带一 / three带一对
                AssertClassification(new Move(Rank.Eight, Rank.Eight, Rank.Eight, Rank.Nine), MoveKind.ThreeWithOne, Rank.Eight, rules);
                AssertClassification(new Move(Rank.Ten, Rank.Ten, Rank.Ten, Rank.J, Rank.J), MoveKind.ThreeWithPair, Rank.Ten, rules);

                // 四带两单 / 四带两对
                AssertClassification(new Move(Rank.Nine, Rank.Nine, Rank.Nine, Rank.Nine, Rank.Three, Rank.Four), MoveKind.FourWithTwoSingles, Rank.Nine, rules);
                AssertClassification(new Move(Rank.Nine, Rank.Nine, Rank.Nine, Rank.Nine, Rank.Three, Rank.Three, Rank.Four, Rank.Four), MoveKind.FourWithTwoPairs, Rank.Nine, rules);

                // 连对
                AssertClassification(new Move(Rank.Three, Rank.Three, Rank.Four, Rank.Four, Rank.Five, Rank.Five), MoveKind.ConsecutivePairs, Rank.Five, rules);

                // 飞机 无带 / 带单 / 带对
                AssertClassification(new Move(Rank.Seven, Rank.Seven, Rank.Seven, Rank.Eight, Rank.Eight, Rank.Eight), MoveKind.Plane, Rank.Eight, rules); // 2飞 无带
                AssertClassification(new Move(Rank.Nine, Rank.Nine, Rank.Nine, Rank.Ten, Rank.Ten, Rank.Ten, Rank.Three, Rank.Four), MoveKind.Plane, Rank.Ten, rules); // 2飞 带两单
                AssertClassification(new Move(Rank.Nine, Rank.Nine, Rank.Nine, Rank.Ten, Rank.Ten, Rank.Ten, Rank.Three, Rank.Three, Rank.Four, Rank.Four), MoveKind.Plane, Rank.Ten, rules); // 2飞 带两对

                // 炸弹（4炸、5炸...）
                AssertClassification(new Move(Rank.A, Rank.A, Rank.A, Rank.A), MoveKind.Bomb, Rank.A, rules);
                AssertClassification(new Move(Rank.K, Rank.K, Rank.K, Rank.K, Rank.K), MoveKind.Bomb, Rank.K, rules);

                // 王炸（多王）
                AssertClassification(new Move(Rank.JokerSmall, Rank.JokerBig), MoveKind.Bomb, Rank.JokerBig, rules); // 双王炸
                AssertClassification(new Move(Rank.JokerSmall, Rank.JokerSmall, Rank.JokerBig), MoveKind.Bomb, Rank.JokerBig, rules); // triple jokers
                AssertClassification(new Move(Rank.JokerSmall, Rank.JokerSmall, Rank.JokerBig, Rank.JokerBig), MoveKind.Bomb, Rank.JokerBig, rules); // four jokers

                // corner case: 9999+888+7 => plane 2飞 带两单 (use 3x9 and 3x8 + single 9,7 attachments)
                AssertClassification(new Move(Rank.Nine, Rank.Nine, Rank.Nine, Rank.Nine, Rank.Eight, Rank.Eight, Rank.Eight, Rank.Seven), MoveKind.Plane, Rank.Nine, rules);

                // corner case: 8888+9999 -> plane 2飞 带两单 (8:4 9:4 -> use 3+3 + two leftovers)
                AssertClassification(new Move(Rank.Eight, Rank.Eight, Rank.Eight, Rank.Eight, Rank.Nine, Rank.Nine, Rank.Nine, Rank.Nine), MoveKind.Plane, Rank.Nine, rules);

                // 比较测试
                // pair 5 vs pair 6
                var p5 = new Move(Rank.Five, Rank.Five);
                var p6 = new Move(Rank.Six, Rank.Six);
                Assert(MoveComparer.CanBeat(p5, p6, rules), "pair6 should beat pair5");

                // triplet vs triplet
                var t7 = new Move(Rank.Seven, Rank.Seven, Rank.Seven);
                var t8 = new Move(Rank.Eight, Rank.Eight, Rank.Eight);
                Assert(MoveComparer.CanBeat(t7, t8, rules), "triplet8 > triplet7");

                // non-bomb cannot beat bomb
                var bomb9 = new Move(Rank.Nine, Rank.Nine, Rank.Nine, Rank.Nine);
                Assert(!MoveComparer.CanBeat(t8, bomb9, rules), "triplet cannot beat bomb");

                // bomb beats non-bomb
                Assert(MoveComparer.CanBeat(t8, bomb9, rules), "bomb should beat triplet");

                // bomb vs bomb sizing
                var bomb4A = new Move(Rank.A, Rank.A, Rank.A, Rank.A);
                var bomb5K = new Move(Rank.K, Rank.K, Rank.K, Rank.K, Rank.K);
                Assert(MoveComparer.CanBeat(bomb4A, bomb5K, rules), "5炸 > 4炸");

                // Joker power ordering according to default mapping:
                // double jokers power set to just below 5炸, so doubleJoker cannot beat 5炸 but can beat 4炸
                var doubleJ = new Move(Rank.JokerSmall, Rank.JokerBig);
                var fiveX = new Move(Rank.Three, Rank.Three, Rank.Three, Rank.Three, Rank.Three); // 5炸
                var fourY = new Move(Rank.Four, Rank.Four, Rank.Four, Rank.Four); // 4炸
                Assert(!MoveComparer.CanBeat(fiveX, doubleJ, rules), "double joker should not beat 5炸 (per default mapping)");
                Assert(MoveComparer.CanBeat(fourY, doubleJ, rules), "double joker should beat 4炸 (per default mapping)");

                // print summary
                Console.WriteLine($"Tests run: {tests}, Failures: {fails}");
                if (fails > 0)
                {
                    Console.WriteLine("Failure details:");
                    foreach (var m in failMessages) Console.WriteLine(m);
                }
                else Console.WriteLine("All tests passed.");
            }

            private static void AssertClassification(Move move, MoveKind expectedKind, Rank expectedMain, RuleSet rules)
            {
                var c = MoveAnalyzer.Detect(move, rules);
                string moveStr = move.ToString();
                if (c.Kind != expectedKind)
                {
                    Assert(false, $"Expected kind {expectedKind} but got {c.Kind} for move [{moveStr}] (classification: {c})");
                    return;
                }
                // if expectedMain is nonzero, check primary rank matches (for types where primary rank makes sense)
                if (expectedMain != 0)
                {
                    // For Plane, primary rank is highest rank in sequence (we used that in detection)
                    if (c.PrimaryRank != expectedMain)
                    {
                        Assert(false, $"Expected mainRank {expectedMain} but got {c.PrimaryRank} for move [{moveStr}] (kind {c.Kind})");
                        return;
                    }
                }
                Console.WriteLine($"[OK] {moveStr} -> {c}");
            }
        }

        // Program Main
        public class Program
        {
            public static void Main(string[] args)
            {
                Console.WriteLine("Running DDZ Move Analyzer tests...");
                TestSuite.RunAll();
                Console.WriteLine("Finished.");
            }
        }
    }
}
