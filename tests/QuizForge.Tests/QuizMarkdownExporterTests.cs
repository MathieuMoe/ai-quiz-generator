using QuizForge.Core.Export;
using QuizForge.Core.Parsing;

namespace QuizForge.Tests;

public class QuizMarkdownExporterTests
{
    [Fact]
    public void Exports_questions_options_and_answer_key()
    {
        var md = QuizMarkdownExporter.ToMarkdown(QuizJsonParser.Parse(TestData.ValidQuizJson));

        Assert.StartsWith("# Photosynthesis", md, StringComparison.Ordinal);
        Assert.Contains("## 1. Where does photosynthesis mainly happen?", md, StringComparison.Ordinal);
        Assert.Contains("- **B.** Chloroplasts", md, StringComparison.Ordinal);
        Assert.Contains("## Answer key", md, StringComparison.Ordinal);
        Assert.Contains("1. **B**:", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_key_title_follows_the_quiz_language()
    {
        var md = QuizMarkdownExporter.ToMarkdown(QuizJsonParser.Parse(TestData.ValidQuizJson), QuizForge.Core.Models.QuizLanguage.French);
        Assert.Contains("## Corrigé", md, StringComparison.Ordinal);
    }
}
