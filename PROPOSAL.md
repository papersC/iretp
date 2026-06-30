# Technical & Commercial Proposal

## Integrated Real Estate Transparency Platform (IRETP)

**Tender reference:** DLD-IRETP-2026-001 (RFP v1.3)
**Submitted to:** Dubai Land Department, Government of Dubai
**Proposal date:** 17 May 2026
**Validity:** 120 days from submission
**Document version:** 1.3 — aligned to RFP v1.3 (Microsoft Fabric / OneLake alignment + §16.2 mandatory checklist)

---

## Table of Contents

1. Executive Summary
2. Understanding of the Requirement
3. Proposed Solution
4. Technology Stack & Architecture
5. Compliance, Security & Data Residency
6. 3-Month Implementation Plan
7. Team Structure & Governance
8. Quality Assurance & Testing Strategy
9. Risk Management
10. Service Levels & Support Model
11. Knowledge Transfer & Training
12. Commercial Summary
13. Assumptions & Dependencies
14. Annexes

---

## 1. Executive Summary

We are pleased to submit this proposal in response to **DLD-IRETP-2026-001** for the design, build, and deployment of the **Integrated Real Estate Transparency Platform (IRETP)** — a unified public-facing and internal platform that consolidates Dubai's real-estate transparency, risk, investment, and regulatory-oversight capabilities into a single digital experience.

Our response is backed by a **working reference implementation** already demonstrating ~90% functional coverage of the RFP across the Early Warning & Risk System (EWRS), KPI dashboards, analytics, GIS map services, Open Data portal, Arabic-first AI agent, ESG rubric, developer scorecards, and investor-grade transparency indices. The residual 3-month programme converts this reference build into a **UAT-certified, DESC-aligned, production-ready platform** integrated with DLD source systems and UAE Pass, with formal VAPT, ISO 27001 alignment, and a signed SOC engagement.

### Headline commitments

| Commitment | Target |
|---|---|
| Functional coverage of RFP mandatory requirements | **100%** at Go-Live |
| Arabic + English bilingual parity | Full UI, Open Data, AI agent |
| Data residency | **100% UAE** (Azure UAE North, primary; UAE Central, DR) |
| Availability SLA | **99.9%** for public tier, **99.95%** for internal tier |
| AI agent response P95 latency | **< 3.5 seconds** |
| KPI dashboard freshness | **≤ 15 minutes** (cached) |
| EWRS alert-to-notification | **< 5 minutes** at Level 1 escalating per §10 SLA matrix |
| Open Data portal API uptime | **99.9%** with partitioned rate limits |
| Go-Live date | **Month 3, Week 12** |

### Why this proposal

1. **De-risked delivery.** The reference implementation shortens the critical path: foundations (Clean Architecture .NET 9 solution, Hangfire jobs, Identity + MFA, SignalR, EF Core + migrations, Docker + Bicep IaC) already exist and have been verified. Test suite: 94/94 passing. Build: 0 warnings, 0 errors.
2. **Built on DLD's existing Microsoft Fabric ecosystem (RFP v1.3 §11.4).** The data plane reads from DLD's existing OneLake Gold layer through a single abstraction (`IFabricGoldDataSource`), no parallel ETL, no parallel data stores. Power BI is retained for the Internal Platform; the External Portal uses a custom-web visualisation layer — see [docs/VISUALISATION_LAYER_ANALYSIS.md](docs/VISUALISATION_LAYER_ANALYSIS.md) for the §11.4.2 comparative analysis.
3. **UAE-native.** Azure UAE-only deployment, UAE Pass OIDC support, DESC-CSP compliance path, bilingual (AR/EN) UX, and an Arabic-first AI agent with an advisory guardrail that refuses investment recommendations per regulatory posture.
4. **Transparency by design.** Public Open Data portal exposes OpenAPI 3.0 contracts; Public Developer Scorecards, beneficial-ownership disclosures, and mortgage transparency are first-class screens, not afterthoughts.
5. **AI governance baked-in.** Tiered model routing, UAE-residency metadata on every inference, deterministic analytics (anomaly/trend/correlation) layered onto RAG, a keyword advisory guardrail, and an automated accuracy harness (EN+AR) that can be extended to DLD's UAT catalogue of 100 questions.

### Architecture Integration Map (RFP v1.3 §11.4.1)

The Architecture Integration Map showing component-by-component alignment with DLD's existing OneLake / Data Factory / Semantic Model layers is at [docs/ARCHITECTURE_INTEGRATION_MAP.md](docs/ARCHITECTURE_INTEGRATION_MAP.md). The Microsoft Fabric Environment Familiarisation Plan is at [docs/familiarisation/06_fabric_environment.md](docs/familiarisation/06_fabric_environment.md).

---

## 2. Understanding of the Requirement

The Dubai Land Department requires a **single integrated platform** that modernises transparency and risk oversight across Dubai's real-estate market. Based on our reading of **DLD-IRETP-2026-001**, the platform must serve four stakeholder groups simultaneously:

| Stakeholder | Primary needs |
|---|---|
| **Public / investors** | Price index, project search, developer scorecards, yield comparisons, GIS heatmaps, Open Data APIs, AI agent (AR/EN). |
| **DLD regulatory staff** | EWRS alerts, escrow monitoring, AML/ownership transparency, audit trails, regulatory reports. |
| **Developers & strategic partners** | Partner APIs, project registration status, escrow compliance, benchmark data. |
| **Ministry & government** | KPI dashboards, ESG indicators, macro indices, international benchmarking. |

### Key functional pillars (from RFP)

1. **FR-001 to FR-010** — Transparency Index, Price Index, Developer Scorecards, Project Search, GIS Map with heatmaps, Escrow Monitoring, AML/Beneficial-Ownership.
2. **EWRS** — 4-level auto-escalation, SLA tracking, multi-channel notifications, dashboards.
3. **AN-001 to AN-005** — KPI dashboards, saved views, drilldowns, zone comparison, forecasting.
4. **AI-001 to AI-004** — Multilingual AI agent, RAG, guardrails, accuracy harness.
5. **OD-001 to OD-005** — Open Data portal, OpenAPI, rate-limited tiers, developer console.
6. **§10 — NFRs** — availability, latency, security, data residency, observability.
7. **§20 — ESG & international benchmarking** — LEED / Estidama Pearl / BREEAM mapping.

### Non-functional drivers we have internalised

- **UAE data residency (§10.2).** All personal data, telemetry, and backups remain in UAE sovereign cloud regions. No cross-border processing without DLD waiver.
- **DESC Cloud Security Policy (CSP).** Solution designed for DESC-CSP certification from day one.
- **Arabic language parity.** Not a translation — the AI agent's retrieval catalogue, the Open Data metadata, and UI microcopy are bilingual by construction.
- **Zero-trust operational posture.** MFA mandatory for internal users, RBAC with policy registry, short-lived internal JWTs (30 min), partner API keys with per-minute ceilings, TLS 1.2 minimum end-to-end.

---

## 3. Proposed Solution

### 3.1 Solution overview

IRETP is delivered as a **multi-tier Clean-Architecture platform** with three independent web-facing services backed by shared application and domain layers:

```
┌───────────────────────────────────────────────────────────────────┐
│                        Public Users & Investors                    │
│           UAE Pass · Partner APIs · Open Data consumers            │
└───────────────────────────────────────────────────────────────────┘
              │                    │                    │
      ┌───────▼───────┐    ┌───────▼───────┐    ┌──────▼──────┐
      │  IRETP.Web    │    │ IRETP.WebAPI  │    │   AI Agent  │
      │ (Blazor SSR)  │    │  (Public v1)  │    │ (bilingual) │
      └───────┬───────┘    └───────┬───────┘    └──────┬──────┘
              │                    │                    │
              │      ┌─────────────▼─────────────┐      │
              └─────►│     IRETP.Application     │◄─────┘
                     │  (MediatR use-cases)      │
                     └─────────────┬─────────────┘
                                   │
                     ┌─────────────▼─────────────┐
                     │     IRETP.Domain          │
                     │  (entities, rules, enums) │
                     └─────────────┬─────────────┘
                                   │
              ┌────────────────────▼────────────────────┐
              │          IRETP.Infrastructure           │
              │  EF Core · Hangfire · Identity · SignalR│
              │  Integrations · Caching · AI routing    │
              └────────────────────┬────────────────────┘
                                   │
   ┌────────────┬──────────────────┼──────────────────┬────────────┐
   │            │                  │                  │            │
┌──▼──┐     ┌──▼───┐         ┌─────▼─────┐       ┌────▼────┐  ┌────▼────┐
│ SQL │     │Storage│        │Key Vault  │       │App      │  │Service  │
│Azure│     │(GRS)  │        │(HSM-backed)│      │Insights │  │Bus      │
└─────┘     └──────┘         └───────────┘       └─────────┘  └─────────┘
                        All resources: UAE North primary, UAE Central DR
```

**Separate internal service** (`IRETP.AdminAPI`) runs on its own port and is gated by a dedicated policy registry — no co-hosting of internal endpoints with the public surface.

### 3.2 Functional coverage against RFP sections

| RFP Section | Capability | Status in reference build |
|---|---|---|
| FR-001 Transparency Index | Composite index, peer cities, time-series | Implemented |
| FR-002 Price Index | Repeat-sales + hedonic, city/zone/type | Implemented |
| FR-003 Developer Scorecards | Public rating + detail pages | Implemented (`/developers/scorecards`) |
| FR-004 Project Search | Faceted search, saved searches | Implemented |
| FR-005 Project Detail | Full detail, media, milestones | Implemented |
| FR-006 Escrow Monitoring | Balances, drawdowns, anomalies | Implemented |
| FR-007 AML / Ownership | Beneficial-ownership disclosure | Implemented (final UI polish due Month 1) |
| FR-008 Mortgage Transparency | Mortgage register view | Implemented (UI polish due Month 1) |
| FR-009 GIS Map | Volume / price-psf / yield heatmaps + project pins | Implemented |
| FR-010 International Benchmarking | JLL / Knight Frank / Numbeo overlay | Implemented |
| EWRS §14 | 4-level escalation, SLA, dedup, dashboards | Implemented |
| AN-001..005 | KPI dashboards, saved views (drag-drop), zone compare | Implemented |
| AI-001..004 | Bilingual agent, tiered routing, guardrails, accuracy harness | Implemented |
| OD-001..005 | OpenAPI 3.0, rate tiers, `/api-docs` console | Implemented |
| §20 ESG | Rubric + LEED / Estidama / BREEAM mapping | Implemented |
| §10 NFRs | SLA health check `/healthz/sla`, Docker hardening | Implemented |

**Residual work (Months 1–3):** Go-Live integrations with DLD source systems, UAE Pass federation, DESC-CSP certification, VAPT, load test at contractual volumes, UAT with DLD's 100-question AI catalogue, ESG GIS heatmap layer wiring, AI-chat front-end polish (citations rendering, retry, explicit language picker), and commercial Bicep parameterisation.

### 3.3 Bilingual & Arabic-first design

- All Blazor pages use RTL-aware layout; direction and locale bound to user preference.
- The AI agent's RAG catalogue is authored in AR and EN; tier routing sends primary queries to a UAE-resident model, with analytics-heavy prompts routed to a dedicated analytics tier.
- Open Data metadata (`dataset.title`, `dataset.description`, field labels) is bilingual in the OpenAPI specs.
- Notifications (EWRS, investor alerts) support AR+EN templates.

---

## 4. Technology Stack & Architecture

### 4.1 Stack summary

| Layer | Technology | Rationale |
|---|---|---|
| Runtime | **.NET 9 LTS** (C# 13) | Long-term support, strongest performance tier for Azure, first-class OpenTelemetry. |
| Web UI | **Blazor Server** (SSR) | Reduced client-side complexity, fast Arabic/English context switching, SignalR-native. |
| APIs | **ASP.NET Core Minimal + Controllers** | Split surface: `IRETP.WebAPI` (public) and `IRETP.AdminAPI` (internal). |
| Domain | **DDD + Clean Architecture** | Framework-agnostic `Domain` and `Application` layers. |
| CQRS / use-cases | **MediatR** | Explicit commands/queries with handler isolation. |
| ORM | **EF Core 9** (SQL Server) | Migrations, change-tracking, compiled queries for hot paths. |
| Background jobs | **Hangfire** | Durable queues; dashboard gated on local / internal network. |
| Real-time | **SignalR** | EWRS alert push; KPI stream for dashboards. |
| Identity | **ASP.NET Identity** + **OIDC** (UAE Pass, Azure AD) | TOTP MFA, password policy, refresh-token rotation. |
| AuthZ | **Policy registry** (`AuthorizationPolicies.cs`) | Centralised role + claim policies, reused across APIs. |
| Caching | In-memory (`IKpiSnapshotCache`) + optional Azure Redis | 15-min KPI freshness SLA. |
| AI | Azure OpenAI / Anthropic (UAE-resident where available) | Tiered routing: nav / analytics / primary / secondary. |
| Observability | Application Insights + Log Analytics Workspace | OTel traces, metrics, structured logs. |
| Containerisation | Docker (multi-stage) + Docker Compose | Hardened: `cap_drop: ALL`, `no-new-privileges`, HEALTHCHECK. |
| IaC | **Bicep** (subscription-scope) | `main.bicep` + `platform.bicep`, pinned to UAE North. |
| CI/CD | GitHub Actions | Build, test, CodeQL, `dotnet list --vulnerable`, ZAP baseline, Bicep lint. |
| Secrets | Azure Key Vault (HSM / purge-protected) | MSI-only access; no secrets in code or config. |

### 4.2 Deployment architecture (production)

- **Region:** UAE North (primary). UAE Central (DR, warm).
- **Compute:** App Service Plan **P1v3**, multi-instance (min 3), zone-redundant where supported.
- **Data:** Azure SQL (Business Critical in prod), GRS storage, Key Vault with purge protection.
- **Networking:** Private endpoints on SQL, Key Vault, Storage. Front Door + WAF at the edge (OWASP managed ruleset).
- **Identity:** UAE Pass OIDC for citizens; Azure AD for DLD staff with MFA enforced.
- **Backup / DR:** SQL PITR (35 days), geo-redundant backups, RPO ≤ 15 min, RTO ≤ 1 hour.

### 4.3 Security architecture

- **MFA** (TOTP authenticator) for all internal users; setup and challenge endpoints already implemented.
- **JWTs:** 30-min internal, 60-min public, 7-day refresh with rotation and reuse detection.
- **Partner API keys:** hashed at rest, header-partitioned rate limiting (600/240/60 tiers), per-key metering.
- **TLS 1.2+** everywhere; HSTS on public surfaces.
- **Secrets:** Key Vault references in App Service; no secrets in repo, verified by pre-commit hook.
- **Audit trail:** immutable append-only table for admin actions and EWRS escalations.
- **CI security gates:** weekly CodeQL (csharp), `dotnet list package --vulnerable` fails build on High/Critical, OWASP ZAP baseline against main.

### 4.4 AI governance

- **Tiered routing.** Navigation prompts → small/fast model. Analytics prompts → analytics-tier. Primary and secondary tiers chosen based on task, language, and sensitivity.
- **UAE residency metadata.** Every model call logs region + residency status; any non-UAE call is flagged in dashboards.
- **Deterministic analytics overlay.** Time-series anomaly (z-score), trend (least-squares with R²), correlation (Pearson) computed in-process and appended to RAG context, so the model quotes numbers rather than inferring them.
- **Advisory guardrail.** A keyword-rule guardrail rejects investment-advice prompts in AR+EN with a bilingual refusal; original input is logged for model-improvement review.
- **Accuracy harness.** `IAiAccuracyHarness` runs a seed catalogue of 14 items (EN + AR, data / navigation / adversarial guardrail) and scores by expected-keyword match. Extends to DLD's UAT set of 100 questions during Month 2.

---

## 5. Compliance, Security & Data Residency

| Control Framework | Alignment | Evidence |
|---|---|---|
| **DESC Cloud Security Policy** | Certification track during Months 1–3 | Control matrix, architecture diagrams, data-flow diagrams delivered Month 1 |
| **ISO 27001:2022** | Roadmap delivered; gap assessment in Month 2 | Policy pack, SoA, risk register |
| **UAE PDPL** | Full compliance | DPIA, lawful basis register, DSR workflow |
| **NESA / SIA IAS** | Mapped to relevant controls | Control mapping annex |
| **OWASP ASVS L2** | Engineering baseline | Design review + VAPT |
| **VAPT** | External engagement Month 2 | Report + remediation evidence |
| **SOC** | 24x7 provider contracted Month 1 | Service definition, runbooks |

**Data residency guarantees:**
- All application and data tiers deployed to UAE regions only.
- AI models used are either UAE-resident or the call is flagged and recorded with residency metadata.
- Backups, logs, and analytics all remain in UAE regions.

---

## 6. 3-Month Implementation Plan

The plan is organised into three calendar months, each with weekly milestones. Work streams run in parallel with explicit dependency gates.

### 6.1 Programme timeline (high level)

| | **Month 1 — Foundation & Integration Design** | **Month 2 — Build, Integration, Hardening** | **Month 3 — UAT, Certification, Go-Live** |
|---|---|---|---|
| **Weeks** | W1 – W4 | W5 – W8 | W9 – W12 |
| **Theme** | Mobilise, set up prod environments, finalise integrations, complete residual UI polish | Integrate DLD systems, harden security, run accuracy harness on full DLD catalogue, performance test | UAT with DLD users, VAPT remediation, DESC-CSP certification, rehearsals, Go-Live |
| **Gate** | G1: Prod environment ready, integration contracts signed | G2: Feature-complete, security-cleared, perf passed | G3: UAT passed, Go/No-Go, Go-Live |

### 6.2 Month 1 — Mobilisation, environments, integration design

**Objectives**
- Kick off programme, stand up production-shape Azure UAE environments via Bicep with commercial parameters.
- Finalise integration contracts with DLD source systems (escrow, project register, ownership, mortgage, cadastral/GIS).
- Set up SOC, start DESC-CSP certification engagement, schedule VAPT.
- Complete residual UI polish (AI chat citations/retry, ESG GIS heatmap wiring, mortgage/BO page completions).

**Week-by-week**

| Week | Activities | Deliverables |
|---|---|---|
| **W1** | Programme kickoff, signed RACI, runbook baseline, DLD system inventory workshops, UAE Pass federation registration | Kickoff pack, PMP v1, RACI, environment plan |
| **W2** | Provision `dev`, `uat`, `prod` resource groups via Bicep with commercial params (AAD admin object IDs, SKU sizing, VNet integration). Wire UAE Pass OIDC in staging | Provisioned environments, UAE Pass smoke test, DR pairing |
| **W3** | Integration design workshops with DLD IT for each source system. Publish data-flow diagrams for DESC-CSP. Finalise SOC service definition | Integration design doc, data-flow diagrams, SOC contract |
| **W4** | Residual UI polish (FR-007 BO, FR-008 mortgage, AI-chat citations + language picker, ESG GIS heatmap layer). Migrate code-shippable backlog from reference repo to client repo. Gate **G1** | Feature-complete public UI, Gate G1 sign-off |

**Gate G1 exit criteria:** environments provisioned; UAE Pass sign-in working in staging; integration contracts signed; SOC contract signed; DESC-CSP engagement active; all residual UI items closed.

### 6.3 Month 2 — Build, integration, hardening, performance

**Objectives**
- Connect live DLD sources (escrow, project, ownership, mortgage, GIS).
- Extend AI accuracy harness to DLD's 100-question UAT catalogue; iterate until ≥ 90% pass rate.
- Execute external VAPT; remediate findings.
- Run load and chaos tests against contractual volumes; tune caches, query plans, SignalR fan-out.

**Week-by-week**

| Week | Activities | Deliverables |
|---|---|---|
| **W5** | Integrate escrow + project register. Enable EWRS indicators on live data. Enable SignalR broadcast to internal console | Live escrow & project data, EWRS evaluating real indicators |
| **W6** | Integrate beneficial-ownership, mortgage, cadastral GIS. Populate benchmark datasets for JLL / Knight Frank / Numbeo feeds | Integrations live in UAT |
| **W7** | External VAPT executes (Week-long engagement). Load test at 5× expected peak. Chaos test DR failover to UAE Central | VAPT report v1, load test report, DR drill report |
| **W8** | Remediate VAPT High/Medium findings. Run AI accuracy harness on full DLD 100-question catalogue, iterate prompts + retrieval. Freeze scope. Gate **G2** | Remediation evidence, AI accuracy ≥ 90%, Gate G2 sign-off |

**Gate G2 exit criteria:** all integrations live in UAT; VAPT High/Medium findings closed; load test meets §10 latency/throughput targets; AI accuracy harness ≥ 90% on DLD catalogue; DESC-CSP pre-assessment clean.

### 6.4 Month 3 — UAT, certification, Go-Live

**Objectives**
- UAT with DLD users on real data in UAT environment; log, triage, fix defects.
- Complete DESC-CSP certification audit.
- Operational rehearsals (ops runbooks, incident drill, escalation drill).
- Cut-over planning and Go-Live.

**Week-by-week**

| Week | Activities | Deliverables |
|---|---|---|
| **W9** | UAT wave 1 (public portal + Open Data + AI). Daily defect triage. Documentation review. Training of DLD super-users | UAT pass rate report, training completed |
| **W10** | UAT wave 2 (internal EWRS + admin). DESC-CSP formal audit. Penetration re-test to verify remediation | UAT sign-off draft, DESC-CSP audit report, re-test confirmation |
| **W11** | Cut-over rehearsal (blue/green). Incident drill with SOC. ISO 27001 gap assessment documented. Final UAT sign-off | Cut-over runbook, SOC drill report, ISO 27001 gap pack |
| **W12** | **Go/No-Go** with DLD steering committee. **Go-Live** cut-over. Hyper-care (24×7 for first 14 days). Operational handover | Signed Go-Live acceptance, hyper-care schedule |

**Gate G3 exit criteria:** UAT signed off; DESC-CSP certification issued; VAPT re-test clean; SOC operational; Go/No-Go approved by DLD.

### 6.5 Gantt (ASCII summary)

```
Week                    1  2  3  4  5  6  7  8  9 10 11 12
Mobilise & Governance  [==]
Environments & IaC     [======]
UAE Pass / OIDC           [==]
Integration design        [=====]
Residual UI polish           [=====]
DLD source integration              [========]
EWRS on live data                   [====]
VAPT (external)                           [==]
VAPT remediation                             [==]
Load / DR testing                         [====]
AI accuracy harness                       [======]
DESC-CSP engagement    [==========================]
ISO 27001 gap                                   [====]
UAT (waves 1–2)                                    [======]
Training & docs                                       [====]
Go/No-Go + Go-Live                                         [=]
Hyper-care                                                 [==]
```

---

## 7. Team Structure & Governance

### 7.1 Core team (proposed)

| Role | FTE | Responsibilities |
|---|---|---|
| Programme Director | 0.5 | Overall accountability, DLD steering committee |
| Delivery Manager | 1.0 | Day-to-day delivery, risk & issue log, gate evidence |
| Solution Architect | 1.0 | Architecture, design authority, integration design |
| Security Architect | 0.5 | DESC-CSP, VAPT coordination, threat model |
| .NET Tech Lead | 1.0 | Backend code quality, performance, reviews |
| Backend Engineers | 3.0 | Features, integrations, EWRS, Hangfire |
| Frontend / Blazor Engineers | 2.0 | Public portal, admin console, Arabic/RTL polish |
| AI / Data Engineer | 1.0 | RAG catalogue, accuracy harness, guardrails |
| QA Lead | 1.0 | Test strategy, UAT coordination, defect triage |
| QA Engineers | 2.0 | Functional, Arabic language, performance, accessibility |
| DevOps / SRE | 1.0 | Bicep, CI/CD, observability, release management |
| BA / Product Analyst | 1.0 | Requirement clarification, UAT scripts, training |
| **Total core FTEs** | **15.0** | |

### 7.2 Governance

- **Weekly:** delivery stand-ups, risk & issue review, demo of increments.
- **Fortnightly:** DLD steering committee; RAG status, gate forecast.
- **Monthly:** executive review with Programme Director and DLD sponsor.
- **Ad hoc:** change-control board for any scope change > 3 person-days.

---

## 8. Quality Assurance & Testing Strategy

| Test type | Scope | Tool / method |
|---|---|---|
| Unit tests | Domain rules, handlers, services | xUnit (in-repo `tests/IRETP.Tests`) |
| Integration tests | EF Core + repositories, handlers | EF InMemory + real `Repository<T>` + `UnitOfWork` |
| API contract | Public + Admin APIs | Postman / Newman + OpenAPI diff |
| UI tests | Blazor pages, RTL, keyboard accessibility | Playwright |
| Accessibility | WCAG 2.1 AA | axe-core + manual audit |
| Performance | §10 NFRs (P95 < 3.5s AI, dashboards < 2s) | k6 |
| Security | OWASP ASVS L2, CodeQL, ZAP, external VAPT | GitHub Actions + external provider |
| AI accuracy | DLD 100-question catalogue (AR/EN) | `IAiAccuracyHarness` scored by expected-keyword match |
| DR / chaos | Failover to UAE Central, pod kill | Azure Chaos Studio |

Defect SLA: Critical — 24h; High — 48h; Medium — 5 business days; Low — next release.

---

## 9. Risk Management

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R1 | DLD source-system integration delays | Medium | High | Start integration design Week 1; agree mock contracts; early sandbox access request |
| R2 | DESC-CSP certification timeline | Medium | High | Engage auditor Week 1; pre-assessment end of Month 1 |
| R3 | UAE Pass federation rollout | Medium | Medium | Register in W1; keep Azure AD fallback for staff |
| R4 | AI accuracy below target on DLD catalogue | Medium | Medium | Deterministic analytics overlay + prompt tuning + retrieval tuning; iterate through Month 2 |
| R5 | Load/perf gaps under peak | Low | High | Early load testing in W7; query plan review; 15-min KPI cache |
| R6 | VAPT High findings close to Go-Live | Medium | High | VAPT in W7, remediation in W8; re-test in W10 |
| R7 | Arabic content quality gaps | Medium | Medium | Native-speaker QA reviewer; UAT wave 1 covers AR journeys |
| R8 | Key person dependency | Low | Medium | Named back-ups for each role; runbooks living in repo |

---

## 10. Service Levels & Support Model

| Metric | Target |
|---|---|
| Public portal availability | 99.9% monthly |
| Internal EWRS availability | 99.95% monthly |
| P95 page load (public) | ≤ 2.0 s |
| P95 AI agent response | ≤ 3.5 s |
| KPI dashboard freshness | ≤ 15 min |
| EWRS alert → notification (L1) | ≤ 5 min |
| Critical incident MTTR | ≤ 2 h |
| Support hours | 24×7 for Critical; 08–20 GST Sat–Thu for P2/P3 |

Support tiers: L1 (service desk), L2 (application support), L3 (engineering). SOC monitors 24×7 from Month 1.

---

## 11. Knowledge Transfer & Training

- **DLD super-users** — 2-day classroom in Week 9 + recorded modules.
- **DLD IT operations** — runbook walkthrough in Week 10 + shadowing during hyper-care.
- **Developer community** — `/api-docs` interactive console, OpenAPI 3.0, example notebooks.
- **Documentation pack** — architecture, ops runbooks, DR playbook, threat model, DPIA, control matrices.

---

## 12. Commercial Summary

*A detailed commercial schedule is provided in a separate sealed envelope as per tender instructions.* The programme is priced on a **fixed-price milestone** basis aligned to the gates G1, G2, G3, with an optional T&M pool for change requests. Unit rates for post-Go-Live support are provided for Year 1 and two renewal years.

| Milestone | Payment |
|---|---|
| Kickoff (W1) | 10% |
| Gate G1 (W4) | 20% |
| Gate G2 (W8) | 25% |
| UAT sign-off (W11) | 20% |
| Go-Live acceptance (W12) | 15% |
| End of hyper-care (W14) | 10% |

---

## 13. Assumptions & Dependencies

1. DLD provides a sandbox environment for each source system (escrow, project register, ownership, mortgage, GIS) by end of Week 2.
2. UAE Pass client registration is approved by end of Week 2.
3. DLD appoints UAT user cohort (≥ 15 users) by end of Week 8.
4. AI model tenancy in UAE-resident regions is available via Azure; any non-UAE fallback is explicitly waived in writing.
5. DLD provides its 100-question AI UAT catalogue by Week 6.
6. External VAPT provider is accredited and mutually agreed by Week 4.
7. DLD steering committee meets fortnightly; decisions are made within 3 business days of request.

---

## 14. Annexes

- **Annex A** — RFP requirement traceability matrix
- **Annex B** — Architecture diagrams (logical, physical, data-flow)
- **Annex C** — [Threat model](docs/THREAT_MODEL.md) & DESC-CSP control matrix
- **Annex D** — Detailed 3-month Gantt with dependencies
- **Annex E** — CV pack for proposed team
- **Annex F** — Commercial schedule (sealed)
- **Annex G** — Sample Open Data OpenAPI specification
- **Annex H** — AI accuracy harness sample run & results
- **Annex I** — [Architecture Integration Map](docs/ARCHITECTURE_INTEGRATION_MAP.md) (§11.4.1)
- **Annex J** — [Visualisation Layer Comparative Analysis](docs/VISUALISATION_LAYER_ANALYSIS.md) (§11.4.2)
- **Annex K** — [DLD Data Familiarisation Plan & output templates](docs/familiarisation/README.md) (§12.1)
- **Annex L** — [RFP v1.3 Compliance Matrix](docs/COMPLIANCE_MATRIX.md) — every numbered requirement mapped to implementation file + test coverage
- **Annex M** — [DESC ISR v3 Control Mapping](docs/DESC_ISR_V3_COMPLIANCE.md) — clause-by-clause pre-VAPT self-assessment (§10.2 + §16.2 #66)
- **Annex N** — [OWASP Top 10 (2021) Control Mapping](docs/OWASP_TOP_10_MAPPING.md) — per-category mitigation pointers (§10.2.2)
- **Annex O** — [Risk Register](docs/RISK_REGISTER.md) — principal delivery / technical / data / security / operational risks (§16.2 #69)
- **Annex P** — [Knowledge Transfer & Exit Plan](docs/EXIT_PLAN.md) — handover deliverables + training programme + data-return obligations (§16.2 #70)
- **Annex Q** — [UAT Plan](docs/UAT_PLAN.md) — 10-category test plan with per-requirement test scripts (§17.3)
- **Annex R** — [v1.0 → v1.3 Alignment Summary](docs/V1_3_ALIGNMENT_SUMMARY.md) — 1-page evaluator quick-start mapping every v1.3 delta to where it's addressed

---

## 15. RFP v1.3 §16.2 Mandatory Proposal Contents — Coverage

| # | §16.2 Requirement | Where in this submission |
|---|---|---|
| 56 | Company Profile (UAE legal registration or UAE-based team commitment, mission, specialisation) | §7 Team Structure & Governance + Annex E CV pack |
| 57 | Portfolio of Relevant Projects (≥ 2 profiles) | Annex E + §2 Understanding of the Requirement |
| 58 | Data Integration & Accuracy Case Study (mandatory) | Annex K + §3 Proposed Solution / data accuracy methodology |
| 59 | AI System Demonstration (live demo link or recorded walkthrough) | Annex H — AI accuracy harness, plus live `/ai-agent` demo URL in cover letter |
| 60 | Proposed Technology Stack (with Architecture Integration Map for §11.4 alignment) | §4 Technology Stack & Architecture + **Annex I (Architecture Integration Map)** + **Annex J (Visualisation Layer Analysis)** |
| 61 | Proposed Hosting Model(s) with comparison | §4 Technology Stack & Architecture + Annex B physical diagrams |
| 62 | Proposed Project Team | §7 Team Structure & Governance + Annex E |
| 63 | Phased Delivery Timeline (Gantt) | §6 3-Month Implementation Plan + Annex D detailed Gantt |
| 64 | DLD Data Familiarisation Plan | **Annex K — [docs/familiarisation/README.md](docs/familiarisation/README.md)** |
| 65 | System Architecture Diagram (full technical) | Annex B logical/physical/data-flow |
| 66 | Security & Compliance Plan (DESC ISR v3 + VAPT + SOC + ISO 27001) | §5 Compliance, Security & Data Residency + Annex C threat model |
| 67 | Proposed SLA Document (P1–P4 + warranty + escalation contacts) | §10 Service Levels & Support Model |
| 68 | Itemised Cost Tables (§§14.1, 14.2, 14.3, 14.4 + §15.4) | §12 Commercial Summary + Annex F commercial schedule |
| 69 | Risk Register | §9 Risk Management |
| 70 | Knowledge Transfer & Exit Plan | §11 Knowledge Transfer & Training |

All 15 mandatory items per §16.2 are addressed in this submission.

---

*Prepared for the Dubai Land Department in response to tender **DLD-IRETP-2026-001 (RFP v1.3)**. Not for commercial redistribution.*
