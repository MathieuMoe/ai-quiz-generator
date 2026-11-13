namespace QuizForge.Core.Abstractions;

/// <summary>Downloads a public web page and returns its readable text.</summary>
public interface IWebPageReader
{
    Task<WebPage> ReadAsync(Uri url, CancellationToken cancellationToken = default);
}

public sealed record WebPage(string Title, string Text, Uri Url, bool Truncated);

/// <summary>Why a web page could not be imported. <see cref="Code"/> is stable and can be translated by the UI.</summary>
public sealed class WebImportException : Exception
{
    public const string InvalidUrl = "invalid_url";
    public const string BlockedAddress = "blocked_address";
    public const string FetchFailed = "fetch_failed";
    public const string UnsupportedContent = "unsupported_content";
    public const string TooLarge = "too_large";
    public const string NoText = "no_text";

    public WebImportException() : this(FetchFailed, "The page could not be imported.") { }

    public WebImportException(string message) : this(FetchFailed, message) { }

    public WebImportException(string message, Exception innerException) : base(message, innerException) => Code = FetchFailed;

    public WebImportException(string code, string message, Exception? innerException = null) : base(message, innerException) => Code = code;

    public string Code { get; }
}
