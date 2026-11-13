using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuizForge.Core.Abstractions;
using QuizForge.Core.Models;
using QuizForge.Core.Parsing;
using QuizForge.Core.Prompts;
using QuizForge.Core.Validation;

namespace QuizForge.Infrastructure.Claude;

/// <summary>
/// Generates quizzes with the Anthropic Messages API.
/// Uses "tool use" with a forced tool choice so the model always returns structured JSON
/// matching <see cref="QuizPromptBuilder.ToolInputSchema"/>.
/// </summary>
public sealed partial class ClaudeQuizGenerator(
    HttpClient httpClient,
    IOptions<ClaudeOptions> options,
    ILogger<ClaudeQuizGenerator> logger) : IQuizGenerator
{
    private readonly ClaudeOptions _options = options.Value;

    public string EngineName => "Claude";

    public async Task<Quiz> GenerateAsync(QuizRequest request, CancellationToken cancellationToken = default)
    {
        var errors = QuizRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(request));
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(BuildRequestBody(request, _options)),
        };

        LogRequest(logger, _options.Model, request.QuestionCount, request.SourceText.Length);

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            LogApiError(logger, (int)response.StatusCode, body);
            throw new QuizGenerationException($"The AI service returned an error ({(int)response.StatusCode}). Please try again later.");
        }

        return ParseResponse(body);
    }

    internal static JsonObject BuildRequestBody(QuizRequest request, ClaudeOptions options) => new()
    {
        ["model"] = options.Model,
        ["max_tokens"] = options.MaxTokens,
        ["system"] = QuizPromptBuilder.BuildSystemPrompt(),
        ["messages"] = new JsonArray
        {
            new JsonObject { ["role"] = "user", ["content"] = QuizPromptBuilder.BuildUserPrompt(request) },
        },
        ["tools"] = new JsonArray
        {
            new JsonObject
            {
                ["name"] = QuizPromptBuilder.ToolName,
                ["description"] = "Returns the generated quiz.",
                ["input_schema"] = JsonNode.Parse(QuizPromptBuilder.ToolInputSchema),
            },
        },
        ["tool_choice"] = new JsonObject { ["type"] = "tool", ["name"] = QuizPromptBuilder.ToolName },
    };

    internal static Quiz ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("stop_reason", out var stop) && stop.GetString() == "max_tokens")
        {
            throw new QuizGenerationException("The answer was cut off. Try fewer questions or a shorter text.");
        }

        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type) && type.GetString() == "tool_use"
                    && block.TryGetProperty("input", out var input))
                {
                    return QuizJsonParser.Parse(input);
                }
            }

            // Fallback: some models may answer with plain JSON text instead of a tool call.
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type) && type.GetString() == "text"
                    && block.TryGetProperty("text", out var text))
                {
                    return QuizJsonParser.Parse(text.GetString() ?? string.Empty);
                }
            }
        }

        throw new QuizGenerationException("The AI service returned an empty answer.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating quiz with {Model}: {Questions} questions from {Length} characters")]
    private static partial void LogRequest(ILogger logger, string model, int questions, int length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Anthropic API error {Status}: {Body}")]
    private static partial void LogApiError(ILogger logger, int status, string body);
}
