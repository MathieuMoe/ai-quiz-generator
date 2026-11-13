using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using QuizForge.Core.Abstractions;
using QuizForge.Infrastructure.Web;

namespace QuizForge.Tests;

public class WebPageReaderTests
{
    private static readonly string LongArticle =
        "<html><head><title>Rome</title></head><body><article><p>" +
        string.Join(" ", Enumerable.Repeat("The Roman Republic was founded in 509 BC.", 10)) +
        "</p></article></body></html>";

    private static WebPageReader Create(HttpStatusCode status, string body, string contentType = "text/html", int maxBytes = 3_000_000)
    {
        var handler = new StubHandler(status, body, contentType);
        return new WebPageReader(new HttpClient(handler), Options.Create(new WebImportOptions { MaxDownloadBytes = maxBytes }));
    }

    [Fact]
    public async Task Reads_title_and_text_of_an_html_page()
    {
        var page = await Create(HttpStatusCode.OK, LongArticle).ReadAsync(new Uri("https://example.com/rome"));

        Assert.Equal("Rome", page.Title);
        Assert.StartsWith("The Roman Republic", page.Text, StringComparison.Ordinal);
        Assert.False(page.Truncated);
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:pass@example.com/")]
    public async Task Rejects_non_http_urls(string url)
    {
        var ex = await Assert.ThrowsAsync<WebImportException>(() => Create(HttpStatusCode.OK, LongArticle).ReadAsync(new Uri(url)));
        Assert.Equal(WebImportException.InvalidUrl, ex.Code);
    }

    [Fact]
    public async Task Rejects_non_html_content()
    {
        var ex = await Assert.ThrowsAsync<WebImportException>(() =>
            Create(HttpStatusCode.OK, "%PDF-1.7", "application/pdf").ReadAsync(new Uri("https://example.com/doc.pdf")));
        Assert.Equal(WebImportException.UnsupportedContent, ex.Code);
    }

    [Fact]
    public async Task Rejects_pages_that_are_too_large()
    {
        var ex = await Assert.ThrowsAsync<WebImportException>(() =>
            Create(HttpStatusCode.OK, LongArticle, maxBytes: 100).ReadAsync(new Uri("https://example.com/")));
        Assert.Equal(WebImportException.TooLarge, ex.Code);
    }

    [Fact]
    public async Task Rejects_pages_without_enough_text()
    {
        var ex = await Assert.ThrowsAsync<WebImportException>(() =>
            Create(HttpStatusCode.OK, "<html><body><p>Hi</p></body></html>").ReadAsync(new Uri("https://example.com/")));
        Assert.Equal(WebImportException.NoText, ex.Code);
    }

    [Fact]
    public async Task Reports_http_errors()
    {
        var ex = await Assert.ThrowsAsync<WebImportException>(() =>
            Create(HttpStatusCode.NotFound, "").ReadAsync(new Uri("https://example.com/missing")));
        Assert.Equal(WebImportException.FetchFailed, ex.Code);
    }

    private sealed class StubHandler(HttpStatusCode status, string body, string contentType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                RequestMessage = request,
                Content = new StringContent(body, Encoding.UTF8, contentType),
            });
    }
}
