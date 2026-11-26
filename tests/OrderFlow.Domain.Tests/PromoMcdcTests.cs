using OrderFlow.Domain;

namespace OrderFlow.Domain.Tests;

public class PromoMcdcTests
{
    [Theory]
    [InlineData("NOPE", 150.00, true, CustomerTier.Regular, false)] // row 7: A=0, B=1, C=1, D=0
    [InlineData("WELCOME", 150.00, true, CustomerTier.Regular, true)] // row 15: A=1, B=1, C=1, D=0
    [InlineData("WELCOME", 149.99, true, CustomerTier.Regular, false)] // row 11: A=1, B=0, C=1, D=0
    [InlineData("WELCOME", 150.00, false, CustomerTier.Regular, false)] // row 13: A=1, B=1, C=0, D=0
    [InlineData("WELCOME", 150.00, false, CustomerTier.VIP, true)] // row 14: A=1, B=1, C=0, D=1
    public void Promo_decision_satisfies_the_policy_and_modified_condition_coverage(
        string code, decimal subtotal, bool firstOrder, CustomerTier tier, bool expected)
    {
        Quoting.PromoApplies(code, subtotal, firstOrder, tier, QuotePolicy.Rules)
            .Should().Be(expected,
                "promo applies exactly when the code matches, the subtotal reaches M, and the order is first or VIP");
    }
}
