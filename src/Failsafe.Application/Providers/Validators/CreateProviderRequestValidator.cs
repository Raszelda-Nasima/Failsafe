using FluentValidation;
using Failsafe.Application.Providers.DTOs;
using Failsafe.Domain.Enums;

namespace Failsafe.Application.Providers.Validators;

public class CreateProviderRequestValidator : AbstractValidator<CreateProviderRequest>
{
    public CreateProviderRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);

        RuleFor(x => x.ProviderType)
            .Must(v => Enum.TryParse<ProviderType>(v, true, out _))
            .WithMessage($"ProviderType must be one of: {string.Join(", ", Enum.GetNames<ProviderType>())}");

        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CostPerTransactionCents).GreaterThanOrEqualTo(0);
    }
}
