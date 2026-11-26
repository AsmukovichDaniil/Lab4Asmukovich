namespace OrderFlow.Domain;

/// <summary>Parameters of the commercial policy applied to quotes (release 1.3). The rules are in kit/QUOTE_POLICY.md.</summary>
public sealed class QuoteRules
{
    public decimal VolumeThreshold { get; init; } = 1000m;
    public decimal LargeOrderThreshold { get; init; } = 5000m;
    public decimal VolumeRate { get; init; } = 0.10m;
    public decimal LargeOrderRate { get; init; } = 0.15m;
    public decimal VipBonus { get; init; } = 0.05m;
    public string PromoCode { get; init; } = "WELCOME";
    public decimal PromoRate { get; init; } = 0.05m;
    public decimal PromoMinimum { get; init; } = 200m;
    public int ParcelSize { get; init; } = 20;
    public decimal ParcelRate { get; init; } = 50m;
    public decimal FreeShippingThreshold { get; init; } = 2000m;
    public decimal VatRate { get; init; } = 0.20m;
    public int MaxQuantity { get; init; } = 100;
}

/// <summary>The amounts of a quote. An order without lines has an empty quote: every amount is zero.</summary>
public readonly record struct Quote(
    decimal Subtotal, decimal Discount, decimal Net, int Parcels, decimal Shipping, decimal Vat, decimal Total);

/// <summary>The quote engine: applies the commercial policy of release 1.3 to a list of lines.</summary>
public static class Quoting
{
    public static Quote Calculate(IReadOnlyCollection<OrderLine> lines, CustomerTier tier, string? promoCode, bool isFirstOrder,
        QuoteRules r)
    {
        if (lines is null)
            throw new ArgumentNullException(nameof(lines));
        Validate(lines, r);
        if (lines.Count == 0)
            return default;                                   // the empty quote

        decimal subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        decimal rate = VolumeRate(subtotal, tier, r);
        if (PromoApplies(promoCode, subtotal, isFirstOrder, tier, r))
            rate += r.PromoRate;
        decimal discount = Round(subtotal * rate);
        decimal net = subtotal - discount;
        int parcels = Parcels(lines.Sum(l => l.Quantity), r);
        decimal shipping = ShippingFor(net, parcels, r);
        decimal vat = Round((net + shipping) * r.VatRate);
        return new Quote(subtotal, discount, net, parcels, shipping, vat, net + shipping + vat);
    }

    /// <summary>The volume discount rate for a subtotal: the band rate, plus the VIP bonus when a band rate applies.</summary>
    public static decimal VolumeRate(decimal subtotal, CustomerTier tier, QuoteRules r)
    {
        decimal rate = subtotal >= r.LargeOrderThreshold ? r.LargeOrderRate
                     : subtotal > r.VolumeThreshold ? r.VolumeRate
                     : 0m;
        if (rate > 0m)
            rate += BonusFor(tier, r);
        return rate;
    }

    /// <summary>Whether the promo code earns its rate on this quote.</summary>
    public static bool PromoApplies(string? promoCode, decimal subtotal, bool isFirstOrder, CustomerTier tier, QuoteRules r) =>
        CodeMatches(promoCode, r) && subtotal > r.PromoMinimum && (isFirstOrder || tier == CustomerTier.VIP);

    /// <summary>The number of parcels for a total quantity: every started parcel counts.</summary>
    public static int Parcels(int totalQuantity, QuoteRules r)
    {
        if (totalQuantity <= 0)
            return 0;
        return (totalQuantity + r.ParcelSize) / r.ParcelSize;
    }

    /// <summary>Shipping for a net amount: free over the threshold, otherwise the parcel rate per parcel.</summary>
    public static decimal ShippingFor(decimal net, int parcels, QuoteRules r) =>
        net >= r.FreeShippingThreshold ? 0m : parcels * r.ParcelRate;

    private static bool CodeMatches(string? promoCode, QuoteRules r) =>
        !string.IsNullOrWhiteSpace(promoCode)
        && string.Equals(promoCode.Trim(), r.PromoCode, StringComparison.OrdinalIgnoreCase);

    private static decimal BonusFor(CustomerTier tier, QuoteRules r) => tier switch
    {
        CustomerTier.Regular => 0m,
        CustomerTier.VIP => r.VipBonus,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "unknown customer tier"),
    };

    private static void Validate(IReadOnlyCollection<OrderLine> lines, QuoteRules r)
    {
        foreach (var line in lines)
        {
            if (line.Quantity < 1 || line.Quantity >= r.MaxQuantity)
                throw new DomainException($"The quantity of {line.ProductName} must be between 1 and {r.MaxQuantity}.");
            if (line.UnitPrice < 0m)
                throw new DomainException($"The unit price of {line.ProductName} cannot be negative.");
        }
    }

    private static decimal Round(decimal v) => Math.Round(v, 2);
}
