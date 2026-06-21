using System.Text;
using System.Text.RegularExpressions;

namespace CodeDictionary.Services;

public static class MarkdownConverter
{
    private static readonly Regex HrRegex = new(@"^-{3,}$|^\*{3,}$|^_{3,}$", RegexOptions.Compiled);
    private static readonly Regex HeaderRegex = new(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^(\s*)[-*+]\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^(\s*)\d+\.\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex BlockquoteRegex = new(@"^>\s?(.*)$", RegexOptions.Compiled);
    private static readonly Regex ImageRegex = new(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex LinkRegex = new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex InlineCodeRegex = new(@"`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex BoldItalicStarRegex = new(@"\*\*\*(.+?)\*\*\*", RegexOptions.Compiled);
    private static readonly Regex BoldItalicUnderscoreRegex = new(@"___(.+?)___", RegexOptions.Compiled);
    private static readonly Regex BoldStarRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex BoldUnderscoreRegex = new(@"__(.+?)__", RegexOptions.Compiled);
    private static readonly Regex ItalicStarRegex = new(@"\*(.+?)\*", RegexOptions.Compiled);
    private static readonly Regex ItalicUnderscoreRegex = new(@"\b_(.+?)_\b", RegexOptions.Compiled);
    private static readonly Regex StrikethroughRegex = new(@"~~(.+?)~~", RegexOptions.Compiled);

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
            if (HrRegex.IsMatch(line))
            {
                CloseParagraph();
                CloseList();
                html.AppendLine("<hr />");
                continue;
            }

            // Headers
            var headerMatch = HeaderRegex.Match(line);
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
            var listMatch = UnorderedListRegex.Match(line);
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
            var orderedMatch = OrderedListRegex.Match(line);
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
            var quoteMatch = BlockquoteRegex.Match(line);
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
        text = ImageRegex.Replace(text, "<img src=\"$2\" alt=\"$1\" />");

        // Links: [text](url)
        text = LinkRegex.Replace(text, "<a href=\"$2\">$1</a>");

        // Inline code: `code`
        text = InlineCodeRegex.Replace(text, "<code>$1</code>");

        // Bold+Italic: ***text*** or ___text___
        text = BoldItalicStarRegex.Replace(text, "<strong><em>$1</em></strong>");
        text = BoldItalicUnderscoreRegex.Replace(text, "<strong><em>$1</em></strong>");

        // Bold: **text** or __text__
        text = BoldStarRegex.Replace(text, "<strong>$1</strong>");
        text = BoldUnderscoreRegex.Replace(text, "<strong>$1</strong>");

        // Italic: *text* or _text_
        text = ItalicStarRegex.Replace(text, "<em>$1</em>");
        text = ItalicUnderscoreRegex.Replace(text, "<em>$1</em>");

        // Strikethrough: ~~text~~
        text = StrikethroughRegex.Replace(text, "<del>$1</del>");

        return text;
    }

    public static string ToMarkdown(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = html
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("</h1>", "\n")
            .Replace("</h2>", "\n")
            .Replace("</h3>", "\n")
            .Replace("</h4>", "\n")
            .Replace("</h5>", "\n")
            .Replace("</h6>", "\n")
            .Replace("</p>", "\n")
            .Replace("</li>", "\n")
            .Replace("</blockquote>", "\n")
            .Replace("</div>", "\n")
            .Replace("<br />", "\n")
            .Replace("<br/>", "\n")
            .Replace("<br>", "\n")
            .Replace("</pre>", "\n");

        text = Regex.Replace(text, @"<h1[^>]*>(.*?)</h1>", "# $1", RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h2[^>]*>(.*?)</h2>", "## $1", RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h3[^>]*>(.*?)</h3>", "### $1", RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h4[^>]*>(.*?)</h4>", "#### $1", RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h5[^>]*>(.*?)</h5>", "##### $1", RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h6[^>]*>(.*?)</h6>", "###### $1", RegexOptions.Singleline);
        text = Regex.Replace(text, @"<hr[^>]*>", "\n---\n");
        text = Regex.Replace(text, @"<blockquote[^>]*>(.*?)</blockquote>", "> $1", RegexOptions.Singleline);
        text = Regex.Replace(text, @"<img[^>]*src=""([^""]*)""[^>]*alt=""([^""]*)""[^>]*>", "![$2]($1)");
        text = Regex.Replace(text, @"<img[^>]*alt=""([^""]*)""[^>]*src=""([^""]*)""[^>]*>", "![$1]($2)");
        text = Regex.Replace(text, @"<a[^>]*href=""([^""]*)""[^>]*>(.*?)</a>", "[$2]($1)");
        text = Regex.Replace(text, @"<pre><code[^>]*>(.*?)</code></pre>", "```\n$1\n```", RegexOptions.Singleline);
        text = Regex.Replace(text, @"<pre>(.*?)</pre>", "```\n$1\n```", RegexOptions.Singleline);
        text = Regex.Replace(text, @"<code[^>]*>(.*?)</code>", "`$1`");
        text = Regex.Replace(text, @"<strong><em>(.*?)</em></strong>", "***$1***");
        text = Regex.Replace(text, @"<em><strong>(.*?)</strong></em>", "***$1***");
        text = Regex.Replace(text, @"<strong>(.*?)</strong>", "**$1**");
        text = Regex.Replace(text, @"<b>(.*?)</b>", "**$1**");
        text = Regex.Replace(text, @"<em>(.*?)</em>", "*$1*");
        text = Regex.Replace(text, @"<i>(.*?)</i>", "*$1*");
        text = Regex.Replace(text, @"<del>(.*?)</del>", "~~$1~~");
        text = Regex.Replace(text, @"<s>(.*?)</s>", "~~$1~~");
        text = Regex.Replace(text, @"<strike>(.*?)</strike>", "~~$1~~");
        text = Regex.Replace(text, @"</ul>", "");
        text = Regex.Replace(text, @"</ol>", "");
        text = Regex.Replace(text, @"<ul[^>]*>", "");
        text = Regex.Replace(text, @"<ol[^>]*>", "");
        text = Regex.Replace(text, @"<li[^>]*>(.*?)</li>", "- $1");
        text = Regex.Replace(text, @"<[^>]+>", "");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        text = Regex.Replace(text, @"^\n+", "", RegexOptions.Multiline);

        return text.Trim();
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
