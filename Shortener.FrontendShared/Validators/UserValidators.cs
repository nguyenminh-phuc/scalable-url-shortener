using FairyBread;
using FluentValidation;
using Shortener.FrontendShared.Dtos;

namespace Shortener.FrontendShared.Validators;

public sealed class CreateUserValidator : AbstractValidator<CreateUserInput>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Username).Length(5, 10);
        RuleFor(x => x.Password).Length(8, 30);
    }
}

public sealed class GenerateTokenValidator : AbstractValidator<GenerateTokenInput>
{
    public GenerateTokenValidator()
    {
        RuleFor(x => x.Username).Length(5, 10);
        RuleFor(x => x.Password).Length(8, 30);
    }
}

public sealed record PasswordWrapper(string Password);

public sealed class PasswordRestValidator : AbstractValidator<PasswordWrapper>
{
    public PasswordRestValidator() => RuleFor(x => x.Password).Length(8, 30);
}

public sealed class PasswordGraphQLValidator : AbstractValidator<string>, IExplicitUsageOnlyValidator
{
    public PasswordGraphQLValidator() => RuleFor(x => x).NotEmpty().Length(8, 30);
}
