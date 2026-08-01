# AI Strategy

**Status:** Ratified · **Owner:** AI Architect / CTO

> **The strategy in one sentence:** deterministic engines compute every number; language models
> render prose over validated facts and are structurally incapable of doing anything else.

---

## 1. Where AI is used, and where it is banned

| Use | Verdict | Rationale |
|---|---|---|
| Narrating a computed `FactSet` in plain language | ✅ **Permitted** | High value, zero numeric risk once traceability is enforced |
| Suggesting expense category classifications *(user confirms)* | ✅ Permitted | A proposal, never an assignment (INV-060) |
| Summarising an incident timeline from structured events | ✅ Permitted | No numeric generation |
| Drafting postmortem prose from a structured template | ✅ Permitted | Human edits and publishes |
| Explaining a `RuleTrace` in plain language | ✅ Permitted | Renders existing structure |
| Computing, estimating, or adjusting any number | ❌ **Banned** | Law 5, NG-09 |
| Producing a probability or projection | ❌ Banned | Non-deterministic, unreplayable, uncalibratable |
| Deciding what the user sees | ❌ Banned | The Signal Gate is deterministic and auditable |
| Ranking or selecting options | ❌ Banned | Pareto dominance is objective; an LLM ranking would be unexplainable |
| Classifying a driver as Controllable/Structural/Stochastic | ❌ Banned | Attribution must be reproducible |
| Agentic financial action | ❌ Banned | NG-03 — Atlas moves nothing |
| Retrieval over the ledger | ❌ Banned | Data minimisation; MR-9 makes it impossible |

**The pattern:** AI is permitted where its output is *prose about facts that already exist*, and
banned everywhere its output would be *a fact*.

---

## 2. Structural enforcement

Prompt engineering is not a control — prompts drift, models change, and a well-intentioned "just
this once" is undetectable in review. The boundary is enforced in the project graph:

```
Narrative.Application
  ├─ references → Advisory.Contracts, Attribution.Contracts, Reliability.Contracts
  └─ references → NOTHING ELSE                              (MR-9, architecture test)
```

The Narrative module **cannot** reference `Ledger`, `Forecast`, `Taxation`, or `Positions`. It could
not compute a financial number if instructed to, because it cannot obtain the inputs.

```
FactSet
  facts: [ { id, label, value, unit, displayString, provenance: ArtifactRef } ]
```

Facts arrive **pre-computed, pre-rounded, and pre-formatted**. The narration layer performs no
arithmetic — not even rounding or unit conversion (BR-902, INV-172).

---

## 3. Traceability verification

Every generated output passes a mechanical check **before** the user sees it:

```
1. Tokenise the generated text
2. Extract every numeric token (including formatted currency and percentages)
3. Resolve each against the FactSet's displayStrings and values
4. Any unresolved token  ⇒  NarrativeRejected
5. On rejection          ⇒  fall back to structured display, count the rejection
```

This is not a heuristic and not a model-based judge — it is exact string and value matching against a
closed set. A hallucinated figure **cannot** reach the user, because the only numbers that can appear
are the ones supplied.

`atlas_llm_narration_rejected_total` is monitored. A rising rejection rate signals prompt or model
drift and triggers a review.

### Per locale, and never translated
Narrative is **generated separately in pt-BR and en** (BR-B07), never machine-translated after
generation. Translating generated prose would break the traceability check outright: the numeric
tokens in a translation no longer match the `FactSet` displayStrings that produced them, so the
safety property would silently come to depend on a translator.

Two consequences: verification compares against the **locale-correct** `displayString`
(`1.234,56` in pt-BR, `1,234.56` in en — same fact, different token), and the golden corpus doubles.
A prompt or model change regresses both corpora (BR-904).

---

## 4. Privacy in the AI path

| Control | Detail |
|---|---|
| **Minimisation** | The `FactSet` contains the smallest set supporting the narrative — never a ledger dump (BR-906) |
| **No free text by default** | Transaction descriptions, goal names, and counterparty names are excluded. Where genuinely needed, they are delimited and marked untrusted |
| **No identifiers** | No CPF, account numbers, institution names, or tenant identifiers reach the provider |
| **No tools** | The narration model has no tool access, no retrieval, no write path. It is a text renderer |
| **No training** | Provider configured for zero data retention where offered; assessed before adoption |
| **Output containment** | Generated text is never executed, never persisted as a fact, never an input to any computation (BR-903) |

### Prompt injection
The primary vector would be a malicious string in an imported transaction description. It is closed
by exclusion: free text is not in the prompt. Where a future feature requires it, the text is
delimited, explicitly marked untrusted, and the traceability check still bounds the damage — an
injected instruction cannot cause a fabricated number to survive verification.

---

## 5. Model governance

| Concern | Practice |
|---|---|
| Provider | Behind an `INarrationProvider` port. Swappable in one registration |
| Version pinning | Explicit model version, never "latest" |
| Change control | A model or prompt change requires regression against the golden narrative corpus (BR-904) |
| Labelling | All generated content is labelled as generated, with the model identifiable (BR-905) |
| Removability | The entire module can be deleted with zero financial impact — proven by `no-frills build` |
| Cost | Narration is low-volume (a few generations per month). Negligible against the budget |

**Removability is the most important governance property.** If the AI layer becomes unreliable, too
expensive, or legally problematic, it can be removed entirely and Atlas remains fully functional.
That optionality is worth more than any capability the layer provides.

---

## 6. Where AI could be used later, and the bar it must clear

| Candidate | Bar it must clear |
|---|---|
| Better spending-category classification | Proposal only; user confirms; measurable accuracy improvement over rules |
| Anomaly detection on spending patterns | Must feed the deterministic Signal Gate, never the user directly |
| Parsing unstructured broker documents | Output must be verifiable against a reconciliation; never trusted blind |
| Conversational query over the FactSet | Must not gain ledger access; traceability check still applies |
| Regime classification for the return model | **Would require full determinism and versioning** — likely disqualifying |

The last row is instructive: a genuinely useful application (LLM-assisted regime labelling) is
probably ruled out, because AI-2 demands bit-identical replay years later and no hosted model can
guarantee that. **The reproducibility requirement is a stronger constraint on AI adoption than any
policy statement**, and it is the right constraint.

---

## 7. The position, stated plainly

Atlas is an **AI-assisted** system, not an AI system. The intelligence is in the domain model, the
stochastic engine, the tax rules, the attribution mathematics, and the calibration loop — all
deterministic, all auditable, all reproducible in twenty years.

The language model makes the output readable. That is a real contribution and it is treated with
respect. It is not the product, and the architecture ensures it can never quietly become the product.

---

**See also:** [ADR-0008](../03-architecture/adr/ADR-0008-llm-narrates-only.md) · [Security Strategy §6](../03-architecture/06-security-strategy.md) · [Product Philosophy Law 5](../00-foundation/03-product-philosophy.md)
