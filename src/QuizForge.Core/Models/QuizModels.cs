namespace QuizForge.Core.Models;

public enum Difficulty
{
    Easy,
    Medium,
    Hard,
}

public enum QuizLanguage
{
    English,
    Spanish,
    French,
    Catalan,
}

/// <summary>What the user asks for: a source text and how the quiz should look.</summary>
public sealed record QuizRequest(
    string SourceText,
    int QuestionCount = 5,
    Difficulty Difficulty = Difficulty.Medium,
    QuizLanguage Language = QuizLanguage.English)
{
    public const int MinQuestions = 1;
    public const int MaxQuestions = 20;
    public const int MinTextLength = 200;
    public const int MaxTextLength = 30_000;
}

/// <summary>A single multiple-choice question.</summary>
public sealed record Question(
    string Prompt,
    IReadOnlyList<string> Options,
    int CorrectIndex,
    string Explanation)
{
    public string CorrectOption => Options[CorrectIndex];
}

/// <summary>A generated quiz: a short summary of the source plus the questions.</summary>
public sealed record Quiz(
    string Title,
    string Summary,
    IReadOnlyList<Question> Questions);
