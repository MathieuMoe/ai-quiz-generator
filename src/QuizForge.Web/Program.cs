using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using QuizForge.Web;
using QuizForge.Core.Abstractions;
using QuizForge.Core.Models;
using QuizForge.Core.Validation;
using QuizForge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(o => o
    .SetDefaultCulture("en")
    .AddSupportedCultures(Localization.SupportedCultures)
    .AddSupportedUICultures(Localization.SupportedCultures));
builder.Services.AddRazorPages();
// Write accented characters (é, ñ, ç, à…) as-is in the HTML instead of &#xE9; entities.
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
    o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));
builder.Services.AddQuizForge(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Protects the (paid) AI API from abuse: 10 generations per minute per client IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("generate", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();
app.UseRateLimiter();

app.MapRazorPages();
app.MapHealthChecks("/health");

// Interface language switcher: stores the choice in the standard ASP.NET Core culture cookie.
app.MapGet("/culture/{culture}", (string culture, string? returnUrl, HttpContext context) =>
{
    if (!Localization.SupportedCultures.Contains(culture))
    {
        culture = "en";
    }

    context.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });

    return Results.LocalRedirect(Localization.IsLocalUrl(returnUrl) ? returnUrl! : "/");
});

// JSON API: the same feature, usable from any client (Postman, another app, ...).
app.MapPost("/api/quiz", async (QuizRequest request, IQuizGenerator generator, CancellationToken ct) =>
{
    var errors = QuizRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [.. errors] });
    }

    try
    {
        return Results.Ok(await generator.GenerateAsync(request, ct));
    }
    catch (QuizGenerationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
})
.RequireRateLimiting("generate");

// Imports the readable text of a public web page (SSRF-protected, see WebPageReader).
app.MapPost("/api/import-url", async (ImportUrlRequest body, IWebPageReader reader, CancellationToken ct) =>
{
    var raw = body.Url?.Trim() ?? string.Empty;
    if (!raw.Contains("://", StringComparison.Ordinal))
    {
        raw = "https://" + raw;
    }

    if (!Uri.TryCreate(raw, UriKind.Absolute, out var url))
    {
        return ImportProblem(WebImportException.InvalidUrl, "Invalid web address.", StatusCodes.Status400BadRequest);
    }

    try
    {
        return Results.Ok(await reader.ReadAsync(url, ct));
    }
    catch (WebImportException ex)
    {
        var status = ex.Code switch
        {
            WebImportException.InvalidUrl or WebImportException.BlockedAddress => StatusCodes.Status400BadRequest,
            WebImportException.FetchFailed => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
        return ImportProblem(ex.Code, ex.Message, status);
    }

    static IResult ImportProblem(string code, string detail, int status) =>
        Results.Problem(detail, statusCode: status, extensions: new Dictionary<string, object?> { ["code"] = code });
})
.RequireRateLimiting("generate");

await app.RunAsync();

/// <summary>Exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
