using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CodeDictionary.Analysis;

namespace CodeDictionary.SyntaxChecking
{
    /// <summary>
    /// Colorizes the text of errors (e.g., changes foreground color).
    /// Squiggles are handled by TextMarkerService.
    /// </summary>
    public class SyntaxErrorColorizer : DocumentColorizingTransformer
    {
        private readonly Dictionary<int, List<BslSyntaxError>> _errorsByLine = new();

        public SyntaxErrorColorizer(TextDocument document)
        {
        }

        public void UpdateErrors(List<BslSyntaxError> errors)
        {
            _errorsByLine.Clear();

            if (errors == null) return;

            foreach (var error in errors)
            {
                if (!_errorsByLine.ContainsKey(error.Line))
                    _errorsByLine[error.Line] = new List<BslSyntaxError>();

                _errorsByLine[error.Line].Add(error);
            }
        }

        public List<BslSyntaxError> GetErrorsAtLine(int lineNumber)
        {
            if (_errorsByLine.TryGetValue(lineNumber, out var errors))
                return errors;
            return new List<BslSyntaxError>();
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (!_errorsByLine.TryGetValue(line.LineNumber, out var errors))
                return;

            int lineStart = line.Offset;
            int lineEnd = line.EndOffset;

            foreach (var error in errors)
            {
                // Calculate start and length within line
                int startOffset = lineStart + Math.Max(0, error.Column - 1);
                int length = Math.Max(1, error.Length);

                // Ensure it doesn't exceed line boundaries
                if (startOffset >= lineEnd) continue;
                int endOffset = Math.Min(lineEnd, startOffset + length);

                if (endOffset <= startOffset) continue;

                ChangeLinePart(startOffset, endOffset, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(Brushes.Red);
                });
            }
        }
    }
}
