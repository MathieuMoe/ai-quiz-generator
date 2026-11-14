using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using QuizForge.Core.Abstractions;
using QuizForge.Infrastructure.Gemini;
using QuizForge.Infrastructure.OpenAi;

namespace QuizForge.Tests;

public class GeminiQuizGeneratorTests
{
    private static string FunctionCallResponse(string quizJson) =>
        $$"""
        { "candidates": [ { "finishReason": "STOP",
            "content": { "role": "model", "parts": [ { "functionCall": { "name": "create_quiz", "args": {{quizJson}} } } ] } } ] }
        """;

    private static (GeminiQuizGenerator Generator, FakeHandler Handler) Create(HttpStatusCode status, string body)
    {
        var handler = new FakeHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/") };
        return (new GeminiQuizGenerator(http, new OpenAiCompatibleOptions { ApiKey = "AQ.test", Model = "gemini-flash-latest" },
            NullLogger<GeminiQuizGenerator>.Instance), handler);
    }

    [Fact]
    public async Task Parses_the_function_call_and_uses_the_native_endpoint()
    {
        var (generator, handler) = Create(HttpStatusCode.OK, FunctionCallResponse(TestData.ValidQuizJson));

        var quiz = await generator.GenerateAsync(TestData.Request());

        Assert.Equal("Photosynthesis", quiz.Title);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent", handler.LastRequestUri?.ToString());
        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("ANY", body.RootElement.GetProperty("toolConfig").GetProperty("functionCallingConfig").GetProperty("mode").GetString());
        Assert.Equal("create_quiz", body.RootElement.GetProperty("tools")[0].GetProperty("functionDeclarations")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void Falls_back_to_json_text()
    {
        var response = $$"""{ "candidates": [ { "content": { "parts": [ { "text": {{JsonSerializer.Serialize(TestData.ValidQuizJson)}} } ] } } ] }""";
        Assert.Equal("Photosynthesis", GeminiQuizGenerator.ParseResponse(response).Title);
    }

    [Fact]
    public void A_cut_off_answer_is_reported()
    {
        Assert.Throws<QuizGenerationException>(() => GeminiQuizGenerator.ParseResponse("""{ "candidates": [ { "finishReason": "MAX_TOKENS", "content": { "parts": [] } } ] }"""));
    }

    [Fact]
    public async Task An_api_error_becomes_a_generation_exception()
    {
        var (generator, _) = Create(HttpStatusCode.BadRequest, """{ "error": { "code": 400, "message": "API key not valid" } }""");
        var ex = await Assert.ThrowsAsync<QuizGenerationException>(() => generator.GenerateAsync(TestData.Request()));
        Assert.Contains("400", ex.Message, StringComparison.Ordinal);
    }

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
