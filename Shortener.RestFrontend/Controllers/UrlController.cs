using FluentValidation;
using FluentValidation.AspNetCore;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Attributes;
using Shortener.FrontendShared.Dtos;
using Shortener.FrontendShared.Middleware;
using Shortener.FrontendShared.Services;
using Shortener.FrontendShared.Validators;
using Shortener.Shared.Entities;
using Shortener.Shared.Utils;
using UrlStats = Shortener.FrontendShared.Dtos.UrlStats;

namespace Shortener.RestFrontend.Controllers;

[Authorize]
[AutoValidation]
[Route("[controller]")]
[ApiController]
public sealed class UrlController(IValidator<ShortIdWrapper> shortIdValidator, IUrlService urlService) : ControllerBase
{
    [HttpPost("Create")]
    public async Task<ActionResult<UrlMapping>> Create([FromBody] CreateUrlInput input)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        UserId userId = (UserId)HttpContext.Items[JwtHandler.UserId]!;

        UrlMapping url = await urlService.Create(userId, input.DestinationUrl);

        return CreatedAtAction(nameof(Create), new { id = url.ShortUrl }, url);
    }

    [HttpGet("{shortId}")]
    public async Task<ActionResult<UrlStats>> Get(string shortId)
    {
        ValidationResult result = await shortIdValidator.ValidateAsync(new ShortIdWrapper(shortId));
        if (!result.IsValid)
        {
            result.AddToModelState(ModelState, null);
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        UserId userId = (UserId)HttpContext.Items[JwtHandler.UserId]!;
        ShortId id = ShortIdUtils.ParseId(shortId);
        if (id.Range != userId.ShardId)
        {
            return Forbid();
        }

        UrlStats url = await urlService.GetById(id);
        return url;
    }

    [HttpGet]
    [Route("~/urls")]
    public async Task<ActionResult<Connection<UrlStats>>> Get([FromQuery] GetUrlsInput input)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        UserId userId = (UserId)HttpContext.Items[JwtHandler.UserId]!;

        Connection<UrlStats> connection = await urlService.GetByUserId(
            userId,
            input.First, input.After, input.Last, input.Before);

        return connection;
    }

    [HttpPatch("Update/{shortId}")]
    public async Task<ActionResult<bool>> Update(string shortId, [FromBody] UpdateUrlRestInput input)
    {
        ValidationResult result = await shortIdValidator.ValidateAsync(new ShortIdWrapper(shortId));
        if (!result.IsValid)
        {
            result.AddToModelState(ModelState, null);
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        UserId userId = (UserId)HttpContext.Items[JwtHandler.UserId]!;
        ShortId id = ShortIdUtils.ParseId(shortId);
        if (id.Range != userId.ShardId)
        {
            return Forbid();
        }

        bool updated = await urlService.Update(id, userId, input.DestinationUrl);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("Delete/{shortId}")]
    public async Task<ActionResult<bool>> Delete(string shortId)
    {
        ValidationResult result = await shortIdValidator.ValidateAsync(new ShortIdWrapper(shortId));
        if (!result.IsValid)
        {
            result.AddToModelState(ModelState, null);
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        UserId userId = (UserId)HttpContext.Items[JwtHandler.UserId]!;
        ShortId id = ShortIdUtils.ParseId(shortId);
        if (id.Range != userId.ShardId)
        {
            return Forbid();
        }

        bool deleted = await urlService.Delete(id, userId);

        return deleted ? NoContent() : NotFound();
    }
}
