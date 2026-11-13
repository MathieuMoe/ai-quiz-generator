using QuizForge.Core.Models;
using QuizForge.Core.Validation;

namespace QuizForge.Tests;

public class QuizRequestValidatorTests
{
    [Fact]
    public void Valid_request_has_no_errors()
    {
        Assert.Empty(QuizRequestValidator.Validate(TestData.Request()));
    }

    [Fact]
    public void Too_short_text_is_rejected()
    {
        var errors = QuizRequestValidator.Validate(new QuizRequest("Too short."));
        Assert.Single(errors);
        Assert.Contains("at least", errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Too_long_text_is_rejected()
    {
        var text = new string('a', QuizRequest.MaxTextLength + 1);
        Assert.Contains(QuizRequestValidator.Validate(new QuizRequest(text)), e => e.Contains("at most", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Question_count_out_of_range_is_rejected(int count)
    {
        var errors = QuizRequestValidator.Validate(TestData.Request(count));
        Assert.Contains(errors, e => e.Contains("number of questions", StringComparison.Ordinal));
    }

    [Fact]
    public void Errors_have_codes_and_arguments_for_translation()
    {
        var error = Assert.Single(QuizRequestValidator.Check(new QuizRequest("Too short.")));
        Assert.Equal(QuizRequestValidator.TextTooShort, error.Code);
        Assert.Equal(QuizRequest.MinTextLength, error.Arguments[0]);
    }

    [Fact]
    public void Unknown_enum_values_are_rejected()
    {
        var request = TestData.Request() with { Difficulty = (Difficulty)42, Language = (QuizLanguage)42 };
        Assert.Equal(2, QuizRequestValidator.Validate(request).Count);
    }
}
