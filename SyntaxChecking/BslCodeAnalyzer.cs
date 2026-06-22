using CodeDictionary.Analysis;
using OneScript.Language;
using OneScript.Language.LexicalAnalysis;
using OneScript.Language.SyntaxAnalysis;
using OneScript.Sources;

namespace CodeDictionary.SyntaxChecking
{
    public class BslCodeAnalyzer : ICodeAnalysisService
    {
        private readonly object _lock = new();
        private readonly List<CodeSyntaxError> _errors = [];
        private readonly List<SymbolInfo> _symbols = [];
        private IReadOnlyDictionary<string, string> _variableTypes = new Dictionary<string, string>();

        public IReadOnlyList<CodeSyntaxError> Errors
        {
            get { lock (_lock) return _errors.ToList(); }
        }
        public IReadOnlyList<SymbolInfo> Symbols
        {
            get { lock (_lock) return _symbols.ToList(); }
        }
        public IReadOnlyDictionary<string, string> VariableTypes
        {
            get { lock (_lock) return new Dictionary<string, string>(_variableTypes); }
        }
        public bool HasErrors => _errors.Any();

        public async Task<AnalysisResult> AnalyzeAsync(string code, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => Analyze(code), cancellationToken);
        }

        public AnalysisResult Analyze(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return new AnalysisResult(Enumerable.Empty<CodeSyntaxError>(), Enumerable.Empty<SymbolInfo>());
            }

            // Предварительная обработка: удаляем строки, начинающиеся с #
            var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var filteredLines = lines.Select(line => line.TrimStart().StartsWith("#") ? "" : line);
            var sanitizedCode = string.Join(Environment.NewLine, filteredLines);

            var newErrors = new List<CodeSyntaxError>();
            var newSymbols = new List<SymbolInfo>();

            try
            {
                var source = new StringCodeSource(sanitizedCode);
                var sourceCode = SourceCodeBuilder.Create()
                                 .FromSource(source)
                                 .Build();
                var iterator = new SourceCodeIterator(sourceCode);
                var lexer = new DefaultLexer
                {
                    Iterator = iterator
                };


                lexer.UnexpectedCharacterFound += (s, args) =>
                {
                    newErrors.Add(new CodeSyntaxError
                    {
                        Line = args.Iterator.CurrentLine,
                        Column = args.Iterator.CurrentColumn,
                        Length = 1,
                        Message = "Неожиданный символ",
                        ErrorType = "UnexpectedCharacter"
                    });
                    args.Iterator.MoveNext();
                    args.IsHandled = true;
                };

                var errorSink = new ListErrorSink();
                var preprocessor = new PreprocessorHandlers();
                var parser = new DefaultBslParser(lexer, errorSink, preprocessor);
                var parserResult = parser.ParseStatefulModule();

                foreach (var error in errorSink.Errors)
                {
                    int line = error.Position.LineNumber;
                    int column = error.Position.ColumnNumber;
                    int length = GetIdentifierLength(code, line, column);

                    newErrors.Add(new CodeSyntaxError
                    {
                        Line = line,
                        Column = column,
                        Length = length,
                        Message = error.Description,
                        ErrorType = "SyntaxError"
                    });
                }

                if (parserResult != null)
                {
                    var extractor = new SymbolExtractor();
                    extractor.Visit(parserResult);

                    foreach (var symbol in extractor.Symbols)
                    {
                        newSymbols.Add(symbol);
                    }

                    foreach (var error in extractor.Errors)
                    {
                        // Update length for extractor errors too
                        error.Length = GetIdentifierLength(code, error.Line, error.Column);
                        newErrors.Add(error);
                    }

                    lock (_lock)
                    {
                        // Merge: keep old types when new analysis can't resolve them
                        // (e.g. after typing '.' the code becomes temporarily invalid)
                        if (_variableTypes is Dictionary<string, string> vt)
                        {
                            foreach (var kv in extractor.VariableTypes)
                                vt[kv.Key] = kv.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                newErrors.Add(new CodeSyntaxError
                {
                    Line = 1,
                    Column = 1,
                    Length = 1,
                    Message = $"Ошибка анализа: {ex.Message}",
                    ErrorType = "FatalError"
                });
            }

            lock (_lock)
            {
                _errors.Clear();
                _errors.AddRange(newErrors);
                _symbols.Clear();
                _symbols.AddRange(newSymbols);
            }

            return new AnalysisResult(newErrors, newSymbols);
        }

        private int GetIdentifierLength(string code, int line, int column)
        {
            if (string.IsNullOrEmpty(code)) return 1;

            try
            {
                var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                if (line <= 0 || line > lines.Length) return 1;

                string lineText = lines[line - 1];
                int startIdx = column - 1;
                if (startIdx < 0 || startIdx >= lineText.Length) return 1;

                int length = 0;
                for (int i = startIdx; i < lineText.Length; i++)
                {
                    char c = lineText[i];
                    if (char.IsLetterOrDigit(c) || c == '_')
                        length++;
                    else
                        break;
                }

                return Math.Max(1, length);
            }
            catch
            {
                return 1;
            }
        }
    }
}
