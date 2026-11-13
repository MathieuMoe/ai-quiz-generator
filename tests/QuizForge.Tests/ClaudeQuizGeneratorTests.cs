using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuizForge.Core.Abstractions;
using QuizForge.Infrastructure.Claude;

namespace QuizForge.Tests;

public class ClaudeQuizGeneratorTests
{
    private static string ToolUseResponse(string quizJson) =>
        $$"""
        {
          "id": "msg_1", "type": "message", "role": "assistant", "stop_reason": "tool_use",
          "content": [ { "type": "tool_use", "id": "tu_1", "name": "create_quiz", "input": {{quizJson}} } ]
        }
        """;

    private static (ClaudeQuizGenerator Generator, FakeHandler Handler) Create(HttpStatusCode status, string body)
    {
        var handler = new FakeHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
        var generator = new ClaudeQuizGenerator(http, Options.Create(new ClaudeOptions { ApiKey = "test" }),
            NullLogger<ClaudeQuizGenerator>.Instance);
        return (generator, handler);
    }

    [Fact]
    public async Task Parses_the_tool_use_answer()
    {
        var (generator, _) = Create(HttpStatusCode.OK, ToolUseResponse(TestData.ValidQuizJson));

        var quiz = await generator.GenerateAsync(TestData.Request());

        Assert.Equal("Photosynthesis", quiz.Title);
        Assert.Single(quiz.Questions);
    }

    [Fact]
    public async Task Sends_a_forced_tool_call_to_the_messages_endpoint()
    {
        var (generator, handler) = Create(HttpStatusCode.OK, ToolUseResponse(TestData.ValidQuizJson));

        await generator.GenerateAsync(TestData.Request());

        Assert.Equal("/v1/messages", handler.LastRequestUri?.AbsolutePath);
        using var sent = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("tool", sent.RootElement.GetProperty("tool_choice").GetProperty("type").GetString());
        Assert.Equal("create_quiz", sent.RootElement.GetProperty("tools")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Falls_back_to_json_text_blocks()
    {
        var text = JsonSerializer.Serialize(TestData.ValidQuizJson);
        var body = $$"""{ "stop_reason": "end_turn", "content": [ { "type": "text", "text": {{text}} } ] }""";
        var (generator, _) = Create(HttpStatusCode.OK, body);

        var quiz = await generator.GenerateAsync(TestData.Request());

        Assert.Single(quiz.Questions);
    }

    [Fact]
    public async Task Api_errors_become_friendly_exceptions()
    {
        var (generator, _) = Create(HttpStatusCode.TooManyRequests, """{ "error": { "type": "rate_limit_error" } }""");

        var ex = await Assert.ThrowsAsync<QuizGenerationException>(() => generator.GenerateAsync(TestData.Request()));
        Assert.Contains("429", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Truncated_answers_are_reported()
    {
        var (generator, _) = Create(HttpStatusCode.OK, """{ "stop_reason": "max_tokens", "content": [] }""");

        var ex = await Assert.ThrowsAsync<QuizGenerationException>(() => generator.GenerateAsync(TestData.Request()));
        Assert.Contains("cut off", ex.Message, StringComparison.Ordinal);
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
