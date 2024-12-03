using System.Net;
using FluentValidation;
using FluentValidation.AspNetCore;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Shortener.FrontendShared.Validators;
using Shortener.RedirectFrontend.Services;
using Shortener.Shared.Entities;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;
using Services_Telemetry = Shortener.RedirectFrontend.Services.Telemetry;

namespace Shortener.RedirectFrontend.Controllers;

public sealed class RedirectController(
    Services_Telemetry telemetry,
    IValidator<ShortIdWrapper> shortIdValidator,
    IRedirectService redirectService,
    IShardService shardService)
    : Controller
{
    [Route("{*shortId}", Order = 999)]
    [HttpGet]
    public async Task<IActionResult> Index(string shortId)
    {
        ValidationResult result = await shortIdValidator.ValidateAsync(new ShortIdWrapper(shortId));
        if (!result.IsValid)
        {
            result.AddToModelState(ModelState, null);
        }

        if (!ModelState.IsValid)
        {
            return RedirectToAction("Index", "Home");
        }

        ShortId id = ShortIdUtils.ParseId(shortId);
        if (!shardService.IsOnline(id.Range))
        {
            return RedirectToAction("Index", "Home");
        }

        IPAddress ip = HttpContext.Connection.RemoteIpAddress!;
        StringValues userAgent = Request.Headers.UserAgent;
        StringValues referrer = Request.Headers.Referer;

        string destinationUrl = await redirectService.Redirect(id, ip, userAgent, referrer);
        telemetry.RedirectCounter.Add(1);

        return Redirect(destinationUrl);
    }
}
