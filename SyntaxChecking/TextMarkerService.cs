using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using CodeDictionary.Analysis;

namespace CodeDictionary.SyntaxChecking
{
    public class TextMarkerService : IBackgroundRenderer
    {
        private readonly TextDocument _document;
        private readonly List<BslSyntaxError> _markers = new();
        private TextView _textView;

        public TextMarkerService(TextDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public void UpdateMarkers(IEnumerable<BslSyntaxError> errors)
        {
            _markers.Clear();
            if (errors != null)
            {
                _markers.AddRange(errors);
            }

            if (_textView != null && _textView.VisualLinesValid)
            {
                _textView.InvalidateLayer(KnownLayer.Selection);
            }
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_markers.Count == 0 || !textView.VisualLinesValid)
                return;

            var visualLines = textView.VisualLines;
            if (visualLines.Count == 0)
                return;

            int viewStart = visualLines.First().FirstDocumentLine.LineNumber;
            int viewEnd = visualLines.Last().LastDocumentLine.LineNumber;

            foreach (var marker in _markers)
            {
                if (marker.Line < viewStart || marker.Line > viewEnd)
                    continue;

                foreach (var line in visualLines)
                {
                    if (line.FirstDocumentLine.LineNumber == marker.Line)
                    {
                        DrawSquiggle(textView, drawingContext, line, marker);
                        break;
                    }
                }
            }
        }

        private void DrawSquiggle(TextView textView, DrawingContext drawingContext, VisualLine line, BslSyntaxError marker)
        {
            if (marker.Line < 1 || marker.Line > _document.LineCount)
                return;

            var documentLine = _document.GetLineByNumber(marker.Line);
            int lineStart = documentLine.Offset;
            int startOffset = lineStart + Math.Max(0, marker.Column - 1);
            int endOffset = startOffset + Math.Max(1, marker.Length);

            startOffset = Math.Max(lineStart, Math.Min(startOffset, lineStart + documentLine.TotalLength));
            endOffset = Math.Max(lineStart, Math.Min(endOffset, lineStart + documentLine.TotalLength));

            if (startOffset >= endOffset) return;

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = startOffset, EndOffset = endOffset }))
            {
                Point startPoint = rect.BottomLeft;
                Point endPoint = rect.BottomRight;
                startPoint.Y -= 1;
                endPoint.Y -= 1;
                DrawWavyLine(drawingContext, startPoint, endPoint);
            }
        }

        private void DrawWavyLine(DrawingContext drawingContext, Point start, Point end)
        {
            var pen = new Pen(Brushes.Red, 0.4);
            pen.Freeze();

            double waveWidth = 2;
            double waveHeight = 1.5;

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(start, false, false);
                double x = start.X;
                bool up = true;
                while (x + waveWidth < end.X)
                {
                    x += waveWidth;
                    double y = start.Y + (up ? -waveHeight : 0);
                    ctx.LineTo(new Point(x, y), true, false);
                    up = !up;
                }
                ctx.LineTo(end, true, false);
            }
            geometry.Freeze();
            drawingContext.DrawGeometry(null, pen, geometry);
        }

        public void AddToTextView(TextView textView)
        {
            if (textView != null && !textView.BackgroundRenderers.Contains(this))
            {
                textView.BackgroundRenderers.Add(this);
                _textView = textView;
            }
        }

        public void RemoveFromTextView(TextView textView)
        {
            if (textView != null)
            {
                textView.BackgroundRenderers.Remove(this);
                if (_textView == textView)
                    _textView = null;
            }
        }
    }

    internal class TextSegment : ISegment
    {
        public int Offset => StartOffset;
        public int Length => EndOffset - StartOffset;
        public int EndOffset { get; set; }
        public int StartOffset { get; set; }
    }
}
