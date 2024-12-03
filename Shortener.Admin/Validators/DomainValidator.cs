using FluentValidation;

namespace Shortener.Admin.Validators;

public sealed record DomainWrapper(string Domain);

public sealed class DomainValidator : AbstractValidator<DomainWrapper>
{
    public DomainValidator() =>
        RuleFor(x => x.Domain).NotEmpty().Must(x => Uri.CheckHostName(x) != UriHostNameType.Unknown);
}
