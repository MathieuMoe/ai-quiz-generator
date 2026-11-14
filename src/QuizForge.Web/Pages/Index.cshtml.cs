using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using QuizForge.Core.Abstractions;
using QuizForge.Core.Export;
using QuizForge.Core.Models;
using QuizForge.Core.Validation;
using QuizForge.Infrastructure.Engines;

namespace QuizForge.Web.Pages;

[EnableRateLimiting("generate")]
public sealed partial class IndexModel(
    QuizEngines engines,
    IStringLocalizer<SharedResource> localizer,
    ILogger<IndexModel> logger) : PageModel
{
    private const long MaxUploadBytes = 1_000_000;
    private static readonly string[] AllowedExtensions = [".txt", ".md"];

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Quiz? Quiz { get; private set; }

    public string? QuizJson { get; private set; }

    public string? QuizMarkdown { get; private set; }

    public string? ErrorMessage { get; private set; }

    /// <summary>The engine chosen in the header selector (or the default one).</summary>
    public QuizEngine Engine => EngineSelection.Current(HttpContext, engines);

    public void OnGet()
    {
        Input.Language = Localization.QuizLanguageForCurrentCulture();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Input.File is { Length: > 0 } file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension == ".pdf")
            {
                // PDFs are converted to text in the browser (pdf.js); this only happens with JavaScript disabled.
                ModelState.AddModelError("Input.File", localizer["Error_PdfNeedsJs"]);
                return Page();
            }

            if (!AllowedExtensions.Contains(extension) || file.Length > MaxUploadBytes)
            {
                ModelState.AddModelError("Input.File", localizer["Error_FileType"]);
                return Page();
            }

            using var reader = new StreamReader(file.OpenReadStream());
            Input.Text = await reader.ReadToEndAsync(cancellationToken);
            ModelState.Remove("Input.Text");
        }

        var request = new QuizRequest(Input.Text ?? string.Empty, Input.QuestionCount, Input.Difficulty, Input.Language);
        foreach (var error in QuizRequestValidator.Check(request))
        {
            ModelState.AddModelError(string.Empty, localizer[$"Validation_{error.Code}", error.Arguments]);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            Quiz = await Engine.Create().GenerateAsync(request, cancellationToken);
            QuizJson = JsonSerializer.Serialize(Quiz, JsonOptions);
            QuizMarkdown = QuizMarkdownExporter.ToMarkdown(Quiz, request.Language);
        }
        catch (QuizGenerationException ex)
        {
            LogGenerationFailed(logger, ex);
            ErrorMessage = ex.Message;
        }

        return Page();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Quiz generation failed")]
    private static partial void LogGenerationFailed(ILogger logger, Exception ex);

    public sealed class InputModel
    {
        public string? Text { get; set; }

        public IFormFile? File { get; set; }

        public int QuestionCount { get; set; } = 5;

        public Difficulty Difficulty { get; set; } = Difficulty.Medium;

        public QuizLanguage Language { get; set; } = QuizLanguage.English;
    }
}
