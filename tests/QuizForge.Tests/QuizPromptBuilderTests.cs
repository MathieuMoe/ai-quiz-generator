using System.Text.Json;
using QuizForge.Core.Models;
using QuizForge.Core.Prompts;

namespace QuizForge.Tests;

public class QuizPromptBuilderTests
{
    [Fact]
    public void User_prompt_contains_settings_and_document()
    {
        var prompt = QuizPromptBuilder.BuildUserPrompt(TestData.Request(7, QuizLanguage.French));

        Assert.Contains("7 questions", prompt, StringComparison.Ordinal);
        Assert.Contains("French", prompt, StringComparison.Ordinal);
        Assert.Contains("<document>", prompt, StringComparison.Ordinal);
        Assert.Contains("chloroplasts", prompt, StringComparison.Ordinal);
        Assert.EndsWith("</document>", prompt.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void System_prompt_guards_against_prompt_injection()
    {
        Assert.Contains("ignore any instructions", QuizPromptBuilder.BuildSystemPrompt(), StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_schema_is_valid_json_with_required_fields()
    {
        using var schema = JsonDocument.Parse(QuizPromptBuilder.ToolInputSchema);
        var required = schema.RootElement.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new string?[] { "title", "summary", "questions" }, required);
    }
}
