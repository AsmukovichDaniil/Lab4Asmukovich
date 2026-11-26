# OrderFlow — quote policy (release 1.3)

From release 1.3 OrderFlow can produce a **quote**: the amounts an order would have under the commercial policy below, computed before the order is placed. The quote engine is delivered in this release as a domain component, `OrderFlow.Domain.Quoting`; the web page and the API operation that call it follow in the next release. This document is the specification the sales department approved. It replaces, for quotes only, the discount and shipping paragraphs of the product description; orders keep the old rules until the switch-over.

## Inputs and result

A quote is computed from four things:

- the **lines** of the intended order: for each line a product, its unit price from the catalogue, and a quantity;
- the customer's **tier**: Regular or VIP;
- the **promo code** the customer entered, if any;
- whether the order would be the customer's **first order**.

The result is a set of amounts: the **subtotal**, the **discount**, the **net** amount, the number of **parcels**, the **shipping** charge, the **VAT** and the **total**.

## Parameters

The engine is parameterised. The sales department sets the parameters per market; the values below are the defaults for the home market.

| Parameter | Meaning | Default |
|---|---|---|
| VolumeThreshold | The subtotal above which the volume discount starts | 1,000.00 |
| LargeOrderThreshold | The subtotal above which the large-order rate applies | 5,000.00 |
| VolumeRate | The discount rate over the volume threshold | 10 % |
| LargeOrderRate | The discount rate over the large-order threshold | 15 % |
| VipBonus | The points a VIP customer earns on top of a band rate | 5 points |
| PromoCode | The promo code in force | WELCOME |
| PromoRate | The rate the promo code earns | 5 % |
| PromoMinimum | The smallest subtotal on which the promo code counts | 200.00 |
| ParcelSize | The number of items that fit in one parcel | 20 |
| ParcelRate | The shipping charge per parcel | 50.00 |
| FreeShippingThreshold | The net amount above which shipping is free | 2,000.00 |
| VatRate | The VAT rate | 20 % |
| MaxQuantity | The largest quantity accepted on one line | 100 |

## Rules

**1. Lines.** Every line has a quantity between 1 and MaxQuantity, both included. A line whose quantity is outside that range makes the whole quote invalid: the engine refuses it with a domain error whose message names the product. Unit prices come from the catalogue and are never negative. An order without lines has an **empty quote**: every amount is zero and there are no parcels.

**2. Subtotal.** The subtotal is the sum over the lines of unit price × quantity.

**3. Volume discount.** The subtotal decides the band. Up to and including VolumeThreshold there is no volume discount. Over VolumeThreshold, up to and including LargeOrderThreshold, the VolumeRate applies. Over LargeOrderThreshold the LargeOrderRate applies. *Over* means strictly more than: a subtotal of exactly 1,000.00 earns no discount, a subtotal of 1,000.01 earns 10 %. A VIP customer earns the VipBonus on top of the band rate whenever a band rate applies: 15 % instead of 10 %, 20 % instead of 15 %. A VIP customer whose subtotal earns no band rate earns nothing.

**4. Promo code.** The promo code earns the PromoRate when all three of the following hold:

- the code entered matches PromoCode; letter case and spaces around the code do not matter, so `welcome` and ` WELCOME ` both match;
- the subtotal is at least PromoMinimum; a subtotal of exactly PromoMinimum qualifies;
- the order is the customer's first order, or the customer is VIP.

When the promo code earns its rate, the rate is added to the volume rate: a Regular customer's first order of 1,200.00 with the code earns 10 % + 5 % = 15 %.

**5. Discount and net.** The discount is the subtotal × (volume rate + promo rate), rounded to the cent. Half a cent rounds up, away from zero: a discount of 100.005 is 100.01. The net amount is the subtotal less the discount.

**6. Parcels.** The items of the order are packed ParcelSize to a parcel, and every started parcel counts: 20 items fill one parcel; 21 items need two. The number of items is the sum of the quantities over the lines.

**7. Shipping.** Shipping is free when the net amount is over FreeShippingThreshold: strictly more than, so a net amount of exactly 2,000.00 still pays shipping. Otherwise shipping is the ParcelRate for every parcel.

**8. VAT and total.** VAT is the VatRate × (net + shipping), rounded to the cent, half a cent up. The total is net + shipping + VAT.

## Examples

**Example 1.** A Regular customer's first order with the code WELCOME: 2 × Standing desk at 499.00, 1 × Office chair at 189.00, 1 × Stapler at 12.90, 1 × Paper clip at 0.10.

| Amount | Value | Because |
|---|---|---|
| Subtotal | 1,200.00 | 998.00 + 189.00 + 12.90 + 0.10 |
| Discount | 180.00 | over 1,000 → 10 %; the code counts (first order, 1,200 ≥ 200) → + 5 %; 15 % of 1,200 |
| Net | 1,020.00 | 1,200.00 − 180.00 |
| Parcels | 1 | 5 items, 20 to a parcel |
| Shipping | 50.00 | 1,020.00 is not over 2,000; one parcel at 50 |
| VAT | 214.00 | 20 % of 1,070.00 |
| Total | 1,284.00 | 1,020.00 + 50.00 + 214.00 |

**Example 2.** A VIP customer, not a first order, no code: 58 × Desk lamp at 89.90.

| Amount | Value | Because |
|---|---|---|
| Subtotal | 5,214.20 | 58 × 89.90 |
| Discount | 1,042.84 | over 5,000 → 15 %, VIP → 20 %; 20 % of 5,214.20 |
| Net | 4,171.36 | 5,214.20 − 1,042.84 |
| Parcels | 3 | 58 items, 20 to a parcel: two full parcels and a started one |
| Shipping | 0.00 | 4,171.36 is over 2,000 |
| VAT | 834.27 | 20 % of 4,171.36 = 834.272 |
| Total | 5,005.63 | 4,171.36 + 0.00 + 834.27 |
