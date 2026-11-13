using QuizForge.Core.Models;

namespace QuizForge.Tests;

internal static class TestData
{
    public const string Photosynthesis =
        "Photosynthesis is the process plants use to convert sunlight into chemical energy. " +
        "It takes place mainly in the chloroplasts, which contain a green pigment called chlorophyll. " +
        "During the light reactions, water molecules are split and oxygen is released into the atmosphere. " +
        "The Calvin cycle then uses carbon dioxide to produce glucose, a sugar that stores energy. " +
        "Without photosynthesis, most ecosystems would collapse because animals depend on plants for food. " +
        "Scientists study photosynthesis to design better crops and renewable fuels.";

    public static QuizRequest Request(int questions = 3, QuizLanguage language = QuizLanguage.English) =>
        new(Photosynthesis, questions, Difficulty.Medium, language);

    public const string ValidQuizJson =
        """
        {
          "title": "Photosynthesis",
          "summary": "Plants turn light into chemical energy.",
          "questions": [
            {
              "prompt": "Where does photosynthesis mainly happen?",
              "options": ["Mitochondria", "Chloroplasts", "Nucleus", "Ribosomes"],
              "correctIndex": 1,
              "explanation": "The text says it takes place mainly in the chloroplasts."
            }
          ]
        }
        """;
}
