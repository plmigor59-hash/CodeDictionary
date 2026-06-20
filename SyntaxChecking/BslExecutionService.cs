using OneScript.Language.Sources;
using OneScript.Sources;
using OneScript.StandardLibrary;
using ScriptEngine;
using ScriptEngine.HostedScript;
using ScriptEngine.HostedScript.Extensions;
using ScriptEngine.Hosting;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CodeDictionary.SyntaxChecking
{
    public class BslExecutionService
    {
        private BslDebugger? _currentDebugger;

        public BslDebugger? GetCurrentDebugger() => _currentDebugger;

        public async Task<BslExecutionResult> ExecuteAsync(string code, string[]? args = null, HashSet<int>? breakpoints = null, Action<BslDebugger>? onDebuggerReady = null, CancellationToken cancellationToken = default)
        {
            var result = new BslExecutionResult();

            if (string.IsNullOrWhiteSpace(code))
            {
                result.Error = "Код не указан";
                return result;
            }

            try
            {
                var host = new BslHostApplication(args ?? []);
                var source = new StringCodeSource(code);
                var sourceCode = SourceCodeBuilder.Create()
                    .FromSource(source)
                    .Build();

                var (engine, debugger) = CreateEngine(breakpoints);
                _currentDebugger = debugger;
                var process = engine.CreateProcess(host, sourceCode);

                if (debugger != null && breakpoints != null && breakpoints.Count > 0)
                {
                    debugger.SetBreakpoints("memory", breakpoints.Select(l => (l, (string)null!)).ToArray());
                    onDebuggerReady?.Invoke(debugger);
                }

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

        private static (HostedScriptEngine engine, BslDebugger? debugger) CreateEngine(HashSet<int>? breakpoints)
        {
            var builder = DefaultEngineBuilder.Create()
                .SetDefaultOptions()
                .UseImports()
                .UseFileSystemLibraries()
                .UseNativeRuntime()
                .UseEventHandlers()
                .SetupEnvironment(env => env.AddStandardLibrary());

            BslDebugger? debugger = null;
            if (breakpoints != null && breakpoints.Count > 0)
            {
                debugger = new BslDebugger();
                builder.WithDebugger(debugger);
            }

            var scriptingEngine = builder.Build();
            var engine = new HostedScriptEngine(scriptingEngine);

            var libPath = FindOneScriptLibPath();
            if (libPath != null)
            {
                var resolver = engine.Services.Resolve<IDependencyResolver>() as FileSystemDependencyResolver;
                if (resolver != null)
                {
                    var path = Path.GetFullPath(libPath);
                    resolver.LibraryRoot = path;
                    if (!resolver.SearchDirectories.Contains(path, StringComparer.OrdinalIgnoreCase))
                        resolver.SearchDirectories.Add(path);
                }
            }

            return (engine, debugger);
        }

        private static string? FindOneScriptLibPath()
        {
            var localLib = Path.Combine(AppContext.BaseDirectory, "lib");
            if (Directory.Exists(localLib))
                return localLib;

            var envScript = Environment.GetEnvironmentVariable("OSCRIPT");
            if (!string.IsNullOrEmpty(envScript))
            {
                var envLib = Path.Combine(envScript, "lib");
                if (Directory.Exists(envLib))
                    return envLib;
            }

            return null;
        }

        private class BslHostApplication(string[] args) : IHostApplication
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

            public string[] GetCommandLineArguments() => args;
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
