-- Ingestion module migration 003: reconciliation records (FR-111, BR-108).
-- Another additive migration (docs/03-architecture/04-data-strategy.md §4), not an edit to 001/002.

CREATE TABLE ingestion.reconciliation (
    reconciliation_id      uuid PRIMARY KEY,
    tenant_id               uuid NOT NULL,
    source_id               text NOT NULL,
    account_id              uuid NOT NULL,
    commodity                text NOT NULL,
    as_of_valid_time        timestamptz NOT NULL,
    as_of_decision_time     timestamptz NOT NULL,
    reported_minor_units    bigint NOT NULL,
    ledger_minor_units      bigint NOT NULL,
    discrepancy_minor_units bigint NOT NULL,
    is_reconciled            boolean NOT NULL,
    reconciled_at            timestamptz NOT NULL
);

CREATE INDEX idx_reconciliation_tenant_source ON ingestion.reconciliation (tenant_id, source_id);

-- INSERT/SELECT only — a reconciliation check is a fact about a point in time; BR-108 requires it
-- is never a silent adjustment, and it is also never silently rewritten after the fact.
GRANT SELECT, INSERT ON ingestion.reconciliation TO atlas_ingestion;
