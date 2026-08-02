# Technical Debt Register

**Status:** Living · **Owner:** CTO · **Format:** [Technical Debt Strategy](../05-engineering/05-technical-debt-strategy.md) §2

First populated at the M0 wrap-up review (2026-08-02) — every item below was found by that review,
not before, which is itself worth noting: this register should have existed since Slice 1.

---

### TD-001 — No `atlas-export` / restore drill exists
**Incurred:** M0 · **Type:** Deliberate, prudent
**Principal:** ~2–3 days — `atlas export --full`, the re-import path, and CI verification per
`docs/03-architecture/04-data-strategy.md` §5
**Interest rate:** LOW today (only synthetic data exists); becomes CRITICAL the moment real personal
data is trusted to the system — "an export that has never been restored is not a backup" is not a
figure of speech in that doc.
**Symptom if unpaid:** no disaster-recovery position exists; the M0 DoD milestone item "Full export
→ clean restore → equality verified" cannot be checked off, only acknowledged as not yet applicable.
**Trigger to pay:** before any non-synthetic financial data is entered into any environment. Not
before M1 by calendar — before *real data*, specifically.
**Owner:** CTO · **Scheduled:** before real data, likely alongside or before M1

---

### TD-002 — Rule-coverage gate is a convention, not a CI gate
**Incurred:** M0 Slice 1 · **Type:** Deliberate, prudent (stated explicitly in `pr.yml`'s own header
comment since Slice 1)
**Principal:** ~1 day — parse `docs/02-domain/05-business-rules.md` for `BR-` identifiers, scan test
assemblies for `[BusinessRule("BR-nnn")]`, fail the build on any rule with no test
**Interest rate:** MEDIUM and rising — every `BR-` added from here without a test becomes invisible
debt instead of a build failure. Currently manageable because one person is tracking it by hand for
one module's ~10 rules; does not scale to a second module's worth of rules.
**Symptom if unpaid:** a business rule can silently ship untested; the only defence is human
discipline, which `docs/05-engineering/03-testing-strategy.md` itself treats as insufficient.
**Trigger to pay:** before M1 adds Ingestion's or Positions' business rules — a second module's rule
set is where hand-tracking stops being credible.
**Owner:** CTO · **Scheduled:** M1

---

### TD-003 — Chart-of-accounts depth (INV-022, ≤ 6) is not enforced
**Incurred:** M0 Slice 2 · **Type:** Inadvertent, prudent — noted at the call site
(`src/Modules/Ledger/Domain/Account.cs`'s `Open` factory comment) as out of scope, but never logged
**Principal:** ~2 hours — a recursive check (CTE or application-side parent-chain walk) before insert
**Interest rate:** LOW — nothing currently creates account hierarchies at all, let alone deep ones;
`atlas-seed`'s synthetic accounts are all flat (no `ParentId` set)
**Symptom if unpaid:** an accidentally deep hierarchy could be created with no rejection; nothing
downstream currently depends on the depth bound, so no visible symptom yet
**Trigger to pay:** before any bulk or hierarchical account-creation path ships (Ingestion's
chart-of-accounts import, M1, is the likely first candidate)
**Owner:** CTO · **Scheduled:** M1, if Ingestion imports a hierarchy; otherwise reassess then

---

### TD-004 — Local dev/CI Postgres credentials are literal strings, duplicated three times
**Incurred:** M0 Slices 2–3 · **Type:** Deliberate, prudent — reasoned explicitly at each site
(migration 001, `LedgerFixture.cs`, `Atlas.Host`'s `appsettings.json`) as "protects nothing of value,
not a secret in the NFR-402 sense"
**Principal:** ~1 day — Key Vault + managed identity per `docs/03-architecture/06-security-strategy.md`
§5, whenever real deployment happens; the duplication itself (3 copies of `atlas_ledger_dev_only`)
could be collapsed independently for ~15 minutes but wasn't, to avoid inventing new public API
surface for a throwaway value (see `docs/decisions/0004`'s reasoning, applied the same way here)
**Interest rate:** LOW while `Atlas.Host` stays local-only (Decision 0003 forbids network exposure
anyway, which is what actually protects this); would be CRITICAL immediately if anyone deployed
current code beyond localhost without fixing it first
**Symptom if unpaid:** none while the localhost-only constraint holds; a real secrets leak if it
doesn't
**Trigger to pay:** any Bicep/cloud-deployment slice — must be paid *before* that slice ships, not
discovered during it
**Owner:** CTO · **Scheduled:** before any cloud deployment slice

---

### TD-005 — Ten of twelve CI gates don't exist yet
**Incurred:** M0 Slice 1 · **Type:** Deliberate, prudent — stated explicitly in `pr.yml`'s header
comment since it was written
**Principal:** large, and correctly spread across future milestones — each gate needs the thing it
checks to exist first (golden files need Tax, M3; determinism needs Forecast, M3; Bicep what-if
needs Bicep; Flutter analyze needs the app; no-frills build needs Progression/Narrative to exist and
be removable; security scans are the one gate with no dependency and are worth adding on their own)
**Interest rate:** LOW currently — nothing the missing gates would check exists yet, so their absence
checks nothing that needs checking
**Symptom if unpaid:** none yet; would become real the day content exists for a gate that still
isn't wired up
**Trigger to pay:** per-gate — each one's trigger is "the slice that creates its first content."
Security scans (secrets/deps/SBOM) are the exception: no dependency, worth scheduling on its own.
**Owner:** CTO · **Scheduled:** security scans next; the rest ride with their content

---

### TD-006 — Registered commodities (equity tickers) live only in process memory
**Incurred:** M1 (Positions, FR-201/202) · **Type:** Deliberate, prudent — reasoned explicitly at the
call site (`docs/decisions/0009`)
**Principal:** large — a real commodity master-data table (persistence, jurisdiction/regulatory
metadata, listing/delisting lifecycle) is MarketData's own scope (FR-205+, M2), not a small add-on
**Interest rate:** LOW today — every registration is a fast, explicit, idempotent
`POST /positions/instruments` call an integrator makes before posting a trade referencing that
ticker; restarting `Atlas.Host` forgetting registrations is a non-issue while nothing depends on
process uptime for correctness. Becomes real the moment a genuine trade-import integration (an OFX
broker-statement adapter, or similar) is expected to work across a host restart without re-
registering every instrument it has ever seen.
**Symptom if unpaid:** `Commodity.BySymbol` throws for a previously-registered ticker after any
`Atlas.Host` restart, until re-registered.
**Trigger to pay:** MarketData's real commodity master-data table (FR-205+, M2) — this debt is paid
off by that table existing, not by hardening the in-memory registry further.
**Owner:** CTO · **Scheduled:** M2, alongside MarketData

---

**See also:** [Technical Debt Strategy](../05-engineering/05-technical-debt-strategy.md) ·
[Risk Register](01-risk-register.md) · [Definition of Done](../05-engineering/04-definition-of-done.md)
