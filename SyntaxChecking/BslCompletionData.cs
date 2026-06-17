using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Material.Icons;
using System.Windows.Media;

namespace CodeDictionary.SyntaxChecking
{


    public class BslCompletionData : ICompletionData
    {
        public BslCompletionData(string text, string description, string type)
        {
            Text = text;
            Description = description;
            Type = type;
        }

        public ImageSource? Image => GetImageForType(Type);

        private ImageSource? GetImageForType(string type)
        {
            var lowerType = type.ToLowerInvariant();
            MaterialIconKind kind = MaterialIconKind.Code;
            Color color = Colors.Gray;

            if (lowerType.Contains("функция")) { kind = MaterialIconKind.Function; color = Colors.Blue; }
            else if (lowerType.Contains("процедура")) { kind = MaterialIconKind.PlayCircle; color = Colors.DarkGreen; }
            else if (lowerType.Contains("переменная")) { kind = MaterialIconKind.Variable; color = Colors.Orange; }
            else if (lowerType.Contains("ключевое слово")) { kind = MaterialIconKind.Key; color = Colors.Black; }

            // Get geometry path from the provider (returns string)
            var pathData = MaterialIconDataProvider.GetData(kind);

            // Parse the string into a Geometry object
            var geometry = Geometry.Parse(pathData);

            // Create a GeometryDrawing
            var drawing = new GeometryDrawing(new SolidColorBrush(color), null, geometry);

            // Create and freeze the DrawingImage
            var drawingImage = new DrawingImage(drawing);
            if (drawingImage.CanFreeze)
                drawingImage.Freeze();

            return drawingImage;
        }
        public string Text { get; }
        public object Content => Text;
        public object Description { get; }
        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            int offset = completionSegment.Offset;
            var document = textArea.Document;

            int start = offset;
            while (start > 0 && IsIdentifierChar(document.GetCharAt(start - 1)))
                start--;

            int end = offset;
            while (end < document.TextLength && IsIdentifierChar(document.GetCharAt(end)))
                end++;

            textArea.Document.Replace(start, end - start, Text + " ");
        }

        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        public string Type { get; }
    }
}
