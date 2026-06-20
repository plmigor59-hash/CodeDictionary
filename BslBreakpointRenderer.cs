using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;

namespace CodeDictionary;

public class DebugLineHighlighter : IBackgroundRenderer
{
    public int? CurrentLine { get; set; }

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!CurrentLine.HasValue) return;

        var visualLine = textView.VisualLines.FirstOrDefault(vl => vl.FirstDocumentLine.LineNumber == CurrentLine.Value);
        if (visualLine != null)
        {
            var y = visualLine.VisualTop - textView.VerticalOffset;
            var rect = new Rect(0, y, textView.ActualWidth, visualLine.Height);
            drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(50, 255, 200, 50)), null, rect);
        }
    }
}
