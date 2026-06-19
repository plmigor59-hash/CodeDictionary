using System.Diagnostics;
using System.Text;

namespace CodeDictionary.SyntaxChecking
{
    public class PythonExecutionService
    {
        public async Task<PythonExecutionResult> ExecuteAsync(string code, CancellationToken cancellationToken = default)
        {
            var result = new PythonExecutionResult();

            if (string.IsNullOrWhiteSpace(code))
            {
                result.Error = "Код не указан";
                return result;
            }

            try
            {
                var psi = new ProcessStartInfo("python")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    result.Error = "Не удалось запустить python.exe. Убедитесь, что Python установлен и доступен в PATH.";
                    return result;
                }

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                using (var outputWaitHandle = new ManualResetEvent(false))
                using (var errorWaitHandle = new ManualResetEvent(false))
                {
                    process.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data == null)
                            outputWaitHandle.Set();
                        else
                            outputBuilder.AppendLine(e.Data);
                    };
                    process.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data == null)
                            errorWaitHandle.Set();
                        else
                            errorBuilder.AppendLine(e.Data);
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await process.StandardInput.WriteAsync(code);
                    await process.StandardInput.FlushAsync();
                    process.StandardInput.Close();

                    await process.WaitForExitAsync(cancellationToken);

                    outputWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                    errorWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                }

                var output = outputBuilder.ToString().TrimEnd();
                var error = errorBuilder.ToString().TrimEnd();

                if (process.ExitCode == 0 && string.IsNullOrEmpty(error))
                {
                    result.Output = output;
                    result.Success = true;
                }
                else
                {
                    result.Error = string.IsNullOrEmpty(error) ? output : error;
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
    }

    public class PythonExecutionResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public bool HasError => !string.IsNullOrEmpty(Error);
    }
}
