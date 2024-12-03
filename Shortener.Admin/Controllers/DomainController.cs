using FluentValidation;
using FluentValidation.AspNetCore;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Attributes;
using Shortener.Admin.Services;
using Shortener.Admin.Validators;

namespace Shortener.Admin.Controllers;

[Authorize]
[AutoValidation]
[Route("[controller]")]
[ApiController]
public sealed class DomainController(IValidator<DomainWrapper> domainValidator, IBannedDomainService domainService) :
    ControllerBase
{
    [HttpPost("Ban/{domain}")]
    public async Task<ActionResult<bool>> Ban(string domain)
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

        bool banned = await domainService.Ban(domain);

        return CreatedAtAction(nameof(Ban), new { id = domain }, banned);
    }

    [HttpDelete("Unban/{domain}")]
    public async Task<ActionResult<bool>> Unban(string domain)
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

        bool deleted = await domainService.Unban(domain);

        return deleted ? NoContent() : NotFound();
    }
}
