using CodeDictionary;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;

public class CustomColorTransformer : DocumentColorizingTransformer
{
    private readonly Func<IReadOnlyList<TextSegmentStyle>> _segmentsProvider;

    public CustomColorTransformer(Func<IReadOnlyList<TextSegmentStyle>> segmentsProvider)
    {
        _segmentsProvider = segmentsProvider;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var segments = _segmentsProvider();
        int lineStartOffset = line.Offset;
        int lineEndOffset = line.EndOffset;

        foreach (var segment in segments)
        {
            int segmentStart = segment.StartOffset;
            int segmentEnd = segment.StartOffset + segment.Length;

            if (segmentEnd <= lineStartOffset || segmentStart >= lineEndOffset)
                continue;

            int start = Math.Max(lineStartOffset, segmentStart);
            int end = Math.Min(lineEndOffset, segmentEnd);

            base.ChangeLinePart(start, end, visualLineElement =>
            {
                if (!string.IsNullOrEmpty(segment.BackgroundColor) && segment.BackgroundColor != "#00000000")
                {
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(segment.BackgroundColor));
                    visualLineElement.TextRunProperties.SetBackgroundBrush(brush);
                }

                if (!string.IsNullOrEmpty(segment.ForegroundColor))
                {
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(segment.ForegroundColor));
                    visualLineElement.TextRunProperties.SetForegroundBrush(brush);
                }

                var currentFontFamily = visualLineElement.TextRunProperties.Typeface.FontFamily;
                var currentFontStyle = visualLineElement.TextRunProperties.Typeface.Style;
                var currentFontWeight = visualLineElement.TextRunProperties.Typeface.Weight;

                if (!string.IsNullOrEmpty(segment.FontFamily))
                {
                    currentFontFamily = new System.Windows.Media.FontFamily(segment.FontFamily);
                }

                if (segment.IsBold)
                {
                    currentFontWeight = FontWeights.Bold;
                }

                if (segment.IsItalic)
                {
                    currentFontStyle = FontStyles.Italic;
                }

                if (segment.IsBold || segment.IsItalic || !string.IsNullOrEmpty(segment.FontFamily))
                {
                    visualLineElement.TextRunProperties.SetTypeface(
                        new Typeface(currentFontFamily, currentFontStyle, currentFontWeight, FontStretches.Normal));
                }

                if (segment.FontSize.HasValue && segment.FontSize.Value > 0)
                {
                    visualLineElement.TextRunProperties.SetFontRenderingEmSize(segment.FontSize.Value);
                }

                if (segment.IsUnderline)
                {
                    visualLineElement.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
                }
            });
        }
    }
}
