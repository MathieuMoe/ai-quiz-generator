using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using QuizForge.Core.Abstractions;
using QuizForge.Core.Models;
using QuizForge.Core.Parsing;
using QuizForge.Core.Prompts;
using QuizForge.Core.Validation;
using QuizForge.Infrastructure.OpenAi;

namespace QuizForge.Infrastructure.Gemini;

/// <summary>
/// Generates quizzes with Google Gemini's native API (generateContent + forced function calling).
/// The native API accepts the new "AQ." keys through the x-goog-api-key header.
/// </summary>
public sealed partial class GeminiQuizGenerator(
    HttpClient httpClient,
    OpenAiCompatibleOptions options,
    ILogger<GeminiQuizGenerator> logger) : IQuizGenerator
{
    public string EngineName => "Gemini";

    public async Task<Quiz> GenerateAsync(QuizRequest request, CancellationToken cancellationToken = default)
    {
        var errors = QuizRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(request));
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, $"models/{Uri.EscapeDataString(options.Model)}:generateContent")
        {
            Content = new StringContent(BuildRequestBody(request).ToJsonString(), Encoding.UTF8, "application/json"),
        };

        LogRequest(logger, options.Model, request.QuestionCount, request.SourceText.Length);

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            LogApiError(logger, (int)response.StatusCode, body);
            throw new QuizGenerationException($"The AI service returned an error ({(int)response.StatusCode}). Please try again later.");
        }

        return ParseResponse(body);
    }

    internal static JsonObject BuildRequestBody(QuizRequest request) => new()
    {
        ["systemInstruction"] = new JsonObject
        {
            ["parts"] = new JsonArray { new JsonObject { ["text"] = QuizPromptBuilder.BuildSystemPrompt() } },
        },
        ["contents"] = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray { new JsonObject { ["text"] = QuizPromptBuilder.BuildUserPrompt(request) } },
            },
        },
        ["tools"] = new JsonArray
        {
            new JsonObject
            {
                ["functionDeclarations"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = QuizPromptBuilder.ToolName,
                        ["description"] = "Returns the generated quiz.",
                        ["parameters"] = JsonNode.Parse(QuizPromptBuilder.ToolInputSchema),
                    },
                },
            },
        },
        // Forces the model to call create_quiz, so the answer is always structured JSON.
        ["toolConfig"] = new JsonObject
        {
            ["functionCallingConfig"] = new JsonObject
            {
                ["mode"] = "ANY",
                ["allowedFunctionNames"] = new JsonArray { QuizPromptBuilder.ToolName },
            },
        },
    };

    internal static Quiz ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0)
        {
            throw new QuizGenerationException("The AI service returned an empty answer.");
        }

        var candidate = candidates[0];
        if (candidate.TryGetProperty("finishReason", out var finish) && finish.GetString() == "MAX_TOKENS")
        {
            throw new QuizGenerationException("The answer was cut off. Try fewer questions or a shorter text.");
        }

        if (candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("functionCall", out var call) && call.TryGetProperty("args", out var args))
                {
                    return QuizJsonParser.Parse(args);
                }
            }

            // Fallback: plain JSON text instead of a function call.
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(text.GetString()))
                {
                    return QuizJsonParser.Parse(text.GetString()!);
                }
            }
        }

        throw new QuizGenerationException("The AI service returned an empty answer.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating quiz with Gemini ({Model}): {Questions} questions from {Length} characters")]
    private static partial void LogRequest(ILogger logger, string model, int questions, int length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gemini API error {Status}: {Body}")]
    private static partial void LogApiError(ILogger logger, int status, string body);
}
