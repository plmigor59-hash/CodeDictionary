using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System.IO;

namespace CodeDictionary.Services
{
    public class SignatureInfo
    {
        public string MethodName { get; set; } = string.Empty;
        public List<string> Parameters { get; set; } = new List<string>();
        public string FullSignature => $"{MethodName}({string.Join(", ", Parameters)})";
    }

    public class RoslynCompletionService : IDisposable
    {
        private readonly AdhocWorkspace _workspace;
        private readonly Project _project;
        private Document? _currentDocument;
        public string HiddenUsings { get; } = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Text;\nusing System.Threading.Tasks;\n";

        public int OffsetShift { get; private set; } = 0;

        public RoslynCompletionService()
        {
            _workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();

            var references = new List<MetadataReference>();

            // В .NET Core / .NET 5+ при публикации в один файл Assembly.Location возвращает пустую строку.
            // Использование TRUSTED_PLATFORM_ASSEMBLIES гарантирует получение путей ко всем системным сборкам.
            if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedAssembliesPaths)
            {
                var paths = trustedAssembliesPaths.Split(Path.PathSeparator);
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        references.Add(MetadataReference.CreateFromFile(path));
                    }
                }
            }
            else
            {
                var assemblies = new[]
                {
                    typeof(object).Assembly,
                    typeof(System.Console).Assembly,
                    typeof(System.Linq.Enumerable).Assembly,
                    typeof(System.Collections.Generic.List<>).Assembly,
                    typeof(System.Text.StringBuilder).Assembly,
                    typeof(System.Threading.Tasks.Task).Assembly,
                    typeof(System.Net.Http.HttpClient).Assembly,
                    typeof(System.IO.File).Assembly,
                    typeof(System.Text.RegularExpressions.Regex).Assembly,
                    typeof(System.Linq.Expressions.Expression).Assembly,
                    typeof(System.Collections.Concurrent.ConcurrentDictionary<,>).Assembly,
                    typeof(System.ComponentModel.Component).Assembly,
                    typeof(System.IO.Compression.ZipArchive).Assembly,
                    typeof(System.Net.WebClient).Assembly,
                    typeof(System.Xml.XmlDocument).Assembly,
                };

                foreach (var assembly in assemblies)
                {
                    if (!string.IsNullOrEmpty(assembly.Location))
                        references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }

                var coreAssemblies = new[] { "System.Runtime", "System.Runtime.Extensions", "mscorlib",
                    "System.Collections", "System.Console", "System.Linq", "System.Text.RegularExpressions",
                    "System.Threading.Tasks", "System.Net.Http", "System.IO", "System.Xml" };
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var name in coreAssemblies)
                {
                    var assembly = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == name);
                    if (assembly != null && !string.IsNullOrEmpty(assembly.Location))
                    {
                        if (!references.Any(r => r.Display != null && r.Display.Contains(name)))
                            references.Add(MetadataReference.CreateFromFile(assembly.Location));
                    }
                }
            }

            var solution = _workspace.CurrentSolution
                .AddProject(projectId, "CompletionProject", "CompletionProject.dll", LanguageNames.CSharp)
                .AddMetadataReferences(projectId, references);

            _project = solution.GetProject(projectId)!;
            _currentDocument = _project.AddDocument("Temporary.cs", SourceText.From(""));
        }

        public Document? CurrentDocument => _currentDocument;

        public void UpdateCode(string code)
        {
            if (_currentDocument == null) return;

            OffsetShift = 0;
            if (!code.TrimStart().StartsWith("using "))
            {
                OffsetShift = HiddenUsings.Length;
                code = HiddenUsings + code;
            }

            _currentDocument = _currentDocument.WithText(SourceText.From(code));
        }

        public async Task<IEnumerable<CompletionItem>> GetCompletionItemsAsync(int position, CancellationToken cancellationToken = default)
        {
            if (_currentDocument == null) return Enumerable.Empty<CompletionItem>();

            var completionService = CompletionService.GetService(_currentDocument);
            if (completionService == null) return Enumerable.Empty<CompletionItem>();

            var completionList = await completionService.GetCompletionsAsync(_currentDocument, position + OffsetShift, cancellationToken: cancellationToken);
            return completionList?.ItemsList ?? Enumerable.Empty<CompletionItem>();
        }

        public async Task<string> GetDescriptionAsync(CompletionItem item, CancellationToken cancellationToken = default)
        {
            if (_currentDocument == null) return string.Empty;

            var completionService = CompletionService.GetService(_currentDocument);
            if (completionService == null) return string.Empty;

            var description = await completionService.GetDescriptionAsync(_currentDocument, item, cancellationToken: cancellationToken);
            return description?.Text ?? string.Empty;
        }

        public async Task<string> GetMethodSignatureAsync(CompletionItem item, int position, CancellationToken cancellationToken = default)
        {
            if (_currentDocument == null || !item.Tags.Contains("Method")) return string.Empty;

            var semanticModel = await _currentDocument.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null) return string.Empty;

            var symbols = semanticModel.LookupSymbols(position + OffsetShift, name: item.DisplayText);
            var methods = symbols.OfType<IMethodSymbol>().ToList();

            if (methods.Any())
            {
                // Для списка выбора берем сигнатуру с наибольшим количеством параметров или первую
                var methodSymbol = methods.OrderByDescending(m => m.Parameters.Length).First();

                var parameters = methodSymbol.Parameters.Select(p =>
                {
                    string typeName = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    return $"{typeName} {p.Name}";
                });

                string signature = $"({string.Join(", ", parameters)})";

                // Если есть другие перегрузки, добавляем "+"
                if (methods.Count > 1) signature += $" (+{methods.Count - 1} перегрузок)";

                return signature;
            }

            return "()";
        }

        public async Task<SignatureInfo?> GetSignatureInfoAsync(int position, CancellationToken cancellationToken = default)
        {
            if (_currentDocument == null) return null;

            var semanticModel = await _currentDocument.GetSemanticModelAsync(cancellationToken);
            var root = await _currentDocument.GetSyntaxRootAsync(cancellationToken);
            if (semanticModel == null || root == null) return null;

            // Смещаемся на 1 назад, чтобы попасть в контекст вызова (перед открывающей скобкой или запятой)
            int adjustedPosition = Math.Max(0, position + OffsetShift - 1);
            var token = root.FindToken(adjustedPosition);

            // Ищем узел вызова метода среди предков
            var invocation = token.Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();

            if (invocation == null) return null;

            // Пытаемся получить символ метода
            var symbolInfo = semanticModel.GetSymbolInfo(invocation.Expression);
            var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

            if (methodSymbol == null)
            {
                // Если не нашли через Expression, пробуем через сам узел вызова
                symbolInfo = semanticModel.GetSymbolInfo(invocation);
                methodSymbol = symbolInfo.Symbol as IMethodSymbol;
            }

            if (methodSymbol == null) return null;

            return new SignatureInfo
            {
                MethodName = methodSymbol.Name,
                Parameters = methodSymbol.Parameters.Select(p =>
                    $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}").ToList()
            };
        }

        public async Task<string> FormatCodeAsync(CancellationToken cancellationToken = default)
        {
            if (_currentDocument == null) return string.Empty;

            var root = await _currentDocument.GetSyntaxRootAsync(cancellationToken);
            if (root == null) return string.Empty;

            var formattedNode = Formatter.Format(root, _workspace);

            string result = formattedNode.ToFullString();
            // Убираем скрытые usings если они были добавлены
            if (OffsetShift > 0 && result.StartsWith(HiddenUsings))
            {
                result = result.Substring(HiddenUsings.Length);
            }

            return result;
        }

        public async Task<IEnumerable<Diagnostic>> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
        {
            if (_currentDocument == null) return Enumerable.Empty<Diagnostic>();

            var semanticModel = await _currentDocument.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null) return Enumerable.Empty<Diagnostic>();

            return semanticModel.GetDiagnostics(cancellationToken: cancellationToken);
        }

        public void Dispose()
        {
            _workspace?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
