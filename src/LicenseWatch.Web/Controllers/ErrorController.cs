using System.Diagnostics;
using LicenseWatch.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LicenseWatch.Web.Controllers;

[Route("error")]
public class ErrorController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        var vm = BuildViewModel(500, "Something went wrong", "We hit an unexpected error. Please try again or contact an administrator.");
        return View("Index", vm);
    }

    [HttpGet("{statusCode:int}")]
    public IActionResult Status(int statusCode)
    {
        if (WantsJson())
        {
            return Problem(statusCode: statusCode, title: "Request failed", detail: "The request could not be completed.");
        }

        if (statusCode == 404)
        {
            var notFound = BuildViewModel(404, "Page not found", "We could not find that page. Check the URL or return to the dashboard.");
            Response.StatusCode = 404;
            return View("NotFound", notFound);
        }

        if (statusCode == 429)
        {
            var retryAfter = HttpContext.Items["RateLimitRetryAfter"] as TimeSpan?;
            var message = retryAfter.HasValue
                ? $"Please wait about {Math.Max(1, (int)Math.Ceiling(retryAfter.Value.TotalSeconds))} seconds and try again."
                : "Please wait a moment and try again.";

            var rateLimitVm = BuildViewModel(429, "Too many requests", message);
            Response.StatusCode = 429;
            return View("TooManyRequests", rateLimitVm);
        }

        var vm = BuildViewModel(statusCode, "Something went wrong", "We hit an unexpected error. Please try again or contact an administrator.");
        Response.StatusCode = statusCode;
        return View("Index", vm);
    }

    private static ErrorPageViewModel BuildViewModel(int statusCode, string title, string message)
    {
        return new ErrorPageViewModel
        {
            StatusCode = statusCode,
            Title = title,
            Message = message,
            RequestId = Activity.Current?.Id
        };
    }

    private bool WantsJson()
    {
        var accept = Request.GetTypedHeaders().Accept;
        if (accept is null || accept.Count == 0)
        {
            return false;
        }

        var wantsJson = accept.Any(a => a.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase));
        var wantsHtml = accept.Any(a => a.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase));
        return wantsJson && !wantsHtml;
    }
}
