using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Attributes;
using Shortener.Admin.Dtos;
using Shortener.Admin.Services;
using Shortener.Admin.Validators;
using ValidationResult = FluentValidation.Results.ValidationResult;

namespace Shortener.Admin.Controllers;

[Authorize]
[AutoValidation]
[Route("[controller]")]
[ApiController]
public class UrlsController(IValidator<DomainWrapper> domainValidator, IUrlService urlService) : ControllerBase
{
    [HttpPost("StartCountAggregation/{domain}")]
    public async Task<ActionResult> StartCountAggregation(string domain)
    {
        ValidationResult result = await domainValidator.ValidateAsync(new DomainWrapper(domain));
        if (!result.IsValid)
        {
            result.AddToModelState(ModelState, null);
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await urlService.MakeCountAggregation(domain);

        return Ok();
    }

    [HttpGet("AggregatedCounts/{domain}")]
    public async Task<ActionResult<UrlCounts>> GetAggregatedCounts(string domain)
    {
        ValidationResult result = await domainValidator.ValidateAsync(new DomainWrapper(domain));
        if (!result.IsValid)
        {
            result.AddToModelState(ModelState, null);
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        UrlCounts counts = await urlService.GetAggregatedCounts(domain);

        return counts;
    }
}
