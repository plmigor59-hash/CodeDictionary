using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;

namespace CodeDictionary;

public class BookmarkMargin : AbstractMargin
{
    private readonly Func<HashSet<int>> _bookmarksProvider;
    private readonly Func<Brush> _iconBrushProvider;

    public BookmarkMargin(Func<HashSet<int>> bookmarksProvider, Func<Brush> iconBrushProvider)
    {
        _bookmarksProvider = bookmarksProvider;
        _iconBrushProvider = iconBrushProvider;
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
    {
        return new PointHitTestResult(this, hitTestParameters.HitPoint);
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null)
        {
            oldTextView.ScrollOffsetChanged -= TextViewScrollOffsetChanged;
        }
        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView != null)
        {
            newTextView.ScrollOffsetChanged += TextViewScrollOffsetChanged;
        }
    }

    private void TextViewScrollOffsetChanged(object sender, EventArgs e)
    {
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var textView = this.TextView;
        if (textView == null || !textView.VisualLinesValid) return;

        var bookmarks = _bookmarksProvider();
        if (bookmarks == null) return;

        var brush = _iconBrushProvider();

        foreach (var visualLine in textView.VisualLines)
        {
            int lineNumber = visualLine.FirstDocumentLine.LineNumber;
            if (bookmarks.Contains(lineNumber))
            {
                double y = visualLine.VisualTop - textView.VerticalOffset;

                // Draw a simple ribbon shape
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(4, y + 2), true, true);
                    ctx.LineTo(new Point(14, y + 2), true, false);
                    ctx.LineTo(new Point(14, y + 16), true, false);
                    ctx.LineTo(new Point(9, y + 12), true, false);
                    ctx.LineTo(new Point(4, y + 16), true, false);
                }
                geometry.Freeze();

                drawingContext.DrawGeometry(brush, null, geometry);
            }
        }
    }

    public void Redraw()
    {
        this.InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(18, 0); // Margin width
    }
}
