using ICSharpCode.AvalonEdit.CodeCompletion;

namespace CodeDictionary.SyntaxChecking.Providers
{
    public class BslGlobalContextProvider : IBslCompletionProvider
    {
        public string Name => "Глобальный контекст";

        public bool IsApplicable(BslSyntaxContext context)
        {
            return context.Kind is BslContextKind.Expression
                or BslContextKind.StatementStart
                or BslContextKind.MethodCallArgument;
        }

        public IEnumerable<ICompletionData> GetCompletions(BslSyntaxContext context, BslAnalysisSnapshot analysis)
        {
            foreach (var (name, type, _) in BslGlobalContext.Procedures)
            {
                yield return new BslCompletionData(name, "Процедура", type);
            }

            foreach (var (name, type, _) in BslGlobalContext.Functions)
            {
                yield return new BslCompletionData(name, "Функция", type);
            }
        }
    }
}
