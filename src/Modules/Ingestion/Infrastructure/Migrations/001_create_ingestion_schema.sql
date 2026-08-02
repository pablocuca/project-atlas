-- Ingestion module migration 001: schema, import_batch audit table, and the restricted role.
-- ImportBatch is a summary record for auditability (docs/02-domain/02-bounded-contexts.md, C16) —
-- per-row detail lives in the archived raw payload (blob) and in Ledger's own posted entries; this
-- table only tracks that a batch happened and its outcome counts.

CREATE SCHEMA IF NOT EXISTS ingestion;

CREATE TABLE ingestion.import_batch (
    batch_id           uuid PRIMARY KEY,
    tenant_id          uuid NOT NULL,
    source_id          text NOT NULL,
    blob_path          text NOT NULL,
    imported_at        timestamptz NOT NULL,
    rows_parsed        integer NOT NULL,
    entries_posted     integer NOT NULL,
    duplicates_skipped integer NOT NULL,
    parse_failures     integer NOT NULL,
    proposal_rejected  integer NOT NULL
);

CREATE INDEX idx_import_batch_tenant_source ON ingestion.import_batch (tenant_id, source_id);

-- Local/test-only credential — see Ledger's migration 001 for the identical reasoning.
CREATE ROLE atlas_ingestion LOGIN PASSWORD 'atlas_ingestion_dev_only';

GRANT USAGE ON SCHEMA ingestion TO atlas_ingestion;
GRANT SELECT, INSERT ON ingestion.import_batch TO atlas_ingestion;

-- No UPDATE, no DELETE — a batch record is a write-once audit trail. Not bitemporal like the
-- ledger (there's no "correction" concept for an import summary), but the same append-only
-- discipline applies for the same reason: an audit trail that can be edited isn't one.
