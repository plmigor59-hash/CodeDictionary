namespace CodeDictionary.SyntaxChecking
{
    public static class BslGlobalContext
    {
        private static List<(string Name, string Type, string[] Params)> _procedures = new()
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

        private static List<(string Name, string Type, string[] Params)> _functions = new()
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

        private static HashSet<string> _allNames = new(
            All.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);

        public static bool IsGlobalMethod(string name) => _allNames.Contains(name);

        private static void RebuildNames()
        {
            _allNames = new HashSet<string>(
                All.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
        }

        public static void ImportGlobalMethods(List<JsonGlobalMethod> methods)
        {
            _procedures.Clear();
            _functions.Clear();

            foreach (var method in methods)
            {
                var sig = method.Signature?.FirstOrDefault();
                var isFunction = !string.IsNullOrEmpty(method.Return);
                var target = isFunction ? _functions : _procedures;
                var typeName = isFunction ? "Функция" : "Процедура";

                var ruParams = sig?.Params?.Select(p => p.Name).ToArray() ?? Array.Empty<string>();
                target.Add((method.Name, typeName, ruParams));

                if (!string.IsNullOrEmpty(method.NameEn))
                {
                    var enParams = sig?.Params?.Select(p => p.Name).ToArray() ?? Array.Empty<string>();
                    target.Add((method.NameEn, typeName, enParams));
                }
            }

            RebuildNames();
        }

        public static void ImportGlobalProperties(List<JsonGlobalProperty> properties)
        {
            foreach (var prop in properties)
            {
                var returnType = prop.Type ?? "";
                _functions.Add((prop.Name, "Функция", []));

                if (!string.IsNullOrEmpty(prop.NameEn))
                    _functions.Add((prop.NameEn, "Функция", []));
            }

            RebuildNames();
        }

        public static void ResetToDefaults()
        {
            _procedures.Clear();
            _functions.Clear();

            // Restore hardcoded defaults
            _procedures.AddRange(new[]
            {
                ("Сообщить", "Процедура", new[] { "Текст" }),
                ("Message", "Процедура", new[] { "Text" }),
                ("Предупреждение", "Процедура", new[] { "Текст" }),
                ("Warning", "Процедура", new[] { "Text" }),
                ("Вопрос", "Процедура", new[] { "Текст", "Кнопки" }),
                ("Question", "Процедура", new[] { "Text", "Buttons" }),
                ("Сигнал", "Процедура", Array.Empty<string>()),
                ("Beep", "Процедура", Array.Empty<string>()),
                ("ПоказатьЗначение", "Процедура", new[] { "Значение" }),
                ("DisplayValue", "Процедура", new[] { "Value" }),
                ("ПоказатьЧислоВистока", "Процедура", Array.Empty<string>()),
                ("ShowNumberLine", "Процедура", Array.Empty<string>()),
                ("НачатьТранзакцию", "Процедура", Array.Empty<string>()),
                ("BeginTransaction", "Процедура", Array.Empty<string>()),
                ("ЗафиксироватьТранзакцию", "Процедура", Array.Empty<string>()),
                ("CommitTransaction", "Процедура", Array.Empty<string>()),
                ("ОтменитьТранзакцию", "Процедура", Array.Empty<string>()),
                ("RollbackTransaction", "Процедура", Array.Empty<string>()),
                ("УстановитьПривилегированныйРежим", "Процедура", new[] { "Установить" }),
                ("SetPrivilegedMode", "Процедура", new[] { "Set" }),
                ("Выполнить", "Процедура", new[] { "Команда" }),
                ("Execute", "Процедура", new[] { "Command" }),
                ("УстановитьУсловноеОформление", "Процедура", new[] { "Оформление" }),
                ("SetConditionalDesign", "Процедура", new[] { "Design" }),
                ("УстановитьОформлениеСтрокиТаблицы", "Процедура", new[] { "Оформление" }),
                ("SetRowDesign", "Процедура", new[] { "Design" }),
                ("ОбновитьОтображениеТаблицы", "Процедура", Array.Empty<string>()),
                ("RefreshTableDisplay", "Процедура", Array.Empty<string>()),
                ("ЗапретРедактированиеОбъекта", "Процедура", new[] { "Редактирование" }),
                ("SetObjectEditLock", "Процедура", new[] { "Edit" }),
                ("РазрешитьРедактированиеОбъекта", "Процедура", Array.Empty<string>()),
                ("UnlockObjectEdit", "Процедура", Array.Empty<string>()),
            });

            _functions.AddRange(new[]
            {
                ("ТранзакцияАктивна", "Функция", Array.Empty<string>()),
                ("TransactionActive", "Функция", Array.Empty<string>()),
                ("ПривилегированныйРежим", "Функция", Array.Empty<string>()),
                ("PrivilegedMode", "Функция", Array.Empty<string>()),
                ("КопироватьФайл", "Функция", new[] { "Откуда", "Куда" }),
                ("CopyFile", "Функция", new[] { "From", "To" }),
                ("ПереместитьФайл", "Функция", new[] { "Откуда", "Куда" }),
                ("MoveFile", "Функция", new[] { "From", "To" }),
                ("УдалитьФайлы", "Функция", new[] { "Маска" }),
                ("DeleteFiles", "Функция", new[] { "Mask" }),
                ("СоздатьКаталог", "Функция", new[] { "Каталог" }),
                ("CreateDirectory", "Функция", new[] { "Directory" }),
                ("КаталогВременныхФайлов", "Функция", Array.Empty<string>()),
                ("TempFilesDirectory", "Функция", Array.Empty<string>()),
                ("КаталогПрограммы", "Функция", Array.Empty<string>()),
                ("ProgramDirectory", "Функция", Array.Empty<string>()),
                ("КаталогДокументов", "Функция", Array.Empty<string>()),
                ("DocumentsDirectory", "Функция", Array.Empty<string>()),
                ("КаталогДанных", "Функция", Array.Empty<string>()),
                ("DataDirectory", "Функция", Array.Empty<string>()),
                ("КаталогСистемы", "Функция", Array.Empty<string>()),
                ("SystemDirectory", "Функция", Array.Empty<string>()),
                ("РабочийКаталог", "Функция", Array.Empty<string>()),
                ("WorkingDirectory", "Функция", Array.Empty<string>()),
                ("ИмяВременногоФайла", "Функция", Array.Empty<string>()),
                ("TempFileName", "Функция", Array.Empty<string>()),
                ("ЗначениеВСтрокуВнутр", "Функция", new[] { "Значение" }),
                ("ValueToStringInternal", "Функция", new[] { "Value" }),
                ("СтрокаВЗначениеВнутр", "Функция", new[] { "Строка" }),
                ("StringToValueInternal", "Функция", new[] { "String" }),
                ("Base64Строка", "Функция", new[] { "Значение" }),
                ("Base64String", "Функция", new[] { "Value" }),
                ("Base64Значение", "Функция", new[] { "Строка" }),
                ("Base64Value", "Функция", new[] { "String" }),
                ("ПолучитьCOMОбъект", "Функция", new[] { "Имя" }),
                ("GetCOMObject", "Функция", new[] { "Name" }),
                ("СоздатьCOMОбъект", "Функция", new[] { "Имя" }),
                ("CreateCOMObject", "Функция", new[] { "Name" }),
                ("COMОбъект", "Функция", new[] { "Имя" }),
                ("COMObject", "Функция", new[] { "Name" }),
                ("HTTPСоединение", "Функция", new[] { "Сервер" }),
                ("HTTPConnection", "Функция", new[] { "Server" }),
                ("ЗначениеЗаполнено", "Функция", new[] { "Значение" }),
                ("ValueIsFilled", "Функция", new[] { "Value" }),
                ("ПустаяСтрока", "Функция", new[] { "Строка" }),
                ("IsBlankString", "Функция", new[] { "String" }),
                ("ТипЗнч", "Функция", new[] { "Значение" }),
                ("TypeOf", "Функция", new[] { "Value" }),
                ("Структура", "Функция", Array.Empty<string>()),
                ("Structure", "Функция", Array.Empty<string>()),
                ("Массив", "Функция", Array.Empty<string>()),
                ("Array", "Функция", Array.Empty<string>()),
                ("СписокЗначений", "Функция", Array.Empty<string>()),
                ("ValueList", "Функция", Array.Empty<string>()),
                ("ТаблицаЗначений", "Функция", Array.Empty<string>()),
                ("ValueTable", "Функция", Array.Empty<string>()),
                ("Соответствие", "Функция", Array.Empty<string>()),
                ("Map", "Функция", Array.Empty<string>()),
                ("ЧтениеТекста", "Функция", new[] { "Путь" }),
                ("ReadText", "Функция", new[] { "Path" }),
                ("ЗаписьТекста", "Функция", new[] { "Путь" }),
                ("WriteText", "Функция", new[] { "Path" }),
                ("ЧтениеXML", "Функция", new[] { "Путь" }),
                ("ReadXML", "Функция", new[] { "Path" }),
                ("ЗаписьXML", "Функция", new[] { "Путь" }),
                ("WriteXML", "Функция", new[] { "Path" }),
                ("Файл", "Функция", new[] { "Путь" }),
                ("File", "Функция", new[] { "Path" }),
                ("РегулярноеВыражение", "Функция", new[] { "Выражение" }),
                ("RegExp", "Функция", new[] { "Pattern" }),
                ("ТекущаяДата", "Функция", Array.Empty<string>()),
                ("CurrentDate", "Функция", Array.Empty<string>()),
                ("ТекущаяУниверсальнаяДата", "Функция", Array.Empty<string>()),
                ("CurrentUniversalDate", "Функция", Array.Empty<string>()),
                ("Дата", "Функция", Array.Empty<string>()),
                ("Date", "Функция", Array.Empty<string>()),
                ("Формат", "Функция", new[] { "Значение", "Строка" }),
                ("Format", "Функция", new[] { "Value", "String" }),
                ("НайтиПоНаименованию", "Функция", new[] { "Наименование" }),
                ("FindByName", "Функция", new[] { "Name" }),
            });

            RebuildNames();
        }
    }
}
