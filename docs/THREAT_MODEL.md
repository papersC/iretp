# IRETP Threat Model

**RFP reference:** DLD-IRETP-2026-001 v1.3, §16.2 #66 (Security & Compliance Plan) + PROPOSAL.md Annex C
**Document purpose:** STRIDE threat model for the IRETP platform with per-asset risks, attack-tree summaries, and the implemented mitigations.
**Audience:** DLD CISO, DESC-authorised VAPT assessor, vendor security lead.

---

## 1. Scope

The threat model covers all assets within the IRETP boundary:

- **External-facing services** — Public Portal (Blazor Server), Public WebAPI, Open Data API, AI Agent
- **Internal-facing services** — Admin Portal (Blazor Server), AdminAPI, EWRS engine, Developer Scoring engine
- **Data plane** — OLTP database, OneLake Gold/Silver layers (via `IFabricGoldDataSource`), AI vector store
- **Infrastructure** — Azure App Service plan, SQL Server, Redis, Key Vault, Hangfire, SignalR hub
- **Supporting integrations** — SMTP / SMS gateways, UAE Central Bank FX feed, Mapbox tile server, UAE Pass / Azure AD OIDC

Out of scope:
- DLD source systems upstream of OneLake (separate threat model owned by DLD)
- DLD-controlled identity provider (separate threat model)
- DESC-certified SOC monitoring infrastructure (vendor-of-vendors model)

---

## 2. Trust boundaries

| # | Boundary | Notes |
|---|---|---|
| TB-1 | Public internet → WebAPI (via WAF + CDN) | Highest-volume attack surface. Anonymous + authenticated traffic mixed. |
| TB-2 | WebAPI → AdminAPI | Not directly traversed — AdminAPI sits behind a private VNET. Any "cross boundary" call indicates either compromise or misconfiguration. |
| TB-3 | DLD-internal network → AdminAPI | Internal users authenticate via federated OIDC + MFA. |
| TB-4 | Application → OneLake Gold | Read-only via `IFabricGoldDataSource`. Operational state never written into Gold. |
| TB-5 | Application → OLTP database | Read/write via EF Core; SQL injection prevented by parameterised queries. |
| TB-6 | Application → AI provider (managed API) | Outbound HTTPS to UAE-region endpoint only; allow-listed in `IHttpClientFactory`. |
| TB-7 | Application → Email/SMS providers | Outbound HTTPS to allow-listed endpoints. |
| TB-8 | DLD CMS author → Public Portal | Mediated by Headless CMS staging/preview workflow; no direct CMS-to-prod path. |

---

## 3. STRIDE per asset

### 3.1 Public WebAPI

| Threat | Description | Mitigation | Where |
|---|---|---|---|
| **S** Spoofing | Attacker presents stolen credentials | MFA mandatory for internal users; JWT signed HS256 with KV-managed key; refresh-token rotation revokes families on logout | `src/IRETP.WebAPI/Controllers/AuthController.cs` |
| **T** Tampering | Modified request body / query parameters | FluentValidation on every command; output-encoding by default; HTTPS everywhere | `src/IRETP.Application/Features/*/Commands/*Validator.cs` |
| **R** Repudiation | User denies an action they performed | Append-only `AuditLog` records every privileged action with actor + timestamp + reason | `src/IRETP.Infrastructure/Services/AuditLogService.cs` |
| **I** Information Disclosure | PII or pre-publication data exposed | No PII in public DTOs (k-anonymised aggregates); pre-publication assessment gates public data; CSP headers; CORS allow-list | `src/IRETP.Application/DTOs/*` |
| **D** Denial of Service | Burst floods the API | WAF + CDN absorb volumetric attacks; per-IP rate limiting; cached KPI snapshot (read-mostly path) | `src/IRETP.WebAPI/Program.cs`, `infra/bicep/platform.bicep` |
| **E** Elevation of Privilege | Public user reaches Admin functions | AdminAPI bound to private VNET — unreachable from public path; AuthorizationPolicies enforced server-side | `src/IRETP.Infrastructure/Identity/AuthorizationPolicies.cs` |

### 3.2 AdminAPI

| Threat | Description | Mitigation | Where |
|---|---|---|---|
| **S** | Compromised admin credential | MFA mandatory; PIM JIT access for SystemAdministrator; password lockout policy | `src/IRETP.WebAPI/Program.cs` |
| **T** | Risk threshold / scoring-weight tampering | Every change audit-logged; central audit trail (Pass 19e) records old value + new value + actor; weight sum enforced server-side | `src/IRETP.Application/Features/EWRS/Commands/UpdateRiskThresholdCommandHandler.cs` |
| **R** | Admin denies escalation decision | EWRS escalation history is immutable via DbContext-enforced append-only `AuditLog` (Pass 19e) | `src/IRETP.Infrastructure/Data/IretpDbContext.cs` |
| **I** | Internal user dumps `BeneficialOwner` table | Every read of confidential entities logged; least-privilege role boundary (DldViewer cannot read BeneficialOwner without supervisor approval) | `src/IRETP.Infrastructure/Services/AuditLogService.cs` |
| **D** | Internal user spams long-running export | Hangfire job throttling; export size cap (50k rows per RFP §3.3 FR-008) | `src/IRETP.WebAPI/Controllers/ExportController.cs` |
| **E** | DldOperator escalates to SystemAdministrator | RBAC enforced via `AuthorizationPolicies`; role change requires SystemAdmin + audit log entry | `src/IRETP.Infrastructure/Identity/AuthorizationPolicies.cs` |

### 3.3 AI Agent

| Threat | Description | Mitigation | Where |
|---|---|---|---|
| **S** | Attacker pretends to be the AI to extract DLD data | All AI responses originate server-side; prompts never travel to client | `src/IRETP.Infrastructure/Services/AIOrchestrator.cs` |
| **T** | Prompt injection bypasses guardrail | Multi-layer guardrail — keyword list + forecast regex; system prompt enforced; RAG context grounded in Gold layer; refusal in EN + AR | `src/IRETP.Infrastructure/Services/KeywordAdvisoryGuardrail.cs` |
| **R** | User denies asking the question | Every AI query logged with userId, prompt, model used, response, citations | `src/IRETP.Infrastructure/Services/AIOrchestrator.cs` |
| **I** | Cross-session memory leaks across users | `UserAiMemory` keyed strictly on userId; consent toggle deletes memory rows (PDPL §19.2) | `src/IRETP.Infrastructure/Identity/UserConsentService.cs` |
| **D** | AI provider rate-limit exhaustion | Multi-model fallback per RFP §5.3; in-process metrics expose fallback state | `src/IRETP.Infrastructure/Services/AIOrchestrator.cs` |
| **E** | Prompt makes the AI hit AdminAPI | AdminAPI on private VNET; AI client allow-listed to AI provider only | `infra/bicep/platform.bicep` |

### 3.4 Data plane

| Threat | Description | Mitigation | Where |
|---|---|---|---|
| **S** | Attacker poisons OneLake Gold layer | IRETP is read-only against Gold; writes occur only via DLD's Data Factory pipelines outside IRETP control | `src/IRETP.Infrastructure/Services/Fabric/PassthroughFabricGoldDataSource.cs` |
| **T** | OLTP row tampering | EF Core change tracking + DbContext-enforced immutability on `AuditLog` and `EscrowTransaction` rows | `src/IRETP.Infrastructure/Data/IretpDbContext.cs` |
| **R** | Database admin denies a destructive action | SQL Server audit + Defender for Cloud logging at the DB level (outside the app) | `infra/bicep/platform.bicep` |
| **I** | SQL injection extracts data | All access via EF Core parameterised queries; no raw SQL string concatenation | `src/IRETP.Infrastructure/Repositories/Repository.cs` |
| **D** | Slow query exhausts DB pool | EF Core query timeout configured; AsNoTracking on hot read paths; KPI cache absorbs dashboard reads | `src/IRETP.Infrastructure/DependencyInjection.cs` |
| **E** | Application identity escalates to DB sysadmin | Managed Identity scoped to db_datareader + db_datawriter only; sysadmin role held by DLD DBA accounts | `infra/bicep/platform.bicep` |

### 3.5 Notification dispatch (RFP §6)

| Threat | Description | Mitigation | Where |
|---|---|---|---|
| **S** | Phishing email impersonating DLD | DLD-controlled SPF / DKIM / DMARC on `iretp.dld.gov.ae`; HMAC unsubscribe tokens prevent URL-spoofing | `src/IRETP.Infrastructure/Services/Notifications/HmacUnsubscribeTokenService.cs` |
| **T** | Modified unsubscribe URL changes consent state | HMAC-SHA256 token bound to (userId, reason); tamper invalidates the signature | same |
| **R** | User denies opt-in | Consent change recorded in `AuditLog` with timestamp + IP | `src/IRETP.Infrastructure/Services/AuditLogService.cs` |
| **I** | Email leaks chart data via image-tag callback | Embedded charts inlined as base64 / data URI — no remote-tracker pixels | `src/IRETP.Infrastructure/Services/Notifications/NotificationTemplates.cs` |
| **D** | Bulk-unsubscribe DoS via guessable tokens | Token space ≥ 2^128; unknown tokens return uniform "if valid" response (no enumeration); rate-limited endpoint | `src/IRETP.Web/Controllers/UnsubscribeController.cs` (or equivalent in WebAPI) |
| **E** | Unsubscribe endpoint bypasses consent for other users | HMAC token bound to (userId, reason) — bearer cannot target another user | `src/IRETP.Infrastructure/Services/Notifications/HmacUnsubscribeTokenService.cs` |

---

## 4. Top attack trees

### AT-1 — Extract beneficial-ownership data from the public portal

Attacker goal: read `BeneficialOwner` rows by abusing the public surface.

```
Root: Read BeneficialOwner data
├── Leaf: SQL injection via public endpoint     → blocked by EF Core parameterisation
├── Leaf: Bypass authorisation                  → blocked by AuthorizationPolicies + AdminAPI on private VNET
├── Leaf: Prompt-injection the AI Agent         → blocked by guardrail + DTO-level PII filter
├── Leaf: Aggregate-disaggregation              → blocked by k-anonymisation (min 5 transactions per zone-period)
└── Leaf: API key compromise on Open Data       → API keys hashed at rest; revocation in < 1 hr per RFP §15.2
```

### AT-2 — Manipulate developer scores

Attacker goal: bias a developer's composite score in their favour.

```
Root: Inflate one developer's composite score
├── Leaf: Modify ScoringWeight directly         → audit-logged; SystemAdmin only; weight sum enforced
├── Leaf: Modify DeveloperScore rows            → blocked by RBAC + DbContext invariants on audit-logged columns
├── Leaf: Inject fake transactions              → IRETP writes nothing to Gold; transactions come from DLD via Data Factory
├── Leaf: Replay an old high score              → DeveloperScore append-only, indexed by quarter; new quarter wins
└── Leaf: Skew RegulatoryViolation register     → managed by RERA outside IRETP; write-back from IRETP impossible
```

### AT-3 — Suppress an EWRS alert

Attacker goal: prevent a critical risk signal from reaching DLD leadership.

```
Root: Suppress L3+ alert
├── Leaf: Mark alert resolved before review     → audit-logged; resolution requires DldSupervisor; immutable history
├── Leaf: Stop AlertEscalationService           → Hangfire job in DLD-controlled cluster; SystemAdmin-only restart; OpenTelemetry alert on missing run
├── Leaf: Modify SLA deadline retroactively     → RiskAlert audit-trail prevents post-hoc change of LastEscalatedAt
├── Leaf: Disable notification channel          → channel config change audit-logged; daily P95 SLA report flags missing channels
└── Leaf: Compromise destination email          → DESC-certified SOC monitors mail flow; alternate channels (SMS + In-Platform) configured per level
```

---

## 5. Mitigation matrix → tests

| Threat | Mitigation | Test |
|---|---|---|
| Audit-log tamper | DbContext-enforced append-only | `AuditLogImmutabilityTests` (3 cases) |
| EWRS escalation bypass | `AlertEscalationService` Hangfire job | `AlertEscalationServiceTests` (5 cases) |
| Threshold-edit accountability | Central audit trail | `UpdateRiskThresholdCommandHandlerTests` (4 cases) |
| Weight-edit accountability | Same | `UpdateScoringWeightsCommandHandlerTests` (7 cases) |
| Notification SLA breach | `NotificationSlaHealthCheck` | `NotificationSlaHealthCheckTests` (5 cases) |
| Fabric pipeline failure | `SlaHealthCheck` Fabric integration | `SlaHealthCheckTests` (6 cases) |
| Advisory-bypass via prompt injection | `KeywordAdvisoryGuardrail` + forecast regex | `KeywordAdvisoryGuardrailTests` (18 cases) |
| Saved-view leak across users | UserId scoping | `SaveAnalyticsViewCommandHandlerTests` (4 cases) |
| Share-token forge | HMAC + 365-day expiry | `SharedAnalyticsViewTests` (6 cases) |
| Unsubscribe forge | HMAC-SHA256 token | `HmacUnsubscribeTokenServiceTests` (6 cases) |
| Documentation drift | Self-validating compliance matrix | `ComplianceMatrixTraceabilityTests` (5 cases) |

---

## 6. Open items (tracked in Risk Register)

| # | Item | Reference |
|---|---|---|
| 1 | DESC-authorised VAPT engagement | `RISK_REGISTER.md` S-01 |
| 2 | DDoS volumetric test under GRETI-announcement load | `RISK_REGISTER.md` S-02 |
| 3 | SOC engagement contract finalised | `DESC_ISR_V3_COMPLIANCE.md` §5.5 |
| 4 | Formal exception register (DESC ISR 1.4) | `DESC_ISR_V3_COMPLIANCE.md` §1.4 — PI |

---

## Cross-references

- [DESC_ISR_V3_COMPLIANCE.md](DESC_ISR_V3_COMPLIANCE.md) — control implementation evidence
- [OWASP_TOP_10_MAPPING.md](OWASP_TOP_10_MAPPING.md) — category-by-category mitigations
- [RISK_REGISTER.md](RISK_REGISTER.md) — programme risks
- [RUNBOOK.md](RUNBOOK.md) — incident response SIRP playbook

*Updated 2026-05-18. Reviewed at every phase go-live; major review at warranty-period start.*
