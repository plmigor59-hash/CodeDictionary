using CodeDictionary.Models;
using System.IO;

namespace CodeDictionary.Services;

public class EntryService
{
    private readonly CategoryService _categoryService;
    private readonly FormattingService _formattingService;

    public EntryService(CategoryService categoryService, FormattingService formattingService)
    {
        _categoryService = categoryService;
        _formattingService = formattingService;
    }

    public void SyncEntryFromForm(CodeEntry entry, string title, string code, string category, string tagsText, string syntax, EditorTabModel activeTab)
    {
        entry.Title = title;
        entry.Code = code;
        entry.Category = _categoryService.NormalizeCategoryPath(category);
        entry.Tags = tagsText
            .Split(',')
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();
        entry.Syntax = syntax;

        entry.Bookmarks = activeTab.Bookmarks.ToList();

        activeTab.Title = entry.Title;
        activeTab.SyntaxName = entry.Syntax;
        activeTab.IsDirty = true;
    }

    public CodeEntry CreateNewEntry(string category)
    {
        return new CodeEntry
        {
            Category = category
        };
    }

    public void UpdateEntryFromForm(CodeEntry entry, string title, string code, string category, string tags, string syntax, EditorTabModel activeTab, List<TextSegmentStyle> segments)
    {
        entry.Title = title;
        entry.Code = code;
        entry.Category = _categoryService.NormalizeCategoryPath(category);
        entry.Tags = tags
            .Split(',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        entry.Syntax = syntax;
        entry.ModifiedAt = DateTime.Now;

        if (activeTab != null)
        {
            activeTab.Entry = entry;
            activeTab.Title = entry.Title;
            activeTab.SyntaxName = entry.Syntax;
            activeTab.IsDirty = false;
        }

        _formattingService.SaveSegmentsToEntry(entry, segments);
    }

    public async Task ImportFromFolderAsync(string rootPath, string baseCategory, CodeDictionaryData data, DataService dataService, bool isDarkTheme)
    {
        var foldersToIgnore = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ext", "Forms", "Help" };

        var allFiles = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => !Path.GetDirectoryName(f)!.Split(Path.DirectorySeparatorChar).Any(p => p.StartsWith("_")))
            .Where(f => !Path.GetFileName(f).StartsWith("_"))
            .ToList();

        var bslFiles = allFiles.Where(f => f.EndsWith(".bsl", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var bslFile in bslFiles)
        {
            string relativePath = Path.GetDirectoryName(Path.GetRelativePath(rootPath, bslFile)) ?? "";

            var pathParts = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(p => !foldersToIgnore.Contains(p));

            string folderCategory = string.Join("/", pathParts);

            string category = string.IsNullOrWhiteSpace(baseCategory)
                ? folderCategory
                : string.IsNullOrWhiteSpace(folderCategory)
                    ? baseCategory
                    : $"{baseCategory}/{folderCategory}";

            category = _categoryService.NormalizeCategoryPath(category);
            if (string.IsNullOrEmpty(category)) category = CategoryService.UncategorizedCategoryName;

            string title = Path.GetFileNameWithoutExtension(bslFile);
            string code = await File.ReadAllTextAsync(bslFile);

            string htmlFile = Path.ChangeExtension(bslFile, ".html");
            string description = "";
            if (File.Exists(htmlFile))
            {
                description = await File.ReadAllTextAsync(htmlFile);
            }
            else
            {
                string indexHtml = Path.Combine(Path.GetDirectoryName(bslFile) ?? "", "index.html");
                if (File.Exists(indexHtml))
                {
                    description = await File.ReadAllTextAsync(indexHtml);
                }
            }

            var entry = new CodeEntry
            {
                Id = Guid.NewGuid(),
                Title = title,
                Extension = Path.GetExtension(bslFile),
                Code = code,
                Category = category,
                Description = description,
                Syntax = isDarkTheme ? "Темная 1C" : "Стандартная 1C",
                Tags = new List<string> { "Imported" }
            };
            data.Entries.Add(entry);
        }

        var htmlFiles = allFiles.Where(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var htmlFile in htmlFiles)
        {
            string bslEquiv = Path.ChangeExtension(htmlFile, ".bsl");
            if (File.Exists(bslEquiv)) continue;
            if (Path.GetFileName(htmlFile).Equals("index.html", StringComparison.OrdinalIgnoreCase)) continue;

            string relativePath = Path.GetDirectoryName(Path.GetRelativePath(rootPath, htmlFile)) ?? "";

            var pathParts = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(p => !foldersToIgnore.Contains(p));

            string folderCategory = string.Join("/", pathParts);

            string category = string.IsNullOrWhiteSpace(baseCategory)
                ? folderCategory
                : string.IsNullOrWhiteSpace(folderCategory)
                    ? baseCategory
                    : $"{baseCategory}/{folderCategory}";

            category = _categoryService.NormalizeCategoryPath(category);
            if (string.IsNullOrEmpty(category)) category = CategoryService.UncategorizedCategoryName;

            string title = Path.GetFileNameWithoutExtension(htmlFile);
            string description = await File.ReadAllTextAsync(htmlFile);

            var entry = new CodeEntry
            {
                Id = Guid.NewGuid(),
                Title = title,
                Extension = Path.GetExtension(htmlFile),
                Code = "",
                Category = category,
                Description = description,
                Syntax = "Темная HTML",
                Tags = new List<string> { "Imported", "Doc" }
            };
            data.Entries.Add(entry);
        }

        _categoryService.NormalizeImportedData(data);
        await dataService.SaveDataAsync(data);
    }
}
