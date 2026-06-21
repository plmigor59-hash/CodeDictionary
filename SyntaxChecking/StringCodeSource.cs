using OneScript.Language.Sources;
using OneScript.Sources;
using ScriptEngine.Machine;

namespace CodeDictionary.SyntaxChecking
{
    internal class StringCodeSource : ICodeSource
    {
        public string Location => "memory";
        private readonly string _code;

        public StringCodeSource(string code)
        {
            _code = code;
        }

        public string GetSourceCode() => _code;
    }
}
