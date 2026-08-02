-- Positions module migration 001: schema, position/lot/disposal tables, and the restricted role.
-- ADR-0018: Positions is a projection over the Ledger, fully rebuildable — conventional state
-- persistence with no independent history, not event-sourced. A sync replaces a tenant+commodity's
-- entire position row set (position, its lots, its disposals) atomically; there is no append-only
-- requirement here the way there is for ledger.journal_entry.

CREATE SCHEMA IF NOT EXISTS positions;

CREATE TABLE positions.position (
    position_id             uuid PRIMARY KEY,
    tenant_id               uuid NOT NULL,
    commodity               text NOT NULL,
    cost_commodity          text NOT NULL,
    quantity                numeric NOT NULL,
    cost_basis_minor_units  bigint NOT NULL,
    synced_at               timestamptz NOT NULL,
    UNIQUE (tenant_id, commodity)
);

CREATE TABLE positions.lot (
    lot_id            uuid PRIMARY KEY,
    position_id       uuid NOT NULL REFERENCES positions.position (position_id) ON DELETE CASCADE,
    quantity          numeric NOT NULL,
    unit_cost_minor_units bigint NOT NULL,
    acquired_at       timestamptz NOT NULL,
    source_entry_id   uuid NOT NULL
);

CREATE INDEX idx_lot_position ON positions.lot (position_id);

CREATE TABLE positions.disposal (
    disposal_id       uuid PRIMARY KEY,
    position_id       uuid NOT NULL REFERENCES positions.position (position_id) ON DELETE CASCADE,
    quantity          numeric NOT NULL,
    proceeds_minor_units bigint NOT NULL,
    disposed_at       timestamptz NOT NULL,
    source_entry_id   uuid NOT NULL
);

CREATE INDEX idx_disposal_position ON positions.disposal (position_id);

-- Local/test-only credential — see Ledger's migration 001 for the identical reasoning.
CREATE ROLE atlas_positions LOGIN PASSWORD 'atlas_positions_dev_only';

GRANT USAGE ON SCHEMA positions TO atlas_positions;
GRANT SELECT, INSERT, UPDATE, DELETE ON positions.position, positions.lot, positions.disposal TO atlas_positions;
