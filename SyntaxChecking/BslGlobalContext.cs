using System.Collections.Generic;

namespace CodeDictionary.SyntaxChecking
{
    public static class BslGlobalContext
    {
        private static readonly List<(string Name, string Type, string[] Params)> _procedures = new()
        {
            // Диалоговые
            ("Сообщить", "Процедура", ["Текст"]), ("Message", "Процедура", ["Text"]),
            ("Предупреждение", "Процедура", ["Текст"]), ("Warning", "Процедура", ["Text"]),
            ("Вопрос", "Процедура", ["Текст", "Кнопки"]), ("Question", "Процедура", ["Text", "Buttons"]),
            ("Сигнал", "Процедура", []), ("Beep", "Процедура", []),
            ("ПоказатьЗначение", "Процедура", ["Значение"]), ("DisplayValue", "Процедура", ["Value"]),
            ("ПоказатьЧислоВистока", "Процедура", []), ("ShowNumberLine", "Процедура", []),

            // Транзакции
            ("НачатьТранзакцию", "Процедура", []), ("BeginTransaction", "Процедура", []),
            ("ЗафиксироватьТранзакцию", "Процедура", []), ("CommitTransaction", "Процедура", []),
            ("ОтменитьТранзакцию", "Процедура", []), ("RollbackTransaction", "Процедура", []),
            ("ТранзакцияАктивна", "Функция", []), ("TransactionActive", "Функция", []),

            // Режимы
            ("УстановитьПривилегированныйРежим", "Процедура", ["Установить"]),
            ("SetPrivilegedMode", "Процедура", ["Set"]),
            ("ПривилегированныйРежим", "Функция", []), ("PrivilegedMode", "Функция", []),

            // Условное оформление
            ("УстановитьУсловноеОформление", "Процедура", ["Оформление"]),
            ("SetConditionalDesign", "Процедура", ["Design"]),
            ("УстановитьОформлениеСтрокиТаблицы", "Процедура", ["Оформление"]),
            ("SetRowDesign", "Процедура", ["Design"]),
            ("ОбновитьОтображениеТаблицы", "Процедура", []),
            ("RefreshTableDisplay", "Процедура", []),

            // Редактирование объектов
            ("ЗапретРедактированиеОбъекта", "Процедура", ["Редактирование"]),
            ("SetObjectEditLock", "Процедура", ["Edit"]),
            ("РазрешитьРедактированиеОбъекта", "Процедура", []),
            ("UnlockObjectEdit", "Процедура", []),

            // Служебные
            ("Выполнить", "Процедура", ["Команда"]), ("Execute", "Процедура", ["Command"]),
        };

        private static readonly List<(string Name, string Type, string[] Params)> _functions = new()
        {
            // Файловые операции
            ("КопироватьФайл", "Функция", ["Откуда", "Куда"]), ("CopyFile", "Функция", ["From", "To"]),
            ("ПереместитьФайл", "Функция", ["Откуда", "Куда"]), ("MoveFile", "Функция", ["From", "To"]),
            ("УдалитьФайлы", "Функция", ["Маска"]), ("DeleteFiles", "Функция", ["Mask"]),
            ("СоздатьКаталог", "Функция", ["Каталог"]), ("CreateDirectory", "Функция", ["Directory"]),
            ("КаталогВременныхФайлов", "Функция", []), ("TempFilesDirectory", "Функция", []),
            ("КаталогПрограммы", "Функция", []), ("ProgramDirectory", "Функция", []),
            ("КаталогДокументов", "Функция", []), ("DocumentsDirectory", "Функция", []),
            ("КаталогДанных", "Функция", []), ("DataDirectory", "Функция", []),
            ("КаталогСистемы", "Функция", []), ("SystemDirectory", "Функция", []),
            ("РабочийКаталог", "Функция", []), ("WorkingDirectory", "Функция", []),
            ("ИмяВременногоФайла", "Функция", []), ("TempFileName", "Функция", []),

            // Преобразование
            ("ЗначениеВСтрокуВнутр", "Функция", ["Значение"]),
            ("ValueToStringInternal", "Функция", ["Value"]),
            ("СтрокаВЗначениеВнутр", "Функция", ["Строка"]),
            ("StringToValueInternal", "Функция", ["String"]),
            ("Base64Строка", "Функция", ["Значение"]), ("Base64String", "Функция", ["Value"]),
            ("Base64Значение", "Функция", ["Строка"]), ("Base64Value", "Функция", ["String"]),

            // COM
            ("ПолучитьCOMОбъект", "Функция", ["Имя"]), ("GetCOMObject", "Функция", ["Name"]),
            ("СоздатьCOMОбъект", "Функция", ["Имя"]), ("CreateCOMObject", "Функция", ["Name"]),
            ("COMОбъект", "Функция", ["Имя"]), ("COMObject", "Функция", ["Name"]),

            // HTTP
            ("HTTPСоединение", "Функция", ["Сервер"]), ("HTTPConnection", "Функция", ["Server"]),

            // Проверка
            ("ЗначениеЗаполнено", "Функция", ["Значение"]), ("ValueIsFilled", "Функция", ["Value"]),
            ("ПустаяСтрока", "Функция", ["Строка"]), ("IsBlankString", "Функция", ["String"]),
            ("ТипЗнч", "Функция", ["Значение"]), ("TypeOf", "Функция", ["Value"]),

            // Типы
            ("Структура", "Функция", []), ("Structure", "Функция", []),
            ("Массив", "Функция", []), ("Array", "Функция", []),
            ("СписокЗначений", "Функция", []), ("ValueList", "Функция", []),
            ("ТаблицаЗначений", "Функция", []), ("ValueTable", "Функция", []),
            ("Соответствие", "Функция", []), ("Map", "Функция", []),
            ("ЧтениеТекста", "Функция", ["Путь"]), ("ReadText", "Функция", ["Path"]),
            ("ЗаписьТекста", "Функция", ["Путь"]), ("WriteText", "Функция", ["Path"]),
            ("ЧтениеXML", "Функция", ["Путь"]), ("ReadXML", "Функция", ["Path"]),
            ("ЗаписьXML", "Функция", ["Путь"]), ("WriteXML", "Функция", ["Path"]),
            ("Файл", "Функция", ["Путь"]), ("File", "Функция", ["Path"]),
            ("РегулярноеВыражение", "Функция", ["Выражение"]), ("RegExp", "Функция", ["Pattern"]),

            // Дата
            ("ТекущаяДата", "Функция", []), ("CurrentDate", "Функция", []),
            ("ТекущаяУниверсальнаяДата", "Функция", []), ("CurrentUniversalDate", "Функция", []),
            ("Дата", "Функция", []), ("Date", "Функция", []),

            // Прочее
            ("Формат", "Функция", ["Значение", "Строка"]), ("Format", "Функция", ["Value", "String"]),
            ("НайтиПоНаименованию", "Функция", ["Наименование"]),
            ("FindByName", "Функция", ["Name"]),
        };

        public static IReadOnlyList<(string Name, string Type, string[] Params)> Procedures => _procedures;
        public static IReadOnlyList<(string Name, string Type, string[] Params)> Functions => _functions;

        public static IReadOnlyList<(string Name, string Type, string[] Params)> All
        {
            get
            {
                var all = new List<(string, string, string[])>();
                all.AddRange(_procedures);
                all.AddRange(_functions);
                return all;
            }
        }

        private static readonly HashSet<string> _allNames = new(
            All.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);

        public static bool IsGlobalMethod(string name) => _allNames.Contains(name);
    }
}
