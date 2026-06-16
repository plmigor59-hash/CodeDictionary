using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeDictionary.Services
{
    public class SignatureInfo
    {
        public string MethodName { get; set; } = string.Empty;
        public List<string> Parameters { get; set; } = new List<string>();
        public string FullSignature => $"{MethodName}({string.Join(", ", Parameters)})";
    }

    public class RoslynCompletionService
    {
        private readonly AdhocWorkspace _workspace;
        private readonly Project _project;
        private Document? _currentDocument;

        public RoslynCompletionService()
        {
            _workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();

            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute).Assembly.Location)
            };

            var solution = _workspace.CurrentSolution
                .AddProject(projectId, "CompletionProject", "CompletionProject.dll", LanguageNames.CSharp)
                .AddMetadataReferences(projectId, references);

            _project = solution.GetProject(projectId)!;
            _currentDocument = _project.AddDocument("Temporary.cs", SourceText.From(""));
        }

        public void UpdateCode(string code)
        {
            if (_currentDocument == null) return;
            _currentDocument = _currentDocument.WithText(SourceText.From(code));
        }

        public async Task<IEnumerable<CompletionItem>> GetCompletionItemsAsync(int position)
        {
            if (_currentDocument == null) return Enumerable.Empty<CompletionItem>();
            
            var completionService = CompletionService.GetService(_currentDocument);
            if (completionService == null) return Enumerable.Empty<CompletionItem>();

            var completionList = await completionService.GetCompletionsAsync(_currentDocument, position);
            return completionList?.Items ?? Enumerable.Empty<CompletionItem>();
        }

        public async Task<string> GetDescriptionAsync(CompletionItem item)
        {
            if (_currentDocument == null) return string.Empty;
            
            var completionService = CompletionService.GetService(_currentDocument);
            if (completionService == null) return string.Empty;

            var description = await completionService.GetDescriptionAsync(_currentDocument, item);
            return description?.Text ?? string.Empty;
        }

        public async Task<SignatureInfo?> GetSignatureInfoAsync(int position)
        {
            if (_currentDocument == null) return null;

            var semanticModel = await _currentDocument.GetSemanticModelAsync();
            var root = await _currentDocument.GetSyntaxRootAsync();
            if (semanticModel == null || root == null) return null;

            var token = root.FindToken(position);
            var invocation = token.Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
            
            if (invocation == null) return null;

            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

            if (methodSymbol == null) return null;

            return new SignatureInfo
            {
                MethodName = methodSymbol.Name,
                Parameters = methodSymbol.Parameters.Select(p => $"{p.Type.Name} {p.Name}").ToList()
            };
        }

        public async Task<string> FormatCodeAsync()
        {
            if (_currentDocument == null) return string.Empty;
            
            var root = await _currentDocument.GetSyntaxRootAsync();
            if (root == null) return string.Empty;

            var formattedNode = Formatter.Format(root, _workspace);
            return formattedNode.ToFullString();
        }
    }
}
