using System.Text.Json;
using QuizForge.Core.Abstractions;
using QuizForge.Core.Models;

namespace QuizForge.Core.Parsing;

/// <summary>
/// Converts the JSON produced by the model into a validated <see cref="Quiz"/>.
/// Language models are not perfectly reliable, so every field is checked.
/// </summary>
public static class QuizJsonParser
{
    public static Quiz Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var doc = JsonDocument.Parse(StripCodeFences(json));
            return Parse(doc.RootElement);
        }
        catch (JsonException ex)
        {
            throw new QuizGenerationException("The model returned invalid JSON.", ex);
        }
    }

    public static Quiz Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new QuizGenerationException("The quiz must be a JSON object.");
        }

        var title = RequiredString(root, "title");
        var summary = RequiredString(root, "summary");

        if (!root.TryGetProperty("questions", out var questionsEl) || questionsEl.ValueKind != JsonValueKind.Array)
        {
            throw new QuizGenerationException("The quiz has no 'questions' array.");
        }

        var questions = new List<Question>();
        var number = 0;
        foreach (var q in questionsEl.EnumerateArray())
        {
            number++;
            questions.Add(ParseQuestion(q, number));
        }

        if (questions.Count == 0)
        {
            throw new QuizGenerationException("The quiz contains no questions.");
        }

        return new Quiz(title, summary, questions);
    }

    private static Question ParseQuestion(JsonElement q, int number)
    {
        if (q.ValueKind != JsonValueKind.Object)
        {
            throw new QuizGenerationException($"Question {number} is not an object.");
        }

        var prompt = RequiredString(q, "prompt", number);
        var explanation = RequiredString(q, "explanation", number);

        if (!q.TryGetProperty("options", out var optionsEl) || optionsEl.ValueKind != JsonValueKind.Array)
        {
            throw new QuizGenerationException($"Question {number} has no options.");
        }

        var options = optionsEl.EnumerateArray()
            .Select(o => o.ValueKind == JsonValueKind.String ? o.GetString()!.Trim() : string.Empty)
            .ToList();

        if (options.Count is < 2 or > 6 || options.Any(string.IsNullOrWhiteSpace))
        {
            throw new QuizGenerationException($"Question {number} must have between 2 and 6 non-empty options.");
        }

        if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
        {
            throw new QuizGenerationException($"Question {number} has duplicate options.");
        }

        if (!q.TryGetProperty("correctIndex", out var idxEl) || !idxEl.TryGetInt32(out var correctIndex)
            || correctIndex < 0 || correctIndex >= options.Count)
        {
            throw new QuizGenerationException($"Question {number} has an invalid 'correctIndex'.");
        }

        return new Question(prompt, options, correctIndex, explanation);
    }

    private static string RequiredString(JsonElement el, string name, int? question = null)
    {
        if (el.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!.Trim();
        }

        var where = question is null ? "The quiz" : $"Question {question}";
        throw new QuizGenerationException($"{where} is missing '{name}'.");
    }

    /// <summary>Removes ```json fences that models sometimes add around JSON.</summary>
    internal static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewLine = trimmed.IndexOf('\n', StringComparison.Ordinal);
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewLine >= 0 && lastFence > firstNewLine
            ? trimmed[(firstNewLine + 1)..lastFence].Trim()
            : trimmed;
    }
}
