# Security Strategy

**Status:** Ratified · **Owner:** CTO / SRE · **Last reviewed:** 2026-08-01

Atlas holds a complete, longitudinal map of one person's financial life — arguably a more sensitive
dataset than any single bank holds, because it is *aggregated*. It also holds **no money**, which
shapes the threat model decisively.

---

## 1. What we are actually protecting

| Asset | Sensitivity | Impact if compromised |
|---|---|---|
| Aggregated financial position over decades | **Critical** | Enables targeted extortion, social engineering, physical risk |
| CPF, account numbers, institution identifiers | **Critical** | Identity fraud, LGPD breach |
| Behavioural history (spending, timing, location patterns) | High | Profiling, inference of health/relationship events |
| Goals, life events, dependents | High | Personal exposure |
| Credentials to third-party sources | **Critical** | Lateral movement into real accounts |
| Forecasts and options | Low | Embarrassment, no direct harm |
| Source code | Low | Intended to be open eventually |

**Key asymmetry:** Atlas cannot move money (NG-03), so the attacker's prize is **information**, not
funds. That makes *confidentiality* the dominant property, ahead of integrity, ahead of
availability — the reverse of a payments system, and it justifies the modest 99% availability
target in [Infrastructure §7](08-infrastructure.md).

---

## 2. Threat model (STRIDE, scoped)

| Threat | Vector | Mitigation |
|---|---|---|
| **Spoofing** | Stolen session, phished credential | Passkeys (WebAuthn) as primary; no password to phish. Short-lived tokens; device binding |
| **Tampering** | Malicious or accidental ledger modification | Append-only storage; DB role has no `UPDATE`/`DELETE` on truth tables; corrections are new rows |
| **Repudiation** | "I didn't record that" | Bitemporal audit trail; every entry carries source and decision time |
| **Information disclosure** | DB exfiltration, log leakage, LLM prompt leakage | Field-level encryption of identifiers; log redaction; `FactSet` minimisation (BR-906) |
| **Denial of service** | Public endpoint flooding | Rate limits; scale-to-zero limits blast radius; cost alerts as a DoS tripwire |
| **Elevation of privilege** | Module reading another's data; container escape | Per-module DB roles; managed identity with least privilege; no secrets in the image |
| **Supply chain** | Compromised NuGet/pub package | Pinned versions, lockfiles, `dotnet list package --vulnerable` in CI, Dependabot, SBOM per build |
| **Third-party** | Aggregator breach | Aggregator credentials never stored by Atlas; token-only, scoped read, revocable |

### Explicitly out of scope
- Nation-state adversaries with physical access to Azure infrastructure.
- Compromise of the user's own Apple device with biometric unlock — Atlas trusts the platform.
- Insider threat: at N=1, the operator is the user. Documented rather than pretended away.

---

## 3. Identity and access

| Layer | Mechanism |
|---|---|
| Primary authentication | **Passkey (WebAuthn / Face ID)**. No password exists to be stolen or reused |
| Fallback | Recovery code, generated once, stored by the user offline |
| Session | Short-lived JWT (15 min) + refresh token bound to the device |
| Sensitive operations | Re-authentication required (export, source credential change, tenant key operations) |
| Service-to-service | Azure Managed Identity. **No connection strings, no shared secrets** |
| Database | Per-module Postgres roles, least privilege, `USAGE` on own schema only |

**Deliberate omissions:** no email/password authentication (the largest single source of breaches),
no SMS second factor (SIM-swap risk in Brazil is material), no OAuth social login (introduces a
third party into the authentication path for no benefit at N=1).

---

## 4. Cryptography

| Data | Mechanism |
|---|---|
| In transit | TLS 1.3 minimum; HSTS; certificate transparency monitoring |
| At rest — platform | Azure Storage Service Encryption + Postgres TDE |
| At rest — field level | AES-256-GCM with a **tenant-scoped data key** for CPF, account numbers, institution identifiers, source tokens |
| Key management | Key Vault; tenant data key wrapped by a Key Vault key; annual rotation |
| Client-side cache | iOS Keychain / Secure Enclave for tokens; encrypted local store for cached data |
| Content addressing | SHA-256 (integrity of artifacts, not secrecy) |

### Cryptographic erasure (LGPD)
The append-only ledger and the right to erasure genuinely conflict. Resolution: **destroy the
tenant data key.** Encrypted personal identifiers become unrecoverable ciphertext; structural
ledger integrity (amounts, dates, relationships) is preserved; the guarantee is technical rather
than a promise about a deletion job. See [BR-A04] and
[Compliance & Legal Posture](../06-governance/03-compliance-and-legal-posture.md).

---

## 5. Secrets

**Zero secrets in:** the repository, container images, environment variables in source control,
build logs, or client bundles.

| Secret | Location | Access |
|---|---|---|
| Postgres credentials | Key Vault | Managed identity, `atlas-api` / `atlas-sim` |
| Tenant data key (wrapped) | Key Vault | Managed identity, unwrapped in memory only |
| LLM API key | Key Vault | `atlas-api` narration path only |
| Aggregator tokens | Encrypted DB column, key in Key Vault | Ingestion module only |
| Signing keys | Key Vault | Identity module only |

CI enforcement: `gitleaks` on every push, `git-secrets` pre-commit hook, and a build failure on any
Key Vault reference resolving to a literal. Rotation is quarterly for provider keys and annual for
the tenant data key; both are runbooked.

---

## 6. Application security controls

| Control | Implementation |
|---|---|
| Input validation | Every command validated at the edge; domain re-validates (defence in depth) |
| Injection | Parameterised SQL exclusively; no dynamic SQL construction anywhere |
| Deserialisation | System.Text.Json with explicit contracts; polymorphic deserialisation disabled |
| Output encoding | Flutter escapes by default; no raw HTML rendering of user or LLM content |
| Rate limiting | Per-principal token bucket; stricter on export and auth endpoints |
| CORS | Explicit allow-list; no wildcards |
| Headers | HSTS, CSP, X-Content-Type-Options, Referrer-Policy |
| Dependency policy | Renovate weekly; critical CVEs block deployment; SBOM (CycloneDX) per build |
| Container | Distroless base, non-root user, read-only root filesystem, no shell |

### LLM-specific controls
The narration path is the only place untrusted-ish content meets a model:

1. **Prompt injection** — the `FactSet` contains only numeric facts and enumerated labels. Free-text
   user content (transaction descriptions, goal names) is **excluded from prompts by default**;
   where included, it is delimited and explicitly marked untrusted.
2. **Data minimisation** — the model receives the smallest fact set that supports the narrative,
   never a ledger dump (BR-906).
3. **Output containment** — generated text is never executed, never persisted as fact, never an
   input to any computation (BR-903). Numeric tokens must resolve to a `Fact.id` or the narration
   is rejected (BR-901).
4. **No tool access.** The narration model has no tools, no retrieval, no write path. It is a
   text renderer.

---

## 7. Logging and telemetry hygiene

**Never logged, at any level:** monetary amounts, account numbers, CPF, institution names, goal
names, transaction descriptions, authentication tokens, or anything derived from them.

**Logged:** correlation ids, entity ids (opaque UUIDs), operation names, durations, error codes,
counts, and coarse status.

Enforcement: a structured-logging wrapper that accepts only allow-listed field types, plus a Roslyn
analyser banning `Money`, `Commodity`, and identifier value objects from log-argument positions.
A CI job greps a sample of production-shaped logs for currency patterns and CPF-shaped digits.

Telemetry leaves the tenant boundary as aggregates only (BR-A05).

---

## 8. Security in the lifecycle

| Phase | Activity |
|---|---|
| Design | Threat model review for any change touching auth, crypto, external I/O, or the LLM path |
| Code | Analysers, secret scanning, dependency scanning on every PR |
| Build | SBOM generation, image vulnerability scan, provenance attestation |
| Deploy | Bicep policy checks; no public database endpoint; no wildcard RBAC |
| Runtime | Auth failure rate and export operations alert; anomalous access patterns reviewed |
| Periodic | Quarterly dependency audit + key rotation review; annual full threat-model revisit |

---

## 9. Incident response

Security incidents use the **same machinery as financial incidents** — declare, runbook, mitigate,
resolve, blameless postmortem — with two additions:

1. **Containment precedes diagnosis.** Revoke tokens and rotate keys first; investigate second.
2. **LGPD notification assessment within 24 hours**, documented regardless of outcome.

Runbooks: `RB-SEC-01` credential compromise · `RB-SEC-02` suspected data exfiltration ·
`RB-SEC-03` dependency CVE with active exploit · `RB-SEC-04` aggregator breach ·
`RB-SEC-05` lost device.

---

**See also:** [Data Strategy §7](04-data-strategy.md) · [AI Strategy](../06-governance/02-ai-strategy.md) · [Compliance & Legal Posture](../06-governance/03-compliance-and-legal-posture.md)
