using System.Globalization;
using QuizForge.Core.Models;

namespace QuizForge.Core.Validation;

/// <summary>
/// A validation problem. <see cref="Code"/> and <see cref="Arguments"/> let the UI show a translated message;
/// <see cref="Message"/> is the default English text (used by the JSON API).
/// </summary>
public sealed record ValidationError(string Code, string Message, params object[] Arguments);

public static class QuizRequestValidator
{
    public const string TextTooShort = "TextTooShort";
    public const string TextTooLong = "TextTooLong";
    public const string QuestionCountOutOfRange = "QuestionCountOutOfRange";
    public const string UnknownDifficulty = "UnknownDifficulty";
    public const string UnknownLanguage = "UnknownLanguage";

    /// <summary>Returns the problems with the request as English messages (empty when valid).</summary>
    public static IReadOnlyList<string> Validate(QuizRequest request) =>
        Check(request).Select(e => e.Message).ToList();

    /// <summary>Returns the problems with the request as structured errors (empty when valid).</summary>
    public static IReadOnlyList<ValidationError> Check(QuizRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<ValidationError>();
        var ci = CultureInfo.InvariantCulture;

        var length = request.SourceText?.Trim().Length ?? 0;
        if (length < QuizRequest.MinTextLength)
        {
            errors.Add(new(TextTooShort,
                string.Format(ci, "The text must be at least {0} characters long.", QuizRequest.MinTextLength),
                QuizRequest.MinTextLength));
        }
        else if (length > QuizRequest.MaxTextLength)
        {
            errors.Add(new(TextTooLong,
                string.Format(ci, "The text must be at most {0:N0} characters long.", QuizRequest.MaxTextLength),
                QuizRequest.MaxTextLength));
        }

        if (request.QuestionCount is < QuizRequest.MinQuestions or > QuizRequest.MaxQuestions)
        {
            errors.Add(new(QuestionCountOutOfRange,
                string.Format(ci, "The number of questions must be between {0} and {1}.", QuizRequest.MinQuestions, QuizRequest.MaxQuestions),
                QuizRequest.MinQuestions, QuizRequest.MaxQuestions));
        }

        if (!Enum.IsDefined(request.Difficulty))
        {
            errors.Add(new(UnknownDifficulty, "Unknown difficulty."));
        }

        if (!Enum.IsDefined(request.Language))
        {
            errors.Add(new(UnknownLanguage, "Unknown language."));
        }

        return errors;
    }
}
