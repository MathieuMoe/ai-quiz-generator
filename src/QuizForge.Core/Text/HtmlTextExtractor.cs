using System.Net;
using System.Text.RegularExpressions;

namespace QuizForge.Core.Text;

/// <summary>
/// Extracts the readable text of a web page: keeps the main content (article / main / body),
/// drops scripts, menus, headers, footers and forms, and keeps paragraph breaks.
/// Dependency-free on purpose; good enough for articles, lessons and documentation pages.
/// </summary>
public static partial class HtmlTextExtractor
{
    public static (string Title, string Text) Extract(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var title = Clean(TitleTag().Match(html).Groups["t"].Value);

        var content = Noise().Replace(html, " ");
        content = FirstBlock(content, "article") ?? FirstBlock(content, "main") ?? FirstBlock(content, "body") ?? content;
        content = Chrome().Replace(content, " ");

        // List items get a dash, block elements become line breaks, every other tag disappears.
        content = ListItem().Replace(content, "\n- ");
        content = BlockTag().Replace(content, "\n");
        content = AnyTag().Replace(content, " ");
        content = WebUtility.HtmlDecode(content);

        var lines = content.Split('\n')
            .Select(l => Spaces().Replace(l, " ").Trim())
            .Where(l => l.Length > 0 && l != "-");

        var text = string.Join("\n", lines);
        return (title, text);
    }

    private static string? FirstBlock(string html, string tag)
    {
        var match = Regex.Match(html, $@"<{tag}\b[^>]*>(?<c>.*)</{tag}\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
        return match.Success && match.Groups["c"].Value.Trim().Length > 0 ? match.Groups["c"].Value : null;
    }

    private static string Clean(string value) => Spaces().Replace(WebUtility.HtmlDecode(value), " ").Trim();

    [GeneratedRegex(@"<title[^>]*>(?<t>.*?)</title\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, 2000)]
    private static partial Regex TitleTag();

    [GeneratedRegex(@"<!--.*?-->|<(script|style|noscript|svg|template|iframe|head|canvas|select)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, 2000)]
    private static partial Regex Noise();

    [GeneratedRegex(@"<(nav|header|footer|aside|form|button|dialog)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, 2000)]
    private static partial Regex Chrome();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase, 2000)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"<br\s*/?>|</?(p|div|section|article|main|h[1-6]|ul|ol|tr|table|blockquote|pre|dd|dt|figcaption)\b[^>]*>", RegexOptions.IgnoreCase, 2000)]
    private static partial Regex BlockTag();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.None, 2000)]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"[ \t\r\f\v ]+", RegexOptions.None, 2000)]
    private static partial Regex Spaces();
}
