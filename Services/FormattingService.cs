using CodeDictionary.Models;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeDictionary.Services;

public class FormattingService
{
    private readonly JsonSerializerOptions _saveOptions;
    private readonly JsonSerializerOptions _loadOptions;

    public FormattingService()
    {
        _saveOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        _loadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public void SaveSegmentsToEntry(CodeEntry entry, List<TextSegmentStyle> segments)
    {
        if (entry == null)
            return;

        var container = new FormattingContainer
        {
            Segments = segments,
            Version = 1,
            SavedAt = DateTime.Now
        };

        entry.FormattingData = JsonSerializer.Serialize(container, _saveOptions);
    }

    public void LoadSegmentsFromEntry(CodeEntry? entry, List<TextSegmentStyle> targetSegments)
    {
        targetSegments.Clear();

        if (entry == null)
            return;

        if (string.IsNullOrEmpty(entry.FormattingData))
            return;

        try
        {
            var container = JsonSerializer.Deserialize<FormattingContainer>(
                entry.FormattingData, _loadOptions);

            if (container?.Segments != null)
            {
                targetSegments.AddRange(container.Segments);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки форматирования: {ex.Message}");
        }
    }

    public void LoadSegmentsIntoTab(EditorTabModel tab, CodeEntry entry)
    {
        tab.Segments.Clear();

        if (string.IsNullOrEmpty(entry.FormattingData))
            return;

        try
        {
            var container = JsonSerializer.Deserialize<FormattingContainer>(entry.FormattingData, _loadOptions);
            if (container?.Segments != null)
            {
                tab.Segments.AddRange(container.Segments);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки форматирования: {ex.Message}");
            tab.Segments.Clear();
        }
    }

    public void ApplyStyleToSelection(
        List<TextSegmentStyle> segments,
        int startOffset,
        int length,
        string? backgroundColor = null,
        string? foregroundColor = null,
        bool? bold = null,
        bool? italic = null,
        bool? underline = null,
        string? fontFamily = null,
        double? fontSize = null)
    {
        var existingSegment = segments.FirstOrDefault(s => s.StartOffset == startOffset && s.Length == length);

        if (existingSegment != null)
        {
            if (backgroundColor != null) existingSegment.BackgroundColor = backgroundColor;
            if (foregroundColor != null) existingSegment.ForegroundColor = foregroundColor;
            if (bold.HasValue) existingSegment.IsBold = bold.Value;
            if (italic.HasValue) existingSegment.IsItalic = italic.Value;
            if (underline.HasValue) existingSegment.IsUnderline = underline.Value;
            if (fontFamily != null) existingSegment.FontFamily = fontFamily;
            if (fontSize.HasValue) existingSegment.FontSize = fontSize.Value;
        }
        else
        {
            var newSegment = new TextSegmentStyle
            {
                StartOffset = startOffset,
                Length = length,
                BackgroundColor = backgroundColor,
                ForegroundColor = foregroundColor,
                IsBold = bold ?? false,
                IsItalic = italic ?? false,
                IsUnderline = underline ?? false,
                FontFamily = fontFamily,
                FontSize = fontSize
            };
            segments.Add(newSegment);
        }
    }

    public void ClearStyleFromSelection(List<TextSegmentStyle> segments, int start, int end)
    {
        segments.RemoveAll(s => s.StartOffset >= start && s.StartOffset + s.Length <= end);
    }

    public void UpdateSegmentsAfterTextChange(List<TextSegmentStyle> segments, int currentLength)
    {
        segments.RemoveAll(s => s.StartOffset + s.Length > currentLength);
    }
}
