using OrderFlow.Domain;

namespace OrderFlow.Domain.Tests;

public static class QuotePolicy
{
    public static QuoteRules Rules => new() { PromoMinimum = 150m, ParcelSize = 12, FreeShippingThreshold = 2000m };
}

public class QuoteTests
{
    private static List<OrderLine> One(decimal price, int quantity = 1) =>
        new() { new OrderLine { ProductName = "Test item", UnitPrice = price, Quantity = quantity } };

    public static TheoryData<decimal, bool> PromoMinimumCases => new()
    {
        { QuotePolicy.Rules.PromoMinimum - 0.01m, false }, // T22: below M
        { QuotePolicy.Rules.PromoMinimum, true },          // T23: exactly M
        { QuotePolicy.Rules.PromoMinimum + 0.01m, true },  // T24: above M
    };

    public static TheoryData<int, int> ParcelBoundaryCases => new()
    {
        { QuotePolicy.Rules.ParcelSize - 1, 1 }, // T29
        { QuotePolicy.Rules.ParcelSize, 1 },     // T30
        { QuotePolicy.Rules.ParcelSize + 1, 2 },  // T31
    };

    [Fact]
    public void T01_Null_order_lines_are_rejected()
    {
        Action act = () => Quoting.Calculate(null!, CustomerTier.Regular, null, false, QuotePolicy.Rules);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void T02_An_empty_order_has_zero_amounts_and_no_parcels()
    {
        Quoting.Calculate(new List<OrderLine>(), CustomerTier.VIP, "WELCOME", true, QuotePolicy.Rules)
            .Should().Be(default(Quote));
    }

    [Fact]
    public void Parcels_for_zero_items_are_zero()
    {
        Quoting.Parcels(0, QuotePolicy.Rules).Should().Be(0,
            "a quote with no items starts no parcel");
    }

    [Theory]
    [InlineData(0)] // T3 below minimum
    [InlineData(-1)]
    public void Invalid_quantities_below_one_are_rejected(int quantity)
    {
        Action act = () => Quoting.Calculate(One(10m, quantity), CustomerTier.Regular, null, false, QuotePolicy.Rules);
        act.Should().Throw<DomainException>().WithMessage("*Test item*");
    }

    [Theory]
    [InlineData(1)]   // T4 lower boundary
    [InlineData(99)]  // T5 interior
    [InlineData(100)] // T6 upper boundary, inclusive
    public void Valid_quantity_boundaries_are_accepted(int quantity)
    {
        Quoting.Calculate(One(0m, quantity), CustomerTier.Regular, null, false, QuotePolicy.Rules)
            .Parcels.Should().Be((quantity + 11) / 12);
    }

    [Fact]
    public void T7_Quantity_over_the_maximum_is_rejected()
    {
        Action act = () => Quoting.Calculate(One(0m, 101), CustomerTier.Regular, null, false, QuotePolicy.Rules);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void T8_Negative_unit_price_is_rejected()
    {
        Action act = () => Quoting.Calculate(One(-0.01m), CustomerTier.Regular, null, false, QuotePolicy.Rules);
        act.Should().Throw<DomainException>().WithMessage("*cannot be negative*");
    }

    [Fact]
    public void T9_Zero_unit_price_is_valid()
    {
        Quoting.Calculate(One(0m), CustomerTier.Regular, null, false, QuotePolicy.Rules)
            .Subtotal.Should().Be(0m);
    }

    [Theory]
    [InlineData(999.99, CustomerTier.Regular, 0.00)] // T10
    [InlineData(1000.00, CustomerTier.Regular, 0.00)] // T11, exact threshold
    [InlineData(1000.01, CustomerTier.Regular, 0.10)] // T12
    [InlineData(4999.99, CustomerTier.Regular, 0.10)] // T13
    [InlineData(5000.00, CustomerTier.Regular, 0.10)] // T14, exact large threshold
    [InlineData(5000.01, CustomerTier.Regular, 0.15)] // T15
    [InlineData(1000.01, CustomerTier.VIP, 0.15)]    // T16
    [InlineData(999.99, CustomerTier.VIP, 0.00)]     // T17
    public void Volume_discount_rate_follows_the_band_and_customer_tier(
        decimal subtotal, CustomerTier tier, decimal expectedRate)
    {
        Quoting.VolumeRate(subtotal, tier, QuotePolicy.Rules)
            .Should().Be(expectedRate, $"the policy assigns {expectedRate:P0} at subtotal {subtotal}");
    }

    [Theory]
    [InlineData(null, false)]       // T18
    [InlineData("", false)]         // T19
    [InlineData("OTHER", false)]    // T21
    [InlineData(" welcome ", true)] // T20
    public void Code_matching_ignores_case_and_surrounding_spaces(string? code, bool matches)
    {
        Quoting.PromoApplies(code, 200m, true, CustomerTier.Regular, QuotePolicy.Rules)
            .Should().Be(matches);
    }

    [Theory]
    [MemberData(nameof(PromoMinimumCases))]
    public void T22_T24_Promo_minimum_uses_the_variant_boundary(decimal subtotal, bool expected)
    {
        Quoting.PromoApplies("WELCOME", subtotal, true, CustomerTier.Regular, QuotePolicy.Rules)
            .Should().Be(expected, $"M is {QuotePolicy.Rules.PromoMinimum} and qualification is inclusive");
    }

    [Theory]
    [InlineData(CustomerTier.Regular, false, false)] // T26: not first, not VIP
    [InlineData(CustomerTier.VIP, false, true)]      // T25: VIP qualifies
    [InlineData(CustomerTier.Regular, true, true)]   // first order qualifies
    public void Promo_requires_a_first_order_or_vip_tier(CustomerTier tier, bool first, bool expected)
    {
        Quoting.PromoApplies("WELCOME", 150.01m, first, tier, QuotePolicy.Rules)
            .Should().Be(expected);
    }

    [Fact]
    public void T27_Promo_can_apply_without_a_volume_discount()
    {
        Quote quote = Quoting.Calculate(One(300m), CustomerTier.Regular, "WELCOME", true, QuotePolicy.Rules);
        quote.Discount.Should().Be(15m);
        quote.Net.Should().Be(285m);
    }

    [Fact]
    public void T25_Vip_earns_the_promo_at_the_minimum_even_when_the_order_is_not_first()
    {
        Quoting.PromoApplies("WELCOME", QuotePolicy.Rules.PromoMinimum, false, CustomerTier.VIP, QuotePolicy.Rules)
            .Should().BeTrue("VIP customers qualify and the minimum is inclusive");
    }

    [Theory]
    [MemberData(nameof(ParcelBoundaryCases))]
    public void Parcel_count_uses_each_started_variant_sized_parcel(int quantity, int expected)
    {
        Quoting.Parcels(quantity, QuotePolicy.Rules).Should().Be(expected,
            $"P is {QuotePolicy.Rules.ParcelSize}; every started parcel counts");
    }

    [Fact]
    public void T32_Quantities_from_multiple_lines_share_the_parcel_capacity()
    {
        var lines = new List<OrderLine>
        {
            new() { ProductName = "A", UnitPrice = 0m, Quantity = 6 },
            new() { ProductName = "B", UnitPrice = 0m, Quantity = 7 },
        };
        Quoting.Calculate(lines, CustomerTier.Regular, null, false, QuotePolicy.Rules).Parcels.Should().Be(2);
    }

    [Theory]
    [InlineData(1999.99, 50)] // T33 below F
    [InlineData(2000.00, 50)] // T34 at F, strict "over"
    [InlineData(2000.01, 0)] // T35 above F
    public void Shipping_is_free_only_above_the_variant_net_threshold(decimal net, decimal expected)
    {
        Quoting.ShippingFor(net, 1, QuotePolicy.Rules).Should().Be(expected,
            $"F is {QuotePolicy.Rules.FreeShippingThreshold} and the policy says over");
    }

    [Fact]
    public void T36_Half_cent_discount_rounds_away_from_zero()
    {
        Quote quote = Quoting.Calculate(One(1000.30m), CustomerTier.VIP, null, false, QuotePolicy.Rules);
        quote.Discount.Should().Be(150.05m, "150.045 is a half-cent and the policy rounds away from zero");
    }

    [Fact]
    public void T37_Vat_is_calculated_on_net_plus_shipping()
    {
        Quote quote = Quoting.Calculate(One(100m), CustomerTier.Regular, null, false, QuotePolicy.Rules);
        quote.Vat.Should().Be(30m, "20% applies to 100 net plus 50 shipping");
        quote.Total.Should().Be(180m);
    }

    [Fact]
    public void T38_The_first_policy_example_has_the_documented_complete_quote()
    {
        var lines = new List<OrderLine>
        {
            new() { ProductName = "Standing desk", UnitPrice = 499m, Quantity = 2 },
            new() { ProductName = "Office chair", UnitPrice = 189m, Quantity = 1 },
            new() { ProductName = "Stapler", UnitPrice = 12.90m, Quantity = 1 },
            new() { ProductName = "Paper clip", UnitPrice = 0.10m, Quantity = 1 },
        };
        Quoting.Calculate(lines, CustomerTier.Regular, "WELCOME", true, QuotePolicy.Rules)
            .Should().Be(new Quote(1200m, 180m, 1020m, 1, 50m, 214m, 1284m));
    }
}
