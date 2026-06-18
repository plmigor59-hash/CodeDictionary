using CodeDictionary.Analysis;
using OneScript.Language;
using OneScript.Language.LexicalAnalysis;
using OneScript.Language.Sources;
using OneScript.Sources;

namespace CodeDictionary.SyntaxChecking
{
    public class SignatureInfo
    {
        public string MethodName { get; set; } = "";
        public string[] ParameterNames { get; set; } = [];
        public string[] ParameterTypes { get; set; } = [];
        public int CurrentParameterIndex { get; set; }
        public bool Found { get; set; }

        public string FullSignature =>
            $"{MethodName}({string.Join(", ", ParameterNames)})";

        public string HighlightedSignature
        {
            get
            {
                if (ParameterNames.Length == 0)
                    return $"{MethodName}()";

                var parts = new List<string>();
                for (int i = 0; i < ParameterNames.Length; i++)
                {
                    var param = ParameterTypes.Length > i && !string.IsNullOrEmpty(ParameterTypes[i])
                        ? $"{ParameterTypes[i]} {ParameterNames[i]}"
                        : ParameterNames[i];

                    if (i == CurrentParameterIndex)
                        parts.Add($"[{param}]");
                    else
                        parts.Add(param);
                }

                return $"{MethodName}({string.Join(", ", parts)})";
            }
        }
    }

    public class BslSignatureHelpService
    {
        public SignatureInfo? GetSignature(string code, int position, IReadOnlyList<SymbolInfo> symbols)
        {
            if (string.IsNullOrEmpty(code) || position <= 0 || position > code.Length)
                return null;

            try
            {
                var source = new SignStringCodeSource(code);
                var sourceCode = SourceCodeBuilder.Create().FromSource(source).Build();
                var iterator = new SourceCodeIterator(sourceCode);
                var lexer = new DefaultLexer { Iterator = iterator };

                // Collect lexems up to cursor
                var lexems = new List<Lexem>();
                while (true)
                {
                    var lexem = lexer.NextLexem();
                    if (lexem.Type == LexemType.EndOfText)
                        break;
                    lexems.Add(lexem);
                    if (iterator.Position >= position)
                        break;
                }

                if (lexems.Count == 0)
                    return null;

                // Find the '(' that triggered this
                int parenIdx = -1;
                for (int i = 0; i < lexems.Count; i++)
                {
                    if (lexems[i].Token == Token.OpenPar)
                    {
                        parenIdx = i;
                    }
                }

                if (parenIdx < 1)
                    return null;

                // The method name should be right before '('
                var methodLex = lexems[parenIdx - 1];
                var methodName = methodLex.Content;

                if (string.IsNullOrEmpty(methodName))
                    return null;

                // Count commas between the last '(' and cursor to find current parameter
                int currentParam = 0;
                int lastParen = parenIdx;
                for (int i = parenIdx + 1; i < lexems.Count; i++)
                {
                    if (lexems[i].Token == Token.Comma)
                        currentParam++;
                }

                // Look up in symbols
                var symbol = symbols.FirstOrDefault(s =>
                    s.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

                if (symbol != null)
                {
                    return new SignatureInfo
                    {
                        MethodName = methodName,
                        ParameterNames = symbol.ParameterNames,
                        ParameterTypes = symbol.ParameterTypes,
                        CurrentParameterIndex = Math.Min(currentParam, Math.Max(0, symbol.ParameterCount - 1)),
                        Found = true
                    };
                }

                // Look up in type system
                foreach (var type in BslTypeSystem.KnownTypes.Values)
                {
                    if (type.Members.TryGetValue(methodName, out var member))
                    {
                        return new SignatureInfo
                        {
                            MethodName = methodName,
                            ParameterNames = member.ParameterNames,
                            ParameterTypes = member.ParameterTypes,
                            CurrentParameterIndex = Math.Min(currentParam, Math.Max(0, member.ParameterNames.Length - 1)),
                            Found = true
                        };
                    }
                }

                return new SignatureInfo
                {
                    MethodName = methodName,
                    Found = false
                };
            }
            catch
            {
                return null;
            }
        }

        private class SignStringCodeSource : ICodeSource
        {
            public string Location => "memory";
            private readonly string _code;
            public SignStringCodeSource(string code) => _code = code;
            public string GetSourceCode() => _code;
        }
    }
}
