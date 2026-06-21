namespace CodeDictionary.Services;

internal static class SyntaxNames
{
    public const string Dark1C = "\u0422\u0435\u043C\u043D\u0430\u044F 1C";
    public const string Light1C = "\u0421\u0442\u0430\u043D\u0434\u0430\u0440\u0442\u043D\u0430\u044F 1C";
    public const string DarkCSharp = "\u0422\u0435\u043C\u043D\u0430\u044F C#";
    public const string LightCSharp = "\u0421\u0442\u0430\u043D\u0434\u0430\u0440\u0442\u043D\u0430\u044F C#";
    public const string DarkPython = "\u0422\u0435\u043C\u043D\u0430\u044F Python";
    public const string LightPython = "\u0421\u0442\u0430\u043D\u0434\u0430\u0440\u0442\u043D\u0430\u044F Python";
    public const string DarkHTML = "\u0422\u0435\u043C\u043D\u0430\u044F HTML";
    public const string LightHTML = "\u0421\u0442\u0430\u043D\u0434\u0430\u0440\u0442\u043D\u0430\u044F HTML";
    public const string DarkXML = "\u0422\u0435\u043C\u043D\u0430\u044F XML";
    public const string LightXML = "\u0421\u0442\u0430\u043D\u0434\u0430\u0440\u0442\u043D\u0430\u044F XML";
    public const string Markdown = "Markdown";
    public const string Cpp = "C++";
    public const string Sql = "SQL";

    public static string For1C(bool dark) => dark ? Dark1C : Light1C;
    public static string ForCSharp(bool dark) => dark ? DarkCSharp : LightCSharp;
    public static string ForPython(bool dark) => dark ? DarkPython : LightPython;
    public static string ForHTML(bool dark) => dark ? DarkHTML : LightHTML;
    public static string ForXML(bool dark) => dark ? DarkXML : LightXML;
}
