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
using Shortener.Shared.Services;

namespace Shortener.RestFrontend.Controllers;

[Authorize]
[AutoValidation]
[Route("[controller]")]
[ApiController]
public sealed class UserController(
    IValidator<PasswordWrapper> passwordValidator,
    IShardService shardService,
    IUserService userService) :
    ControllerBase
{
    [AllowAnonymous]
    [HttpPost("Create")]
    public async Task<ActionResult<User>> Create([FromBody] CreateUserInput user)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        (long shardId, string token) = await userService.Create(user.Username, user.Password);
        return new User { ShardId = shardId, Username = user.Username, Token = token };
    }

    [AllowAnonymous]
    [HttpPost("GenerateToken")]
    public async Task<ActionResult<User>> GenerateToken([FromBody] GenerateTokenInput user)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!shardService.IsOnline(user.ShardId))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        string token = await userService.GenerateToken(user.ShardId, user.Username, user.Password);
        return new User { ShardId = user.ShardId, Username = user.Username, Token = token };
    }

    [HttpPatch("Update")]
    public async Task<ActionResult<bool>> Update([FromBody] string password)
    {
        ValidationResult result = await passwordValidator.ValidateAsync(new PasswordWrapper(password));
        if (!result.IsValid)
        {
            result.AddToModelState(ModelState, null);
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }


        UserId userId = (UserId)HttpContext.Items[JwtHandler.UserId]!;
        bool update = await userService.Update(userId, password);

        return update ? NoContent() : NotFound();
    }

    [HttpDelete("Delete")]
    public async Task<ActionResult<bool>> Delete()
    {
        UserId userId = (UserId)HttpContext.Items[JwtHandler.UserId]!;
        bool deleted = await userService.Delete(userId);

        return deleted ? NoContent() : NotFound();
    }
}
