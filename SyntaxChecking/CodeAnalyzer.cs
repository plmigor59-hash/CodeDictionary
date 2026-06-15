using OneScript.Language;
using OneScript.Language.LexicalAnalysis;
using OneScript.Language.Sources;
using OneScript.Language.SyntaxAnalysis;
using OneScript.Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeDictionary.Analysis;

namespace CodeDictionary.SyntaxChecking
{
    public class CodeAnalyzer : IOneScriptAnalysisService
    {
        private readonly object _lock = new();
        private readonly List<BslSyntaxError> _errors = [];
        private readonly List<SymbolInfo> _symbols = [];

        public IReadOnlyList<BslSyntaxError> Errors => _errors;
        public IReadOnlyList<SymbolInfo> Symbols => _symbols;
        public bool HasErrors => _errors.Any();

        public async Task<AnalysisResult> AnalyzeAsync(string code, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => Analyze(code), cancellationToken);
        }

        public AnalysisResult Analyze(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return new AnalysisResult(Enumerable.Empty<BslSyntaxError>(), Enumerable.Empty<SymbolInfo>());
            }

            // Предварительная обработка: удаляем строки, начинающиеся с #
            var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var filteredLines = lines.Select(line => line.TrimStart().StartsWith("#") ? "" : line);
            var sanitizedCode = string.Join(Environment.NewLine, filteredLines);

            var newErrors = new List<BslSyntaxError>();
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
                    newErrors.Add(new BslSyntaxError
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

                    newErrors.Add(new BslSyntaxError
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
                }
            }
            catch (Exception ex)
            {
                newErrors.Add(new BslSyntaxError
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

        public class StringCodeSource : ICodeSource
        {
            public string Location => "memory";
            private readonly string _code;

            public StringCodeSource(string code)
            {
                _code = code;
            }

            public string GetSourceCode() => _code;
        }
    }
}
