using QuizForge.Core.Abstractions;
using QuizForge.Core.Models;
using QuizForge.Infrastructure.Demo;

namespace QuizForge.Tests;

public class DemoQuizGeneratorTests
{
    private readonly DemoQuizGenerator _generator = new();

    [Fact]
    public async Task Generates_the_requested_number_of_valid_questions()
    {
        var quiz = await _generator.GenerateAsync(TestData.Request(3));

        Assert.Equal(3, quiz.Questions.Count);
        Assert.All(quiz.Questions, q =>
        {
            Assert.Equal(4, q.Options.Count);
            Assert.InRange(q.CorrectIndex, 0, 3);
            Assert.Contains("_____", q.Prompt, StringComparison.Ordinal);
            Assert.Equal(4, q.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        });
    }

    [Fact]
    public async Task Is_deterministic()
    {
        var first = await _generator.GenerateAsync(TestData.Request(3));
        var second = await _generator.GenerateAsync(TestData.Request(3));

        Assert.Equal(first.Questions.Select(q => q.Prompt), second.Questions.Select(q => q.Prompt));
    }

    [Fact]
    public async Task Uses_the_requested_language()
    {
        var quiz = await _generator.GenerateAsync(TestData.Request(1, QuizLanguage.Spanish));
        Assert.StartsWith("Completa la frase", quiz.Questions[0].Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ignores_titles_without_final_punctuation()
    {
        var text = "Plant biology for beginners and curious students\n" + TestData.Photosynthesis;
        var quiz = await _generator.GenerateAsync(new QuizRequest(text, 5));

        Assert.All(quiz.Questions, q => Assert.DoesNotContain("beginners", q.Prompt, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rejects_invalid_requests()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _generator.GenerateAsync(new QuizRequest("short")));
    }

    [Fact]
    public async Task Throws_when_text_has_no_usable_sentences()
    {
        var text = string.Join(" ", Enumerable.Repeat("Hi.", 100));
        await Assert.ThrowsAsync<QuizGenerationException>(() => _generator.GenerateAsync(new QuizRequest(text)));
    }
}
