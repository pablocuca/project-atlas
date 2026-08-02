namespace Atlas.Modules.Ingestion.Domain;

// FR-107: "user-defined column mapping." No UI exists yet to build this interactively (Slice 1),
// so it's supplied directly by whoever runs the import — the mechanism is what's being proven here,
// not the UI around it.
//
// PrimaryAccountId is the account this statement belongs to (e.g. Checking); UnclassifiedAccountId
// is the counter-account every row posts against until FR-301 (expense classification, a separate
// slice, a separate bounded context) exists to reclassify it — a standard suspense-account pattern,
// not a workaround.
public sealed record ColumnMapping(
    Guid PrimaryAccountId,
    Guid UnclassifiedAccountId,
    string Commodity,
    int DateColumnIndex,
    int DescriptionColumnIndex,
    int AmountColumnIndex,
    bool HasHeaderRow);
