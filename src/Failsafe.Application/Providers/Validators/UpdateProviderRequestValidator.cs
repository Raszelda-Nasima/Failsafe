using FluentValidation;
using Failsafe.Application.Providers.DTOs;

namespace Failsafe.Application.Providers.Validators;

public class UpdateProviderRequestValidator : AbstractValidator<UpdateProviderRequest>
{
    public UpdateProviderRequestValidator()
    {
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CostPerTransactionCents).GreaterThanOrEqualTo(0);
    }
}