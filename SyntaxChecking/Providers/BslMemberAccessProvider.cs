using ICSharpCode.AvalonEdit.CodeCompletion;

namespace CodeDictionary.SyntaxChecking.Providers
{
    public class BslMemberAccessProvider : IBslCompletionProvider
    {
        public string Name => "Члены объекта";

        public bool IsApplicable(BslSyntaxContext context)
        {
            return context.Kind == BslContextKind.MemberAccess;
        }

        public IEnumerable<ICompletionData> GetCompletions(BslSyntaxContext context, BslAnalysisSnapshot analysis)
        {
            var typeName = ResolveTypeName(context.LeftSide, analysis.VariableTypes);
            if (string.IsNullOrEmpty(typeName))
                yield break;

            if (!BslTypeSystem.KnownTypes.TryGetValue(typeName, out var typeInfo))
            {
                foreach (var kv in BslTypeSystem.KnownTypes)
                {
                    if (string.Equals(kv.Key, typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        typeInfo = kv.Value;
                        break;
                    }
                }
                if (typeInfo == null)
                    yield break;
            }

            foreach (var member in typeInfo.Members.Values)
            {
                var displayText = member.IsFunction
                    ? $"{member.Name}()"
                    : member.Name;

                yield return new BslCompletionData(member.Name, displayText,
                    member.IsFunction ? "Функция" : "Процедура");
            }
        }

        private static string? ResolveTypeName(string leftSide, IReadOnlyDictionary<string, string> variableTypes)
        {
            if (string.IsNullOrEmpty(leftSide))
                return null;

            var varName = leftSide.Split('.').First();

            if (variableTypes.TryGetValue(varName, out var mappedType))
                return mappedType;

            return null;
        }
    }
}
