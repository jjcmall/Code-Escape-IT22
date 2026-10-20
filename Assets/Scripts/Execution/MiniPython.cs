using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CodeEscape.Execution
{
    /// <summary>Raised for any problem in the player's code. Always shown to the player as a friendly message.</summary>
    public sealed class PyError : Exception
    {
        public int Line { get; }
        public PyError(string message, int line) : base(message) { Line = line; }
    }

    /// <summary>Hard limits that keep a run bounded, whatever the player types.</summary>
    public static class RunLimits
    {
        public const int MaxSteps = 50000;
        public const int MaxCallDepth = 20;
        public const int MaxOutputChars = 2000;
        public const int MaxOutputLines = 50;
        public const int MaxCollection = 10000;
        public const int MaxExprDepth = 60;
        public const int MaxMilliseconds = 500;
    }

    // ------------------------------------------------------------------ lexer

    sealed class Tok
    {
        public char K;      // n number, s string, i identifier, o operator, e end of line
        public string T;
        public object V;
    }

    sealed class SrcLine
    {
        public int Indent;
        public int Number;
        public List<Tok> Toks;
    }

    static class Lexer
    {
        static readonly string[] Ops3 = { "//=" };
        static readonly string[] Ops2 = { "==", "!=", "<=", ">=", "//", "**", "+=", "-=", "*=", "/=", "%=" };
        const string Ops1 = "+-*/%<>=()[],:.";

        public static List<SrcLine> Lex(string source)
        {
            var lines = new List<SrcLine>();
            string[] raw = (source ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int n = 0; n < raw.Length; n++)
            {
                string text = raw[n];
                int indent = 0, i = 0;
                while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
                {
                    indent += text[i] == '\t' ? 4 : 1;
                    i++;
                }
                if (i >= text.Length || text[i] == '#')
                    continue;
                var toks = LexLine(text, i, n + 1);
                if (toks.Count == 0)
                    continue;
                toks.Add(new Tok { K = 'e', T = "end of line" });
                lines.Add(new SrcLine { Indent = indent, Number = n + 1, Toks = toks });
            }
            return lines;
        }

        static List<Tok> LexLine(string s, int i, int line)
        {
            var toks = new List<Tok>();
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t') { i++; continue; }
                if (c == '#') break;

                if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                {
                    int start = i;
                    bool isFloat = false;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                    if (i < s.Length && s[i] == '.') { isFloat = true; i++; while (i < s.Length && char.IsDigit(s[i])) i++; }
                    string num = s.Substring(start, i - start);
                    if (isFloat)
                        toks.Add(new Tok { K = 'n', T = num, V = double.Parse(num, CultureInfo.InvariantCulture) });
                    else if (long.TryParse(num, NumberStyles.None, CultureInfo.InvariantCulture, out long l))
                        toks.Add(new Tok { K = 'n', T = num, V = l });
                    else
                        throw new PyError("That number is too big.", line);
                    if (i < s.Length && (char.IsLetter(s[i]) || s[i] == '_'))
                        throw new PyError("A number can't be followed directly by letters.", line);
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                    string name = s.Substring(start, i - start);
                    if (name.StartsWith("__", StringComparison.Ordinal))
                        throw new PyError("Names that start with two underscores aren't allowed here.", line);
                    toks.Add(new Tok { K = 'i', T = name });
                    continue;
                }

                if (c == '\'' || c == '"')
                {
                    char quote = c;
                    var sb = new StringBuilder();
                    i++;
                    bool closed = false;
                    while (i < s.Length)
                    {
                        char d = s[i];
                        if (d == quote) { closed = true; i++; break; }
                        if (d == '\\' && i + 1 < s.Length)
                        {
                            char e = s[i + 1];
                            sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                            i += 2;
                            continue;
                        }
                        sb.Append(d);
                        i++;
                    }
                    if (!closed) throw new PyError("A piece of text is missing its closing quote.", line);
                    toks.Add(new Tok { K = 's', T = sb.ToString(), V = sb.ToString() });
                    continue;
                }

                string op = null;
                foreach (string o in Ops3) if (string.CompareOrdinal(s, i, o, 0, 3) == 0) op = o;
                if (op == null) foreach (string o in Ops2) if (string.CompareOrdinal(s, i, o, 0, 2) == 0) op = o;
                if (op == null && Ops1.IndexOf(c) >= 0) op = c.ToString();
                if (op == null)
                    throw new PyError("The character '" + c + "' isn't supported here.", line);
                toks.Add(new Tok { K = 'o', T = op });
                i += op.Length;
            }
            return toks;
        }
    }

    // ------------------------------------------------------------------ AST + parser

    enum NK
    {
        Num, Str, Name, List, Bin, Neg, Not, And, Or, Cmp, Call, Index, Method,
        Block, If, For, While, Def, Return, Pass, Break, Continue, Assign, Aug, ExprStmt
    }

    sealed class Node
    {
        public NK Kind;
        public int Line;
        public string S;
        public object V;
        public Node A, B, C;
        public List<Node> Kids = new List<Node>();
        public List<Node> Kids2 = new List<Node>();
        public List<string> Ops;
    }

    sealed class Parser
    {
        readonly List<SrcLine> lines;
        int pos;
        List<Tok> t;
        int ti;
        int line;
        int depth;

        public Parser(List<SrcLine> lines) { this.lines = lines; }

        public Node ParseProgram()
        {
            var block = new Node { Kind = NK.Block, Line = 1 };
            while (pos < lines.Count)
            {
                if (lines[pos].Indent != lines[0].Indent)
                    throw new PyError("This line is indented differently from the lines around it.", lines[pos].Number);
                block.Kids.Add(ParseStatement());
            }
            return block;
        }

        Tok Cur => t[ti];
        bool IsOp(string s) => Cur.K == 'o' && Cur.T == s;
        bool IsWord(string s) => Cur.K == 'i' && Cur.T == s;

        PyError Err(string msg) { return new PyError(msg, line); }

        void ExpectOp(string s)
        {
            if (!IsOp(s)) throw Err("Expected '" + s + "' but found " + Describe(Cur) + ".");
            ti++;
        }

        static string Describe(Tok k)
        {
            return k.K == 'e' ? "the end of the line" : "'" + k.T + "'";
        }

        void EndOfLine()
        {
            if (Cur.K != 'e') throw Err("Didn't expect " + Describe(Cur) + " here.");
        }

        Node ParseBlock(int parentIndent, int headerLine)
        {
            if (pos >= lines.Count || lines[pos].Indent <= parentIndent)
                throw new PyError("Expected an indented block after this line.", headerLine);
            int blockIndent = lines[pos].Indent;
            var block = new Node { Kind = NK.Block, Line = lines[pos].Number };
            while (pos < lines.Count && lines[pos].Indent == blockIndent)
                block.Kids.Add(ParseStatement());
            if (pos < lines.Count && lines[pos].Indent > blockIndent)
                throw new PyError("This line is indented too far.", lines[pos].Number);
            return block;
        }

        // Body after ':' - either the rest of the same line, or an indented block below.
        Node ParseBody(SrcLine header)
        {
            ExpectOp(":");
            if (Cur.K == 'e')
            {
                pos++;
                return ParseBlock(header.Indent, header.Number);
            }
            var block = new Node { Kind = NK.Block, Line = line };
            block.Kids.Add(ParseSimple());
            EndOfLine();
            pos++;
            return block;
        }

        Node ParseStatement()
        {
            SrcLine header = lines[pos];
            t = header.Toks;
            ti = 0;
            line = header.Number;
            depth = 0;

            if (Cur.K == 'i')
            {
                switch (Cur.T)
                {
                    case "if": return ParseIf(header);
                    case "for": return ParseFor(header);
                    case "while": return ParseWhile(header);
                    case "def": return ParseDef(header);
                    case "elif":
                    case "else":
                        throw Err("'" + Cur.T + "' needs an 'if' before it.");
                    case "class": case "try": case "with": case "lambda":
                        throw Err("'" + Cur.T + "' isn't supported in this game.");
                }
            }
            var node = ParseSimple();
            EndOfLine();
            pos++;
            return node;
        }

        Node ParseIf(SrcLine header)
        {
            var node = new Node { Kind = NK.If, Line = line };
            ti++;
            node.Kids.Add(ParseExpr());
            node.Kids2.Add(ParseBody(header));
            while (pos < lines.Count && lines[pos].Indent == header.Indent && lines[pos].Toks[0].K == 'i')
            {
                string word = lines[pos].Toks[0].T;
                if (word != "elif" && word != "else") break;
                SrcLine h = lines[pos];
                t = h.Toks; ti = 1; line = h.Number;
                if (word == "elif")
                {
                    node.Kids.Add(ParseExpr());
                    node.Kids2.Add(ParseBody(h));
                }
                else
                {
                    node.C = ParseBody(h);
                    break;
                }
            }
            return node;
        }

        Node ParseFor(SrcLine header)
        {
            var node = new Node { Kind = NK.For, Line = line };
            ti++;
            if (Cur.K != 'i') throw Err("Expected a variable name after 'for'.");
            node.S = Cur.T; ti++;
            if (!IsWord("in")) throw Err("Expected 'in' in the for loop.");
            ti++;
            node.A = ParseExpr();
            node.B = ParseBody(header);
            return node;
        }

        Node ParseWhile(SrcLine header)
        {
            var node = new Node { Kind = NK.While, Line = line };
            ti++;
            node.A = ParseExpr();
            node.B = ParseBody(header);
            return node;
        }

        Node ParseDef(SrcLine header)
        {
            var node = new Node { Kind = NK.Def, Line = line };
            ti++;
            if (Cur.K != 'i') throw Err("Expected a function name after 'def'.");
            node.S = Cur.T; ti++;
            ExpectOp("(");
            var ps = new List<string>();
            while (!IsOp(")"))
            {
                if (Cur.K != 'i') throw Err("Expected a parameter name.");
                ps.Add(Cur.T); ti++;
                if (IsOp(",")) ti++; else break;
            }
            ExpectOp(")");
            node.V = ps.ToArray();
            node.B = ParseBody(header);
            return node;
        }

        Node ParseSimple()
        {
            if (Cur.K == 'i')
            {
                switch (Cur.T)
                {
                    case "pass": ti++; return new Node { Kind = NK.Pass, Line = line };
                    case "break": ti++; return new Node { Kind = NK.Break, Line = line };
                    case "continue": ti++; return new Node { Kind = NK.Continue, Line = line };
                    case "return":
                        ti++;
                        var r = new Node { Kind = NK.Return, Line = line };
                        if (Cur.K != 'e') r.A = ParseExpr();
                        return r;
                    case "import": case "from": case "global": case "del": case "assert": case "raise": case "exec": case "eval": case "open":
                        throw Err("'" + Cur.T + "' isn't allowed in this game.");
                }
            }

            Node e = ParseExpr();
            if (IsOp("="))
            {
                if (e.Kind != NK.Name && e.Kind != NK.Index) throw Err("You can only assign to a variable or a list item.");
                ti++;
                return new Node { Kind = NK.Assign, Line = line, A = e, B = ParseExpr() };
            }
            if (Cur.K == 'o' && (Cur.T == "+=" || Cur.T == "-=" || Cur.T == "*=" || Cur.T == "/=" || Cur.T == "//=" || Cur.T == "%="))
            {
                if (e.Kind != NK.Name && e.Kind != NK.Index) throw Err("You can only assign to a variable or a list item.");
                string op = Cur.T.Substring(0, Cur.T.Length - 1);
                ti++;
                return new Node { Kind = NK.Aug, Line = line, S = op, A = e, B = ParseExpr() };
            }
            return new Node { Kind = NK.ExprStmt, Line = line, A = e };
        }

        // ---- expressions

        Node ParseExpr()
        {
            if (++depth > RunLimits.MaxExprDepth) throw Err("This expression is nested too deeply.");
            Node n = ParseOr();
            depth--;
            return n;
        }

        Node ParseOr()
        {
            Node left = ParseAnd();
            while (IsWord("or"))
            {
                ti++;
                left = new Node { Kind = NK.Or, Line = line, A = left, B = ParseAnd() };
            }
            return left;
        }

        Node ParseAnd()
        {
            Node left = ParseNot();
            while (IsWord("and"))
            {
                ti++;
                left = new Node { Kind = NK.And, Line = line, A = left, B = ParseNot() };
            }
            return left;
        }

        Node ParseNot()
        {
            if (IsWord("not"))
            {
                ti++;
                if (++depth > RunLimits.MaxExprDepth) throw Err("This expression is nested too deeply.");
                var n = new Node { Kind = NK.Not, Line = line, A = ParseNot() };
                depth--;
                return n;
            }
            return ParseCompare();
        }

        Node ParseCompare()
        {
            Node first = ParseAdd();
            Node cmp = null;
            while (true)
            {
                string op = null;
                if (Cur.K == 'o' && (Cur.T == "==" || Cur.T == "!=" || Cur.T == "<" || Cur.T == ">" || Cur.T == "<=" || Cur.T == ">="))
                    { op = Cur.T; ti++; }
                else if (IsWord("in")) { op = "in"; ti++; }
                else if (IsWord("not") && t[ti + 1].K == 'i' && t[ti + 1].T == "in") { op = "not in"; ti += 2; }
                if (op == null) break;
                if (cmp == null)
                {
                    cmp = new Node { Kind = NK.Cmp, Line = line, Ops = new List<string>() };
                    cmp.Kids.Add(first);
                }
                cmp.Ops.Add(op);
                cmp.Kids.Add(ParseAdd());
            }
            return cmp ?? first;
        }

        Node ParseAdd()
        {
            Node left = ParseMul();
            while (Cur.K == 'o' && (Cur.T == "+" || Cur.T == "-"))
            {
                string op = Cur.T; ti++;
                left = new Node { Kind = NK.Bin, Line = line, S = op, A = left, B = ParseMul() };
            }
            return left;
        }

        Node ParseMul()
        {
            Node left = ParseUnary();
            while (Cur.K == 'o' && (Cur.T == "*" || Cur.T == "/" || Cur.T == "//" || Cur.T == "%"))
            {
                string op = Cur.T; ti++;
                left = new Node { Kind = NK.Bin, Line = line, S = op, A = left, B = ParseUnary() };
            }
            return left;
        }

        Node ParseUnary()
        {
            if (IsOp("-") || IsOp("+"))
            {
                bool neg = Cur.T == "-";
                ti++;
                if (++depth > RunLimits.MaxExprDepth) throw Err("This expression is nested too deeply.");
                Node operand = ParseUnary();
                depth--;
                return neg ? new Node { Kind = NK.Neg, Line = line, A = operand } : operand;
            }
            return ParsePower();
        }

        Node ParsePower()
        {
            Node b = ParsePostfix();
            if (IsOp("**"))
            {
                ti++;
                if (++depth > RunLimits.MaxExprDepth) throw Err("This expression is nested too deeply.");
                Node exp = ParseUnary();
                depth--;
                return new Node { Kind = NK.Bin, Line = line, S = "**", A = b, B = exp };
            }
            return b;
        }

        Node ParsePostfix()
        {
            Node n = ParseAtom();
            while (true)
            {
                if (IsOp("("))
                {
                    ti++;
                    var call = new Node { Kind = NK.Call, Line = line, A = n };
                    ParseArgs(call);
                    n = call;
                }
                else if (IsOp("["))
                {
                    ti++;
                    Node idx = ParseExpr();
                    ExpectOp("]");
                    n = new Node { Kind = NK.Index, Line = line, A = n, B = idx };
                }
                else if (IsOp("."))
                {
                    ti++;
                    if (Cur.K != 'i') throw Err("Expected a method name after '.'.");
                    string name = Cur.T; ti++;
                    if (!IsOp("(")) throw Err("Only method calls like .append(x) are supported after a '.'.");
                    ti++;
                    var m = new Node { Kind = NK.Method, Line = line, A = n, S = name };
                    ParseArgs(m);
                    n = m;
                }
                else break;
            }
            return n;
        }

        void ParseArgs(Node call)
        {
            while (!IsOp(")"))
            {
                call.Kids.Add(ParseExpr());
                if (IsOp(",")) ti++; else break;
            }
            ExpectOp(")");
        }

        Node ParseAtom()
        {
            Tok k = Cur;
            switch (k.K)
            {
                case 'n': ti++; return new Node { Kind = NK.Num, Line = line, V = k.V };
                case 's': ti++; return new Node { Kind = NK.Str, Line = line, V = k.V };
                case 'i':
                    ti++;
                    if (k.T == "True") return new Node { Kind = NK.Num, Line = line, V = true };
                    if (k.T == "False") return new Node { Kind = NK.Num, Line = line, V = false };
                    if (k.T == "None") return new Node { Kind = NK.Num, Line = line, V = null };
                    switch (k.T)
                    {
                        case "if": case "else": case "elif": case "for": case "while": case "def": case "in": case "and": case "or": case "not":
                        case "return": case "pass": case "break": case "continue":
                            throw Err("'" + k.T + "' can't be used here.");
                    }
                    return new Node { Kind = NK.Name, Line = line, S = k.T };
                case 'o':
                    if (k.T == "(")
                    {
                        ti++;
                        Node e = ParseExpr();
                        ExpectOp(")");
                        return e;
                    }
                    if (k.T == "[")
                    {
                        ti++;
                        var list = new Node { Kind = NK.List, Line = line };
                        while (!IsOp("]"))
                        {
                            list.Kids.Add(ParseExpr());
                            if (IsOp(",")) ti++; else break;
                        }
                        ExpectOp("]");
                        return list;
                    }
                    break;
            }
            throw Err("Didn't expect " + Describe(k) + " here. Is something missing?");
        }
    }

    // ------------------------------------------------------------------ interpreter

    enum Flow { Normal, Break, Continue, Return }

    sealed class Builtin
    {
        public string Name;
    }

    sealed class FuncDef
    {
        public string Name;
        public string[] Params;
        public Node Body;
    }

    sealed class Interpreter
    {
        static readonly HashSet<string> BuiltinNames = new HashSet<string>
        {
            "print", "len", "str", "int", "float", "range", "abs", "max", "min", "sum", "round", "bool"
        };

        readonly Dictionary<string, object> globals = new Dictionary<string, object>();
        Dictionary<string, object> locals;
        readonly StringBuilder output = new StringBuilder();
        int outputLines;
        int steps;
        int callDepth;
        object retVal;
        readonly Stopwatch clock = Stopwatch.StartNew();

        public string Output => output.ToString();

        public void Run(Node program)
        {
            Exec(program);
        }

        void Tick(int line)
        {
            if (++steps > RunLimits.MaxSteps)
                throw new PyError("The program ran too long (it may loop forever).", line);
            if ((steps & 255) == 0 && clock.ElapsedMilliseconds > RunLimits.MaxMilliseconds)
                throw new PyError("The program ran too long (it may loop forever).", line);
        }

        // ---- statements

        Flow Exec(Node n)
        {
            Tick(n.Line);
            switch (n.Kind)
            {
                case NK.Block:
                    foreach (Node s in n.Kids)
                    {
                        Flow f = Exec(s);
                        if (f != Flow.Normal) return f;
                    }
                    return Flow.Normal;

                case NK.If:
                    for (int i = 0; i < n.Kids.Count; i++)
                    {
                        if (Truthy(Eval(n.Kids[i])))
                            return Exec(n.Kids2[i]);
                    }
                    return n.C != null ? Exec(n.C) : Flow.Normal;

                case NK.For:
                {
                    object iter = Eval(n.A);
                    List<object> items;
                    if (iter is List<object> l) items = new List<object>(l);
                    else if (iter is string str)
                    {
                        items = new List<object>();
                        foreach (char c in str) items.Add(c.ToString());
                    }
                    else throw new PyError("A for loop needs a list, text or range() to go through, not " + TypeName(iter) + ".", n.Line);
                    foreach (object item in items)
                    {
                        SetVar(n.S, item);
                        Flow f = Exec(n.B);
                        if (f == Flow.Break) break;
                        if (f == Flow.Return) return f;
                    }
                    return Flow.Normal;
                }

                case NK.While:
                    while (Truthy(Eval(n.A)))
                    {
                        Flow f = Exec(n.B);
                        if (f == Flow.Break) break;
                        if (f == Flow.Return) return f;
                    }
                    return Flow.Normal;

                case NK.Def:
                    SetVar(n.S, new FuncDef { Name = n.S, Params = (string[])n.V, Body = n.B });
                    return Flow.Normal;

                case NK.Return:
                    retVal = n.A != null ? Eval(n.A) : null;
                    return Flow.Return;

                case NK.Pass: return Flow.Normal;
                case NK.Break: return Flow.Break;
                case NK.Continue: return Flow.Continue;

                case NK.Assign:
                    Assign(n.A, Eval(n.B));
                    return Flow.Normal;

                case NK.Aug:
                {
                    object cur = Eval(n.A);
                    Assign(n.A, Arith(n.S, cur, Eval(n.B), n.Line));
                    return Flow.Normal;
                }

                case NK.ExprStmt:
                    Eval(n.A);
                    return Flow.Normal;
            }
            throw new PyError("That kind of statement isn't supported.", n.Line);
        }

        void SetVar(string name, object value)
        {
            if (locals != null) locals[name] = value; else globals[name] = value;
        }

        void Assign(Node target, object value)
        {
            if (target.Kind == NK.Name)
            {
                SetVar(target.S, value);
                return;
            }
            object container = Eval(target.A);
            object index = Eval(target.B);
            if (!(container is List<object> list))
                throw new PyError("Only list items can be changed this way, not " + TypeName(container) + ".", target.Line);
            int i = ListIndex(list.Count, index, target.Line);
            list[i] = value;
        }

        static int ListIndex(int count, object index, int line)
        {
            if (!(index is long) && !(index is bool))
                throw new PyError("List positions must be whole numbers.", line);
            long i = index is bool b ? (b ? 1 : 0) : (long)index;
            if (i < 0) i += count;
            if (i < 0 || i >= count) throw new PyError("That position is outside the list (it has " + count + " items).", line);
            return (int)i;
        }

        // ---- expressions

        object Eval(Node n)
        {
            Tick(n.Line);
            switch (n.Kind)
            {
                case NK.Num:
                case NK.Str:
                    return n.V;

                case NK.Name:
                    if (locals != null && locals.TryGetValue(n.S, out object lv)) return lv;
                    if (globals.TryGetValue(n.S, out object gv)) return gv;
                    if (BuiltinNames.Contains(n.S)) return new Builtin { Name = n.S };
                    throw new PyError("The name '" + n.S + "' hasn't been given a value yet.", n.Line);

                case NK.List:
                {
                    var list = new List<object>();
                    foreach (Node k in n.Kids) list.Add(Eval(k));
                    return list;
                }

                case NK.Neg:
                {
                    object v = Eval(n.A);
                    if (v is long l) { try { return checked(-l); } catch (OverflowException) { throw new PyError("That number is too big.", n.Line); } }
                    if (v is bool b) return b ? -1L : 0L;
                    if (v is double d) return -d;
                    throw new PyError("Can't put a minus sign in front of " + TypeName(v) + ".", n.Line);
                }

                case NK.Not: return !Truthy(Eval(n.A));
                case NK.And: { object l = Eval(n.A); return Truthy(l) ? Eval(n.B) : l; }
                case NK.Or: { object l = Eval(n.A); return Truthy(l) ? l : Eval(n.B); }

                case NK.Bin:
                    return Arith(n.S, Eval(n.A), Eval(n.B), n.Line);

                case NK.Cmp:
                {
                    object left = Eval(n.Kids[0]);
                    for (int i = 0; i < n.Ops.Count; i++)
                    {
                        object right = Eval(n.Kids[i + 1]);
                        if (!Compare(n.Ops[i], left, right, n.Line)) return false;
                        left = right;
                    }
                    return true;
                }

                case NK.Index:
                {
                    object c = Eval(n.A);
                    object idx = Eval(n.B);
                    if (c is List<object> list) return list[ListIndex(list.Count, idx, n.Line)];
                    if (c is string s) return s[ListIndex(s.Length, idx, n.Line)].ToString();
                    throw new PyError("You can only use [ ] on a list or text, not " + TypeName(c) + ".", n.Line);
                }

                case NK.Method:
                    return CallMethod(n);

                case NK.Call:
                    return CallValue(n);
            }
            throw new PyError("That kind of expression isn't supported.", n.Line);
        }

        object CallMethod(Node n)
        {
            object target = Eval(n.A);
            var args = new List<object>();
            foreach (Node k in n.Kids) args.Add(Eval(k));
            if (target is List<object> list && n.S == "append")
            {
                if (args.Count != 1) throw new PyError("append() needs exactly one value.", n.Line);
                if (list.Count >= RunLimits.MaxCollection) throw new PyError("That list got too big.", n.Line);
                list.Add(args[0]);
                return null;
            }
            if (target is string s && args.Count == 0)
            {
                if (n.S == "upper") return s.ToUpperInvariant();
                if (n.S == "lower") return s.ToLowerInvariant();
            }
            throw new PyError("The method ." + n.S + "() isn't available for " + TypeName(target) + " here.", n.Line);
        }

        object CallValue(Node n)
        {
            object f = Eval(n.A);
            var args = new List<object>();
            foreach (Node k in n.Kids) args.Add(Eval(k));

            if (f is Builtin b) return CallBuiltin(b.Name, args, n.Line);
            if (f is FuncDef fn)
            {
                if (args.Count != fn.Params.Length)
                    throw new PyError(fn.Name + "() needs " + fn.Params.Length + " value(s) but got " + args.Count + ".", n.Line);
                if (callDepth >= RunLimits.MaxCallDepth)
                    throw new PyError("Too many functions are calling each other (is a function calling itself forever?).", n.Line);
                var saved = locals;
                var fresh = new Dictionary<string, object>();
                for (int i = 0; i < args.Count; i++) fresh[fn.Params[i]] = args[i];
                locals = fresh;
                callDepth++;
                try
                {
                    retVal = null;
                    Flow flow = Exec(fn.Body);
                    object result = flow == Flow.Return ? retVal : null;
                    retVal = null;
                    return result;
                }
                finally
                {
                    callDepth--;
                    locals = saved;
                }
            }
            throw new PyError("You can only call a function, not " + TypeName(f) + ".", n.Line);
        }

        object CallBuiltin(string name, List<object> a, int line)
        {
            switch (name)
            {
                case "print":
                {
                    var parts = new List<string>();
                    foreach (object o in a) parts.Add(Str(o));
                    string text = string.Join(" ", parts);
                    outputLines++;
                    if (outputLines > RunLimits.MaxOutputLines || output.Length + text.Length + 1 > RunLimits.MaxOutputChars)
                        throw new PyError("The program printed too much.", line);
                    output.Append(text).Append('\n');
                    return null;
                }
                case "len":
                    Need(a, 1, name, line);
                    if (a[0] is string s) return (long)s.Length;
                    if (a[0] is List<object> l) return (long)l.Count;
                    throw new PyError("len() works on text and lists, not " + TypeName(a[0]) + ".", line);
                case "str":
                    Need(a, 1, name, line);
                    return Str(a[0]);
                case "bool":
                    Need(a, 1, name, line);
                    return Truthy(a[0]);
                case "int":
                    Need(a, 1, name, line);
                    return ToInt(a[0], line);
                case "float":
                    Need(a, 1, name, line);
                    return ToFloat(a[0], line);
                case "abs":
                    Need(a, 1, name, line);
                    if (a[0] is double ad) return Math.Abs(ad);
                    if (IsInt(a[0])) { long v = AsLong(a[0]); try { return checked(Math.Abs(v)); } catch (OverflowException) { throw new PyError("That number is too big.", line); } }
                    throw new PyError("abs() needs a number.", line);
                case "round":
                    if (a.Count < 1 || a.Count > 2) throw new PyError("round() needs 1 or 2 values.", line);
                    if (!IsNum(a[0])) throw new PyError("round() needs a number.", line);
                    if (a.Count == 1)
                    {
                        if (IsInt(a[0])) return AsLong(a[0]);
                        double r = Math.Round(AsDouble(a[0]), MidpointRounding.ToEven);
                        if (double.IsNaN(r) || double.IsInfinity(r) || Math.Abs(r) > 9e18) throw new PyError("That number is too big.", line);
                        return (long)r;
                    }
                    if (!IsInt(a[1])) throw new PyError("round() digits must be a whole number.", line);
                    if (IsInt(a[0])) return AsLong(a[0]);
                    return Math.Round(AsDouble(a[0]), (int)Math.Max(0, Math.Min(15, AsLong(a[1]))), MidpointRounding.ToEven);
                case "range":
                {
                    if (a.Count < 1 || a.Count > 3) throw new PyError("range() needs 1 to 3 values.", line);
                    foreach (object o in a) if (!IsInt(o)) throw new PyError("range() needs whole numbers.", line);
                    long start = 0, stop, step = 1;
                    if (a.Count == 1) stop = AsLong(a[0]);
                    else { start = AsLong(a[0]); stop = AsLong(a[1]); if (a.Count == 3) step = AsLong(a[2]); }
                    if (step == 0) throw new PyError("range() step can't be 0.", line);
                    var list = new List<object>();
                    for (long i = start; step > 0 ? i < stop : i > stop; i += step)
                    {
                        if (list.Count >= RunLimits.MaxCollection) throw new PyError("That range is too big.", line);
                        list.Add(i);
                    }
                    return list;
                }
                case "sum":
                {
                    Need(a, 1, name, line);
                    if (!(a[0] is List<object> items)) throw new PyError("sum() needs a list.", line);
                    object total = 0L;
                    foreach (object o in items) total = Arith("+", total, o, line);
                    return total;
                }
                case "max":
                case "min":
                {
                    List<object> items = a.Count == 1 && a[0] is List<object> one ? one : a;
                    if (items.Count == 0) throw new PyError(name + "() needs at least one value.", line);
                    object best = items[0];
                    for (int i = 1; i < items.Count; i++)
                    {
                        bool better = name == "max" ? Compare(">", items[i], best, line) : Compare("<", items[i], best, line);
                        if (better) best = items[i];
                    }
                    return best;
                }
            }
            throw new PyError("Unknown function " + name + ".", line);
        }

        static void Need(List<object> a, int count, string name, int line)
        {
            if (a.Count != count)
                throw new PyError(name + "() needs " + count + " value" + (count == 1 ? "" : "s") + ".", line);
        }

        // ---- values

        static bool IsInt(object o) { return o is long || o is bool; }
        static bool IsNum(object o) { return o is long || o is bool || o is double; }
        static long AsLong(object o) { return o is bool b ? (b ? 1L : 0L) : (long)o; }
        static double AsDouble(object o) { return o is double d ? d : AsLong(o); }

        static bool Truthy(object o)
        {
            if (o == null) return false;
            if (o is bool b) return b;
            if (o is long l) return l != 0;
            if (o is double d) return d != 0;
            if (o is string s) return s.Length > 0;
            if (o is List<object> list) return list.Count > 0;
            return true;
        }

        static string TypeName(object o)
        {
            if (o == null) return "None";
            if (o is bool) return "a True/False value";
            if (o is long) return "a whole number";
            if (o is double) return "a decimal number";
            if (o is string) return "text";
            if (o is List<object>) return "a list";
            return "a function";
        }

        static string Str(object o)
        {
            if (o is string s) return s;
            return Repr(o);
        }

        static string Repr(object o)
        {
            if (o == null) return "None";
            if (o is bool b) return b ? "True" : "False";
            if (o is long l) return l.ToString(CultureInfo.InvariantCulture);
            if (o is double d)
            {
                if (double.IsPositiveInfinity(d)) return "inf";
                if (double.IsNegativeInfinity(d)) return "-inf";
                if (double.IsNaN(d)) return "nan";
                string r = d.ToString("R", CultureInfo.InvariantCulture);
                return r.IndexOfAny(new[] { '.', 'E' }) < 0 ? r + ".0" : r;
            }
            if (o is string s) return "'" + s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n") + "'";
            if (o is List<object> list)
            {
                var parts = new List<string>();
                foreach (object item in list) parts.Add(Repr(item));
                return "[" + string.Join(", ", parts) + "]";
            }
            return "<function>";
        }

        static object ToInt(object o, int line)
        {
            if (IsInt(o)) return AsLong(o);
            if (o is double d)
            {
                if (double.IsNaN(d) || double.IsInfinity(d) || Math.Abs(d) > 9e18) throw new PyError("That number is too big to turn into a whole number.", line);
                return (long)Math.Truncate(d);
            }
            if (o is string s)
            {
                if (long.TryParse(s.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) return l;
                throw new PyError("int() can't turn the text '" + s + "' into a whole number.", line);
            }
            throw new PyError("int() can't convert " + TypeName(o) + ".", line);
        }

        static object ToFloat(object o, int line)
        {
            if (IsNum(o)) return AsDouble(o);
            if (o is string s)
            {
                if (double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
                throw new PyError("float() can't turn the text '" + s + "' into a number.", line);
            }
            throw new PyError("float() can't convert " + TypeName(o) + ".", line);
        }

        static bool ValuesEqual(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (IsNum(a) && IsNum(b))
                return (IsInt(a) && IsInt(b)) ? AsLong(a) == AsLong(b) : AsDouble(a) == AsDouble(b);
            if (a is string sa && b is string sb) return sa == sb;
            if (a is List<object> la && b is List<object> lb)
            {
                if (la.Count != lb.Count) return false;
                for (int i = 0; i < la.Count; i++) if (!ValuesEqual(la[i], lb[i])) return false;
                return true;
            }
            return false;
        }

        static bool Compare(string op, object a, object b, int line)
        {
            switch (op)
            {
                case "==": return ValuesEqual(a, b);
                case "!=": return !ValuesEqual(a, b);
                case "in":
                case "not in":
                {
                    bool found;
                    if (b is List<object> list)
                    {
                        found = false;
                        foreach (object item in list) if (ValuesEqual(item, a)) { found = true; break; }
                    }
                    else if (b is string s && a is string sub) found = s.Contains(sub);
                    else throw new PyError("'in' needs a list or text on the right side.", line);
                    return op == "in" ? found : !found;
                }
            }
            int cmp;
            if (IsNum(a) && IsNum(b))
                cmp = (IsInt(a) && IsInt(b)) ? AsLong(a).CompareTo(AsLong(b)) : AsDouble(a).CompareTo(AsDouble(b));
            else if (a is string sa && b is string sb)
                cmp = string.CompareOrdinal(sa, sb);
            else
                throw new PyError("Can't compare " + TypeName(a) + " with " + TypeName(b) + ".", line);

            switch (op)
            {
                case "<": return cmp < 0;
                case ">": return cmp > 0;
                case "<=": return cmp <= 0;
                default: return cmp >= 0;
            }
        }

        static object Arith(string op, object a, object b, int line)
        {
            try
            {
                return ArithInner(op, a, b, line);
            }
            catch (OverflowException)
            {
                throw new PyError("That number is too big.", line);
            }
        }

        static object ArithInner(string op, object a, object b, int line)
        {
            if (op == "+")
            {
                if (a is string sa && b is string sb)
                {
                    if (sa.Length + sb.Length > RunLimits.MaxCollection) throw new PyError("That text got too long.", line);
                    return sa + sb;
                }
                if (a is List<object> la && b is List<object> lb)
                {
                    if (la.Count + lb.Count > RunLimits.MaxCollection) throw new PyError("That list got too big.", line);
                    var joined = new List<object>(la);
                    joined.AddRange(lb);
                    return joined;
                }
            }
            if (op == "*")
            {
                if (a is string || a is List<object>) { object t = a; a = b; b = t; }
                if (b is string || b is List<object>)
                {
                    if (!IsInt(a)) throw new PyError("You can only repeat text or a list a whole number of times.", line);
                    long times = Math.Max(0, AsLong(a));
                    if (b is string rs)
                    {
                        if (rs.Length * times > RunLimits.MaxCollection) throw new PyError("That text would get too long.", line);
                        var sb = new StringBuilder();
                        for (long i = 0; i < times; i++) sb.Append(rs);
                        return sb.ToString();
                    }
                    var rl = (List<object>)b;
                    if (rl.Count * times > RunLimits.MaxCollection) throw new PyError("That list would get too big.", line);
                    var result = new List<object>();
                    for (long i = 0; i < times; i++) result.AddRange(rl);
                    return result;
                }
            }

            if (!IsNum(a) || !IsNum(b))
                throw new PyError("Can't use '" + op + "' with " + TypeName(a) + " and " + TypeName(b) + ".", line);

            if (op == "/")
            {
                double d = AsDouble(b);
                if (d == 0) throw new PyError("Can't divide by zero.", line);
                return AsDouble(a) / d;
            }

            if (IsInt(a) && IsInt(b))
            {
                long x = AsLong(a), y = AsLong(b);
                switch (op)
                {
                    case "+": return checked(x + y);
                    case "-": return checked(x - y);
                    case "*": return checked(x * y);
                    case "//":
                    {
                        if (y == 0) throw new PyError("Can't divide by zero.", line);
                        long q = checked(x / y);
                        if (x % y != 0 && ((x < 0) != (y < 0))) q--;
                        return q;
                    }
                    case "%":
                    {
                        if (y == 0) throw new PyError("Can't divide by zero.", line);
                        long r = y == -1 ? 0 : x % y;
                        if (r != 0 && ((r < 0) != (y < 0))) r += y;
                        return r;
                    }
                    case "**":
                    {
                        if (y < 0) return Math.Pow(x, y);
                        if (x == 0 || x == 1) return y == 0 ? 1L : x;
                        if (x == -1) return y % 2 == 0 ? 1L : -1L;
                        long result = 1;
                        for (long i = 0; i < y; i++)
                        {
                            result = checked(result * x);
                        }
                        return result;
                    }
                }
            }
            else
            {
                double x = AsDouble(a), y = AsDouble(b);
                switch (op)
                {
                    case "+": return x + y;
                    case "-": return x - y;
                    case "*": return x * y;
                    case "//":
                        if (y == 0) throw new PyError("Can't divide by zero.", line);
                        return Math.Floor(x / y);
                    case "%":
                    {
                        if (y == 0) throw new PyError("Can't divide by zero.", line);
                        double r = x % y;
                        if (r != 0 && ((r < 0) != (y < 0))) r += y;
                        return r;
                    }
                    case "**": return Math.Pow(x, y);
                }
            }
            throw new PyError("Unsupported operator " + op + ".", line);
        }
    }

    // ------------------------------------------------------------------ public API

    public sealed class RunResult
    {
        public bool Success;
        public string Output = string.Empty;
        public string Error;
        public int ErrorLine;
    }

    [Serializable]
    public sealed class RunTest
    {
        /// <summary>Lines run before the player's code, e.g. "price = 2" (use a new line for several).</summary>
        public string setup;
        /// <summary>What the finished program must print.</summary>
        public string expectedOutput;
    }

    public sealed class JudgeResult
    {
        public bool Passed;
        /// <summary>Plain-language explanation for the player when the answer is not accepted.</summary>
        public string Message = string.Empty;
        public string Output = string.Empty;
        public string Error;
        public int FailedTest = -1;
    }

    /// <summary>Entry point for running the player's code safely: no files, network, imports or reflection, and hard limits.</summary>
    public static class CodeRunner
    {
        public const string Blank = "____";
        public const int MaxFillLength = 120;

        /// <summary>Runs a whole program. Never throws.</summary>
        public static RunResult Run(string source)
        {
            var interpreter = new Interpreter();
            try
            {
                List<SrcLine> lines = Lexer.Lex(source);
                Node program = new Parser(lines).ParseProgram();
                interpreter.Run(program);
                return new RunResult { Success = true, Output = interpreter.Output };
            }
            catch (PyError e)
            {
                return new RunResult { Success = false, Output = interpreter.Output, Error = e.Message, ErrorLine = e.Line };
            }
            catch (Exception)
            {
                // Anything unexpected (including a deep-recursion failure) is reported as a normal error.
                return new RunResult { Success = false, Output = interpreter.Output, Error = "Something in this code couldn't be run." };
            }
        }

        /// <summary>Checks the typed text itself before it goes into the template. Null when fine.</summary>
        public static string ValidateFill(string fill)
        {
            if (string.IsNullOrWhiteSpace(fill)) return "Type something in the blank first.";
            if (fill.Length > MaxFillLength) return "That's too long for the blank (max " + MaxFillLength + " characters).";
            if (fill.IndexOf('\n') >= 0 || fill.IndexOf('\r') >= 0) return "Keep your answer on one line.";
            return null;
        }

        /// <summary>The template with the blank filled in. Null when the template has no blank.</summary>
        public static string Fill(string template, string fill)
        {
            if (string.IsNullOrEmpty(template) || !template.Contains(Blank)) return null;
            int at = template.IndexOf(Blank, StringComparison.Ordinal);
            return template.Substring(0, at) + fill.Trim() + template.Substring(at + Blank.Length);
        }

        /// <summary>Runs one test: its setup lines, then the filled-in template.</summary>
        public static RunResult RunWithSetup(string template, string fill, string setup)
        {
            string problem = ValidateFill(fill);
            if (problem != null) return new RunResult { Success = false, Error = problem };
            string filled = Fill(template, fill);
            if (filled == null) return new RunResult { Success = false, Error = "This question has no blank to fill." };

            string head = string.IsNullOrWhiteSpace(setup) ? string.Empty : setup.Trim('\r', '\n') + "\n";
            int offset = head.Length == 0 ? 0 : head.Split('\n').Length - 1;
            RunResult result = Run(head + filled);
            if (result.ErrorLine > 0) result.ErrorLine = Math.Max(1, result.ErrorLine - offset);
            return result;
        }

        /// <summary>Runs the code against every test. Passed only if all outputs match.</summary>
        public static JudgeResult Judge(string template, string fill, IList<RunTest> tests)
        {
            var judged = new JudgeResult();
            if (tests == null || tests.Count == 0)
            {
                judged.Message = "This question has no tests.";
                return judged;
            }

            for (int i = 0; i < tests.Count; i++)
            {
                RunResult r = RunWithSetup(template, fill, tests[i].setup);
                judged.Output = r.Output;
                if (!r.Success)
                {
                    judged.FailedTest = i;
                    judged.Error = r.Error;
                    judged.Message = r.ErrorLine > 0 ? "Line " + r.ErrorLine + ": " + r.Error : r.Error;
                    return judged;
                }
                if (Normalize(r.Output) != Normalize(tests[i].expectedOutput))
                {
                    judged.FailedTest = i;
                    string context = Context(tests[i].setup);
                    judged.Message = "Your code printed:\n" + (Normalize(r.Output).Length == 0 ? "(nothing)" : Normalize(r.Output)) +
                        "\nbut it should print:\n" + Normalize(tests[i].expectedOutput) +
                        (context.Length > 0 ? "\n(when " + context + ")" : string.Empty);
                    return judged;
                }
            }
            judged.Passed = true;
            return judged;
        }

        static string Context(string setup)
        {
            if (string.IsNullOrWhiteSpace(setup)) return string.Empty;
            var parts = new List<string>();
            foreach (string l in setup.Split('\n'))
                if (!string.IsNullOrWhiteSpace(l)) parts.Add(l.Trim());
            return string.Join(", ", parts);
        }

        static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd();
            return string.Join("\n", lines).Trim('\n');
        }
    }
}
