using QuizForge.Core.Text;

namespace QuizForge.Tests;

public class HtmlTextExtractorTests
{
    private const string Page =
        """
        <!DOCTYPE html>
        <html><head><title>The Water Cycle &amp; Climate</title><style>body{color:red}</style></head>
        <body>
          <header><nav><a href="/">Home</a><a href="/about">About us</a></nav></header>
          <main>
            <article>
              <h1>The water cycle</h1>
              <p>Water <b>evaporates</b> from oceans&nbsp;and lakes.</p>
              <ul><li>Evaporation</li><li>Condensation</li></ul>
              <script>trackUser();</script>
            </article>
          </main>
          <footer>© 2026 Example – Privacy policy</footer>
        </body></html>
        """;

    [Fact]
    public void Extracts_the_title_and_decodes_entities()
    {
        var (title, _) = HtmlTextExtractor.Extract(Page);
        Assert.Equal("The Water Cycle & Climate", title);
    }

    [Fact]
    public void Keeps_the_article_and_drops_menus_scripts_and_footer()
    {
        var (_, text) = HtmlTextExtractor.Extract(Page);

        Assert.Contains("The water cycle", text, StringComparison.Ordinal);
        Assert.Contains("Water evaporates from oceans and lakes.", text, StringComparison.Ordinal);
        Assert.Contains("- Evaporation", text, StringComparison.Ordinal);
        Assert.DoesNotContain("About us", text, StringComparison.Ordinal);
        Assert.DoesNotContain("trackUser", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Privacy", text, StringComparison.Ordinal);
        Assert.DoesNotContain("color:red", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Works_without_article_or_main()
    {
        var (_, text) = HtmlTextExtractor.Extract("<html><body><p>First paragraph.</p><p>Second paragraph.</p></body></html>");
        Assert.Equal("First paragraph.\nSecond paragraph.", text);
    }
}
