using OrderFlow.Domain;

namespace OrderFlow.Domain.Tests;

// Tests for the nine pricing rules that the product description leaves unstated.

public class R1_InclusiveThresholdTests
{
    [Theory]
    [InlineData(CustomerTier.Regular, 0.10)]
    [InlineData(CustomerTier.VIP, 0.15)]
    public void With_an_inclusive_threshold_a_subtotal_of_exactly_1000_is_discounted(CustomerTier tier, decimal expectedRate)
    {
        var rules = new PricingRules { ThresholdInclusive = true };

        decimal rate = Pricing.DiscountRate(1000.00m, tier, promoApplies: false, rules);

        rate.Should().Be(expectedRate);
    }

    [Fact]
    public void By_default_a_subtotal_of_exactly_1000_is_not_discounted()
    {
        var rules = new PricingRules();

        decimal rate = Pricing.DiscountRate(1000.00m, CustomerTier.VIP, promoApplies: false, rules);

        rate.Should().Be(0m);
    }
}

public class R2_PromoUnderThresholdTests
{
    [Theory]
    [InlineData(CustomerTier.Regular)]
    [InlineData(CustomerTier.VIP)]
    public void The_promo_code_gives_5_percent_under_the_threshold_whatever_the_tier(CustomerTier tier)
    {
        var rules = new PricingRules();

        decimal rate = Pricing.DiscountRate(500.00m, tier, promoApplies: true, rules);

        rate.Should().Be(0.05m);
    }
}

public class R3_FreeShippingOnNetAmountTests
{
    [Fact]
    public void A_subtotal_over_2000_still_pays_shipping_when_the_discount_brings_the_net_amount_under_it()
    {
        var lines = Orders.Lines(("Test product", 2100.00m, 1));

        OrderTotals totals = Pricing.Calculate(lines, CustomerTier.Regular, promoApplies: false, new PricingRules());

        totals.Net.Should().Be(1890.00m);
        totals.Shipping.Should().Be(50.00m, "free shipping is decided on the net amount, not on the subtotal");
    }

    [Fact]
    public void Shipping_is_free_when_the_net_amount_is_over_2000()
    {
        var lines = Orders.Lines(("Test product", 2300.00m, 1));

        OrderTotals totals = Pricing.Calculate(lines, CustomerTier.Regular, promoApplies: false, new PricingRules());

        totals.Net.Should().Be(2070.00m);
        totals.Shipping.Should().Be(0m);
    }
}

public class R4_EmptyOrderTests
{
    [Fact]
    public void An_order_without_lines_has_zero_totals()
    {
        var noLines = new List<OrderLine>();

        OrderTotals totals = Pricing.Calculate(noLines, CustomerTier.VIP, promoApplies: true, new PricingRules());

        totals.Should().Be(new OrderTotals(0m, 0m, 0m, 0m, 0m, 0m));
    }
}

public class R5_VatTests
{
    [Fact]
    public void VAT_is_20_percent_of_the_net_amount_plus_shipping()
    {
        var lines = Orders.Lines(("Test product", 100.00m, 1));

        OrderTotals totals = Pricing.Calculate(lines, CustomerTier.Regular, promoApplies: false, new PricingRules());

        totals.Vat.Should().Be(30.00m, "20 % of 100 + 50");
        totals.Total.Should().Be(180.00m);
    }
}

public class R6_DiscountBeforeVatTests
{
    [Fact]
    public void The_discount_is_taken_before_VAT_and_the_total_is_net_plus_shipping_plus_VAT()
    {
        var lines = Orders.Standard();

        OrderTotals totals = Pricing.Calculate(lines, CustomerTier.Regular, promoApplies: false, new PricingRules());

        totals.Net.Should().Be(1080.00m, "1,200 less 10 %");
        totals.Vat.Should().Be(226.00m, "20 % of 1,080 + 50, not of 1,200 + 50");
        totals.Total.Should().Be(totals.Net + totals.Shipping + totals.Vat);
    }
}

public class R7_HalfCentRoundingTests
{
    [Fact]
    public void A_half_cent_in_the_discount_rounds_away_from_zero_by_default()
    {
        var lines = Orders.Lines(("Test product", 1000.30m, 1));   // 15 % = 150.045

        OrderTotals totals = Pricing.Calculate(lines, CustomerTier.VIP, promoApplies: false, new PricingRules());

        totals.Discount.Should().Be(150.05m);
        totals.Total.Should().Be(1080.30m);
    }

    [Fact]
    public void A_half_cent_in_the_discount_rounds_to_even_when_the_invoice_asks_for_it()
    {
        var lines = Orders.Lines(("Test product", 1000.30m, 1));

        OrderTotals totals = Pricing.Calculate(lines, CustomerTier.VIP, promoApplies: false, new PricingRules(),
            MidpointRounding.ToEven);

        totals.Discount.Should().Be(150.04m);
        totals.Total.Should().Be(1080.31m, "the invoice is one cent higher than the order when the half-cent rounds down");
    }
}

public class R8_LineTotalTests
{
    [Fact]
    public void A_line_total_is_the_unit_price_times_the_quantity()
    {
        var line = new OrderLine { ProductName = "Stapler", UnitPrice = 12.90m, Quantity = 3 };

        decimal lineTotal = line.LineTotal;

        lineTotal.Should().Be(38.70m);
    }

    [Fact]
    public void The_subtotal_is_the_sum_of_the_line_totals()
    {
        var lines = Orders.Lines(("Stapler", 12.90m, 3), ("Desk lamp", 45.00m, 2));

        decimal subtotal = Pricing.Subtotal(lines);

        subtotal.Should().Be(128.70m, "38.70 + 90.00");
    }

    [Fact]
    public void A_line_with_a_quantity_of_zero_adds_nothing_to_the_subtotal()
    {
        var lines = Orders.Lines(("Stapler", 12.90m, 3), ("Desk lamp", 45.00m, 0));

        decimal subtotal = Pricing.Subtotal(lines);

        subtotal.Should().Be(38.70m);
    }
}

public class R9_FullAddressTests
{
    [Fact]
    public void The_full_address_is_the_street_the_city_and_the_country_separated_by_commas()
    {
        var customer = new Customer { Address = "Svobody Ave 10", City = "Lviv", Country = "Ukraine" };

        string fullAddress = customer.FullAddress;

        fullAddress.Should().Be("Svobody Ave 10, Lviv, Ukraine");
    }

    [Fact]
    public void An_empty_city_leaves_an_empty_slot_between_the_commas()
    {
        // Documents the current behaviour; whether it is wanted is a question for the product owner.
        var customer = new Customer { Address = "Svobody Ave 10", City = "", Country = "Ukraine" };

        string fullAddress = customer.FullAddress;

        fullAddress.Should().Be("Svobody Ave 10, , Ukraine");
    }
}
