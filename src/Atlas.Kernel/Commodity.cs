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

    // Decision 0009: mutable, thread-safe, additive-only. Positions (C8, FR-201/202) needs Ledger to
    // be able to post and re-hydrate tradable-instrument commodities (e.g. individual equity
    // tickers) that Atlas.Kernel cannot enumerate statically the way BRL/USD are — a real commodity
    // master-data table is still MarketData's (M1/M2), not Kernel's, to own. Register lets a caller
    // introduce a symbol once; every later BySymbol call (including Ledger's own row rehydration)
    // resolves it identically.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Commodity> KnownBySymbol =
        new([new KeyValuePair<string, Commodity>(Brl.Symbol, Brl), new KeyValuePair<string, Commodity>(Usd.Symbol, Usd)]);

    // Idempotent for an identical redefinition (the common case: a module registering the same
    // instrument on every startup); throws if a symbol is re-registered with different fields, since
    // that would silently change what every already-persisted row referencing that symbol means.
    public static void Register(Commodity commodity)
    {
        var stored = KnownBySymbol.GetOrAdd(commodity.Symbol, commodity);
        if (stored != commodity)
            throw new ArgumentException(
                $"Commodity '{commodity.Symbol}' is already registered with different fields.", nameof(commodity));
    }

    // Round-trips a Commodity through its symbol for persistence layers, which store only the
    // symbol (docs/03-architecture/04-data-strategy.md §2.2 — posting.commodity is a bare text
    // column, deliberately: there is no commodity master-data table yet). Resolves anything
    // Atlas.Kernel knows statically (BRL/USD) plus anything a module has Registered.
    public static Commodity BySymbol(string symbol) =>
        KnownBySymbol.TryGetValue(symbol, out var commodity)
            ? commodity
            : throw new ArgumentException(
                $"Unknown commodity symbol '{symbol}'. Only {string.Join(", ", KnownBySymbol.Keys)} are known " +
                "until it is registered via Commodity.Register.", nameof(symbol));
}
