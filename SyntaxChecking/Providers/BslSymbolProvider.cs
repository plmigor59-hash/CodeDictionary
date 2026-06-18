using ICSharpCode.AvalonEdit.CodeCompletion;

namespace CodeDictionary.SyntaxChecking.Providers
{
    public class BslSymbolProvider : IBslCompletionProvider
    {
        public string Name => "Символы";

        public bool IsApplicable(BslSyntaxContext context)
        {
            return context.Kind is BslContextKind.Expression
                or BslContextKind.StatementStart
                or BslContextKind.MethodCallArgument;
        }

        public IEnumerable<ICompletionData> GetCompletions(BslSyntaxContext context, BslAnalysisSnapshot analysis)
        {
            foreach (var symbol in analysis.Symbols)
            {
                var displayText = symbol.ParameterCount > 0
                    ? symbol.Signature
                    : symbol.Name;

                yield return new BslCompletionData(symbol.Name, displayText, symbol.Type);
            }
        }
    }
}
