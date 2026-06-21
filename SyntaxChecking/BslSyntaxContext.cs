using OneScript.Language.LexicalAnalysis;
using OneScript.Language.Sources;
using OneScript.Sources;

namespace CodeDictionary.SyntaxChecking
{
    public enum BslContextKind
    {
        StatementStart,
        Expression,
        MemberAccess,
        NewObject,
        MethodCallArgument,
        StringOrComment,
        Unknown
    }

    public class BslSyntaxContext
    {
        public BslContextKind Kind { get; private set; }
        public string LeftSide { get; private set; } = string.Empty;
        public int CursorPosition { get; private set; }
        public bool HasLeftSide => !string.IsNullOrEmpty(LeftSide);

        public static BslSyntaxContext Detect(string code, int offset)
        {
            var ctx = new BslSyntaxContext { CursorPosition = offset };

            if (string.IsNullOrEmpty(code) || offset <= 0 || offset > code.Length)
            {
                ctx.Kind = BslContextKind.Unknown;
                return ctx;
            }

            if (IsInsideStringOrComment(code, offset))
            {
                ctx.Kind = BslContextKind.StringOrComment;
                return ctx;
            }

            // Collect lexems up to cursor position
            var lexems = new List<(Lexem Lex, int EndOffset)>();

            try
            {
                var source = new StringCodeSource(code);
                var sourceCode = SourceCodeBuilder.Create().FromSource(source).Build();
                var iterator = new SourceCodeIterator(sourceCode);
                var lexer = new DefaultLexer { Iterator = iterator };

                while (true)
                {
                    var lexem = lexer.NextLexem();
                    if (lexem.Type == LexemType.EndOfText)
                        break;

                    // Calculate end offset: last character of this lexem
                    int lexEnd = iterator.Position;
                    lexems.Add((lexem, lexEnd));

                    if (lexEnd >= offset)
                        break;
                }
            }
            catch
            {
                ctx.Kind = BslContextKind.Unknown;
                return ctx;
            }

            // Analyze the lexems around the cursor
            if (lexems.Count == 0)
            {
                ctx.Kind = BslContextKind.StatementStart;
                return ctx;
            }

            // Find the lexem at or before cursor
            int cursorIdx = -1;
            for (int i = 0; i < lexems.Count; i++)
            {
                if (lexems[i].EndOffset >= offset)
                {
                    cursorIdx = i;
                    break;
                }
            }
            if (cursorIdx < 0)
                cursorIdx = lexems.Count - 1;

            // Look at the lexem just before the cursor
            int prevIdx = cursorIdx - 1;
            if (prevIdx >= 0)
            {
                var prevLex = lexems[prevIdx].Lex;

                if (prevLex.Token == Token.Dot)
                {
                    ctx.Kind = BslContextKind.MemberAccess;
                    // Collect left side of dot
                    ctx.LeftSide = CollectLeftSide(lexems, prevIdx);
                    return ctx;
                }

                if (prevLex.Token == Token.NewObject)
                {
                    ctx.Kind = BslContextKind.NewObject;
                    return ctx;
                }

                if (prevLex.Token == Token.OpenPar || prevLex.Token == Token.Comma)
                {
                    ctx.Kind = BslContextKind.MethodCallArgument;
                    return ctx;
                }

                // Check if we're at start of statement
                if (prevLex.Token == Token.Semicolon || prevLex.Type == LexemType.EndOperator)
                {
                    ctx.Kind = BslContextKind.StatementStart;
                    return ctx;
                }

                if (IsBlockEndToken(prevLex.Token))
                {
                    ctx.Kind = BslContextKind.StatementStart;
                    return ctx;
                }
            }
            else
            {
                ctx.Kind = BslContextKind.StatementStart;
                return ctx;
            }

            // Check current lexem type
            if (cursorIdx >= 0 && cursorIdx < lexems.Count)
            {
                var curLex = lexems[cursorIdx].Lex;

                if (curLex.Type == LexemType.EndOfText || curLex.Type == LexemType.EndOperator)
                {
                    ctx.Kind = BslContextKind.StatementStart;
                    return ctx;
                }

                if (curLex.Token == Token.Dot)
                {
                    ctx.Kind = BslContextKind.MemberAccess;
                    ctx.LeftSide = CollectLeftSide(lexems, cursorIdx);
                    return ctx;
                }

                if (curLex.Type == LexemType.Identifier || curLex.Type == LexemType.PreprocessorDirective)
                {
                    ctx.Kind = BslContextKind.Expression;
                    return ctx;
                }
            }

            ctx.Kind = BslContextKind.Expression;
            return ctx;
        }

        private static string CollectLeftSide(List<(Lexem Lex, int EndOffset)> lexems, int dotIdx)
        {
            var parts = new List<string>();

            // Walk backwards from the dot
            for (int i = dotIdx - 1; i >= 0; i--)
            {
                var lex = lexems[i].Lex;

                if (lex.Type == LexemType.Identifier)
                {
                    parts.Add(lex.Content);
                }
                else if (lex.Token == Token.Dot)
                {
                    // Continue collecting
                    continue;
                }
                else
                {
                    break;
                }
            }

            parts.Reverse();
            return string.Join(".", parts);
        }

        private static bool IsBlockEndToken(Token token)
        {
            switch (token)
            {
                case Token.EndIf:
                case Token.EndProcedure:
                case Token.EndFunction:
                case Token.Else:
                case Token.ElseIf:
                case Token.EndLoop:
                case Token.EndTry:
                case Token.Exception:
                case Token.Then:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsInsideStringOrComment(string code, int offset)
        {
            return CodeDictionary.Services.CodeStringHelper.IsInsideStringOrComment(code, offset);
        }
    }
}
