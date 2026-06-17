using CodeDictionary.Models;
using System.IO;

namespace CodeDictionary.Services;

public class CategoryService
{
    public const string CategorySeparator = "/";
    public const string UncategorizedCategoryName = "Без категории";

    public string NormalizeCategoryPath(string? categoryPath)
    {
        if (string.IsNullOrWhiteSpace(categoryPath))
        {
            return string.Empty;
        }

        var segments = categoryPath
            .Replace('\\', CategorySeparator[0])
            .Split(CategorySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        return string.Join(CategorySeparator, segments);
    }

    public string GetCategorySegmentName(string categoryPath)
    {
        var normalized = NormalizeCategoryPath(categoryPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return UncategorizedCategoryName;
        }

        var lastSeparatorIndex = normalized.LastIndexOf(CategorySeparator, StringComparison.Ordinal);
        return lastSeparatorIndex >= 0
            ? normalized[(lastSeparatorIndex + 1)..]
            : normalized;
    }

    public string GetParentCategoryPath(string categoryPath)
    {
        var normalized = NormalizeCategoryPath(categoryPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lastSeparatorIndex = normalized.LastIndexOf(CategorySeparator, StringComparison.Ordinal);
        return lastSeparatorIndex > 0
            ? normalized[..lastSeparatorIndex]
            : string.Empty;
    }

    public IEnumerable<string> EnumerateCategoryAncestors(string categoryPath)
    {
        var normalized = NormalizeCategoryPath(categoryPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        var segments = normalized.Split(CategorySeparator, StringSplitOptions.RemoveEmptyEntries);
        var currentPath = string.Empty;
        foreach (var segment in segments)
        {
            currentPath = string.IsNullOrWhiteSpace(currentPath)
                ? segment
                : $"{currentPath}{CategorySeparator}{segment}";
            yield return currentPath;
        }
    }

    public bool IsPathWithin(string path, string parentPath)
    {
        var normalizedPath = NormalizeCategoryPath(path);
        var normalizedParentPath = NormalizeCategoryPath(parentPath);

        if (string.IsNullOrWhiteSpace(normalizedParentPath))
        {
            return false;
        }

        return string.Equals(normalizedPath, normalizedParentPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith($"{normalizedParentPath}{CategorySeparator}", StringComparison.OrdinalIgnoreCase);
    }

    public List<CategoryNode> BuildCategoryTree(IEnumerable<CodeEntry> entries, List<string> categories)
    {
        var nodeLookup = new Dictionary<string, CategoryNode>(StringComparer.OrdinalIgnoreCase);

        CategoryNode GetOrCreateNode(string path)
        {
            var normalizedPath = NormalizeCategoryPath(path);
            if (nodeLookup.TryGetValue(normalizedPath, out var existingNode))
            {
                return existingNode;
            }

            var node = new CategoryNode
            {
                Name = string.IsNullOrWhiteSpace(normalizedPath)
                    ? UncategorizedCategoryName
                    : GetCategorySegmentName(normalizedPath),
                FullPath = normalizedPath
            };

            nodeLookup[normalizedPath] = node;

            var parentPath = GetParentCategoryPath(normalizedPath);
            if (!string.IsNullOrWhiteSpace(parentPath))
            {
                GetOrCreateNode(parentPath).Children.Add(node);
            }

            return node;
        }

        foreach (var categoryPath in GetKnownCategoryPaths(entries, categories))
        {
            foreach (var ancestor in EnumerateCategoryAncestors(categoryPath))
            {
                GetOrCreateNode(ancestor);
            }
        }

        var uncategorizedNode = GetOrCreateNode(string.Empty);

        foreach (var entry in entries)
        {
            var normalizedCategory = NormalizeCategoryPath(entry.Category);
            var node = string.IsNullOrWhiteSpace(normalizedCategory)
                ? uncategorizedNode
                : GetOrCreateNode(normalizedCategory);

            node.Entries.Add(new CodeEntryViewModel(entry));
        }

        var roots = nodeLookup.Values
            .Where(node => string.IsNullOrWhiteSpace(GetParentCategoryPath(node.FullPath)))
            .OrderBy(node => node.FullPath == string.Empty ? 1 : 0)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SortCategoryNodes(roots);
        return roots;
    }

    public void SortCategoryNodes(IEnumerable<CategoryNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.Children.Sort((left, right) =>
                string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            node.Entries.Sort((left, right) =>
                string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase));
            SortCategoryNodes(node.Children);
        }
    }

    public CodeEntryViewModel? FindEntryViewModel(IEnumerable<CategoryNode> categories, Guid entryId)
    {
        foreach (var category in categories)
        {
            var directEntry = category.Entries.FirstOrDefault(entry => entry.Entry.Id == entryId);
            if (directEntry != null)
            {
                return directEntry;
            }

            var nestedEntry = FindEntryViewModel(category.Children, entryId);
            if (nestedEntry != null)
            {
                return nestedEntry;
            }
        }

        return null;
    }

    public CategoryNode? FindCategoryNode(IEnumerable<CategoryNode> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.FullPath, path, StringComparison.CurrentCultureIgnoreCase))
                return node;

            var found = FindCategoryNode(node.Children, path);
            if (found != null) return found;
        }
        return null;
    }

    public void ExpandAncestors(IEnumerable<CategoryNode> nodes, string path)
    {
        var ancestors = EnumerateCategoryAncestors(path).ToList();
        foreach (var ancestor in ancestors)
        {
            var node = FindCategoryNode(nodes, ancestor);
            if (node != null) node.IsExpanded = true;
        }
    }

    public bool ExpandIfHasEntries(CategoryNode node)
    {
        bool hasMatchingEntries = node.Entries.Count > 0;
        bool hasMatchingChildren = false;

        foreach (var child in node.Children)
        {
            if (ExpandIfHasEntries(child))
            {
                hasMatchingChildren = true;
            }
        }

        if (hasMatchingEntries || hasMatchingChildren)
        {
            node.IsExpanded = true;
            return true;
        }

        return false;
    }

    public List<string> GetKnownCategoryPaths(IEnumerable<CodeEntry> entries, List<string> categories)
    {
        return categories
            .Concat(entries.Select(entry => entry.Category))
            .Select(NormalizeCategoryPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .SelectMany(EnumerateCategoryAncestors)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void EnsureCategoryPathExists(string? categoryPath, List<string> categories)
    {
        var normalizedPath = NormalizeCategoryPath(categoryPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        foreach (var ancestor in EnumerateCategoryAncestors(normalizedPath))
        {
            if (!categories.Any(existing => string.Equals(NormalizeCategoryPath(existing), ancestor, StringComparison.OrdinalIgnoreCase)))
            {
                categories.Add(ancestor);
            }
        }
    }

    public void NormalizeImportedData(CodeDictionaryData data)
    {
        foreach (var entry in data.Entries)
        {
            entry.Category = NormalizeCategoryPath(entry.Category);
        }

        var normalizedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in data.Categories)
        {
            var normalizedCategory = NormalizeCategoryPath(category);
            if (string.IsNullOrWhiteSpace(normalizedCategory))
            {
                continue;
            }

            foreach (var ancestor in EnumerateCategoryAncestors(normalizedCategory))
            {
                normalizedCategories.Add(ancestor);
            }
        }

        foreach (var entry in data.Entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Category))
            {
                foreach (var ancestor in EnumerateCategoryAncestors(entry.Category))
                {
                    normalizedCategories.Add(ancestor);
                }
            }
        }

        data.Categories = normalizedCategories
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public CodeDictionaryData BuildCategoryExportData(string categoryPath, CodeDictionaryData sourceData)
    {
        var normalizedCategoryPath = NormalizeCategoryPath(categoryPath);
        var exportedEntries = sourceData.Entries
            .Where(entry => IsPathWithin(NormalizeCategoryPath(entry.Category), normalizedCategoryPath))
            .Select(CloneEntry)
            .ToList();

        var exportedCategories = sourceData.Categories
            .Select(NormalizeCategoryPath)
            .Where(path => IsPathWithin(path, normalizedCategoryPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var entryCategory in exportedEntries.Select(entry => NormalizeCategoryPath(entry.Category)))
        {
            if (string.IsNullOrWhiteSpace(entryCategory))
            {
                continue;
            }

            foreach (var ancestor in EnumerateCategoryAncestors(entryCategory))
            {
                if (!exportedCategories.Any(path => string.Equals(path, ancestor, StringComparison.OrdinalIgnoreCase)))
                {
                    exportedCategories.Add(ancestor);
                }
            }
        }

        exportedCategories = exportedCategories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!exportedCategories.Any(path => string.Equals(path, normalizedCategoryPath, StringComparison.OrdinalIgnoreCase)))
        {
            exportedCategories.Insert(0, normalizedCategoryPath);
        }

        return new CodeDictionaryData
        {
            Entries = exportedEntries,
            Categories = exportedCategories
        };
    }

    public static CodeEntry CloneEntry(CodeEntry entry)
    {
        return new CodeEntry
        {
            Id = entry.Id,
            Title = entry.Title,
            Description = entry.Description,
            Code = entry.Code,
            Category = entry.Category,
            Tags = entry.Tags.ToList(),
            Syntax = entry.Syntax,
            CreatedAt = entry.CreatedAt,
            ModifiedAt = entry.ModifiedAt,
            FormattingData = entry.FormattingData
        };
    }

    public static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "category" : sanitized;
    }

    public static string EnsureExportExtension(string fileName, int filterIndex)
    {
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return fileName;
        }

        return filterIndex == 2 ? $"{fileName}.xml" : $"{fileName}.json";
    }

    public void SaveExpansionState(HashSet<string> expandedCategories, IEnumerable<CategoryNode> nodes)
    {
        expandedCategories.Clear();
        SaveExpansionStateRecursive(expandedCategories, nodes);
    }

    private void SaveExpansionStateRecursive(HashSet<string> expandedCategories, IEnumerable<CategoryNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsExpanded)
            {
                expandedCategories.Add(node.FullPath);
                SaveExpansionStateRecursive(expandedCategories, node.Children);
            }
        }
    }

    public void ApplyExpansionState(HashSet<string> expandedCategories, IEnumerable<CategoryNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (expandedCategories.Contains(node.FullPath))
            {
                node.IsExpanded = true;
            }
            ApplyExpansionState(expandedCategories, node.Children);
        }
    }

    public string ReplaceCategoryPrefix(string path, string sourcePath, string newPath)
    {
        var normalizedPath = NormalizeCategoryPath(path);
        var normalizedSource = NormalizeCategoryPath(sourcePath);
        var normalizedNew = NormalizeCategoryPath(newPath);

        if (string.IsNullOrWhiteSpace(normalizedSource))
        {
            return normalizedPath;
        }

        if (string.Equals(normalizedPath, normalizedSource, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedNew;
        }

        var suffix = normalizedPath.Substring(normalizedSource.Length);
        if (suffix.StartsWith(CategorySeparator, StringComparison.Ordinal))
        {
            suffix = suffix.Substring(CategorySeparator.Length);
        }

        return string.IsNullOrWhiteSpace(suffix)
            ? normalizedNew
            : $"{normalizedNew}{CategorySeparator}{suffix}";
    }

    public object? GetNeighborData(IEnumerable<CategoryNode> roots, object? item)
    {
        if (item == null) return null;

        (object? parent, int index, IList<object>? siblings) FindInNodes(IEnumerable<object> nodes, object target)
        {
            var list = nodes.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == target) return (null, i, list);

                if (list[i] is CategoryNode cat)
                {
                    var result = FindInNodes(cat.Items, target);
                    if (result.siblings != null)
                    {
                        return (result.parent ?? cat, result.index, result.siblings);
                    }
                }
            }
            return (null, -1, null);
        }

        var (parent, index, siblings) = FindInNodes(roots, item);
        if (siblings == null) return null;

        object? neighbor = null;
        if (siblings.Count > 1)
        {
            if (index + 1 < siblings.Count) neighbor = siblings[index + 1];
            else if (index - 1 >= 0) neighbor = siblings[index - 1];
        }

        if (neighbor == null) neighbor = parent;

        if (neighbor is CodeEntryViewModel evm) return evm.Entry;
        if (neighbor is CategoryNode cn) return cn.FullPath;
        return neighbor;
    }

    public void FindCheckedItems(IEnumerable<object>? items, List<CodeEntryViewModel> entries, List<CategoryNode> categories)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (item is CategoryNode cat)
            {
                if (cat.IsChecked) categories.Add(cat);
                FindCheckedItems(cat.Items, entries, categories);
            }
            else if (item is CodeEntryViewModel entry)
            {
                if (entry.IsChecked) entries.Add(entry);
            }
        }
    }

    public bool IsCategoryPathTakenByAnotherNode(string sourcePath, string newPath, List<string> categories)
    {
        return categories.Any(existing =>
        {
            var normalizedExisting = NormalizeCategoryPath(existing);
            if (string.IsNullOrWhiteSpace(normalizedExisting))
            {
                return false;
            }

            if (IsPathWithin(normalizedExisting, sourcePath))
            {
                return false;
            }

            return string.Equals(normalizedExisting, newPath, StringComparison.OrdinalIgnoreCase);
        });
    }

    public void RemapCategoryPath(string sourcePath, string newPath, CodeDictionaryData data)
    {
        var updatedCategoryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var existing in data.Categories)
        {
            var normalizedExisting = NormalizeCategoryPath(existing);
            if (IsPathWithin(normalizedExisting, sourcePath))
            {
                updatedCategoryPaths.Add(ReplaceCategoryPrefix(normalizedExisting, sourcePath, newPath));
            }
            else if (!string.IsNullOrWhiteSpace(normalizedExisting))
            {
                updatedCategoryPaths.Add(normalizedExisting);
            }
        }

        foreach (var entry in data.Entries)
        {
            var normalizedEntryCategory = NormalizeCategoryPath(entry.Category);
            if (IsPathWithin(normalizedEntryCategory, sourcePath))
            {
                entry.Category = ReplaceCategoryPrefix(normalizedEntryCategory, sourcePath, newPath);
            }
        }

        data.Categories = updatedCategoryPaths
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
