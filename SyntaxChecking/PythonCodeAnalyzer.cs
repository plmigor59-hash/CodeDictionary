using CodeDictionary.Analysis;

namespace CodeDictionary.SyntaxChecking
{
    public class PythonCodeAnalyzer : ICodeAnalysisService
    {
        private static readonly HashSet<string> BlockKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "if", "elif", "else", "for", "while", "def", "class", "try",
            "except", "finally", "with", "async"
        };

        private static readonly HashSet<string> LoopKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "break", "continue"
        };

        private static readonly HashSet<string> ReturnYieldKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "return", "yield"
        };

        public Task<AnalysisResult> AnalyzeAsync(string code, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Analyze(code), cancellationToken);
        }

        public AnalysisResult Analyze(string code)
        {
            var errors = new List<CodeSyntaxError>();

            if (string.IsNullOrWhiteSpace(code))
                return new AnalysisResult(errors, Enumerable.Empty<SymbolInfo>());

            var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var indentStack = new Stack<int>();
            indentStack.Push(0);
            bool inFunction = false;
            bool inLoop = false;
            bool hasAsyncPrefix = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                int lineNum = i + 1;

                var (stripped, indent, hasTabs, hasSpaces) = AnalyzeIndent(line);

                if (stripped.Length == 0 || stripped.StartsWith("#"))
                    continue;

                if (hasTabs && hasSpaces)
                {
                    errors.Add(MakeError(lineNum, 1, 1,
                        "Смешивание табуляции и пробелов в отступах", "Indentation"));
                }

                if (stripped.StartsWith("\"\"\"") || stripped.StartsWith("'''"))
                {
                    var closing = stripped.StartsWith("\"\"\"") ? "\"\"\"" : "'''";
                    int endIdx = stripped.IndexOf(closing, 3);
                    if (endIdx == -1)
                    {
                        errors.Add(MakeError(lineNum, 1, 1,
                            "Незакрытая тройная кавычка", "StringError"));
                        continue;
                    }
                    continue;
                }

                if (IsInsideString(line, out var strError))
                {
                    if (strError != null)
                        errors.Add(MakeError(lineNum, strError.Value.col, strError.Value.len,
                            strError.Value.msg, "StringError"));
                    continue;
                }

                if (!CheckBrackets(line, lineNum, errors))
                    continue;

                var tokens = TokenizeLine(stripped);

                if (tokens.Count == 0)
                    continue;

                string firstToken = tokens[0].ToLower();

                if (firstToken == "async" && tokens.Count > 1 && tokens[1].ToLower() == "def")
                {
                    hasAsyncPrefix = true;
                    firstToken = "def";
                }
                else if (firstToken == "async" && tokens.Count > 1 && tokens[1].ToLower() == "with")
                {
                    hasAsyncPrefix = true;
                    firstToken = "with";
                }
                else
                {
                    hasAsyncPrefix = false;
                }

                if (BlockKeywords.Contains(firstToken))
                {
                    if (firstToken == "elif" || firstToken == "else" || firstToken == "except" || firstToken == "finally")
                    {
                        if (indent > 0 && !indentStack.Contains(indent))
                        {
                            errors.Add(MakeError(lineNum, 1, indent,
                                $"Неверный отступ для '{firstToken}'", "Indentation"));
                            continue;
                        }
                    }

                    if (firstToken != "else" && firstToken != "finally")
                    {
                        if (!stripped.TrimEnd().EndsWith(":") && !stripped.TrimEnd().EndsWith(":"))
                        {
                            errors.Add(MakeError(lineNum, tokens[0].StartsWith("async") ? 6 : 1,
                                firstToken.Length,
                                $"Пропущено двоеточие ':' в конце строки с '{firstToken}'", "SyntaxError"));
                        }
                    }
                    else if (!stripped.TrimEnd().EndsWith(":"))
                    {
                        errors.Add(MakeError(lineNum, 1, firstToken.Length,
                            $"Пропущено двоеточие ':' после '{firstToken}'", "SyntaxError"));
                    }
                }

                if (firstToken == "return" || firstToken == "yield")
                {
                    if (!inFunction)
                    {
                        errors.Add(MakeError(lineNum, 1, firstToken.Length,
                            $"'{firstToken}' вне функции", "ScopeError"));
                    }
                }

                if (LoopKeywords.Contains(firstToken))
                {
                    if (!inLoop)
                    {
                        errors.Add(MakeError(lineNum, 1, firstToken.Length,
                            $"'{firstToken}' вне цикла", "ScopeError"));
                    }
                }

                int? nextIndent = null;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var nextLine = lines[j];
                    if (string.IsNullOrWhiteSpace(nextLine) || nextLine.TrimStart().StartsWith("#"))
                        continue;
                    nextIndent = GetIndentLevel(nextLine);
                    break;
                }

                if (nextIndent.HasValue && nextIndent > indent && !stripped.TrimEnd().EndsWith(":"))
                {
                    errors.Add(MakeError(lineNum, 1, 1,
                        "Неожиданный отступ: строка не заканчивается на ':'", "Indentation"));
                }

                if (firstToken == "def")
                    inFunction = true;
                else if (firstToken == "for" || firstToken == "while")
                    inLoop = true;

                bool isBlockEnd = nextIndent.HasValue && nextIndent <= indent &&
                                  (firstToken != "elif" && firstToken != "else" &&
                                   firstToken != "except" && firstToken != "finally");
                if (isBlockEnd)
                {
                    if (firstToken == "def")
                    {
                        bool hasInnerDef = false;
                        for (int j = i + 1; j < lines.Length; j++)
                        {
                            var inner = lines[j].Trim();
                            if (inner.StartsWith("#") || inner.Length == 0) continue;
                            if (inner.StartsWith("def ") || inner.StartsWith("class "))
                            {
                                hasInnerDef = true;
                                break;
                            }
                            if (!inner.StartsWith(" ") && !inner.StartsWith("\t"))
                                break;
                        }
                        if (!hasInnerDef)
                            inFunction = false;
                    }
                    else if (firstToken == "for" || firstToken == "while")
                    {
                        inLoop = false;
                    }
                }
            }

            return new AnalysisResult(errors, Enumerable.Empty<SymbolInfo>());
        }

        private static (string stripped, int indent, bool hasTabs, bool hasSpaces) AnalyzeIndent(string line)
        {
            int indent = 0;
            bool hasTabs = false;
            bool hasSpaces = false;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == ' ')
                {
                    indent++;
                    hasSpaces = true;
                }
                else if (line[i] == '\t')
                {
                    indent += 4;
                    hasTabs = true;
                }
                else
                {
                    break;
                }
            }

            return (line.TrimStart(), indent, hasTabs, hasSpaces);
        }

        private static int GetIndentLevel(string line)
        {
            int count = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == ' ') count++;
                else if (line[i] == '\t') count += 4;
                else break;
            }
            return count;
        }

        private static List<string> TokenizeLine(string line)
        {
            var tokens = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inToken = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '#' && !inToken)
                    break;

                if (char.IsWhiteSpace(c))
                {
                    if (inToken)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        inToken = false;
                    }
                    continue;
                }

                if (c == '(' || c == ')' || c == '[' || c == ']' || c == '{' || c == '}' ||
                    c == ':' || c == ',' || c == ';' || c == '+' || c == '-' || c == '*' ||
                    c == '/' || c == '%' || c == '=' || c == '<' || c == '>' || c == '!' ||
                    c == '&' || c == '|' || c == '^' || c == '~' || c == '@')
                {
                    if (inToken)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        inToken = false;
                    }
                    tokens.Add(c.ToString());
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    if (inToken)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        inToken = false;
                    }

                    var str = ExtractStringLiteral(line, ref i);
                    if (str != null)
                        tokens.Add(str);
                    continue;
                }

                inToken = true;
                current.Append(c);
            }

            if (inToken)
                tokens.Add(current.ToString());

            return tokens;
        }

        private static string? ExtractStringLiteral(string line, ref int pos)
        {
            char quote = line[pos];
            int start = pos;

            if (pos + 2 < line.Length && line[pos + 1] == quote && line[pos + 2] == quote)
            {
                pos += 3;
                while (pos < line.Length)
                {
                    if (pos + 2 < line.Length && line[pos] == quote && line[pos + 1] == quote && line[pos + 2] == quote)
                    {
                        pos += 2;
                        break;
                    }
                    pos++;
                }
            }
            else
            {
                pos++;
                while (pos < line.Length)
                {
                    if (line[pos] == '\\')
                    {
                        pos += 2;
                        continue;
                    }
                    if (line[pos] == quote)
                        break;
                    pos++;
                }
            }

            int end = pos < line.Length ? pos : line.Length - 1;
            return line.Substring(start, end - start + 1);
        }

        private static bool IsInsideString(string line, out (int col, int len, string msg)? error)
        {
            error = null;
            char? activeQuote = null;
            bool isTriple = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '#')
                    break;

                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    if (activeQuote == null)
                    {
                        if (i + 2 < line.Length && line[i + 1] == c && line[i + 2] == c)
                        {
                            activeQuote = c;
                            isTriple = true;
                            i += 2;
                        }
                        else
                        {
                            activeQuote = c;
                            isTriple = false;
                        }
                    }
                    else if (c == activeQuote)
                    {
                        if (isTriple && i + 2 < line.Length && line[i + 1] == c && line[i + 2] == c)
                        {
                            activeQuote = null;
                            i += 2;
                        }
                        else if (!isTriple)
                        {
                            activeQuote = null;
                        }
                    }
                }
            }

            if (activeQuote != null)
            {
                error = (line.Length, 1, $"Незакрытая кавычка '{activeQuote}'");
                return true;
            }

            return false;
        }

        private static bool CheckBrackets(string line, int lineNum, List<CodeSyntaxError> errors)
        {
            var stack = new Stack<(char bracket, int col)>();
            var pairs = new Dictionary<char, char>
            {
                [')'] = '(',
                [']'] = '[',
                ['}'] = '{',
            };
            var openers = new HashSet<char> { '(', '[', '{' };

            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '#' && !inString)
                    break;

                if (c == '\\' && inString)
                {
                    i++;
                    continue;
                }

                if ((c == '"' || c == '\'') && !inString)
                {
                    inString = true;
                    stringChar = c;
                    if (i + 2 < line.Length && line[i + 1] == c && line[i + 2] == c)
                        i += 2;
                    continue;
                }

                if (inString && c == stringChar)
                {
                    if (i + 2 < line.Length && line[i + 1] == stringChar && line[i + 2] == stringChar)
                        i += 2;
                    inString = false;
                    continue;
                }

                if (inString)
                    continue;

                if (openers.Contains(c))
                {
                    stack.Push((c, i + 1));
                }
                else if (pairs.TryGetValue(c, out var expected))
                {
                    if (stack.Count == 0)
                    {
                        errors.Add(MakeError(lineNum, i + 1, 1,
                            $"Неожиданная закрывающая скобка '{c}'", "BracketError"));
                        return true;
                    }

                    var top = stack.Pop();
                    if (top.bracket != expected)
                    {
                        errors.Add(MakeError(lineNum, i + 1, 1,
                            $"Непарная скобка '{c}': ожидалось '{GetClosingChar(top.bracket)}'", "BracketError"));
                        return true;
                    }
                }
            }

            if (stack.Count > 0)
            {
                while (stack.Count > 1) stack.Pop();
                var top = stack.Pop();
                errors.Add(MakeError(lineNum, top.col, 1,
                    $"Незакрытая скобка '{top.bracket}'", "BracketError"));
                return true;
            }

            return true;
        }

        private static char GetClosingChar(char opener)
        {
            return opener switch
            {
                '(' => ')',
                '[' => ']',
                '{' => '}',
                _ => '?',
            };
        }

        private static CodeSyntaxError MakeError(int line, int col, int len, string msg, string type)
        {
            return new CodeSyntaxError
            {
                Line = line,
                Column = col,
                Length = len,
                Message = msg,
                ErrorType = type
            };
        }
    }
}
