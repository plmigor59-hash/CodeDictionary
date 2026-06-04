using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.IO;
using System.Linq;

namespace XshdEditor
{
    public class SyntaxDefinitionViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _extensions = string.Empty;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Extensions
        {
            get => _extensions;
            set { _extensions = value; OnPropertyChanged(); }
        }

        public ObservableCollection<XshdColorRule> Colors { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public static SyntaxDefinitionViewModel Load(string filePath)
        {
            var viewModel = new SyntaxDefinitionViewModel();

            using (var reader = XmlReader.Create(filePath))
            {
                var xshd = HighlightingLoader.LoadXshd(reader);

                viewModel.Name = xshd.Name ?? "Unknown";

                if (xshd.Extensions != null && xshd.Extensions.Count > 0)
                {
                    viewModel.Extensions = string.Join(", ", xshd.Extensions);
                }

                foreach (var element in xshd.Elements)
                {
                    if (element is XshdColor color)
                    {
                        viewModel.Colors.Add(new XshdColorRule
                        {
                            Name = color.Name,
                            Foreground = GetBrushColorHex(color.Foreground, "#000000"),
                            Background = GetBrushColorHex(color.Background, "#00000000"),
                            ExampleText = color.ExampleText ?? string.Empty,
                            FontWeight = color.FontWeight?.ToString() ?? "Normal",
                            FontStyle = color.FontStyle?.ToString() ?? "Normal"
                        });
                    }
                }
            }

            return viewModel;
        }

        public void Save(string filePath)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = System.Text.Encoding.UTF8
            };

            // Write XSHD manually to ensure RuleSet content is present and compatible
            using (var writer = XmlWriter.Create(filePath, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("SyntaxDefinition", "http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008");
                writer.WriteAttributeString("name", string.IsNullOrEmpty(this.Name) ? "" : this.Name);
                if (!string.IsNullOrEmpty(Extensions))
                {
                    // use semicolon-separated extensions
                    var exts = Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
                    writer.WriteAttributeString("extensions", string.Join(";", exts));
                }

                // Write Color definitions
                foreach (var colorRule in Colors)
                {
                    writer.WriteStartElement("Color");
                    writer.WriteAttributeString("name", colorRule.Name ?? string.Empty);
                    if (!string.IsNullOrEmpty(colorRule.Foreground) && colorRule.Foreground != "#00000000")
                        writer.WriteAttributeString("foreground", colorRule.Foreground);
                    if (!string.IsNullOrEmpty(colorRule.Background) && colorRule.Background != "#00000000")
                        writer.WriteAttributeString("background", colorRule.Background);
                    if (!string.IsNullOrEmpty(colorRule.FontStyle) && colorRule.FontStyle != "Normal")
                    {
                        var fs = ConvertToFontStyle(colorRule.FontStyle);
                        if (fs.HasValue)
                        {
                            // write canonical name in lowercase like "italic"/"normal"/"oblique"
                            writer.WriteAttributeString("fontStyle", fs.Value.ToString().ToLower());
                        }
                        else
                        {
                            // fallback: convert to lowercase
                            writer.WriteAttributeString("fontStyle", colorRule.FontStyle.ToLower());
                        }
                    }
                    if (!string.IsNullOrEmpty(colorRule.FontWeight) && colorRule.FontWeight != "Normal")
                    {
                        var fw = ConvertToFontWeight(colorRule.FontWeight);
                        if (fw.HasValue)
                        {
                            writer.WriteAttributeString("fontWeight", fw.Value.ToString().ToLower());
                        }
                        else
                        {
                            writer.WriteAttributeString("fontWeight", colorRule.FontWeight.ToLower());
                        }
                    }
                    if (!string.IsNullOrEmpty(colorRule.ExampleText))
                        writer.WriteAttributeString("exampleText", colorRule.ExampleText);
                    writer.WriteEndElement();
                }

                // Write basic RuleSets
                writer.WriteStartElement("RuleSet");
                writer.WriteAttributeString("ignoreCase", "true");
                writer.WriteEndElement(); // empty RuleSet

                writer.WriteStartElement("RuleSet");
                writer.WriteAttributeString("name", "Main");
                writer.WriteAttributeString("ignoreCase", "true");
                writer.WriteEndElement(); // Main RuleSet

                writer.WriteEndElement(); // SyntaxDefinition
                writer.WriteEndDocument();
            }
        }

        private HighlightingBrush CreateHighlightingBrush(string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex) || colorHex == "#00000000")
                return null;

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                return new SimpleHighlightingBrush(color);
            }
            catch
            {
                return null;
            }
        }

        private static string GetBrushColorHex(HighlightingBrush? brush, string fallback)
        {
            if (brush == null)
            {
                return fallback;
            }

            var colorProperty = brush.GetType().GetProperty("Color");
            if (colorProperty?.GetValue(brush) is Color color)
            {
                return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            }

            var text = brush.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    var converted = ColorConverter.ConvertFromString(text);
                    if (converted is Color parsedColor)
                    {
                        return $"#{parsedColor.A:X2}{parsedColor.R:X2}{parsedColor.G:X2}{parsedColor.B:X2}";
                    }
                }
                catch
                {
                }
            }

            return fallback;
        }

        private FontWeight? ConvertToFontWeight(string fontWeight)
        {
            return fontWeight?.ToLower() switch
            {
                "bold" => FontWeights.Bold,
                "normal" => FontWeights.Normal,
                "light" => FontWeights.Light,
                "ultrabold" => FontWeights.UltraBold,
                "semibold" => FontWeights.SemiBold,
                _ => FontWeights.Normal
            };
        }

        private FontStyle? ConvertToFontStyle(string fontStyle)
        {
            return fontStyle?.ToLower() switch
            {
                "italic" => FontStyles.Italic,
                "oblique" => FontStyles.Oblique,
                "normal" => FontStyles.Normal,
                _ => FontStyles.Normal
            };
        }
    }
}
