using ICSharpCode.AvalonEdit.CodeCompletion;

namespace CodeDictionary.SyntaxChecking.Providers
{
    public class BslNewTypeProvider : IBslCompletionProvider
    {
        public string Name => "Конструкторы";

        public bool IsApplicable(BslSyntaxContext context)
        {
            return context.Kind == BslContextKind.NewObject;
        }

        public IEnumerable<ICompletionData> GetCompletions(BslSyntaxContext context, BslAnalysisSnapshot analysis)
        {
            foreach (var typeName in BslTypeSystem.KnownTypes.Keys)
            {
                yield return new BslCompletionData(typeName, typeName, "Тип");
            }
        }
    }
}
