using NUnit.Framework;

namespace CodeEscape.Execution.Tests
{
    public class MiniPythonTests
    {
        static string Out(string source)
        {
            RunResult r = CodeRunner.Run(source);
            Assert.IsTrue(r.Success, "Unexpected error: " + r.Error + " (line " + r.ErrorLine + ")");
            return r.Output.TrimEnd('\n');
        }

        static RunResult Fails(string source)
        {
            RunResult r = CodeRunner.Run(source);
            Assert.IsFalse(r.Success, "Expected an error but it ran: " + r.Output);
            Assert.IsNotEmpty(r.Error);
            return r;
        }

        // ---- language features

        [Test] public void PrintsText() => Assert.AreEqual("hello", Out("print('hello')"));
        [Test] public void PrintsSeveralValuesWithSpaces() => Assert.AreEqual("a 1 2.5 True None", Out("print('a', 1, 2.5, True, None)"));
        [Test] public void Variables() => Assert.AreEqual("7", Out("x = 3\ny = 4\nprint(x + y)"));
        [Test] public void IntegerMath() => Assert.AreEqual("14", Out("print(2 + 3 * 4)"));
        [Test] public void DivisionGivesFloat() => Assert.AreEqual("2.5", Out("print(5 / 2)"));
        [Test] public void FloatWholeShowsPointZero() => Assert.AreEqual("2.0", Out("print(4 / 2)"));
        [Test] public void FloorDivisionAndModuloLikePython() => Assert.AreEqual("-4 1 -3 -3", Out("print(-7 // 2, -7 % 2, 7 // -3, 7 % -5)"));
        [Test] public void Power() => Assert.AreEqual("1024", Out("print(2 ** 10)"));
        [Test] public void NegativePowerPrecedence() => Assert.AreEqual("-4", Out("print(-2 ** 2)"));
        [Test] public void StringConcatAndRepeat() => Assert.AreEqual("abab-cd", Out("print('ab' * 2 + '-' + 'cd')"));
        [Test] public void Comparisons() => Assert.AreEqual("True False True", Out("print(1 < 2, 2 == 3, 'a' != 'b')"));
        [Test] public void ChainedComparison() => Assert.AreEqual("True False", Out("print(1 < 2 < 3, 1 < 3 < 2)"));
        [Test] public void BooleanOperators() => Assert.AreEqual("True False True", Out("print(True and True, True and False, not False or False)"));
        [Test] public void IfElifElse() => Assert.AreEqual("B", Out("s = 75\nif s >= 90:\n    print('A')\nelif s >= 70:\n    print('B')\nelse:\n    print('C')"));
        [Test] public void ElseBranch() => Assert.AreEqual("C", Out("s = 10\nif s >= 90:\n    print('A')\nelse:\n    print('C')"));
        [Test] public void ForRange() => Assert.AreEqual("0\n1\n2", Out("for i in range(3):\n    print(i)"));
        [Test] public void ForRangeStartStop() => Assert.AreEqual("2 3 4", Out("t = []\nfor i in range(2, 5):\n    t.append(i)\nprint(t[0], t[1], t[2])"));
        [Test] public void ForRangeStep() => Assert.AreEqual("[10, 7, 4, 1]", Out("r = []\nfor i in range(10, 0, -3):\n    r.append(i)\nprint(r)"));
        [Test] public void WhileLoop() => Assert.AreEqual("8", Out("n = 1\nwhile n < 8:\n    n = n * 2\nprint(n)"));
        [Test] public void BreakAndContinue() => Assert.AreEqual("1\n3", Out("for i in range(10):\n    if i == 4:\n        break\n    if i % 2 == 0:\n        continue\n    print(i)"));
        [Test] public void AugmentedAssign() => Assert.AreEqual("12", Out("x = 2\nx += 4\nx *= 2\nprint(x)"));
        [Test] public void InlineBody() => Assert.AreEqual("yes", Out("if 1 < 2: print('yes')"));
        [Test] public void Lists() => Assert.AreEqual("[1, 2, 3] 3 2", Out("a = [1, 2]\na.append(3)\nprint(a, len(a), a[1])"));
        [Test] public void NegativeIndexAndAssign() => Assert.AreEqual("[1, 2, 9]", Out("a = [1, 2, 3]\na[-1] = 9\nprint(a)"));
        [Test] public void ListOfStringsRepr() => Assert.AreEqual("['a', 'b']", Out("print(['a', 'b'])"));
        [Test] public void IteratesOverList() => Assert.AreEqual("6", Out("t = 0\nfor n in [1, 2, 3]:\n    t += n\nprint(t)"));
        [Test] public void IteratesOverString() => Assert.AreEqual("a\nb", Out("for c in 'ab':\n    print(c)"));
        [Test] public void StringIndexAndLen() => Assert.AreEqual("c 5", Out("s = 'abcde'\nprint(s[2], len(s))"));
        [Test] public void InOperator() => Assert.AreEqual("True False True", Out("print(2 in [1, 2], 5 in [1, 2], 'a' in 'cat')"));
        [Test] public void Conversions() => Assert.AreEqual("12 3 3.0 5", Out("print(int('12'), int(3.9), float(3), str(5))"));
        [Test] public void StrOfNumberConcat() => Assert.AreEqual("n=5", Out("print('n=' + str(5))"));
        [Test] public void Builtins() => Assert.AreEqual("3 9 1 10 4", Out("print(abs(-3), max(4, 9), min([5, 1]), sum([1, 2, 3, 4]), round(3.6))"));
        [Test] public void FunctionWithReturn() => Assert.AreEqual("25", Out("def sq(n):\n    return n * n\nprint(sq(5))"));
        [Test] public void FunctionDefaultReturnsNone() => Assert.AreEqual("None", Out("def f():\n    pass\nprint(f())"));
        [Test] public void FunctionScopeIsLocal() => Assert.AreEqual("1", Out("x = 1\ndef f():\n    x = 99\n    return x\nf()\nprint(x)"));
        [Test] public void FunctionReadsGlobal() => Assert.AreEqual("11", Out("base = 10\ndef f(n):\n    return base + n\nprint(f(1))"));
        [Test] public void RecursionWithinLimit() => Assert.AreEqual("120", Out("def f(n):\n    if n <= 1:\n        return 1\n    return n * f(n - 1)\nprint(f(5))"));
        [Test] public void ReturnInsideLoop() => Assert.AreEqual("3", Out("def find(a):\n    for x in a:\n        if x > 2:\n            return x\nprint(find([1, 2, 3, 4]))"));
        [Test] public void CommentsAndBlankLines() => Assert.AreEqual("1", Out("# hi\n\nx = 1  # trailing\nprint(x)"));
        [Test] public void EscapedStrings() => Assert.AreEqual("a\nb", Out("print('a\\nb')"));
        [Test] public void TabsCountAsIndent() => Assert.AreEqual("1", Out("if True:\n\tprint(1)"));

        // ---- errors are friendly and carry line numbers

        [Test]
        public void UndefinedName()
        {
            RunResult r = Fails("x = 1\nprint(y)");
            Assert.AreEqual(2, r.ErrorLine);
            StringAssert.Contains("'y'", r.Error);
        }

        [Test] public void DivideByZero() => StringAssert.Contains("zero", Fails("print(1 / 0)").Error);
        [Test] public void FloorDivideByZero() => StringAssert.Contains("zero", Fails("print(1 // 0)").Error);
        [Test] public void ModuloByZero() => StringAssert.Contains("zero", Fails("print(1 % 0)").Error);
        [Test] public void TextPlusNumber() => Fails("print('a' + 1)");
        [Test] public void BadIndex() => StringAssert.Contains("outside", Fails("a = [1]\nprint(a[5])").Error);
        [Test] public void MissingColon() => Fails("if True\n    print(1)");
        [Test] public void MissingBlock() => Fails("if True:\nprint(1)");
        [Test] public void UnclosedParen() => Fails("print((1 + 2)");
        [Test] public void UnclosedQuote() => Fails("print('abc)");
        [Test] public void UnknownCharacter() => Fails("x = 5 $ 3");
        [Test] public void TooMuchIndent() => Fails("x = 1\n    y = 2");
        [Test] public void BadIntText() => Fails("print(int('abc'))");
        [Test] public void CallNonFunction() => Fails("x = 1\nx()");
        [Test] public void WrongArgumentCount() => Fails("def f(a):\n    return a\nf(1, 2)");
        [Test] public void UnknownMethod() => Fails("print('a'.join('b'))");
        [Test] public void AttributeWithoutCall() => Fails("a = [1]\nprint(a.size)");
        [Test] public void ErrorKeepsEarlierOutput() => Assert.AreEqual("1\n", Fails("print(1)\nprint(x)").Output);

        // ---- safety: nothing dangerous or unbounded can run

        [Test] public void ImportBlocked() => StringAssert.Contains("import", Fails("import os").Error);
        [Test] public void FromImportBlocked() => Fails("from os import system");
        [Test] public void DunderBlocked() => Fails("print(__import__('os'))");
        [Test] public void DunderAttributeBlocked() => Fails("x = [].__class__");
        [Test] public void OpenBlocked() => Fails("open('secret.txt')");
        [Test] public void ExecBlocked() => Fails("exec('print(1)')");
        [Test] public void EvalBlocked() => Fails("eval('1+1')");
        [Test] public void ClassBlocked() => Fails("class A:\n    pass");
        [Test] public void LambdaBlocked() => Fails("f = lambda x: x");

        [Test]
        public void InfiniteWhileStops()
        {
            RunResult r = Fails("while True:\n    pass");
            StringAssert.Contains("too long", r.Error);
        }

        [Test] public void InfiniteForLoopWithWorkStops() => StringAssert.Contains("too long", Fails("n = 0\nwhile True:\n    n = n + 1").Error);
        [Test] public void NestedLoopsStop() => Fails("for i in range(1000):\n    for j in range(1000):\n        x = i * j");
        [Test] public void InfiniteRecursionStops() => StringAssert.Contains("calling", Fails("def f(n):\n    return f(n + 1)\nf(1)").Error);
        [Test] public void MutualRecursionStops() => Fails("def a():\n    return b()\ndef b():\n    return a()\na()");
        [Test] public void HugeRangeBlocked() => Fails("for i in range(1000000000):\n    pass");
        [Test] public void HugeStringRepeatBlocked() => Fails("x = 'a' * 100000000\nprint(len(x))");
        [Test] public void HugeListRepeatBlocked() => Fails("x = [0] * 100000000");
        [Test] public void StringDoublingBlocked() => Fails("s = 'ab'\nfor i in range(40):\n    s = s + s");
        [Test] public void ListGrowthBlocked() => Fails("a = []\nwhile True:\n    a.append(1)");
        [Test] public void OutputFloodBlocked() => StringAssert.Contains("too much", Fails("for i in range(1000):\n    print('x' * 100)").Error);
        [Test] public void LongSingleLineOutputBlocked() => Fails("print('a' * 5000)");
        [Test] public void IntegerOverflowIsAnError() => StringAssert.Contains("too big", Fails("print(10 ** 30)").Error);
        [Test] public void IntegerOverflowByMultiplyIsAnError() => Fails("x = 2\nfor i in range(100):\n    x = x * x");
        [Test] public void HugeNumberLiteralIsAnError() => Fails("print(99999999999999999999999)");
        [Test] public void DeepNestingIsAnError() => Fails("print(" + new string('(', 500) + "1" + new string(')', 500) + ")");
        [Test] public void DeepUnaryNestingIsAnError() => Fails("print(" + new string('-', 5000) + "1)");
        [Test] public void DeepNotNestingIsAnError() => Fails("print(" + string.Concat(System.Linq.Enumerable.Repeat("not ", 3000)) + "True)");
        [Test] public void DeepListNestingIsAnError() => Fails("x = " + new string('[', 2000) + new string(']', 2000));
        [Test] public void GarbageDoesNotThrow()
        {
            var rnd = new System.Random(1234);
            string alphabet = "abc123 +-*/()[]:\n\t'\"=.,#%<>!_ifelsdwhiprntdef";
            for (int i = 0; i < 400; i++)
            {
                var chars = new char[rnd.Next(1, 80)];
                for (int c = 0; c < chars.Length; c++) chars[c] = alphabet[rnd.Next(alphabet.Length)];
                Assert.DoesNotThrow(() => CodeRunner.Run(new string(chars)));
            }
        }

        // ---- judging a fill against tests

        static readonly RunTest[] PriceTests =
        {
            new RunTest { setup = "price = 2\nqty = 7", expectedOutput = "14" },
            new RunTest { setup = "price = 5\nqty = 3", expectedOutput = "15" },
        };

        const string PriceTemplate = "total = ____\nprint(total)";

        [Test] public void JudgeAcceptsCorrectFill() => Assert.IsTrue(CodeRunner.Judge(PriceTemplate, "price * qty", PriceTests).Passed);
        [Test] public void JudgeRejectsHardCodedAnswer() => Assert.IsFalse(CodeRunner.Judge(PriceTemplate, "14", PriceTests).Passed);
        [Test] public void JudgeRejectsWrongOperator()
        {
            JudgeResult r = CodeRunner.Judge(PriceTemplate, "price + qty", PriceTests);
            Assert.IsFalse(r.Passed);
            StringAssert.Contains("9", r.Message);
            StringAssert.Contains("14", r.Message);
            StringAssert.Contains("price = 2", r.Message);
        }
        [Test] public void JudgeReportsSyntaxErrorWithTemplateLine()
        {
            JudgeResult r = CodeRunner.Judge(PriceTemplate, "price *", PriceTests);
            Assert.IsFalse(r.Passed);
            StringAssert.Contains("Line 1", r.Message);
        }
        [Test] public void JudgeRejectsEmptyFill() => Assert.IsFalse(CodeRunner.Judge(PriceTemplate, "  ", PriceTests).Passed);
        [Test] public void JudgeRejectsMultiLineFill() => Assert.IsFalse(CodeRunner.Judge(PriceTemplate, "1\nprint(2)", PriceTests).Passed);
        [Test] public void JudgeRejectsOverlongFill() => Assert.IsFalse(CodeRunner.Judge(PriceTemplate, new string('1', 200), PriceTests).Passed);
        [Test] public void JudgeRejectsInjectedBlankText() => Assert.IsFalse(CodeRunner.Judge(PriceTemplate, "price * qty; import os", PriceTests).Passed);
        [Test] public void JudgeFailsWithoutTests() => Assert.IsFalse(CodeRunner.Judge(PriceTemplate, "1", new RunTest[0]).Passed);
        [Test] public void JudgeFailsWithoutBlankInTemplate() => Assert.IsFalse(CodeRunner.Judge("print(1)", "1", PriceTests).Passed);
        [Test] public void JudgeIgnoresTrailingSpaces() => Assert.IsTrue(CodeRunner.Judge("print(____)", "1", new[] { new RunTest { expectedOutput = "1  \n" } }).Passed);
        [Test] public void JudgeAllowsFillIndentedInsideBlock()
        {
            const string template = "for i in range(3):\n    ____";
            Assert.IsTrue(CodeRunner.Judge(template, "print(i)", new[] { new RunTest { expectedOutput = "0\n1\n2" } }).Passed);
        }
    }
}
