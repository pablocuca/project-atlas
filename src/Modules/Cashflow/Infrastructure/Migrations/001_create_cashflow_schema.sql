-- Cashflow module migration 001: schema, classification_decision table, and the restricted role.
-- INV-060: classification is a versioned, audited user decision — enforced at the database
-- permission level exactly like Ingestion's import_batch and Ingestion's reconciliation tables:
-- no UPDATE, no DELETE. Reclassifying a category means inserting a new row, never touching an old
-- one, so the audit trail cannot be edited even by a bug, let alone a person.

CREATE SCHEMA IF NOT EXISTS cashflow;

CREATE TABLE cashflow.classification_decision (
    decision_id         uuid PRIMARY KEY,
    tenant_id           uuid NOT NULL,
    category_account_id uuid NOT NULL,
    classification      text NOT NULL,
    rationale           text,
    decided_at          timestamptz NOT NULL
);

CREATE INDEX idx_classification_decision_category ON cashflow.classification_decision (tenant_id, category_account_id);

-- Local/test-only credential — see Ledger's migration 001 for the identical reasoning.
CREATE ROLE atlas_cashflow LOGIN PASSWORD 'atlas_cashflow_dev_only';

GRANT USAGE ON SCHEMA cashflow TO atlas_cashflow;
GRANT SELECT, INSERT ON cashflow.classification_decision TO atlas_cashflow;
