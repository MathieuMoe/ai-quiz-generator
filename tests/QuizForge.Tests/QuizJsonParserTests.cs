using QuizForge.Core.Abstractions;
using QuizForge.Core.Parsing;

namespace QuizForge.Tests;

public class QuizJsonParserTests
{
    [Fact]
    public void Parses_a_valid_quiz()
    {
        var quiz = QuizJsonParser.Parse(TestData.ValidQuizJson);

        Assert.Equal("Photosynthesis", quiz.Title);
        var question = Assert.Single(quiz.Questions);
        Assert.Equal(4, question.Options.Count);
        Assert.Equal("Chloroplasts", question.CorrectOption);
    }

    [Fact]
    public void Accepts_json_wrapped_in_code_fences()
    {
        var quiz = QuizJsonParser.Parse("```json\n" + TestData.ValidQuizJson + "\n```");
        Assert.Single(quiz.Questions);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("""{ "title": "T", "summary": "S" }""")]
    [InlineData("""{ "title": "T", "summary": "S", "questions": [] }""")]
    [InlineData("""{ "summary": "S", "questions": [] }""")]
    public void Rejects_malformed_quizzes(string json)
    {
        Assert.Throws<QuizGenerationException>(() => QuizJsonParser.Parse(json));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(-1)]
    public void Rejects_correct_index_out_of_range(int index)
    {
        var json = TestData.ValidQuizJson.Replace("\"correctIndex\": 1", $"\"correctIndex\": {index}", StringComparison.Ordinal);
        var ex = Assert.Throws<QuizGenerationException>(() => QuizJsonParser.Parse(json));
        Assert.Contains("correctIndex", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_duplicate_options()
    {
        var json = TestData.ValidQuizJson.Replace("\"Nucleus\"", "\"chloroplasts\"", StringComparison.Ordinal);
        Assert.Throws<QuizGenerationException>(() => QuizJsonParser.Parse(json));
    }

    [Fact]
    public void Rejects_empty_options()
    {
        var json = TestData.ValidQuizJson.Replace("\"Nucleus\"", "\"  \"", StringComparison.Ordinal);
        Assert.Throws<QuizGenerationException>(() => QuizJsonParser.Parse(json));
    }
}
