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

            foreach (var error in errors)
            {
                // We keep this to potentially change the foreground color 
                // or just to have a placeholder for line transformation logic.
                // Currently, we'll just let the squiggle do the work.
            }
        }
    }
}
