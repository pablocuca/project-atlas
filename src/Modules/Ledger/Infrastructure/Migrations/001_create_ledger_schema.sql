-- Ledger module migration 001: schema, tables, balance trigger, indexes, and the restricted role.
-- Naming convention: ledger/{nnn}_{description}.sql (docs/05-engineering/01-repository-structure.md).
-- Schema shape and design rationale: docs/03-architecture/04-data-strategy.md §2.2-2.3,
-- corrected per docs/decisions/0002-append-only-schema.md (no decision_to, no UPDATE, ever).

CREATE SCHEMA IF NOT EXISTS ledger;

CREATE TABLE ledger.account (
    account_id  uuid PRIMARY KEY,
    tenant_id   uuid NOT NULL,
    code        text NOT NULL,
    name        text NOT NULL,
    type        text NOT NULL CHECK (type IN ('Asset', 'Liability', 'Equity', 'Income', 'Expense')),
    commodity   text NOT NULL,
    parent_id   uuid REFERENCES ledger.account (account_id),
    opened_at   timestamptz NOT NULL,
    closed_at   timestamptz,
    UNIQUE (tenant_id, code) -- INV-022: codes unique per tenant, enforced by the database.
);

CREATE TABLE ledger.journal_entry (
    entry_id        uuid PRIMARY KEY,
    tenant_id       uuid NOT NULL,
    valid_time      timestamptz NOT NULL,
    decision_time   timestamptz NOT NULL,
    kind            text NOT NULL CHECK (kind IN ('Original', 'Reversal', 'Replacement')),
    corrects_entry  uuid REFERENCES ledger.journal_entry (entry_id),
    idempotency_key text NOT NULL,
    source_id       text NOT NULL,
    description     text NOT NULL,
    UNIQUE (tenant_id, source_id, idempotency_key) -- BR-103: the entire duplicate-import defence.
);

CREATE TABLE ledger.posting (
    posting_id  bigserial PRIMARY KEY,
    entry_id    uuid NOT NULL REFERENCES ledger.journal_entry (entry_id),
    account_id  uuid NOT NULL REFERENCES ledger.account (account_id),
    commodity   text NOT NULL,
    minor_units bigint NOT NULL, -- signed; + debit, − credit. No numeric/decimal/money type (ADR-0003).
    lot_ref     uuid
);

-- BR-100 as a database-level backstop (Domain already enforces it; this catches anything that
-- reaches the database some other way, e.g. a future direct-SQL tool).
CREATE FUNCTION ledger.check_entry_balances_zero() RETURNS trigger AS $$
DECLARE
    unbalanced_commodity text;
BEGIN
    SELECT p.commodity INTO unbalanced_commodity
    FROM ledger.posting p
    WHERE p.entry_id = NEW.entry_id
    GROUP BY p.commodity
    HAVING SUM(p.minor_units) <> 0
    LIMIT 1;

    IF unbalanced_commodity IS NOT NULL THEN
        RAISE EXCEPTION 'BR-100 violation: entry % does not balance to zero in commodity %',
            NEW.entry_id, unbalanced_commodity;
    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE CONSTRAINT TRIGGER trg_posting_balance_check
    AFTER INSERT ON ledger.posting
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW
    EXECUTE FUNCTION ledger.check_entry_balances_zero();

-- account_id lives on posting, tenant_id/valid_time on journal_entry — there is no single table
-- carrying all three, so the "(tenant_id, account_id, valid_time)" index named in data-strategy.md
-- §2.3 is realised as this pair instead.
CREATE INDEX idx_journal_entry_tenant_valid_time ON ledger.journal_entry (tenant_id, valid_time);
CREATE INDEX idx_posting_account_id ON ledger.posting (account_id);
CREATE INDEX idx_posting_entry_id ON ledger.posting (entry_id);

-- Naturally append-ordered by decision_time; BRIN costs almost nothing (data-strategy.md §2.3).
CREATE INDEX idx_journal_entry_decision_time ON ledger.journal_entry USING BRIN (decision_time);

-- The restricted role. LOGIN + password here is deliberately local/test-only — Testcontainers spins
-- up an ephemeral, throwaway database per test run, so this credential protects nothing of value.
-- Real deployment provisions this role's credential via Key Vault + managed identity
-- (docs/03-architecture/06-security-strategy.md §5), which is Slice 3+ (Bicep) scope, not this one.
CREATE ROLE atlas_ledger LOGIN PASSWORD 'atlas_ledger_dev_only';

GRANT USAGE ON SCHEMA ledger TO atlas_ledger;
GRANT SELECT, INSERT ON ledger.account, ledger.journal_entry, ledger.posting TO atlas_ledger;
GRANT USAGE, SELECT ON SEQUENCE ledger.posting_posting_id_seq TO atlas_ledger;

-- ledger.account is the one narrow exception to "insert-only": closing an account is a genuine,
-- one-way lifecycle transition (INV-021), not a belief revision the way a journal entry correction
-- is — there's no "preserve both old and new belief" requirement for it. A column-level grant on
-- exactly closed_at allows that transition while making BR-105 (account type immutable) a database
-- fact, not just a Domain-code promise: code, name, type, commodity, opened_at cannot be updated by
-- this role, full stop.
GRANT UPDATE (closed_at) ON ledger.account TO atlas_ledger;

-- Explicitly never granted, on any ledger table: UPDATE beyond the one column above, DELETE,
-- TRUNCATE. This is NFR-705 and Decision 0002's "Type + DB permission" mechanism made real, not a
-- convention — the grants that would allow broader mutation simply do not exist.
