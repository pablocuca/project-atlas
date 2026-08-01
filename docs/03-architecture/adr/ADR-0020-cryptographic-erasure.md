# ADR-0020 — Cryptographic erasure to reconcile LGPD with an append-only ledger

**Status:** Accepted · **Date:** 2026-08-01 · **Deciders:** CTO, Security

## Context

Two requirements are in genuine, direct conflict:

- **ADR-0002** requires the ledger to be append-only with no deletion path. Deleting truth destroys
  auditability and makes every historical statement unverifiable.
- **LGPD Art. 18** grants the data subject a right to deletion of personal data.

Pretending this conflict does not exist is how compliance debt is created. It needs a technical
resolution, not a procedural promise.

## Decision

Implement **cryptographic erasure**. Personal identifiers (CPF, account numbers, institution
identifiers, source tokens, free-text descriptions) are encrypted with a **tenant-scoped data key**
held in Key Vault. Erasure destroys the key. The ciphertext remains, structurally intact and
permanently unreadable.

Structural ledger data — amounts, dates, account relationships, commodity types — is not personal
data on its own once identifiers are unrecoverable, and is retained.

## Rationale

- Ciphertext without a key is, on the prevailing regulatory reading, no longer personal data.
- The guarantee is **technical and instantaneous**, not a promise that a deletion job ran correctly
  across backups, replicas, and archives — which is exactly where row-deletion approaches fail.
- Backups are covered automatically: they contain the same unreadable ciphertext.
- The ledger's structural integrity, and therefore every historical audit, survives.

## Alternatives considered

| Alternative | Pros | Cons | Why rejected |
|---|---|---|---|
| Hard row deletion | Unambiguous | Destroys ledger integrity; cannot reach backups reliably; breaks ADR-0002 | Direct conflict with the system of record |
| Anonymisation in place | Retains structure | Mutates append-only truth; re-identification risk from behavioural patterns | Violates immutability; weaker guarantee |
| Refuse deletion, cite legal basis | Simplest | Legally fragile; ethically poor for a personal data store | Not defensible |
| Full-database encryption only | Simple | All-or-nothing; cannot erase one subject | Insufficient granularity |

## Consequences

### Positive
- LGPD compliance without compromising the system of record.
- Backups and archives are covered by construction.
- Field-level encryption also raises the baseline security posture (defence against DB exfiltration).

### Negative — accepted costs
- Key management becomes critical: **losing the tenant key is equivalent to erasure.** Key backup,
  rotation, and recovery are runbooked and drilled.
- Encrypted columns cannot be indexed or searched directly. Mitigated with deterministic
  blind-index columns for the few fields that require lookup.
- Slight performance cost on read paths touching identifiers.

## Reversal cost

**High.** Encryption boundaries are structural. This must be designed in from the first migration,
which is why it is decided now rather than when a deletion request arrives.

## Compliance

BR-A04. Key Vault holds the wrapped tenant key. Erasure is a runbooked procedure with a mandatory
confirmation step and an audit record.

## References
[Security Strategy §4](../06-security-strategy.md) · [Compliance & Legal Posture](../../06-governance/03-compliance-and-legal-posture.md)
