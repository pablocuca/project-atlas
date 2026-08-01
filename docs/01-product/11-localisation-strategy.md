# Localisation Strategy

**Status:** Ratified · **Owner:** Product / UX Architect · **Last reviewed:** 2026-08-01

> Atlas ships a **bilingual product surface** (pt-BR, en) over an **English-only specification and
> codebase**. This document defines exactly where the boundary sits, and the rules that keep
> translation from silently breaking correctness or the legal posture.

---

## 1. Scope

| Surface | Bilingual? | Default | Notes |
|---|---|---|---|
| App UI (labels, cards, navigation) | ✅ | pt-BR | Runtime switchable |
| Card questions and interpretations | ✅ | pt-BR | Law 1 applies in both languages |
| Change events and driver names | ✅ | pt-BR | Driver *keys* are English; display names are localised |
| Options and their disclosures | ✅ | pt-BR | **Copy lint runs per locale** — see §5 |
| Narrative prose (LLM) | ✅ | pt-BR | Separate golden corpus per locale |
| Notifications | ✅ | pt-BR | Follows the user's active locale |
| Runbooks and postmortem templates | ✅ | pt-BR | |
| Error and validation messages | ✅ | pt-BR | Error **codes** are English and stable |
| Rule traces (tax explanations) | ✅ | pt-BR | Rule *keys* English; labels localised |
| Legal disclaimers | ✅ | pt-BR | See §7 — pt-BR is legally operative |
| **Specification library (`docs/`)** | ❌ | en | Single source of truth. No translation, no drift |
| **Code identifiers, namespaces, types** | ❌ | en | Universal practice; required if open-sourced |
| **Requirement / rule identifiers** | ❌ | en | `FR-`, `BR-`, `INV-` are language-neutral tokens |
| **Commit messages, ADRs, PRs** | ❌ | en | |
| **Export `SCHEMA.md` and manifests** | ❌ | en | Technical interchange format |
| **Telemetry, logs, span names** | ❌ | en | Operational, machine-consumed |
| **Tax ruleset legal citations** | ❌ | pt-BR only | They cite Brazilian statutes — see §6 |

---

## 2. The three-tier term policy

The single most important rule in this document, because the instinct under deadline is to translate
everything.

### Tier 1 — Regulatory terms: **never translated, in any locale**

`come-cotas` · `tabela regressiva` · `isenção` · `DARF` · `IRRF` · `ganho de capital` ·
`custo médio` · `PGBL` · `VGBL` · `FGTS` · `JCP` · `nota de corretagem` · `saldo devedor` ·
`SAC` · `Price` · `CDI` · `SELIC` · `IPCA` · `IGP-M`

These appear **verbatim in the English UI**, with an English gloss on first use:

> *Come-cotas withholding applied — R$ 412,80*
> *(come-cotas: mandatory semi-annual withholding on open-ended Brazilian funds)*

**Why.** A regulatory term is a pointer to a legal referent. "Semi-annual fund withholding" is not
*come-cotas* — it is a description that happens to resemble it, and the moment the law changes the
description drifts while the term does not. Translating it would be the same category of error as
translating *habeas corpus*.

### Tier 2 — Domain terms: translated, with a fixed canonical pair

| English (canonical) | pt-BR | Notes |
|---|---|---|
| Financial Independence (FI) | Independência Financeira (IF) | **FI-days → dias-IF** |
| FI Date | Data de IF | |
| Freedom Ratio | Índice de Liberdade | |
| Spending Floor | Piso de Gastos | |
| Confidence Target | Meta de Confiança | |
| Health Score | Índice de Saúde | |
| Controllable / Structural / Stochastic | Controlável / Estrutural / Estocástico | Driver classes |
| Discipline Index | Índice de Disciplina | |
| Discretionary Error Budget | Orçamento de Erro Discricionário | |
| Emergency Coverage | Cobertura de Emergência | |
| Change Event | Evento de Mudança | |
| Ranked Option | Opção Avaliada | **Not** "Recomendação" — see §5 |
| Digital Twin | Gêmeo Digital | |

The pairing is **fixed and versioned** in the translation catalogue. Changing an established
translation requires the same review as renaming a domain term (see
[Ubiquitous Language](../00-foundation/05-ubiquitous-language.md)) — a synonym appearing in the UI
is the same defect in Portuguese as in English.

### Tier 3 — SRE vocabulary: **English acronyms inside Portuguese prose**

`SLI` · `SLO` · `error budget` · `burn rate` · `runbook` · `postmortem` · `SEV-1..4` · `incident`

> *SLO de taxa de poupança violado por 3 meses. Burn rate de 1,21× — o error budget se esgota em
> outubro. Runbook RB-FIN-03 anexado.*

**Why.** This is how Brazilian engineers actually speak, and it is the vocabulary the user already
owns. Forcing *"orçamento de erro"* into prose would be a translation that makes the text *less*
comprehensible to its only reader — the classic localisation failure of translating the jargon and
losing the audience.

**Exception:** "Discretionary Error Budget" (Tier 2) *is* translated, because it is a user-facing
financial concept rather than an operational one. The line is: **operational vocabulary stays
English; financial vocabulary is translated.**

---

## 3. Locale is a server concern

The client performs **no formatting** ([Coding Standards §7](../05-engineering/02-coding-standards.md)).
Bilingual support therefore moves locale into the request context and the `FactSet` contract:

```
Fact
  id, label{locale}, value, unit
  displayString{locale}          ← locale-keyed, formatted server-side
  provenance: ArtifactRef
```

```
Request context carries: locale ∈ { pt-BR, en }
```

This is the **right** outcome, not a compromise:

- Number and currency formatting is tested server-side against golden files, where the test
  infrastructure already lives — instead of being a formatting bug discovered in a screenshot.
- "No business logic in the client" (a load-bearing rule for a possible future client rewrite)
  survives intact.
- The traceability check that rejects untraceable numeric tokens (BR-901) can compare against the
  locale-correct `displayString` rather than guessing at formats.

### Formatting

| | pt-BR | en |
|---|---|---|
| Number | `1.234,56` | `1,234.56` |
| Currency | `R$ 1.234,56` | `R$ 1,234.56` |
| Percent | `8,4%` | `8.4%` |
| Date | `01/08/2026` | `2026-08-01` |
| Month-year | `ago/2026` | `Aug 2026` |
| Large amounts | `R$ 1,2 mi` · `R$ 340 mil` | `R$ 1.2M` · `R$ 340K` |
| Duration | `41 dias-IF` | `41 FI-days` |
| List separator | `;` in CSV export | `,` |

> ⚠️ **Language and currency are orthogonal.** The English locale still displays **BRL**. Atlas does
> not convert currency for display, ever — a converted figure is a different number, and silently
> showing one would violate Law 8 and break every reconciliation the user might attempt. English is
> a *language* setting, not a *jurisdiction* setting.

### Message construction

**String concatenation for user-facing text is banned.** Portuguese gender and number agreement make
concatenated fragments wrong in ways English does not reveal:

```
❌  "$count " + t("suppressed_movements")        // "14 movimento suprimidos"
✅  ICU: "{count, plural, one {# movimento suprimido} other {# movimentos suprimidos}}"
```

All user-facing strings are full ICU messages with placeholders. A translation catalogue entry
containing a partial sentence fails review.

---

## 4. Which language, when

| Context | Locale used |
|---|---|
| App UI | User's active setting; defaults to device locale, falling back to pt-BR |
| Push notification | User's active setting at dispatch time |
| Narrative generation | User's active setting; generated per locale, never translated after generation |
| Rule trace | User's active setting |
| Export | English filenames and `SCHEMA.md`; data values unchanged |
| Logs, traces, metrics | English, always |
| Domain error codes | English, always. The *message* is localised at the edge |

**Narrative is generated per locale, never machine-translated after the fact.** Translating generated
prose would break the traceability check (BR-901) — the numeric tokens in the translation would no
longer match the `FactSet` displayStrings that produced them, and the safety property would silently
depend on a translator.

---

## 5. The copy lint runs per locale *(compliance-critical)*

[ADR-0022](../03-architecture/adr/ADR-0022-advice-posture.md) keeps Atlas outside the CVM advice
perimeter, and that boundary is enforced mechanically by a copy lint. **A lint with only an English
banned list would hold the boundary in English and leak it entirely in Portuguese.**

Every banned category is maintained per locale:

| Category | en | pt-BR |
|---|---|---|
| Imperative advice | "you should", "buy", "sell", "invest in" | "você deve", "você deveria", "compre", "venda", "invista em", "aplique em" |
| Urgency | "act now", "don't miss", "last chance", "hurry" | "aja agora", "não perca", "última chance", "corra", "aproveite" |
| Guarantee | "guaranteed", "safe", "risk-free", "certain" | "garantido", "seguro", "sem risco", "certo", "livre de risco" |
| Prescription framing | "recommendation", "we recommend" | "recomendação", "recomendamos", "indicamos", "sugerimos que você" |

Note the last row: **`Ranked Option` translates to `Opção Avaliada`, never `Recomendação`.**
"Recomendação" carries a specific connotation in Brazilian financial regulation that the English
"option" does not, and a well-meaning translator would choose it. This is exactly the kind of drift
the lint exists to catch.

**Rule:** adding a locale requires adding its banned lists first. A locale with no banned list fails
the build (BR-B04).

---

## 6. Tax rulesets stay Portuguese

Ruleset legal citations (`legalBasis`) reference Brazilian statutes and remain **in Portuguese
only**, in both locales. Rule *keys* are English (`rendaVariavel.isencaoMensal`); rule *display
labels* are localised.

An English-locale rule trace therefore reads:

```
2. Monthly exemption check → not applicable, threshold exceeded
   [rule: rendaVariavel.isencaoMensal · Lei nº ..., art. ...]
```

The citation is not translated because a translated legal citation is not a citation — it cannot be
looked up, and it cannot be verified by the tax professional who reviews the ruleset annually.

---

## 7. Legal disclaimers

Both locales carry the disclaimers from
[Compliance & Legal Posture §8](../06-governance/03-compliance-and-legal-posture.md).

**The pt-BR version is the legally operative one** for a Brazilian user. The English version is a
courtesy translation and says so in its own text. Where the two could be read differently, Portuguese
governs — and any change to the English disclaimer requires re-checking the Portuguese, never the
reverse.

---

## 8. Accessibility

| Requirement | Detail |
|---|---|
| `lang` attribute per string | Mandatory. Without it VoiceOver reads Portuguese with English phonemes, which is unintelligible rather than merely wrong |
| Mixed-language strings | A Tier-1 regulatory term inside English prose is wrapped with `lang="pt-BR"` so it is pronounced correctly |
| Text expansion | Portuguese runs ~15–25% longer than English. No fixed-width text containers; all layouts tested at both locales |
| Dynamic Type | Tested at maximum size in **both** locales — pt-BR at large text is the worst case |

---

## 9. Testing

| Test | Scope |
|---|---|
| Copy lint | Per locale, all banned categories (BR-B04) |
| Formatting golden files | Number, currency, percent, date, duration per locale |
| ICU completeness | Every message key present in every locale; missing key fails the build |
| No-concatenation lint | Bans string concatenation in user-facing text paths |
| Narrative golden corpus | **Separate corpus per locale**; prompt or model change regresses both |
| Traceability | Verified per locale — pt-BR `1.234,56` must resolve against its own displayString |
| Layout | Both locales at maximum Dynamic Type |
| Terminology consistency | Tier-2 catalogue is authoritative; a synonym in the UI fails review |

---

## 10. What this deliberately does not do

| Not doing | Why |
|---|---|
| Translate the specification | Locked decision. Two copies drift; a stale spec is worse than none, because decisions get made against it |
| Machine-translate at runtime | Untraceable, unreviewable, and would bypass the copy lint entirely |
| Support a third locale | No user. Adding one is additive: catalogue + banned lists + golden files |
| Convert currency by locale | Language ≠ jurisdiction. BRL is BRL |
| Localise identifiers, codes, or telemetry | Machine-consumed; stability matters more than readability |

---

**See also:** [ADR-0023](../03-architecture/adr/ADR-0023-bilingual-product-english-spec.md) · [Ubiquitous Language](../00-foundation/05-ubiquitous-language.md) · [UX Architecture](07-ux-architecture.md) · [Recommendation Engine](../04-engines/05-recommendation-engine.md)
