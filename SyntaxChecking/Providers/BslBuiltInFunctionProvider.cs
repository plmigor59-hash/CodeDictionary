using ICSharpCode.AvalonEdit.CodeCompletion;

namespace CodeDictionary.SyntaxChecking.Providers
{
    public class BslBuiltInFunctionProvider : IBslCompletionProvider
    {
        public string Name => "Встроенные функции";

        public bool IsApplicable(BslSyntaxContext context)
        {
            return context.Kind is BslContextKind.Expression
                or BslContextKind.StatementStart
                or BslContextKind.MethodCallArgument;
        }

        public IEnumerable<ICompletionData> GetCompletions(BslSyntaxContext context, BslAnalysisSnapshot analysis)
        {
            foreach (var (name, type) in BslBuiltInFunctions.GetAll())
            {
                yield return new BslCompletionData(name, name, type);
            }
        }
    }
}
