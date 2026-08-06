namespace Failsafe.Domain.Enums;

// The payment providers a bank might integrate with. Kept as a closed
// enum rather than a free-text string, since routing/priority logic
// needs to reason over a known, finite set of provider kinds.
public enum ProviderType
{
    Visa,
    Mastercard,
    PayPal,
    EFT,
    MobileMoney
}