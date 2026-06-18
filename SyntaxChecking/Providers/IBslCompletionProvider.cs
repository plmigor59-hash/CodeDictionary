using ICSharpCode.AvalonEdit.CodeCompletion;

namespace CodeDictionary.SyntaxChecking.Providers
{
    public class BslAnalysisSnapshot
    {
        public IReadOnlyList<Analysis.SymbolInfo> Symbols { get; init; } = [];
        public IReadOnlyDictionary<string, string> VariableTypes { get; init; } = new Dictionary<string, string>();
    }

    public interface IBslCompletionProvider
    {
        string Name { get; }
        bool IsApplicable(BslSyntaxContext context);
        IEnumerable<ICompletionData> GetCompletions(BslSyntaxContext context, BslAnalysisSnapshot analysis);
    }
}
