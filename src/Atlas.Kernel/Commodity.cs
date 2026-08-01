namespace Atlas.Kernel;

// docs/02-domain/04-domain-model.md §1.
public enum CommodityKind
{
    FiatCurrency,
    Equity,
    FiiQuota,
    FundQuota,
    FixedIncomeInstrument,
    PensionPlan,
    RealAsset,
    Crypto,

    // INV-013: excluded from forecast distributions and reported separately (NG-10).
    Unmodelled,
}

// Commodity := (symbol, kind, minorUnitScale, jurisdiction?). Currency is a Commodity.
public sealed record Commodity
{
    public string Symbol { get; }
    public CommodityKind Kind { get; }
    public int MinorUnitScale { get; }
    public string? Jurisdiction { get; }

    private Commodity(string symbol, CommodityKind kind, int minorUnitScale, string? jurisdiction)
    {
        Symbol = symbol;
        Kind = kind;
        MinorUnitScale = minorUnitScale;
        Jurisdiction = jurisdiction;
    }

    public static Commodity Create(string symbol, CommodityKind kind, int minorUnitScale, string? jurisdiction = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Commodity symbol must not be empty.", nameof(symbol));
        if (minorUnitScale < 0)
            throw new ArgumentException("Commodity minor-unit scale must not be negative.", nameof(minorUnitScale));

        return new Commodity(symbol, kind, minorUnitScale, jurisdiction);
    }

    public static readonly Commodity Brl = Create("BRL", CommodityKind.FiatCurrency, 2, "BR");
    public static readonly Commodity Usd = Create("USD", CommodityKind.FiatCurrency, 2, "US");
}
