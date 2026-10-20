using NUnit.Framework;

namespace CodeEscape.Execution.Tests
{
    public class CodeTextTests
    {
        [TestCase("age>3", "age > 3")]
        [TestCase("age  >   3 ;", "age > 3")]
        [TestCase("COUNT( * )", "COUNT(*)")]
        [TestCase("  GROUP    BY  ", "GROUP BY")]
        [TestCase("$n*2", "$n * 2")]
        [TestCase("count( $items );;", "count($items)")]
        [TestCase("“Sam”", "\"Sam\"")]
        [TestCase("‘Rex’", "'Rex'")]
        public void FormattingDifferencesSquashToTheSame(string a, string b)
        {
            Assert.AreEqual(CodeText.Squash(b), CodeText.Squash(a));
        }

        [TestCase("GROUPBY", "GROUP BY")]
        [TestCase("asitem", "as $item")]
        [TestCase("age > 3", "age > 4")]
        [TestCase("age>3", "age<3")]
        [TestCase("$n * 2", "$n * 3")]
        [TestCase("ORDERBY age", "ORDER BY age")]
        public void RealDifferencesStayDifferent(string a, string b)
        {
            Assert.AreNotEqual(CodeText.Squash(b), CodeText.Squash(a));
        }

        [Test] public void EmptyIsEmpty() => Assert.AreEqual(string.Empty, CodeText.Squash(null));
        [Test] public void OnlySemicolonsIsEmpty() => Assert.AreEqual(string.Empty, CodeText.Squash(" ; ; "));
        [Test] public void KeepsSpaceBetweenWords() => Assert.AreEqual("echo $x", CodeText.Squash("echo   $x;"));
    }
}
