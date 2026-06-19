using System.Text;
using OneScript.Language.Sources;
using OneScript.Sources;
using OneScript.StandardLibrary;
using ScriptEngine.HostedScript;
using ScriptEngine.HostedScript.Extensions;
using ScriptEngine.Hosting;
using ScriptEngine.Machine;

namespace CodeDictionary.SyntaxChecking
{
    public class BslExecutionService
    {
        public async Task<BslExecutionResult> ExecuteAsync(string code, CancellationToken cancellationToken = default)
        {
            var result = new BslExecutionResult();

            if (string.IsNullOrWhiteSpace(code))
            {
                result.Error = "Код не указан";
                return result;
            }

            try
            {
                var host = new BslHostApplication();
                var source = new StringCodeSource(code);
                var sourceCode = SourceCodeBuilder.Create()
                    .FromSource(source)
                    .Build();

                var engine = CreateEngine();
                var process = engine.CreateProcess(host, sourceCode);

                var exitCode = await Task.Run(() => process.Start(), cancellationToken);

                if (exitCode == 0)
                {
                    result.Output = host.Output.TrimEnd();
                    result.Success = true;
                }
                else
                {
                    result.Error = host.ErrorOutput.TrimEnd();
                    if (string.IsNullOrEmpty(result.Error))
                        result.Error = host.Output.TrimEnd();
                }
            }
            catch (OperationCanceledException)
            {
                result.Error = "Выполнение отменено";
            }
            catch (Exception ex)
            {
                result.Error = $"Ошибка выполнения: {ex.Message}";
            }

            return result;
        }

        private static HostedScriptEngine CreateEngine()
        {
            var builder = DefaultEngineBuilder.Create()
                .SetDefaultOptions()
                .UseImports()
                .UseFileSystemLibraries()
                .UseNativeRuntime()
                .UseEventHandlers()
                .SetupEnvironment(env => env.AddStandardLibrary());

            var scriptingEngine = builder.Build();
            return new HostedScriptEngine(scriptingEngine);
        }

        private class BslHostApplication : IHostApplication
        {
            public StringBuilder OutputBuilder { get; } = new();
            public StringBuilder ErrorBuilder { get; } = new();

            public string Output => OutputBuilder.ToString();
            public string ErrorOutput => ErrorBuilder.ToString();

            public void Echo(string str, MessageStatusEnum status = MessageStatusEnum.Ordinary)
            {
                if (status == MessageStatusEnum.Important)
                    ErrorBuilder.AppendLine(str);
                else
                    OutputBuilder.AppendLine(str);
            }

            public void ShowExceptionInfo(Exception exc)
            {
                ErrorBuilder.AppendLine(exc.ToString());
            }

            public bool InputString(out string result, string prompt, int maxLen, bool multiline)
            {
                result = string.Empty;
                return false;
            }

            public string[] GetCommandLineArguments()
            {
                return [];
            }
        }

        private class StringCodeSource : ICodeSource
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

    public class BslExecutionResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public bool HasError => !string.IsNullOrEmpty(Error);
    }
}
