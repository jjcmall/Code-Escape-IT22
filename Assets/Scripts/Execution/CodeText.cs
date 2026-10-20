using System.Text;

namespace CodeEscape.Execution
{
    /// <summary>Text helpers for comparing typed code, so harmless formatting differences aren't marked wrong.</summary>
    public static class CodeText
    {
        /// <summary>
        /// Collapses formatting that doesn't change the meaning of a short snippet: surrounding/duplicate spaces,
        /// spaces next to symbols (age>3 equals age > 3, COUNT( * ) equals COUNT(*)), curly quotes, and a final semicolon.
        /// A space is kept only between two letters/digits (GROUP BY, as $item).
        /// </summary>
        public static string Squash(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var spaced = new StringBuilder(text.Length);
            foreach (char raw in text)
            {
                char c = raw;
                if (c == '‘' || c == '’') c = '\'';
                else if (c == '“' || c == '”') c = '"';
                spaced.Append(char.IsWhiteSpace(c) ? ' ' : c);
            }

            string s = spaced.ToString().Trim();
            while (s.EndsWith(";"))
                s = s.Substring(0, s.Length - 1).TrimEnd();

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != ' ')
                {
                    sb.Append(c);
                    continue;
                }
                // Skip runs of spaces, keep one only between two word characters.
                int j = i;
                while (j < s.Length && s[j] == ' ') j++;
                if (sb.Length > 0 && j < s.Length && IsWord(sb[sb.Length - 1]) && IsWord(s[j]))
                    sb.Append(' ');
                i = j - 1;
            }
            return sb.ToString();
        }

        static bool IsWord(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$';
        }
    }
}
