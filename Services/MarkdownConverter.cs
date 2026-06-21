using System.Text;
using System.Text.RegularExpressions;

namespace CodeDictionary.Services;

public static class MarkdownConverter
{
    public static string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var lines = markdown.Split('\n');
        var html = new StringBuilder();
        bool inCodeBlock = false;
        bool inParagraph = false;
        bool inList = false;

        void CloseParagraph()
        {
            if (inParagraph) { html.AppendLine("</p>"); inParagraph = false; }
        }

        void CloseList()
        {
            if (inList) { html.AppendLine("</ul>"); inList = false; }
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            // Fenced code blocks
            if (line.StartsWith("```"))
            {
                CloseParagraph();
                CloseList();
                if (inCodeBlock)
                {
                    html.AppendLine("</code></pre>");
                    inCodeBlock = false;
                }
                else
                {
                    html.AppendLine("<pre><code>");
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                html.AppendLine(EscapeHtml(line));
                continue;
            }

            // Empty line
            if (string.IsNullOrWhiteSpace(line))
            {
                CloseParagraph();
                CloseList();
                continue;
            }

            // HR
            if (Regex.IsMatch(line, @"^-{3,}$|^\*{3,}$|^_{3,}$"))
            {
                CloseParagraph();
                CloseList();
                html.AppendLine("<hr />");
                continue;
            }

            // Headers
            var headerMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
            if (headerMatch.Success)
            {
                CloseParagraph();
                CloseList();
                int level = headerMatch.Groups[1].Length;
                string content = ProcessInline(headerMatch.Groups[2].Value);
                html.AppendLine($"<h{level}>{content}</h{level}>");
                continue;
            }

            // Unordered list
            var listMatch = Regex.Match(line, @"^(\s*)[-*+]\s+(.+)$");
            if (listMatch.Success)
            {
                CloseParagraph();
                if (!inList)
                {
                    html.AppendLine("<ul>");
                    inList = true;
                }
                string content = ProcessInline(listMatch.Groups[2].Value);
                html.AppendLine($"<li>{content}</li>");
                continue;
            }

            // Ordered list
            var orderedMatch = Regex.Match(line, @"^(\s*)\d+\.\s+(.+)$");
            if (orderedMatch.Success)
            {
                CloseParagraph();
                if (!inList)
                {
                    html.AppendLine("<ol>");
                    inList = true;
                }
                string content = ProcessInline(orderedMatch.Groups[2].Value);
                html.AppendLine($"<li>{content}</li>");
                continue;
            }

            // Blockquote
            var quoteMatch = Regex.Match(line, @"^>\s?(.*)$");
            if (quoteMatch.Success)
            {
                CloseParagraph();
                CloseList();
                string content = ProcessInline(quoteMatch.Groups[1].Value);
                html.AppendLine($"<blockquote>{content}</blockquote>");
                continue;
            }

            // Regular paragraph
            if (!inParagraph)
            {
                html.Append("<p>");
                inParagraph = true;
            }
            else
            {
                html.Append(' ');
            }
            html.Append(ProcessInline(line));
        }

        CloseParagraph();
        CloseList();
        if (inCodeBlock) html.AppendLine("</code></pre>");

        var result = html.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? string.Empty : result;
    }

    private static string ProcessInline(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Escape HTML first
        text = EscapeHtml(text);

        // Images: ![alt](url)
        text = Regex.Replace(text, @"!\[([^\]]*)\]\(([^)]+)\)", "<img src=\"$2\" alt=\"$1\" />");

        // Links: [text](url)
        text = Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\">$1</a>");

        // Inline code: `code`
        text = Regex.Replace(text, @"`([^`]+)`", "<code>$1</code>");

        // Bold+Italic: ***text*** or ___text___
        text = Regex.Replace(text, @"\*\*\*(.+?)\*\*\*", "<strong><em>$1</em></strong>");
        text = Regex.Replace(text, @"\_\_\_(.+?)\_\_\_", "<strong><em>$1</em></strong>");

        // Bold: **text** or __text__
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        text = Regex.Replace(text, @"\_\_(.+?)\_\_", "<strong>$1</strong>");

        // Italic: *text* or _text_
        text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
        text = Regex.Replace(text, @"\b_(.+?)_\b", "<em>$1</em>");

        // Strikethrough: ~~text~~
        text = Regex.Replace(text, @"~~(.+?)~~", "<del>$1</del>");

        return text;
    }

    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}
