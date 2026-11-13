using System.Text.RegularExpressions;
using QuizForge.Core.Abstractions;
using QuizForge.Core.Models;
using QuizForge.Core.Validation;

namespace QuizForge.Infrastructure.Demo;

/// <summary>
/// Offline generator used when no API key is configured.
/// Builds "fill in the blank" questions from the text itself so the app can be tried
/// (and tested) without any external service. Deterministic: same input, same quiz.
/// </summary>
public sealed partial class DemoQuizGenerator : IQuizGenerator
{
    private const string Blank = "_____";

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "after", "again", "against", "because", "before", "between", "during", "should", "their",
        "there", "these", "those", "through", "under", "where", "which", "while", "would", "other",
        "porque", "aunque", "cuando", "desde", "donde", "entre", "hasta", "mientras", "también", "todos",
        "parce", "depuis", "pendant", "toujours", "quelque", "aussi", "entre", "comme", "alors", "encore",
    };

    public string EngineName => "Demo (offline)";

    public Task<Quiz> GenerateAsync(QuizRequest request, CancellationToken cancellationToken = default)
    {
        var errors = QuizRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(request));
        }

        return Task.FromResult(Build(request));
    }

    private static Quiz Build(QuizRequest request)
    {
        var text = WhiteSpace().Replace(request.SourceText.Trim(), " ");

        // Split on sentence ends and on line breaks (titles, list items), then keep real sentences only.
        var sentences = SentenceSplit().Split(request.SourceText.Trim())
            .Select(s => WhiteSpace().Replace(s, " ").Trim())
            .Where(s => s.Length > 0 && ".!?".Contains(s[^1], StringComparison.Ordinal) && WordCount(s) is >= 6 and <= 45)
            .ToList();

        var keywords = Words().Matches(text)
            .Select(m => m.Value)
            .Where(w => w.Length >= 6 && !StopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var labels = Labels(request.Language);
        var questions = new List<Question>();
        var usedAnswers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sentence in sentences)
        {
            if (questions.Count == request.QuestionCount)
            {
                break;
            }

            var answer = Words().Matches(sentence)
                .Select(m => m.Value)
                .Where(w => w.Length >= 6 && !StopWords.Contains(w) && !usedAnswers.Contains(w))
                .OrderByDescending(w => w.Length)
                .FirstOrDefault();

            if (answer is null)
            {
                continue;
            }

            var distractors = keywords
                .Where(k => !k.Equals(answer, StringComparison.OrdinalIgnoreCase)
                            && !sentence.Contains(k, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => Math.Abs(k.Length - answer.Length))
                .ThenBy(k => k, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            if (distractors.Count < 3)
            {
                continue;
            }

            var correctIndex = questions.Count % 4;
            var options = new List<string>(distractors);
            options.Insert(correctIndex, answer);

            var prompt = $"{labels.Complete} «{ReplaceFirst(sentence, answer, Blank)}»";
            questions.Add(new Question(prompt, options, correctIndex, $"{labels.Source} «{sentence}»"));
            usedAnswers.Add(answer);
        }

        if (questions.Count == 0)
        {
            throw new QuizGenerationException(labels.NotEnough);
        }

        var summary = string.Join(" ", sentences.Take(2));
        var title = $"{labels.Title}: {string.Join(" ", text.Split(' ').Take(6))}…";
        return new Quiz(title, summary, questions);
    }

    private static string ReplaceFirst(string text, string value, string replacement)
    {
        var index = text.IndexOf(value, StringComparison.Ordinal);
        return index < 0 ? text : string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + value.Length));
    }

    private static int WordCount(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private static (string Title, string Complete, string Source, string NotEnough) Labels(QuizLanguage language) => language switch
    {
        QuizLanguage.Spanish => ("Cuestionario demo", "Completa la frase:", "Frase original:", "El texto no tiene suficientes frases para crear preguntas."),
        QuizLanguage.French => ("Quiz démo", "Complète la phrase :", "Phrase d'origine :", "Le texte ne contient pas assez de phrases pour créer des questions."),
        QuizLanguage.Catalan => ("Qüestionari demo", "Completa la frase:", "Frase original:", "El text no té prou frases per crear preguntes."),
        _ => ("Demo quiz", "Complete the sentence:", "Original sentence:", "The text does not contain enough sentences to build questions."),
    };

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"(?<=[.!?])\s+|\r?\n")]
    private static partial Regex SentenceSplit();

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex Words();
}
