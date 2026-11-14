using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using QuizForge.Core.Abstractions;
using QuizForge.Core.Models;
using QuizForge.Core.Parsing;
using QuizForge.Core.Prompts;
using QuizForge.Core.Validation;

namespace QuizForge.Infrastructure.OpenAi;

/// <summary>
/// Generates quizzes with any OpenAI-compatible Chat Completions API (ChatGPT, Gemini, Ollama).
/// Uses function calling with the same JSON schema as the Claude generator, so the output is always structured.
/// </summary>
public sealed partial class OpenAiCompatibleQuizGenerator(
    HttpClient httpClient,
    OpenAiCompatibleOptions options,
    string engineName,
    ILogger<OpenAiCompatibleQuizGenerator> logger) : IQuizGenerator
{
    public string EngineName => engineName;

    public async Task<Quiz> GenerateAsync(QuizRequest request, CancellationToken cancellationToken = default)
    {
        var errors = QuizRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(request));
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            // Buffered body with Content-Length: some OpenAI-compatible servers and proxies reject chunked requests.
            Content = new StringContent(BuildRequestBody(request, options).ToJsonString(), Encoding.UTF8, "application/json"),
        };

        LogRequest(logger, engineName, options.Model, request.QuestionCount, request.SourceText.Length);

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            LogApiError(logger, engineName, (int)response.StatusCode, body);
            throw new QuizGenerationException($"The AI service returned an error ({(int)response.StatusCode}). Please try again later.");
        }

        return ParseResponse(body);
    }

    internal static JsonObject BuildRequestBody(QuizRequest request, OpenAiCompatibleOptions options) => new()
    {
        ["model"] = options.Model,
        ["messages"] = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = QuizPromptBuilder.BuildSystemPrompt() },
            new JsonObject { ["role"] = "user", ["content"] = QuizPromptBuilder.BuildUserPrompt(request) },
        },
        ["tools"] = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = QuizPromptBuilder.ToolName,
                    ["description"] = "Returns the generated quiz.",
                    ["parameters"] = JsonNode.Parse(QuizPromptBuilder.ToolInputSchema),
                },
            },
        },
        // "required" forces a tool call and is understood by OpenAI, Gemini and Ollama.
        ["tool_choice"] = "required",
    };

    internal static Quiz ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            throw new QuizGenerationException("The AI service returned an empty answer.");
        }

        var choice = choices[0];
        if (choice.TryGetProperty("finish_reason", out var finish) && finish.GetString() == "length")
        {
            throw new QuizGenerationException("The answer was cut off. Try fewer questions or a shorter text.");
        }

        if (!choice.TryGetProperty("message", out var message))
        {
            throw new QuizGenerationException("The AI service returned an empty answer.");
        }

        if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in calls.EnumerateArray())
            {
                if (call.TryGetProperty("function", out var function) && function.TryGetProperty("arguments", out var arguments))
                {
                    // OpenAI and Gemini send the arguments as a JSON string; some local servers send an object.
                    return arguments.ValueKind == JsonValueKind.String
                        ? QuizJsonParser.Parse(arguments.GetString() ?? string.Empty)
                        : QuizJsonParser.Parse(arguments);
                }
            }
        }

        // Fallback: models without function calling may answer with plain JSON text.
        if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(content.GetString()))
        {
            return QuizJsonParser.Parse(content.GetString()!);
        }

        throw new QuizGenerationException("The AI service returned an empty answer.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating quiz with {Engine} ({Model}): {Questions} questions from {Length} characters")]
    private static partial void LogRequest(ILogger logger, string engine, string model, int questions, int length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Engine} API error {Status}: {Body}")]
    private static partial void LogApiError(ILogger logger, string engine, int status, string body);
}
