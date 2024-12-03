using FairyBread;
using FluentValidation;
using Shortener.FrontendShared.Dtos;
using Shortener.Shared;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;

namespace Shortener.FrontendShared.Validators;

public sealed record ShortIdWrapper(string ShortId);

public sealed class ShortIdValidator : AbstractValidator<ShortIdWrapper>
{
    public ShortIdValidator() => RuleFor(x => x.ShortId).NotEmpty().Matches(UrlEncoder.Base62Regex());
}

public sealed class ShortUrlValidator : AbstractValidator<string>, IExplicitUsageOnlyValidator
{
    public ShortUrlValidator() => RuleFor(x => x).NotEmpty().Must(x => ShortIdUtils.TryParseUrl(x, out _));
}

public sealed class CreateUrlValidator : AbstractValidator<CreateUrlInput>
{
    public CreateUrlValidator() =>
        RuleFor(x => x.DestinationUrl).NotEmpty().Must(x => UrlUtils.IsValidHttpUrl(x, out Uri? _));
}

public sealed class GetUrlsValidator : AbstractValidator<GetUrlsInput>
{
    public GetUrlsValidator()
    {
        When(x => x.First is not null, () =>
        {
            RuleFor(x => x.First).InclusiveBetween(1, Constants.MaxUrlsPageSize);
            RuleFor(x => x.Last).Null();
            RuleFor(x => x.Before).Null();
        });
        When(x => x.After is not null, () =>
        {
            RuleFor(x => x.First).NotNull();
            RuleFor(x => x.After).NotEmpty().Must(IsValidCursor);
        });

        When(x => x.Last is not null, () =>
        {
            RuleFor(x => x.Last).InclusiveBetween(1, Constants.MaxUrlsPageSize);
            RuleFor(x => x.First).Null();
            RuleFor(x => x.After).Null();
        });
        When(x => x.After is not null, () =>
        {
            RuleFor(x => x.First).NotNull();
            RuleFor(x => x.After).NotEmpty().Must(IsValidCursor);
        });
    }

    private static bool IsValidCursor(string? cursor)
    {
        try
        {
            ShortIdUtils.ParseCursor(cursor);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public sealed class UpdateUrlRestValidator : AbstractValidator<UpdateUrlRestInput>
{
    public UpdateUrlRestValidator() =>
        RuleFor(x => x.DestinationUrl).NotEmpty().Must(u => UrlUtils.IsValidHttpUrl(u, out Uri? _));
}

public sealed class UpdateUrlGraphQLValidator : AbstractValidator<UpdateUrlInput>
{
    public UpdateUrlGraphQLValidator()
    {
        RuleFor(x => x.ShortUrl).NotEmpty().Must(x => ShortIdUtils.TryParseUrl(x, out _));
        RuleFor(x => x.DestinationUrl).NotEmpty().Must(u => UrlUtils.IsValidHttpUrl(u, out Uri? _));
    }
}
