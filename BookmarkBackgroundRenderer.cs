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

        var isDarkTheme = Theme.CurrentTheme.ToString() == "Dark";

        if (bookmarks == null) return;

        foreach (var lineNumber in bookmarks)
        {
            if (lineNumber < 1 || lineNumber > textView.Document.LineCount) continue;

            var line = textView.Document.GetLineByNumber(lineNumber);
            var visualLine = textView.VisualLines.FirstOrDefault(vl => vl.FirstDocumentLine.LineNumber == lineNumber);
            
            


            if (visualLine != null)
            {
                var rect = new Rect(0, visualLine.VisualTop - textView.VerticalOffset, textView.ActualWidth, visualLine.Height);
               

                //if (isDarkTheme)
                   drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(183, 92, 89, 238)), null, rect);
                   //else
                   // drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), null, rect);
            }
        }
    }
}
