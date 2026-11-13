using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using QuizForge.Core.Abstractions;
using QuizForge.Core.Models;
using QuizForge.Core.Text;

namespace QuizForge.Infrastructure.Web;

/// <summary>
/// Downloads a public web page and extracts its text.
/// Security: http/https only, every connection (including redirects) is checked against
/// <see cref="NetworkGuard"/> at connect time, size and time limits, HTML/text only.
/// </summary>
public sealed class WebPageReader(HttpClient httpClient, IOptions<WebImportOptions> options) : IWebPageReader
{
    public const string HttpClientName = "web-import";

    private static readonly string[] SupportedMediaTypes = ["text/html", "application/xhtml+xml", "text/plain"];

    private readonly WebImportOptions _options = options.Value;

    public async Task<WebPage> ReadAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(url.UserInfo))
        {
            throw new WebImportException(WebImportException.InvalidUrl, "Only http and https addresses are supported.");
        }

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,text/plain;q=0.9");
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is WebImportException blocked)
        {
            throw blocked;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new WebImportException(WebImportException.FetchFailed, "The page could not be downloaded.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new WebImportException(WebImportException.FetchFailed, $"The site answered with status {(int)response.StatusCode}.");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "text/html";
            if (!SupportedMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
            {
                throw new WebImportException(WebImportException.UnsupportedContent, $"Unsupported content type: {mediaType}.");
            }

            if (response.Content.Headers.ContentLength > _options.MaxDownloadBytes)
            {
                throw new WebImportException(WebImportException.TooLarge, "The page is too large.");
            }

            var bytes = await ReadLimitedAsync(response, _options.MaxDownloadBytes, cancellationToken).ConfigureAwait(false);
            var body = GetEncoding(response).GetString(bytes);

            var (title, text) = mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase)
                ? (url.Host, body.Trim())
                : HtmlTextExtractor.Extract(body);

            if (text.Length < QuizRequest.MinTextLength)
            {
                throw new WebImportException(WebImportException.NoText, "The page does not contain enough text.");
            }

            var truncated = text.Length > QuizRequest.MaxTextLength;
            if (truncated)
            {
                text = text[..QuizRequest.MaxTextLength];
            }

            var finalUrl = response.RequestMessage?.RequestUri ?? url;
            return new WebPage(string.IsNullOrWhiteSpace(title) ? finalUrl.Host : title, text, finalUrl, truncated);
        }
    }

    /// <summary>Handler used by the "web-import" HttpClient: validates the IP of every connection.</summary>
    public static SocketsHttpHandler CreateHandler(WebImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            UseProxy = false, // a proxy would hide the real destination from the guard
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = async (context, ct) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct).ConfigureAwait(false);
                var allowed = addresses.Where(a => options.AllowPrivateNetworks || NetworkGuard.IsPublic(a)).ToArray();
                if (allowed.Length == 0)
                {
                    throw new WebImportException(WebImportException.BlockedAddress, "This address points to a private network and cannot be imported.");
                }

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, ct).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, int maxBytes, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new WebImportException(WebImportException.TooLarge, "The page is too large.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static Encoding GetEncoding(HttpResponseMessage response)
    {
        var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"');
        try
        {
            return string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }
}
