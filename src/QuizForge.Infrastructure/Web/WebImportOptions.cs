namespace QuizForge.Infrastructure.Web;

public sealed class WebImportOptions
{
    public const string SectionName = "WebImport";

    /// <summary>
    /// Allows importing pages from localhost and private networks. Keep it false in production:
    /// otherwise anyone could use the server to read internal services (SSRF).
    /// </summary>
    public bool AllowPrivateNetworks { get; set; }

    public int MaxDownloadBytes { get; set; } = 3_000_000;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
}
