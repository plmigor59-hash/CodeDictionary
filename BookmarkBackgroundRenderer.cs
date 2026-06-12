using ICSharpCode.AvalonEdit.Rendering;
using System.Windows.Media;
using System.Windows;

namespace CodeDictionary;

public class BookmarkBackgroundRenderer : IBackgroundRenderer
{
    private readonly Func<IReadOnlyList<int>> _bookmarksProvider;
    private readonly TextView _textView;

    public BookmarkBackgroundRenderer(TextView textView, Func<IReadOnlyList<int>> bookmarksProvider)
    {
        _textView = textView;
        _bookmarksProvider = bookmarksProvider;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var bookmarks = _bookmarksProvider();

        var isDarkTheme = Application.Current.Resources["IsDarkTheme"] is bool dark && dark;

        if (bookmarks == null) return;

        foreach (var lineNumber in bookmarks)
        {
            if (lineNumber < 1 || lineNumber > textView.Document.LineCount) continue;

            var line = textView.Document.GetLineByNumber(lineNumber);
            var visualLine = textView.VisualLines.FirstOrDefault(vl => vl.FirstDocumentLine.LineNumber == lineNumber);

            if (visualLine != null)
            {
                var rect = new Rect(0, visualLine.VisualTop - textView.VerticalOffset, textView.ActualWidth, visualLine.Height);
                // Draw a light blue background for the bookmarked line

                if (isDarkTheme)
                    drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(222, 255, 248, 238)), null, rect);
                else

                    drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(83, 92, 89, 255)), null, rect);
            }
        }
    }
}
