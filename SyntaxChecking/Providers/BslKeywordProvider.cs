using ICSharpCode.AvalonEdit.CodeCompletion;

namespace CodeDictionary.SyntaxChecking.Providers
{
    public class BslKeywordProvider : IBslCompletionProvider
    {
        public string Name => "Ключевые слова";

        private static readonly (string Ru, string En, string Type)[] _statementStartKeywords =
        {
            ("Процедура", "Procedure", "Ключевое слово"),
            ("Функция", "Function", "Ключевое слово"),
            ("КонецПроцедуры", "EndProcedure", "Ключевое слово"),
            ("КонецФункции", "EndFunction", "Ключевое слово"),
            ("Если", "If", "Ключевое слово"),
            ("ИначеЕсли", "ElsIf", "Ключевое слово"),
            ("Иначе", "Else", "Ключевое слово"),
            ("КонецЕсли", "EndIf", "Ключевое слово"),
            ("Для", "For", "Ключевое слово"),
            ("Каждого", "Each", "Ключевое слово"),
            ("Цикл", "Do", "Ключевое слово"),
            ("КонецЦикла", "EndDo", "Ключевое слово"),
            ("Пока", "While", "Ключевое слово"),
            ("Попытка", "Try", "Ключевое слово"),
            ("Исключение", "Except", "Ключевое слово"),
            ("КонецПопытки", "EndTry", "Ключевое слово"),
            ("Перем", "Var", "Ключевое слово"),
            ("Возврат", "Return", "Ключевое слово"),
            ("Прервать", "Break", "Ключевое слово"),
            ("Продолжить", "Continue", "Ключевое слово"),
            ("ВызватьИсключение", "Raise", "Ключевое слово"),
            ("Выполнить", "Execute", "Ключевое слово"),
            ("Перейти", "Goto", "Ключевое слово"),
            ("ДобавитьОбработчик", "AddHandler", "Ключевое слово"),
            ("УдалитьОбработчик", "RemoveHandler", "Ключевое слово"),
            ("Асинх", "Async", "Ключевое слово"),
            ("Ждать", "Await", "Ключевое слово"),
            ("Экспорт", "Export", "Ключевое слово"),
            ("Знач", "Val", "Ключевое слово"),
        };

        private static readonly (string Ru, string En, string Type)[] _expressionKeywords =
        {
            ("Истина", "True", "Ключевое слово"),
            ("Ложь", "False", "Ключевое слово"),
            ("Неопределено", "Undefined", "Ключевое слово"),
            ("Null", "Null", "Ключевое слово"),
            ("И", "And", "Ключевое слово"),
            ("Или", "Or", "Ключевое слово"),
            ("Не", "Not", "Ключевое слово"),
            ("Новый", "New", "Ключевое слово"),
            ("Тогда", "Then", "Ключевое слово"),
            ("Из", "In", "Ключевое слово"),
            ("По", "To", "Ключевое слово"),
        };

        public bool IsApplicable(BslSyntaxContext context)
        {
            return context.Kind is BslContextKind.StatementStart or BslContextKind.Expression;
        }

        public IEnumerable<ICompletionData> GetCompletions(BslSyntaxContext context, BslAnalysisSnapshot analysis)
        {
            var keywords = context.Kind == BslContextKind.StatementStart
                ? _statementStartKeywords.Concat(_expressionKeywords)
                : _expressionKeywords;

            foreach (var (ru, en, type) in keywords)
            {
                yield return new BslCompletionData(ru, type, type);
                yield return new BslCompletionData(en, type, type);
            }
        }
    }
}
