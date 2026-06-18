namespace CodeDictionary.Analysis
{
    public sealed class CodeSyntaxError
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public int Length { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;
    }

    public sealed class SymbolInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Line { get; set; }
        public int Column { get; set; }
        public string[] ParameterNames { get; set; } = [];
        public string[] ParameterTypes { get; set; } = [];

        public int ParameterCount => ParameterNames?.Length ?? 0;

        public string Signature => ParameterCount > 0
            ? $"{Name}({string.Join(", ", ParameterNames)})"
            : Name;

        public string Position => $"Стр. {Line}, Кол. {Column}";
    }

    public sealed class AnalysisResult
    {
        public IReadOnlyList<CodeSyntaxError> Errors { get; }
        public IReadOnlyList<SymbolInfo> Symbols { get; }

        public AnalysisResult(IEnumerable<CodeSyntaxError> errors, IEnumerable<SymbolInfo> symbols)
        {
            Errors = errors?.ToList() ?? new List<CodeSyntaxError>();
            Symbols = symbols?.ToList() ?? new List<SymbolInfo>();
        }

        public bool HasErrors => Errors.Any();
        public bool HasSymbols => Symbols.Any();
    }
}
