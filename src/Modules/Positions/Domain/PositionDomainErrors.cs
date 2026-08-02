using Atlas.Kernel;

namespace Atlas.Modules.Positions.Domain;

public static class PositionDomainErrors
{
    public static readonly DomainError QuantityMustBePositive = DomainError.Of(
        "POSITIONS.QUANTITY_MUST_BE_POSITIVE", "A lot acquisition or disposal quantity must be positive.");

    // INV-041: a disposal may not exceed remaining lot quantity.
    public static readonly DomainError InsufficientQuantity = DomainError.Of(
        "POSITIONS.INSUFFICIENT_QUANTITY", "A disposal may not exceed the position's remaining quantity.");

    public static readonly DomainError CostCommodityMismatch = DomainError.Of(
        "POSITIONS.COST_COMMODITY_MISMATCH", "Unit cost or proceeds must be denominated in the position's cost commodity.");
}
