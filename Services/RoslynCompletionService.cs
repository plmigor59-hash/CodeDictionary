using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CodeDictionary.Services
{
    public class RoslynCompletionService
    {
        private readonly AdhocWorkspace _workspace;
        private readonly Project _project;
        private Document? _currentDocument;
        private string? _lastCode;

        public RoslynCompletionService()
        {
            _workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();

            // Добавляем ссылки на основные сборки для более точного анализа
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
        }

        private Document GetOrCreateDocument(string code)
        {
            if (_currentDocument != null && _lastCode == code)
            {
                return _currentDocument;
            }

            _lastCode = code;
            _currentDocument = _project.AddDocument("Temporary.cs", SourceText.From(code));
            return _currentDocument;
        }

        public async Task<IEnumerable<CompletionItem>> GetCompletionItemsAsync(string code, int position)
        {
            var document = GetOrCreateDocument(code);
            var completionService = CompletionService.GetService(document);

            if (completionService == null) return Enumerable.Empty<CompletionItem>();

            int start = position;
            while (start > 0 && char.IsLetterOrDigit(code[start - 1]))
            {
                start--;
            }
            string prefix = code.Substring(start, position - start);

            var completionList = await completionService.GetCompletionsAsync(document, position);

            if (completionList == null) return Enumerable.Empty<CompletionItem>();

            if (!string.IsNullOrEmpty(prefix))
            {
                return completionList.Items.Where(i => i.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            }

            return completionList.Items;
        }

        public async Task<string> GetDescriptionAsync(CompletionItem item, string code)
        {
            var document = GetOrCreateDocument(code);
            var completionService = CompletionService.GetService(document);
            if (completionService == null) return string.Empty;

            var description = await completionService.GetDescriptionAsync(document, item);
            return description?.Text ?? string.Empty;
        }
    }

}
