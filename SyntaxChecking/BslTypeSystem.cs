using System.Collections.Generic;

namespace CodeDictionary.SyntaxChecking
{
    public class BslMemberInfo
    {
        public string Name { get; init; } = "";
        public string ReturnType { get; init; } = "";
        public string[] ParameterNames { get; init; } = [];
        public string[] ParameterTypes { get; init; } = [];
        public bool IsFunction { get; init; }
    }

    public class BslTypeInfo
    {
        public string Name { get; init; } = "";
        public Dictionary<string, BslMemberInfo> Members { get; init; } = new();
        public string BaseTypeName { get; init; } = "";
    }

    public static class BslTypeSystem
    {
        public static readonly Dictionary<string, BslTypeInfo> KnownTypes = new()
        {
            ["Структура"] = new BslTypeInfo
            {
                Name = "Структура",
                Members = new()
                {
                    ["Вставить"] = new() { Name = "Вставить", ParameterNames = ["Ключ", "Значение"] },
                    ["Свойство"] = new() { Name = "Свойство", ParameterNames = ["Ключ"], ReturnType = "Булево", IsFunction = true },
                    ["Удалить"] = new() { Name = "Удалить", ParameterNames = ["Ключ"] },
                    ["Количество"] = new() { Name = "Количество", ReturnType = "Число", IsFunction = true },
                    ["Очистить"] = new() { Name = "Очистить" },
                }
            },
            ["Массив"] = new BslTypeInfo
            {
                Name = "Массив",
                Members = new()
                {
                    ["Вставить"] = new() { Name = "Вставить", ParameterNames = ["Индекс", "Значение"] },
                    ["Добавить"] = new() { Name = "Добавить", ParameterNames = ["Значение"] },
                    ["Удалить"] = new() { Name = "Удалить", ParameterNames = ["Индекс"] },
                    ["Количество"] = new() { Name = "Количество", ReturnType = "Число", IsFunction = true },
                    ["Очистить"] = new() { Name = "Очистить" },
                    ["Найти"] = new() { Name = "Найти", ParameterNames = ["Значение"], ReturnType = "Число", IsFunction = true },
                }
            },
            ["СписокЗначений"] = new BslTypeInfo
            {
                Name = "СписокЗначений",
                Members = new()
                {
                    ["Добавить"] = new() { Name = "Добавить", ParameterNames = ["Значение", "Представление"] },
                    ["Вставить"] = new() { Name = "Вставить", ParameterNames = ["Индекс", "Значение", "Представление"] },
                    ["Удалить"] = new() { Name = "Удалить", ParameterNames = ["Индекс"] },
                    ["Количество"] = new() { Name = "Количество", ReturnType = "Число", IsFunction = true },
                    ["Очистить"] = new() { Name = "Очистить" },
                    ["Получить"] = new() { Name = "Получить", ParameterNames = ["Индекс"], ReturnType = "Значение", IsFunction = true },
                    ["НайтиПоЗначению"] = new() { Name = "НайтиПоЗначению", ParameterNames = ["Значение"], ReturnType = "Число", IsFunction = true },
                    ["НайтиПоИдентификатору"] = new() { Name = "НайтиПоИдентификатору", ParameterNames = ["Идентификатор"], ReturnType = "Число", IsFunction = true },
                    ["Сдвинуть"] = new() { Name = "Сдвинуть", ParameterNames = ["Индекс", "Сдвиг"] },
                    ["ЗаполнитьЗначения"] = new() { Name = "ЗаполнитьЗначения", ParameterNames = ["Значение"] },
                }
            },
            ["ТаблицаЗначений"] = new BslTypeInfo
            {
                Name = "ТаблицаЗначений",
                Members = new()
                {
                    ["Колонки"] = new() { Name = "Колонки", IsFunction = true, ReturnType = "КолонкиТаблицыЗначений" },
                    ["ДобавитьКолонку"] = new() { Name = "ДобавитьКолонку", ParameterNames = ["Имя", "Тип"] },
                    ["УдалитьКолонку"] = new() { Name = "УдалитьКолонку", ParameterNames = ["Имя"] },
                    ["Добавить"] = new() { Name = "Добавить" },
                    ["Вставить"] = new() { Name = "Вставить", ParameterNames = ["Индекс"] },
                    ["Удалить"] = new() { Name = "Удалить", ParameterNames = ["Индекс"] },
                    ["Количество"] = new() { Name = "Количество", ReturnType = "Число", IsFunction = true },
                    ["Очистить"] = new() { Name = "Очистить" },
                    ["Найти"] = new() { Name = "Найти", ParameterNames = ["Значение"], ReturnType = "СтрокаТаблицыЗначений", IsFunction = true },
                    ["НайтиСтроки"] = new() { Name = "НайтиСтроки", ParameterNames = ["Отбор"], ReturnType = "Массив", IsFunction = true },
                    ["Итог"] = new() { Name = "Итог", ParameterNames = ["Колонка"], ReturnType = "Число", IsFunction = true },
                    ["Свернуть"] = new() { Name = "Свернуть", ParameterNames = ["КолонкиГруппировки", "КолонкиСвертки"] },
                    ["Сортировать"] = new() { Name = "Сортировать", ParameterNames = ["Колонка"] },
                    ["Выгрузить"] = new() { Name = "Выгрузить", ReturnType = "ТаблицаЗначений", IsFunction = true },
                    ["Загрузить"] = new() { Name = "Загрузить", ParameterNames = ["Таблица"] },
                    ["ИтогПоКолонке"] = new() { Name = "ИтогПоКолонке", ParameterNames = ["Колонка"], ReturnType = "Число", IsFunction = true },
                }
            },
            ["Соответствие"] = new BslTypeInfo
            {
                Name = "Соответствие",
                Members = new()
                {
                    ["Вставить"] = new() { Name = "Вставить", ParameterNames = ["Ключ", "Значение"] },
                    ["Получить"] = new() { Name = "Получить", ParameterNames = ["Ключ"], ReturnType = "Значение", IsFunction = true },
                    ["Удалить"] = new() { Name = "Удалить", ParameterNames = ["Ключ"] },
                    ["Количество"] = new() { Name = "Количество", ReturnType = "Число", IsFunction = true },
                    ["Очистить"] = new() { Name = "Очистить" },
                }
            },
            ["ЧтениеТекста"] = new BslTypeInfo
            {
                Name = "ЧтениеТекста",
                Members = new()
                {
                    ["Прочитать"] = new() { Name = "Прочитать", ReturnType = "Строка", IsFunction = true },
                    ["ПрочитатьСтроку"] = new() { Name = "ПрочитатьСтроку", ReturnType = "Строка", IsFunction = true },
                    ["Закрыть"] = new() { Name = "Закрыть" },
                }
            },
            ["ЗаписьТекста"] = new BslTypeInfo
            {
                Name = "ЗаписьТекста",
                Members = new()
                {
                    ["Записать"] = new() { Name = "Записать", ParameterNames = ["Текст"] },
                    ["ЗаписатьСтроку"] = new() { Name = "ЗаписатьСтроку", ParameterNames = ["Строка"] },
                    ["Закрыть"] = new() { Name = "Закрыть" },
                }
            },
            ["ЧтениеXML"] = new BslTypeInfo
            {
                Name = "ЧтениеXML",
                Members = new()
                {
                    ["Прочитать"] = new() { Name = "Прочитать", ReturnType = "Строка", IsFunction = true },
                    ["Закрыть"] = new() { Name = "Закрыть" },
                }
            },
            ["ЗаписьXML"] = new BslTypeInfo
            {
                Name = "ЗаписьXML",
                Members = new()
                {
                    ["Записать"] = new() { Name = "Записать", ParameterNames = ["Текст"] },
                    ["Закрыть"] = new() { Name = "Закрыть" },
                }
            },
            ["Файл"] = new BslTypeInfo
            {
                Name = "Файл",
                Members = new()
                {
                    ["Существует"] = new() { Name = "Существует", ReturnType = "Булево", IsFunction = true },
                    ["ПолучитьИмя"] = new() { Name = "ПолучитьИмя", ReturnType = "Строка", IsFunction = true },
                    ["ПолучитьРасширение"] = new() { Name = "ПолучитьРасширение", ReturnType = "Строка", IsFunction = true },
                    ["ПолучитьПуть"] = new() { Name = "ПолучитьПуть", ReturnType = "Строка", IsFunction = true },
                }
            },
            ["РегулярноеВыражение"] = new BslTypeInfo
            {
                Name = "РегулярноеВыражение",
                Members = new()
                {
                    ["Совпадает"] = new() { Name = "Совпадает", ParameterNames = ["Строка"], ReturnType = "Булево", IsFunction = true },
                    ["НайтиСовпадения"] = new() { Name = "НайтиСовпадения", ParameterNames = ["Строка"], ReturnType = "Массив", IsFunction = true },
                    ["Заменить"] = new() { Name = "Заменить", ParameterNames = ["Строка", "Замена"], ReturnType = "Строка", IsFunction = true },
                    ["Разделить"] = new() { Name = "Разделить", ParameterNames = ["Строка"], ReturnType = "Массив", IsFunction = true },
                }
            },
            ["XDTOСхема"] = new BslTypeInfo
            {
                Name = "XDTOСхема",
                Members = new()
                {
                    ["Пакет"] = new() { Name = "Пакет", ReturnType = "XDTOПакет", IsFunction = true },
                    ["Типы"] = new() { Name = "Типы", ReturnType = "Массив", IsFunction = true },
                }
            },
            ["XDTOПакет"] = new BslTypeInfo
            {
                Name = "XDTOПакет",
                Members = new()
                {
                    ["ПолучитьТип"] = new() { Name = "ПолучитьТип", ParameterNames = ["Имя"], ReturnType = "XDTOТип", IsFunction = true },
                }
            },
            ["HTTPСоединение"] = new BslTypeInfo
            {
                Name = "HTTPСоединение",
                Members = new()
                {
                    ["Получить"] = new() { Name = "Получить", ParameterNames = ["Ресурс"], ReturnType = "HTTPОтвет", IsFunction = true },
                    ["Отправить"] = new() { Name = "Отправить", ParameterNames = ["Ресурс", "Данные"], ReturnType = "HTTPОтвет", IsFunction = true },
                    ["Разместить"] = new() { Name = "Разместить", ParameterNames = ["Ресурс", "Данные"], ReturnType = "HTTPОтвет", IsFunction = true },
                    ["Удалить"] = new() { Name = "Удалить", ParameterNames = ["Ресурс"], ReturnType = "HTTPОтвет", IsFunction = true },
                }
            },
        };

        public static BslTypeInfo? Resolve(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            if (KnownTypes.TryGetValue(typeName, out var typeInfo))
                return typeInfo;

            foreach (var kv in KnownTypes)
            {
                if (string.Equals(kv.Key, typeName, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }

            return null;
        }
    }
}
