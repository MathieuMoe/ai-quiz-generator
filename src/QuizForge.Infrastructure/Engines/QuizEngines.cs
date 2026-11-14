using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuizForge.Core.Abstractions;
using QuizForge.Infrastructure.Claude;
using QuizForge.Infrastructure.Demo;
using QuizForge.Infrastructure.Gemini;
using QuizForge.Infrastructure.OpenAi;

namespace QuizForge.Infrastructure.Engines;

/// <summary>An AI engine the user can choose in the interface.</summary>
public sealed record QuizEngine(string Id, string Name, bool IsDemo, Func<IQuizGenerator> Create);

/// <summary>
/// The AI engines available with the current configuration (each provider appears when its key is set),
/// plus the offline demo, which is always available. The interface lets the user pick one.
/// </summary>
public sealed class QuizEngines
{
    public const string ClaudeId = "claude";
    public const string OpenAiId = "openai";
    public const string GeminiId = "gemini";
    public const string OllamaId = "ollama";
    public const string DemoId = "demo";

    private readonly Dictionary<string, QuizEngine> _byId;

    public QuizEngines(
        IHttpClientFactory httpClientFactory,
        IOptions<ClaudeOptions> claude,
        IOptionsMonitor<OpenAiCompatibleOptions> openAi,
        IOptions<AiOptions> ai,
        DemoQuizGenerator demo,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(claude);
        ArgumentNullException.ThrowIfNull(openAi);
        ArgumentNullException.ThrowIfNull(ai);
        loggerFactory ??= NullLoggerFactory.Instance;
        var engines = new List<QuizEngine>();

        if (!string.IsNullOrWhiteSpace(claude.Value.ApiKey))
        {
            engines.Add(new(ClaudeId, "Claude", false, () =>
                new ClaudeQuizGenerator(httpClientFactory.CreateClient(ClaudeId), claude, loggerFactory.CreateLogger<ClaudeQuizGenerator>())));
        }

        foreach (var (id, name, section) in new[]
        {
            (OpenAiId, "ChatGPT", OpenAiCompatibleOptions.OpenAi),
            (GeminiId, "Gemini", OpenAiCompatibleOptions.Gemini),
            (OllamaId, "Ollama (local)", OpenAiCompatibleOptions.Ollama),
        })
        {
            var options = openAi.Get(section);
            if (options.IsConfigured)
            {
                engines.Add(new(id, name, false, id == GeminiId
                    ? () => new GeminiQuizGenerator(httpClientFactory.CreateClient(id), options, loggerFactory.CreateLogger<GeminiQuizGenerator>())
                    : () => new OpenAiCompatibleQuizGenerator(httpClientFactory.CreateClient(id), options, name, loggerFactory.CreateLogger<OpenAiCompatibleQuizGenerator>())));
            }
        }

        engines.Add(new(DemoId, demo.EngineName, true, () => demo));

        All = engines;
        _byId = engines.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
        Default = (ai.Value.Default is { } preferred && _byId.TryGetValue(preferred, out var chosen)) ? chosen : engines[0];
    }

    public IReadOnlyList<QuizEngine> All { get; }

    public QuizEngine Default { get; }

    /// <summary>The engine with this id, or the default one when the id is unknown or not configured.</summary>
    public QuizEngine Get(string? id) => id is not null && _byId.TryGetValue(id, out var engine) ? engine : Default;
}

/// <summary>"Ai" section: which engine is selected by default (claude, openai, gemini, ollama or demo).</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string? Default { get; set; }
}
