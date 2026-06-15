using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeDictionary.Analysis
{
    public sealed class BslSyntaxError
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
        
        // Useful for UI display
        public string Position => $"Стр. {Line}, Кол. {Column}";
    }

    public sealed class AnalysisResult
    {
        public IReadOnlyList<BslSyntaxError> Errors { get; }
        public IReadOnlyList<SymbolInfo> Symbols { get; }

        public AnalysisResult(IEnumerable<BslSyntaxError> errors, IEnumerable<SymbolInfo> symbols)
        {
            Errors = errors?.ToList() ?? new List<BslSyntaxError>();
            Symbols = symbols?.ToList() ?? new List<SymbolInfo>();
        }

        public bool HasErrors => Errors.Any();
        public bool HasSymbols => Symbols.Any();
    }
}
