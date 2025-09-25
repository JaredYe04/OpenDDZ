using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using OpenDDZ.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils
{
    internal class DDZAnalyzerTest
    {
        public static class TestSuite
        {
            static int tests = 0;
            static int fails = 0;
            static List<string> failMessages = new List<string>();
            private static Logger logger = Logger.Instance;
            static void Assert(bool cond, string msg)
            {
                tests++;
                if (!cond)
                {
                    fails++;
                    failMessages.Add(msg);
                    logger.Error("[FAIL] " + msg);
                    Console.WriteLine("[FAIL] " + msg);
                }
            }

            public static void RunAll()
            {
                Console.WriteLine("Running DDZ Move Analyzer tests...");
                //粗略测试，覆盖主要牌型和比较逻辑
                TestMoveClassification();
                TestMoveComparison();
                TestSpecialCases();

                //牌型分类全面测试（能否正确识别各种牌型）
                TestMoveKindFull();
                //牌型特征全面测试（例如带单、带对，飞机的n飞，连对等）

                //牌型比较全面测试（能否正确比较各种牌型的大小关系）

                //特殊情况测试（例如王炸与普通炸弹的比较，飞机带单/带对的比较等）

                PrintSummary();
                if (fails == 0)
                {
                    logger.Info("All tests passed.");
                }
                else
                {
                    logger.Error($"{fails} tests failed out of {tests}.");
                }
            }
            static void AssertKind(string move, MoveKind expectedKind, RuleSet rules)
            {
                var m = new Move(move);
                var c = MoveUtils.Detect(m, rules);
                string moveStr = move.ToString();
                if (c.Kind != expectedKind)
                {
                    Assert(false, $"Expected kind {expectedKind} but got {c.Kind} for move [{moveStr}] (classification: {c})");
                    return;
                }

                Console.WriteLine($"[OK] {moveStr} -> {c}");
            }
            private static void AssertClassification(Move move, MoveKind expectedKind, Rank expectedMain, RuleSet rules)
            {
                var c = MoveUtils.Detect(move, rules);
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
            private static void TestMoveKindFull()
            {
                var rules = RuleSet.Default;
                Console.WriteLine("== Full Move Kind Tests ==");
                //单牌，对子，三张，三带一，三带一对，四带二单，四带二对，顺子，连对，飞机（无带/带单/带对，双飞/三飞/四飞），炸弹（4炸/5炸/6炸/7炸/8炸），王炸
                //逐一测试每种牌型的边界情况和典型情况
                // ---------- 基本牌型 ----------
                AssertKind("3", MoveKind.Single, rules);
                AssertKind("33", MoveKind.Pair, rules);
                AssertKind("333", MoveKind.Triplet, rules);
                AssertKind("3334", MoveKind.ThreeWithOne, rules);
                AssertKind("33344", MoveKind.ThreeWithPair, rules);

                // ---------- 四带二 ----------
                AssertKind("333344", MoveKind.FourWithTwoSingles, rules);
                AssertKind("33334455", MoveKind.FourWithTwoPairs, rules);

                // ---------- 顺子 ----------
                AssertKind("34567", MoveKind.Straight, rules);
                AssertKind("3456789TJQKA", MoveKind.Straight, rules);
                AssertKind("3456", MoveKind.Invalid, rules); // 少于5张

                // ---------- 连对 ----------
                AssertKind("334455", MoveKind.ConsecutivePairs, rules);
                AssertKind("3344556677", MoveKind.ConsecutivePairs, rules);
                AssertKind("33445566", MoveKind.ConsecutivePairs, rules);
                AssertKind("3344557788", MoveKind.Invalid, rules); // 不连续

                // ---------- 飞机 ----------
                AssertKind("333444", MoveKind.Plane, rules); // 双飞，无带牌
                AssertKind("33344466", MoveKind.Plane, rules);
                AssertKind("333444555777", MoveKind.Plane, rules);
                AssertKind("33344455", MoveKind.Plane, rules); // 双飞带对子
                AssertKind("33344455", MoveKind.Plane, rules); // 双飞带对子
                AssertKind("3334445", MoveKind.Invalid, rules); // 不合法
                AssertKind("333444555", MoveKind.Plane, rules); // 三飞
                AssertKind("33344455566", MoveKind.Invalid, rules);
                AssertKind("333444555666", MoveKind.Plane, rules); // 四飞无带
                AssertKind("33344455566677", MoveKind.Invalid, rules);

                // ---------- 炸弹 ----------
                AssertKind("3333", MoveKind.Bomb, rules);
                AssertKind("33334", MoveKind.Invalid, rules);
                AssertKind("33333", MoveKind.Bomb, rules);
                AssertKind("333333", MoveKind.Bomb, rules);
                AssertKind("3333333", MoveKind.Bomb, rules);
                AssertKind("33333333", MoveKind.Bomb, rules);
                AssertKind("XX", MoveKind.Bomb, rules);
                AssertKind("YY", MoveKind.Bomb, rules);
                AssertKind("XY", MoveKind.Bomb, rules); // 王炸
                AssertKind("XXY", MoveKind.Bomb, rules);
                AssertKind("XXYY", MoveKind.Bomb, rules);

                // ---------- 边界与特殊情况 ----------
                AssertKind("2223456", MoveKind.Invalid, rules); // 顺子不能有2
                AssertKind("33X", MoveKind.Invalid, rules); // 三张带王无效
                AssertKind("333X", MoveKind.ThreeWithOne, rules);
                AssertKind("333XX", MoveKind.ThreeWithPair, rules);
                AssertKind("3333XY", MoveKind.FourWithTwoSingles, rules);
                AssertKind("3333XXYY", MoveKind.FourWithTwoPairs, rules);
                AssertKind("334455667788", MoveKind.ConsecutivePairs, rules); // 连对典型
                AssertKind("333444666", MoveKind.Invalid, rules);
                AssertKind("33344466677", MoveKind.Invalid, rules);
                AssertKind("3334446667788", MoveKind.Invalid, rules);
                AssertKind("AAA22234",MoveKind.Invalid, rules); // 无效牌型
                AssertKind("333444XY", MoveKind.Plane, rules);

                Console.WriteLine("== Full Move Kind Tests Finished ==");

            }
            private static void TestMoveClassification()
            {
                var rules = RuleSet.Default;
                Console.WriteLine("== Move Classification ==");
                // 单张/对子/三张
                AssertClassification(new Move(Rank.Three), MoveKind.Single, Rank.Three, rules);
                AssertClassification(new Move(Rank.Four, Rank.Four), MoveKind.Pair, Rank.Four, rules);
                AssertClassification(new Move(Rank.Seven, Rank.Seven, Rank.Seven), MoveKind.Triplet, Rank.Seven, rules);

                // 三带一/三带一对
                AssertClassification(new Move(Rank.Eight, Rank.Eight, Rank.Eight, Rank.Nine), MoveKind.ThreeWithOne, Rank.Eight, rules);
                AssertClassification(new Move(Rank.Ten, Rank.Ten, Rank.Ten, Rank.J, Rank.J), MoveKind.ThreeWithPair, Rank.Ten, rules);

                // 四带两单/四带两对
                AssertClassification(new Move(Rank.Nine, Rank.Nine, Rank.Nine, Rank.Nine, Rank.Three, Rank.Four), MoveKind.FourWithTwoSingles, Rank.Nine, rules);
                AssertClassification(new Move(Rank.Nine, Rank.Nine, Rank.Nine, Rank.Nine, Rank.Three, Rank.Three, Rank.Four, Rank.Four), MoveKind.FourWithTwoPairs, Rank.Nine, rules);

                // 连对
                AssertClassification(new Move(Rank.Three, Rank.Three, Rank.Four, Rank.Four, Rank.Five, Rank.Five), MoveKind.ConsecutivePairs, Rank.Five, rules);

                // 飞机（无带/带单/带对）
                AssertClassification(new Move(Rank.Seven, Rank.Seven, Rank.Seven, Rank.Eight, Rank.Eight, Rank.Eight), MoveKind.Plane, Rank.Eight, rules);
                AssertClassification(new Move(Rank.Nine, Rank.Nine, Rank.Nine, Rank.Ten, Rank.Ten, Rank.Ten, Rank.Three, Rank.Four), MoveKind.Plane, Rank.Ten, rules);
                AssertClassification(new Move(Rank.Nine, Rank.Nine, Rank.Nine, Rank.Ten, Rank.Ten, Rank.Ten, Rank.Three, Rank.Three, Rank.Four, Rank.Four), MoveKind.Plane, Rank.Ten, rules);

                // 炸弹
                AssertClassification(new Move(Rank.A, Rank.A, Rank.A, Rank.A), MoveKind.Bomb, Rank.A, rules);
                AssertClassification(new Move(Rank.K, Rank.K, Rank.K, Rank.K, Rank.K), MoveKind.Bomb, Rank.K, rules);

                // 王炸
                AssertClassification(new Move("XY"), MoveKind.Bomb, Rank.JokerBig, rules);
                AssertClassification(new Move("XX"), MoveKind.Bomb, Rank.JokerSmall, rules);
                AssertClassification(new Move("XXYY"), MoveKind.Bomb, Rank.JokerBig, rules);
            }

            private static void TestMoveComparison()
            {
                var rules = RuleSet.Default;
                Console.WriteLine("== Move Comparison ==");
                // 对子比较
                var p5 = new Move(Rank.Five, Rank.Five);
                var p6 = new Move(Rank.Six, Rank.Six);
                Assert(MoveUtils.CanBeat(p5, p6, rules), "pair6 should beat pair5");

                // 三张比较
                var t7 = new Move("777");
                var t8 = new Move("888");
                Assert(MoveUtils.CanBeat(t7, t8, rules), "triplet8 > triplet7");

                // 非炸弹不能压炸弹
                var bomb9 = new Move("9999");
                Assert(MoveUtils.CanBeat(t8, bomb9, rules), "triplet cannot beat bomb");

                // 炸弹能压非炸弹
                Assert(!MoveUtils.CanBeat(bomb9, t8, rules), "bomb should beat triplet");

                // 炸弹大小比较
                var bomb4A = new Move(Rank.A, Rank.A, Rank.A, Rank.A);
                var bomb5K = new Move(Rank.K, Rank.K, Rank.K, Rank.K, Rank.K);
                Assert(MoveUtils.CanBeat(bomb4A, bomb5K, rules), "5炸 > 4炸");

                // 王炸与普通炸弹比较
                var doubleJ = new Move(Rank.JokerSmall, Rank.JokerBig);
                var fiveX = new Move(Rank.Three, Rank.Three, Rank.Three, Rank.Three, Rank.Three);
                var fourY = new Move(Rank.Four, Rank.Four, Rank.Four, Rank.Four);
                Assert(!MoveUtils.CanBeat(fiveX, doubleJ, rules), "double joker should not beat 5炸 (per default mapping)");
                Assert(MoveUtils.CanBeat(fourY, doubleJ, rules), "double joker should beat 4炸 (per default mapping)");

                Assert(!MoveUtils.CanBeat(new Move("QQQA"), new Move("X"), rules), "单牌不能压三带一！");

                //大王炸与小王炸比较
                Assert(MoveUtils.CanBeat(new Move("XX"), new Move("YY"), rules), "bigger joker should beat small joker");
            }

            private static void TestSpecialCases()
            {
                var rules = RuleSet.Default;
                Console.WriteLine("== Special Cases ==");
                // 9999+888+7 => plane 2飞 带两单
                AssertClassification(new Move(Rank.Nine, Rank.Nine, Rank.Nine, Rank.Nine, Rank.Eight, Rank.Eight, Rank.Eight, Rank.Seven), MoveKind.Plane, Rank.Nine, rules);

                // 8888+9999 -> plane 2飞 带两单
                AssertClassification(new Move(Rank.Eight, Rank.Eight, Rank.Eight, Rank.Eight, Rank.Nine, Rank.Nine, Rank.Nine, Rank.Nine), MoveKind.Plane, Rank.Nine, rules);
            }


            private static void PrintSummary()
            {
                Console.WriteLine($"Tests run: {tests}, Failures: {fails}");
                if (fails > 0)
                {
                    Console.WriteLine("Failure details:");
                    foreach (var m in failMessages) Console.WriteLine(m);
                }
                else Console.WriteLine("All tests passed.");
                Console.WriteLine("Finished.");
            }
        }

        public static void Main(string[] args)
        {
            TestSuite.RunAll();
        }
    }
}
