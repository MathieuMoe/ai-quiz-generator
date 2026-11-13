using QuizForge.Core.Models;

namespace QuizForge.Core.Abstractions;

/// <summary>Turns a source text into a quiz.</summary>
public interface IQuizGenerator
{
    /// <summary>Human-readable name of the engine, shown in the UI (e.g. "Claude" or "Demo").</summary>
    string EngineName { get; }

    Task<Quiz> GenerateAsync(QuizRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Raised when a quiz cannot be generated (API error, invalid model output, ...).</summary>
public sealed class QuizGenerationException : Exception
{
    public QuizGenerationException() { }

    public QuizGenerationException(string message) : base(message) { }

    public QuizGenerationException(string message, Exception innerException) : base(message, innerException) { }
}
