using Atlas.Kernel;

namespace Atlas.Modules.Ingestion.Domain;

public static class IngestionDomainErrors
{
    public static readonly DomainError ZeroAmountRow = DomainError.Of(
        "INGESTION.ZERO_AMOUNT_ROW", "A row with a zero amount cannot become a balanced entry.");

    public static readonly DomainError EmptyPayload = DomainError.Of(
        "INGESTION.EMPTY_PAYLOAD", "The raw payload has no content to parse.");
}
