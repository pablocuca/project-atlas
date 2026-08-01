# Product Philosophy — The Fourteen Laws

**Status:** Ratified · **Owner:** CTO / Product · **Last reviewed:** 2026-08-01

Every feature, card, endpoint, and metric in Atlas is judged against these laws. A feature that
violates one does not ship, regardless of how much it is wanted. Laws change only by ADR.

---

### Law 1 — Every surface answers a question

No card, chart, or number exists without a written question it answers. The question is stored
**with** the component and is visible to the user on demand.

> ❌ `Portfolio · R$ 560.000`
> ✅ `Can I currently cover 6 months of essential spending without selling equities? · Yes, 8.2 months`

If you cannot write the question, the number does not belong on the screen.

---

### Law 2 — Attribution precedes display

A change is never shown as a change until its cause is decomposed into **Controllable**,
**Structural**, or **Stochastic**. Stochastic movement is never an event, never an alert, never a
headline. It is variance, and variance consumes an error budget.

This law exists because the naive version of this product — "today's return moved your FI date by
4 days" — is an anxiety generator that teaches users to react to noise. It is the single most
likely way Atlas fails as a product.

---

### Law 3 — The ledger is the only truth

Everything else — valuations, forecasts, scores, streaks, recommendations — is a **projection**
and is disposable. Any projection must be rebuildable from the ledger and market data alone. If a
projection cannot be rebuilt, it is a bug.

---

### Law 4 — Nothing is ever overwritten

The ledger is append-only and **bitemporal**: it records both when a fact was true in the world
and when Atlas learned it. Corrections are new entries, never mutations. Without this, "what
changed since yesterday" is unanswerable, because late-arriving broker data silently rewrites the
past.

---

### Law 5 — Determinism computes, language narrates

An LLM never produces, adjusts, rounds, or infers a number. The deterministic engine computes a
structured, validated fact set; the language layer renders it into prose where **every sentence is
traceable to a computed value**. Untraceable prose is blocked at generation time, not reviewed
after.

---

### Law 6 — Every forecast is an immutable artifact

A forecast is content-addressed by `(twin snapshot hash, model version, parameter set, seed)` and
stored forever. This is what allows Atlas to distinguish *your life changed* from *we shipped a
new model* — and it is what makes calibration possible at all.

---

### Law 7 — The system grades itself in public

Forecast calibration runs permanently and is displayed. When reliability degrades below its SLO,
Atlas **suppresses its own recommendations** and says so. A model that cannot be wrong cannot be
trusted.

---

### Law 8 — Uncertainty is shown, never hidden

Point estimates are forbidden as sole outputs. Every probability carries a band; every projection
carries a distribution; every assumption is one click from the number it produced. Where model
uncertainty is unquantified, the system says "unquantified" rather than implying zero.

---

### Law 9 — Silence is a feature

The default state of Atlas is quiet. Notification is earned by crossing a statistical significance
gate *and* an SLO burn-rate gate. A system that speaks every day trains the user to ignore it,
which is worse than a system that never speaks.

---

### Law 10 — Optimise the user's time to zero

Success is the user thinking about money **less**, not more. Any feature whose value depends on
frequent visits is suspect. Any feature that reduces required attention while preserving control
is favoured.

---

### Law 11 — Gamify process, never outcomes

Progression may reward only behaviours **fully under the user's control** and **causally linked to
FI**. Never returns. Never portfolio value. A score that drops because the market dropped punishes
the user for noise and destroys trust in the entire scoring system.

---

### Law 12 — Taxes are core domain

In Brazil, tax drag and withdrawal sequencing dominate the FI date. Any component that computes a
future value without routing through the tax engine is wrong by construction and must fail a test.

---

### Law 13 — The data outlives the platform

At any moment, a single command must export the complete ledger, twin history, and forecast
archive into an open, documented, vendor-neutral format. Azure, .NET, and Flutter are replaceable.
The data is not.

---

### Law 14 — Decision support, not advice

Atlas presents **evaluated options with quantified tradeoffs and stated unknowns**. It ranks; it
does not instruct. It never claims to know the user's full circumstances, and it states explicitly
what it does not model. This is both an ethical and a legal boundary — see
[Compliance & Legal Posture](../06-governance/03-compliance-and-legal-posture.md).

---

## Applying the laws

Every pull request template carries a **Law Check**. A reviewer may block on any law with no
further justification required. Violations that are accepted deliberately become entries in the
[Technical Debt Register](../05-engineering/05-technical-debt-strategy.md) with an explicit
interest rate and paydown date — never silent exceptions.

---

**See also:** [Non-Goals](04-non-goals.md) · [Dashboard Strategy](../01-product/05-dashboard-strategy.md) · [AI Strategy](../06-governance/02-ai-strategy.md)
