using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;

namespace CodeDictionary;

public class BookmarkMargin : AbstractMargin
{
    private readonly Func<HashSet<int>> _bookmarksProvider;
    private readonly Func<Brush> _iconBrushProvider;
    private readonly Func<HashSet<int>>? _breakpointsProvider;

    public BookmarkMargin(Func<HashSet<int>> bookmarksProvider, Func<Brush> iconBrushProvider, Func<HashSet<int>>? breakpointsProvider = null)
    {
        _bookmarksProvider = bookmarksProvider;
        _iconBrushProvider = iconBrushProvider;
        _breakpointsProvider = breakpointsProvider;
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

    private void TextViewScrollOffsetChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var textView = this.TextView;
        if (textView == null || !textView.VisualLinesValid) return;

        var bookmarks = _bookmarksProvider();
        var brush = _iconBrushProvider();

        foreach (var visualLine in textView.VisualLines)
        {
            int lineNumber = visualLine.FirstDocumentLine.LineNumber;
            bool hasBookmark = bookmarks != null && bookmarks.Contains(lineNumber);
            bool hasBreakpoint = _breakpointsProvider != null && _breakpointsProvider().Contains(lineNumber);

            if (hasBreakpoint)
            {
                double y = visualLine.VisualTop - textView.VerticalOffset;
                drawingContext.DrawEllipse(
                    new SolidColorBrush(Color.FromRgb(235, 60, 60)),
                    new Pen(new SolidColorBrush(Color.FromRgb(180, 30, 30)), 1.5),
                    new Point(9, y + 9), 6, 6);
            }

            if (hasBookmark)
            {
                double y = visualLine.VisualTop - textView.VerticalOffset;
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
