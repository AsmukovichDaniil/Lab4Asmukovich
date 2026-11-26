using OrderFlow.Domain;

namespace OrderFlow.Domain.Tests;

/// <summary>Factory methods for the order lines the tests work with. Every call returns a fresh list.</summary>
public static class Orders
{
    /// <summary>2 × Standing desk, 1 × Office chair, 1 × Stapler, 1 × Paper clip: subtotal 1,200.00.</summary>
    public static List<OrderLine> Standard() => Lines(
        ("Standing desk", 499.00m, 2),
        ("Office chair", 189.00m, 1),
        ("Stapler", 12.90m, 1),
        ("Paper clip", 0.10m, 1));

    public static List<OrderLine> Lines(params (string Name, decimal UnitPrice, int Quantity)[] lines) =>
        lines.Select(l => new OrderLine { ProductName = l.Name, UnitPrice = l.UnitPrice, Quantity = l.Quantity }).ToList();
}
