using System.Windows.Media;

namespace XshdEditor
{
    public class XshdColorRule
    {
        public string Name { get; set; } = string.Empty;
        public string Foreground { get; set; } = "#000000";
        public string Background { get; set; } = "#00000000";
        public string FontStyle { get; set; } = "Normal";
        public string FontWeight { get; set; } = "Normal";
        public string ExampleText { get; set; } = string.Empty;

        public Color ForegroundColor
        {
            get => ParseColor(Foreground, Colors.Black);
            set => Foreground = ToHex(value);
        }

        public Color BackgroundColor
        {
            get => ParseColor(Background, Colors.Transparent);
            set => Background = ToHex(value);
        }

        private static Color ParseColor(string? colorText, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(colorText))
            {
                return fallback;
            }

            try
            {
                var converted = ColorConverter.ConvertFromString(colorText);
                return converted is Color color ? color : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static string ToHex(Color color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
