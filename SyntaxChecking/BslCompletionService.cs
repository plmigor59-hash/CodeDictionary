using ICSharpCode.AvalonEdit.CodeCompletion;
using CodeDictionary.SyntaxChecking.Providers;

namespace CodeDictionary.SyntaxChecking
{
    public class BslCompletionService
    {
        private readonly List<IBslCompletionProvider> _providers;

        public BslCompletionService()
        {
            _providers =
            [
                new BslKeywordProvider(),
                new BslSymbolProvider(),
                new BslBuiltInFunctionProvider(),
                new BslMemberAccessProvider(),
                new BslNewTypeProvider(),
            ];
        }

        public IReadOnlyList<IBslCompletionProvider> Providers => _providers;

        public List<ICompletionData> GetCompletions(BslSyntaxContext context, BslAnalysisSnapshot analysis)
        {
            var result = new List<ICompletionData>();

            foreach (var provider in _providers)
            {
                if (provider.IsApplicable(context))
                {
                    result.AddRange(provider.GetCompletions(context, analysis));
                }
            }

            return result
                .GroupBy(d => d.Text, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(d => d.Text)
                .ToList();
        }
    }
}
