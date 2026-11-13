using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QuizForge.Core.Abstractions;
using QuizForge.Infrastructure.Claude;
using QuizForge.Infrastructure.Demo;
using QuizForge.Infrastructure.Web;

namespace QuizForge.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the quiz generator: Claude when an API key is configured
    /// (Claude:ApiKey or ANTHROPIC_API_KEY), otherwise the offline demo generator.
    /// </summary>
    public static IServiceCollection AddQuizForge(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Web page import (with SSRF protection)
        services.AddOptions<WebImportOptions>().Bind(configuration.GetSection(WebImportOptions.SectionName));
        services.AddHttpClient<IWebPageReader, WebPageReader>((sp, client) =>
            {
                client.Timeout = sp.GetRequiredService<IOptions<WebImportOptions>>().Value.Timeout;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("QuizForgeBot/1.0 (+https://github.com/MathieuMoe/ai-quiz-generator)");
            })
            .ConfigurePrimaryHttpMessageHandler(sp => WebPageReader.CreateHandler(sp.GetRequiredService<IOptions<WebImportOptions>>().Value));

        services.AddOptions<ClaudeOptions>()
            .Bind(configuration.GetSection(ClaudeOptions.SectionName))
            .PostConfigure(o => o.ApiKey ??= configuration["ANTHROPIC_API_KEY"]);

        var apiKey = configuration[$"{ClaudeOptions.SectionName}:ApiKey"] ?? configuration["ANTHROPIC_API_KEY"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton<IQuizGenerator, DemoQuizGenerator>();
            return services;
        }

        services.AddHttpClient<IQuizGenerator, ClaudeQuizGenerator>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ClaudeOptions>>().Value;
            client.BaseAddress = options.BaseUrl;
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", options.ApiVersion);
        });

        return services;
    }
}
