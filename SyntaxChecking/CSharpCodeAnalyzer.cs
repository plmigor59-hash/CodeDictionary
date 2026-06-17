using CodeDictionary.Analysis;
using CodeDictionary.Services;
using Microsoft.CodeAnalysis;

namespace CodeDictionary.SyntaxChecking
{
    public class CSharpCodeAnalyzer : ICodeAnalysisService
    {
        private readonly RoslynCompletionService _roslynService;

        public CSharpCodeAnalyzer(RoslynCompletionService roslynService)
        {
            _roslynService = roslynService;
        }

        public async Task<AnalysisResult> AnalyzeAsync(string code, CancellationToken cancellationToken = default)
        {
            _roslynService.UpdateCode(code);
            var diagnostics = await _roslynService.GetDiagnosticsAsync();

            var errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning)
                .Select(d =>
                {
                    var lineSpan = d.Location.GetLineSpan();
                    // Roslyn lines are 0-based, our UI uses 1-based
                    return new CodeSyntaxError
                    {
                        Line = lineSpan.StartLinePosition.Line + 1 - (_roslynService.OffsetShift > 0 ? CountLines(_roslynService.HiddenUsings) : 0),
                        Column = lineSpan.StartLinePosition.Character + 1,
                        Length = d.Location.SourceSpan.Length,
                        Message = d.GetMessage(),
                        ErrorType = d.Severity.ToString()
                    };
                })
                .Where(e => e.Line > 0) // Filter out errors in hidden usings
                .ToList();

            return new AnalysisResult(errors, Enumerable.Empty<CodeDictionary.Analysis.SymbolInfo>());
        }

        private int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n' && (i == 0 || text[i - 1] != '\r'))
                    count++;
                else if (text[i] == '\r')
                    count++;
            }
            return count;
        }
    }
}
