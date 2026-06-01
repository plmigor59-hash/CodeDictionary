using CodeDictionary;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

public class CustomColorTransformer : DocumentColorizingTransformer
{
    private List<TextSegmentStyle> _segments;

    public CustomColorTransformer(List<TextSegmentStyle> segments)
    {
        _segments = segments;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        int lineStartOffset = line.Offset;
        int lineEndOffset = line.EndOffset;

        foreach (var segment in _segments)
        {
            int segmentStart = segment.StartOffset;
            int segmentEnd = segment.StartOffset + segment.Length;

            // Проверяем, пересекается ли сегмент с текущей строкой
            if (segmentEnd <= lineStartOffset || segmentStart >= lineEndOffset)
                continue;

            int start = Math.Max(lineStartOffset, segmentStart);
            int end = Math.Min(lineEndOffset, segmentEnd);

            // Применяем стиль к отрезку
            base.ChangeLinePart(start, end, (visualLineElement) =>
            {
                // Установка фона
                if (!string.IsNullOrEmpty(segment.BackgroundColor) && segment.BackgroundColor != "#00000000")

                    {
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(segment.BackgroundColor));
                    visualLineElement.TextRunProperties.SetBackgroundBrush(brush);
                }

                // Установка цвета текста
                if (!string.IsNullOrEmpty(segment.ForegroundColor))
                {
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(segment.ForegroundColor));
                    visualLineElement.TextRunProperties.SetForegroundBrush(brush);
                }

                // Определяем текущие параметры шрифта
                var currentFontFamily = visualLineElement.TextRunProperties.Typeface.FontFamily;
                var currentFontStyle = visualLineElement.TextRunProperties.Typeface.Style;
                var currentFontWeight = visualLineElement.TextRunProperties.Typeface.Weight;

                // Применяем шрифт, если указан
                if (!string.IsNullOrEmpty(segment.FontFamily))
                {
                    currentFontFamily = new System.Windows.Media.FontFamily(segment.FontFamily);
                }

                // Жирный шрифт
                if (segment.IsBold)
                {
                    currentFontWeight = FontWeights.Bold;
                }

                // Курсив
                if (segment.IsItalic)
                {
                    currentFontStyle = FontStyles.Italic;
                }

                // Применяем typeface с учетом всех изменений
                if (segment.IsBold || segment.IsItalic || !string.IsNullOrEmpty(segment.FontFamily))
                {
                    visualLineElement.TextRunProperties.SetTypeface(
                        new Typeface(currentFontFamily, currentFontStyle, currentFontWeight, FontStretches.Normal));
                }

                // Размер шрифта
                if (segment.FontSize.HasValue && segment.FontSize.Value > 0)
                {
                    visualLineElement.TextRunProperties.SetFontRenderingEmSize(segment.FontSize.Value);
                }

                // Подчёркивание
                if (segment.IsUnderline)
                {
                    visualLineElement.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
                }
            });
        }
    }
}