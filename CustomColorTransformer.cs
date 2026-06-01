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

                // Жирный шрифт
                if (segment.IsBold)
                {
                    visualLineElement.TextRunProperties.SetTypeface(
                        new Typeface(visualLineElement.TextRunProperties.Typeface.FontFamily,
                                    FontStyles.Normal,
                                    FontWeights.Bold,
                                    FontStretches.Normal));
                }

                // Курсив
                if (segment.IsItalic)
                {
                    visualLineElement.TextRunProperties.SetTypeface(
                        new Typeface(visualLineElement.TextRunProperties.Typeface.FontFamily,
                                    FontStyles.Italic,
                                    visualLineElement.TextRunProperties.Typeface.Weight,
                                    FontStretches.Normal));
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