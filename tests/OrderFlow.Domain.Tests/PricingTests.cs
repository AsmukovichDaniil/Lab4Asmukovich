using FluentAssertions.Execution;
using OrderFlow.Domain;

namespace OrderFlow.Domain.Tests;

// The pricing tests as rewritten and reviewed for release 1.3 (the five starter tests of 1.2 are gone).
// The tests for the undocumented rules R1–R9 are in RulesTests.cs.

public class DiscountTests
{
    [Theory]
    [InlineData(1000.00, 0.00)]
    [InlineData(1000.01, 0.10)]
    [InlineData(1200.00, 0.10)]
    public void A_regular_customer_gets_10_percent_only_over_1000(decimal subtotal, decimal expectedRate)
    {
        var rules = new PricingRules();

        decimal rate = Pricing.DiscountRate(subtotal, CustomerTier.Regular, promoApplies: false, rules);

        rate.Should().Be(expectedRate);
    }

    [Theory]
    [InlineData(1000.00, 0.00)]
    [InlineData(1000.01, 0.15)]
    [InlineData(1200.00, 0.15)]
    public void A_VIP_customer_gets_15_percent_only_over_1000(decimal subtotal, decimal expectedRate)
    {
        var rules = new PricingRules();

        decimal rate = Pricing.DiscountRate(subtotal, CustomerTier.VIP, promoApplies: false, rules);

        rate.Should().Be(expectedRate);
    }

    [Fact]
    public void The_promo_code_adds_5_percent_to_the_VIP_discount()
    {
        var rules = new PricingRules();

        decimal rate = Pricing.DiscountRate(1200m, CustomerTier.VIP, promoApplies: true, rules);

        rate.Should().Be(0.20m, because: "15 % for the tier and 5 % for the code stack");
    }

    [Fact]
    public void The_discount_of_a_regular_order_of_1200_is_120()
    {
        var lines = Orders.Standard();

        OrderTotals totals = Pricing.Calculate(lines, CustomerTier.Regular, promoApplies: false, new PricingRules());

        totals.Discount.Should().Be(120.00m);
    }
}

public class ShippingTests
{
    [Theory]
    [InlineData(100.00, 50)]
    [InlineData(1500.00, 50)]
    [InlineData(2000.00, 50)]
    [InlineData(2000.01, 0)]
    [InlineData(2500.00, 0)]
    public void Shipping_costs_50_up_to_2000_and_is_free_above(decimal amount, decimal expectedShipping)
    {
        var rules = new PricingRules();

        decimal shipping = Pricing.ShippingFor(amount, rules);

        shipping.Should().Be(expectedShipping);
    }
}

public class SubtotalTests
{
    [Fact]
    public void Adding_a_line_raises_the_subtotal_by_the_line_total()
    {
        var lines = Orders.Standard();
        lines.Add(new OrderLine { ProductName = "Paper clip", UnitPrice = 0.10m, Quantity = 1 });

        decimal subtotal = Pricing.Subtotal(lines);

        subtotal.Should().Be(1200.10m);
    }
}

public class OrderTotalsTests
{
    [Fact]
    public void Totals_of_a_regular_order_of_1200()
    {
        var lines = Orders.Standard();

        OrderTotals totals = Pricing.Calculate(lines, CustomerTier.Regular, promoApplies: false, new PricingRules());

        totals.Should().Be(new OrderTotals(
            Subtotal: 1200.00m, Discount: 120.00m, Shipping: 50.00m, Net: 1080.00m, Vat: 226.00m, Total: 1356.00m));
    }

    [Fact]
    public void Every_line_of_the_totals_of_a_regular_order_of_1200()
    {
        var lines = Orders.Standard();

        OrderTotals totals = Pricing.Calculate(lines, CustomerTier.Regular, promoApplies: false, new PricingRules());

        using (new AssertionScope())
        {
            totals.Subtotal.Should().Be(1200.00m);
            totals.Discount.Should().Be(120.00m, "10 % of 1,200");
            totals.Shipping.Should().Be(50.00m, "a net amount of 1,080 is not over 2,000");
            totals.Net.Should().Be(1080.00m);
            totals.Vat.Should().Be(226.00m, "20 % of 1,080 + 50");
            totals.Total.Should().Be(1356.00m);
        }
    }
}
