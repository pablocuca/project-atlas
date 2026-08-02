-- Ingestion module migration 002: fuzzy cross-source duplicate candidates (FR-110).
-- A new migration, not an edit to 001 — schema evolution is additive-only
-- (docs/03-architecture/04-data-strategy.md §4), the same discipline the ledger's own migrations
-- already follow.

CREATE TABLE ingestion.duplicate_candidate (
    candidate_id      uuid PRIMARY KEY,
    tenant_id         uuid NOT NULL,
    new_entry_id      uuid NOT NULL,
    existing_entry_id uuid NOT NULL,
    similarity        double precision NOT NULL,
    status            text NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'ConfirmedDuplicate', 'ConfirmedDistinct')),
    detected_at       timestamptz NOT NULL
);

CREATE INDEX idx_duplicate_candidate_tenant ON ingestion.duplicate_candidate (tenant_id, status);

-- INSERT/SELECT only, matching import_batch's posture — there is no resolution UI yet to update
-- `status` from 'Pending' (docs/decisions/0006's reasoning for CONFIRM applies here too: the write
-- path is real, the review surface is a later, separate concern). Adding a resolution flow means
-- adding UPDATE (status) here in a future migration, not editing this one.
GRANT SELECT, INSERT ON ingestion.duplicate_candidate TO atlas_ingestion;
