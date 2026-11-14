using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using QuizForge.Core.Abstractions;
using QuizForge.Infrastructure.OpenAi;

namespace QuizForge.Tests;

public class OpenAiCompatibleQuizGeneratorTests
{
    private static string ToolCallResponse(string arguments) =>
        $$"""
        {
          "id": "chatcmpl-1", "object": "chat.completion",
          "choices": [ { "index": 0, "finish_reason": "tool_calls",
            "message": { "role": "assistant", "content": null,
              "tool_calls": [ { "id": "call_1", "type": "function", "function": { "name": "create_quiz", "arguments": {{JsonSerializer.Serialize(arguments)}} } } ] } } ]
        }
        """;

    private static (OpenAiCompatibleQuizGenerator Generator, FakeHandler Handler) Create(HttpStatusCode status, string body)
    {
        var handler = new FakeHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var generator = new OpenAiCompatibleQuizGenerator(http, new OpenAiCompatibleOptions { ApiKey = "test", Model = "gpt-test" },
            "ChatGPT", NullLogger<OpenAiCompatibleQuizGenerator>.Instance);
        return (generator, handler);
    }

    [Fact]
    public async Task Parses_the_function_call_arguments()
    {
        var (generator, _) = Create(HttpStatusCode.OK, ToolCallResponse(TestData.ValidQuizJson));

        var quiz = await generator.GenerateAsync(TestData.Request());

        Assert.Equal("Photosynthesis", quiz.Title);
        Assert.Single(quiz.Questions);
        Assert.Equal("ChatGPT", generator.EngineName);
    }

    [Fact]
    public async Task Sends_a_chat_completions_request_with_the_quiz_function()
    {
        var (generator, handler) = Create(HttpStatusCode.OK, ToolCallResponse(TestData.ValidQuizJson));

        await generator.GenerateAsync(TestData.Request());

        Assert.Equal("https://api.openai.com/v1/chat/completions", handler.LastRequestUri?.ToString());
        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal("gpt-test", root.GetProperty("model").GetString());
        Assert.Equal("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("user", root.GetProperty("messages")[1].GetProperty("role").GetString());
        Assert.Equal("create_quiz", root.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("required", root.GetProperty("tool_choice").GetString());
    }

    [Fact]
    public void Falls_back_to_json_in_the_message_content()
    {
        var response = $$"""{ "choices": [ { "finish_reason": "stop", "message": { "role": "assistant", "content": {{JsonSerializer.Serialize(TestData.ValidQuizJson)}} } } ] }""";

        var quiz = OpenAiCompatibleQuizGenerator.ParseResponse(response);

        Assert.Equal("Photosynthesis", quiz.Title);
    }

    [Fact]
    public void A_cut_off_answer_is_reported()
    {
        var response = """{ "choices": [ { "finish_reason": "length", "message": { "role": "assistant", "content": "{" } } ] }""";

        Assert.Throws<QuizGenerationException>(() => OpenAiCompatibleQuizGenerator.ParseResponse(response));
    }

    [Fact]
    public async Task An_api_error_becomes_a_generation_exception()
    {
        var (generator, _) = Create(HttpStatusCode.Unauthorized, """{ "error": { "message": "bad key" } }""");

        var ex = await Assert.ThrowsAsync<QuizGenerationException>(() => generator.GenerateAsync(TestData.Request()));
        Assert.Contains("401", ex.Message, StringComparison.Ordinal);
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
