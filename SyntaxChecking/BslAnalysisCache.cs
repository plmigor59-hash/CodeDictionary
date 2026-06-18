using CodeDictionary.Analysis;
using OneScript.Language;
using OneScript.Language.LexicalAnalysis;
using OneScript.Language.Sources;
using OneScript.Language.SyntaxAnalysis;
using OneScript.Sources;

namespace CodeDictionary.SyntaxChecking
{
    public class BslAnalysisCache
    {
        private string _lastCode = "";
        private AnalysisResult? _lastResult;
        private SymbolExtractor? _lastExtractor;
        private readonly object _lock = new();

        public async Task<(AnalysisResult Result, SymbolExtractor? Extractor)> AnalyzeAsync(
            string code, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                return (new AnalysisResult([], []), null);

            lock (_lock)
            {
                if (code == _lastCode && _lastResult != null)
                    return (_lastResult, _lastExtractor);
            }

            return await Task.Run(() =>
            {
                var errors = new List<CodeSyntaxError>();
                var symbols = new List<SymbolInfo>();
                SymbolExtractor? extractor = null;

                try
                {
                    var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    var filteredLines = lines.Select(line => line.TrimStart().StartsWith("#") ? "" : line);
                    var sanitizedCode = string.Join(Environment.NewLine, filteredLines);

                    var source = new CacheStringCodeSource(sanitizedCode);
                    var sourceCode = SourceCodeBuilder.Create().FromSource(source).Build();
                    var iterator = new SourceCodeIterator(sourceCode);
                    var lexer = new DefaultLexer { Iterator = iterator };

                    lexer.UnexpectedCharacterFound += (s, args) =>
                    {
                        errors.Add(new CodeSyntaxError
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
                        errors.Add(new CodeSyntaxError
                        {
                            Line = error.Position.LineNumber,
                            Column = error.Position.ColumnNumber,
                            Length = 1,
                            Message = error.Description,
                            ErrorType = "SyntaxError"
                        });
                    }

                    if (parserResult != null)
                    {
                        extractor = new SymbolExtractor();
                        extractor.Visit(parserResult);
                        symbols.AddRange(extractor.Symbols);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new CodeSyntaxError
                    {
                        Line = 1,
                        Column = 1,
                        Length = 1,
                        Message = $"Ошибка анализа: {ex.Message}",
                        ErrorType = "FatalError"
                    });
                }

                var result = new AnalysisResult(errors, symbols);

                lock (_lock)
                {
                    _lastCode = code;
                    _lastResult = result;
                    _lastExtractor = extractor;
                }

                return (result, extractor);
            }, token);
        }

        public void Invalidate()
        {
            lock (_lock)
            {
                _lastCode = "";
                _lastResult = null;
                _lastExtractor = null;
            }
        }

        public (AnalysisResult? Result, SymbolExtractor? Extractor) GetCached()
        {
            lock (_lock)
            {
                return (_lastResult, _lastExtractor);
            }
        }

        private class CacheStringCodeSource : ICodeSource
        {
            public string Location => "memory";
            private readonly string _code;
            public CacheStringCodeSource(string code) => _code = code;
            public string GetSourceCode() => _code;
        }
    }
}
