using Microsoft.AspNetCore.Mvc.RazorPages;
using QuizForge.Core.Abstractions;

namespace QuizForge.Web.Pages;

public sealed class ApiModel(IQuizGenerator generator) : PageModel
{
    public void OnGet() => ViewData["Engine"] = generator.EngineName;
}
