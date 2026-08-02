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

    private static readonly IReadOnlyDictionary<string, Commodity> KnownBySymbol =
        new Dictionary<string, Commodity> { [Brl.Symbol] = Brl, [Usd.Symbol] = Usd };

    // Round-trips a Commodity through its symbol for persistence layers, which store only the
    // symbol (docs/03-architecture/04-data-strategy.md §2.2 — posting.commodity is a bare text
    // column, deliberately: there is no commodity master-data table yet). Only resolves what
    // Atlas.Kernel statically knows today; a real lookup arrives with MarketData (M1/M2).
    public static Commodity BySymbol(string symbol) =>
        KnownBySymbol.TryGetValue(symbol, out var commodity)
            ? commodity
            : throw new ArgumentException(
                $"Unknown commodity symbol '{symbol}'. Only {string.Join(", ", KnownBySymbol.Keys)} are known " +
                "until a commodity master-data table exists.", nameof(symbol));
}
