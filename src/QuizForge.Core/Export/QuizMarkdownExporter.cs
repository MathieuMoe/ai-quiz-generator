using System.Globalization;
using System.Text;
using QuizForge.Core.Models;

namespace QuizForge.Core.Export;

/// <summary>Exports a quiz as Markdown, with the answer key at the end (handy for teachers).</summary>
public static class QuizMarkdownExporter
{
    private static readonly string[] Letters = ["A", "B", "C", "D", "E", "F"];

    public static string ToMarkdown(Quiz quiz, QuizLanguage language = QuizLanguage.English)
    {
        ArgumentNullException.ThrowIfNull(quiz);
        var sb = new StringBuilder();
        var ci = CultureInfo.InvariantCulture;

        sb.Append("# ").AppendLine(quiz.Title).AppendLine()
          .Append("> ").AppendLine(quiz.Summary).AppendLine();

        for (var i = 0; i < quiz.Questions.Count; i++)
        {
            var q = quiz.Questions[i];
            sb.AppendLine(ci, $"## {i + 1}. {q.Prompt}").AppendLine();
            for (var o = 0; o < q.Options.Count; o++)
            {
                sb.AppendLine(ci, $"- **{Letters[o]}.** {q.Options[o]}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("---").AppendLine().Append("## ").AppendLine(AnswerKey(language)).AppendLine();
        for (var i = 0; i < quiz.Questions.Count; i++)
        {
            var q = quiz.Questions[i];
            sb.AppendLine(ci, $"{i + 1}. **{Letters[q.CorrectIndex]}**: {q.Explanation}");
        }

        return sb.ToString();
    }

    private static string AnswerKey(QuizLanguage language) => language switch
    {
        QuizLanguage.Spanish => "Respuestas",
        QuizLanguage.French => "Corrigé",
        QuizLanguage.Catalan => "Respostes",
        _ => "Answer key",
    };
}
