using System.Collections.Generic;
using System.Globalization;

namespace CodeEscape.Programming
{
    /// <summary>
    /// One run of the player's code for a challenge: what was typed, the setup it ran with, what it printed,
    /// and whether the challenge was solved. World effects (CodeEffect) use it to show the code's result in the level.
    /// </summary>
    public sealed class CodeExecution
    {
        public QuestionData Question;
        public IChallengeSource Source;
        public string Fill = string.Empty;
        /// <summary>The test setup the code ran with (e.g. "price = 2\nquantity = 7"); empty when none.</summary>
        public string Setup = string.Empty;
        public string Output = string.Empty;
        public bool Passed;

        List<string> lines;
        List<long> numbers;

        /// <summary>The non-empty printed lines, in order.</summary>
        public IReadOnlyList<string> Lines
        {
            get
            {
                if (lines == null)
                {
                    lines = new List<string>();
                    foreach (string raw in (Output ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
                    {
                        string line = raw.Trim();
                        if (line.Length > 0)
                            lines.Add(line);
                    }
                }
                return lines;
            }
        }

        /// <summary>The printed lines that are whole numbers, in order.</summary>
        public IReadOnlyList<long> Numbers
        {
            get
            {
                if (numbers == null)
                {
                    numbers = new List<long>();
                    foreach (string line in Lines)
                        if (long.TryParse(line, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long n))
                            numbers.Add(n);
                }
                return numbers;
            }
        }

        /// <summary>The setup lines joined for display ("price = 2   quantity = 7").</summary>
        public string SetupSummary
        {
            get
            {
                var parts = new List<string>();
                foreach (string raw in (Setup ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length > 0)
                        parts.Add(line);
                }
                return string.Join("   ", parts);
            }
        }
    }
}
