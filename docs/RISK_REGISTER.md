# IRETP Project Risk Register

**RFP reference:** DLD-IRETP-2026-001 v1.3, §16.2 deliverable #69
**Document purpose:** Identify the principal delivery, technical, data, security, and operational risks for the IRETP programme, with probability and impact ratings and proposed mitigations.
**Audience:** DLD Project Sponsor, DLD Steering Committee, vendor PMO.
**Review cadence:** Monthly during delivery; quarterly post-go-live.

---

## Scoring legend

| Likelihood | Range |
|---|---|
| L1 — Very low | <10% chance during programme |
| L2 — Low | 10–25% |
| L3 — Medium | 25–50% |
| L4 — High | 50–75% |
| L5 — Very high | >75% |

| Impact | Effect if realised |
|---|---|
| I1 — Negligible | Schedule slip < 2 days; no data exposure; no DLD reputational impact |
| I2 — Minor | Schedule slip 2–10 days; recoverable defect; no PDPL/DESC breach |
| I3 — Moderate | Schedule slip 2–4 weeks; UAT re-test required; minor SLA miss |
| I4 — Major | Phase deferred; data accuracy below 99.5%; SLA breach > 24 h |
| I5 — Severe | DESC compliance failure; PDPL breach; GRETI ranking damage; programme suspended |

Severity = Likelihood × Impact.

---

## 1. Delivery risks

| ID | Risk | Likelihood | Impact | Severity | Mitigation | Owner |
|---|---|---|---|---|---|---|
| D-01 | 120-day delivery window doesn't accommodate UAT defect cycles | L3 | I4 | High | Pre-built reference implementation covers ~88% functionality, leaving the residual ~12% + integration as the critical path. Phased UAT with each delivery to compress defect-resolution loops. | Vendor PM |
| D-02 | Phase 1 Pre-Publication Analytics Assessment (§12.2) fails on first attempt | L3 | I4 | High | Pre-publication assessment runs continuously from week 2 against incremental Silver-layer loads. 99.5% accuracy threshold tracked weekly. | Vendor Data Lead |
| D-03 | DLD source-system access delayed | L3 | I4 | High | Familiarisation kickoff in week 1 with DLD Data Liaison Officer; written request pack issued at contract signing. | DLD Data Liaison + Vendor PM |
| D-04 | Microsoft Fabric / OneLake schema not yet stable enough for Gold-layer reads | L2 | I4 | Medium | `IFabricGoldDataSource` abstraction allows passthrough-mirror mode in lower environments; promotion to OneLakeDirect happens after the Gold schema is signed off by DLD. | Vendor Data Lead |
| D-05 | DESC-CSP certification timeline extends beyond Phase 1 | L3 | I3 | Medium | Hosting provider already DESC-CSP certified (Azure UAE North). Application-layer compliance evidence captured in `DESC_ISR_V3_COMPLIANCE.md` from kickoff. | Vendor Security Lead |

## 2. Technical risks

| ID | Risk | Likelihood | Impact | Severity | Mitigation | Owner |
|---|---|---|---|---|---|---|
| T-01 | AI model UAE-residency endpoint unavailable for chosen primary model | L3 | I3 | Medium | Multi-model orchestration layer per RFP §5.3 supports swap to UAE-hosted self-hosted LLM (Llama/Mistral) without code change. Fallback declared in proposal §4. | Vendor AI Lead |
| T-02 | AI accuracy < 90% on DLD 100-question UAT set | L3 | I4 | High | `AiAccuracyHarness` runs a 14-question seed set in CI; expansion to 100 begins in Phase 1 week 4 with DLD. RAG context grounded in Gold layer; deterministic stats from `TimeSeriesAnalyzer` reduce hallucination on numerical answers. | Vendor AI Lead |
| T-03 | Map performance fails the 5 s init / 2 s layer-switch budget | L2 | I3 | Low | MapLibre + Mapbox-style vector tiles, pre-generated GeoJSON for zone boundaries, client-side filter on existing dataset (no roundtrip). Verified under load in Pass 13. | Vendor FE Lead |
| T-04 | Chart performance fails the 2 s filter / 3 s analytics-render budget at 50k rows | L3 | I3 | Medium | KPI snapshot cache absorbs dashboard reads. Analytics queries use AsNoTracking + indexed columns. Worst-case server-side aggregation. | Vendor BE Lead |
| T-05 | Concurrent-user load test (5,000 public + 500 internal) fails | L2 | I4 | Medium | Bicep IaC provisions App Service P1v3 multi-instance + autoscale; Redis Premium for shared cache. Load test scripted in `tests/` and rerun before each phase go-live. | Vendor Infra Lead |

## 3. Data risks

| ID | Risk | Likelihood | Impact | Severity | Mitigation | Owner |
|---|---|---|---|---|---|---|
| DA-01 | Historical data quality issues block 5-year publication | L3 | I4 | High | `docs/familiarisation/05_historical_data.md` template forces year-by-year quality baseline before publication. Remediation plan agreed with DLD before any year is enabled on the public portal. | Vendor Data Lead |
| DA-02 | DLD source schema changes mid-flight | L3 | I3 | Medium | RFP §15.1 provides 10 BD update window. Data Factory pipelines decoupled from application via Gold layer — schema changes absorbed in Silver→Gold transformation. | Vendor Data Lead |
| DA-03 | PII leaks via aggregate disaggregation attack | L2 | I5 | Medium | Public aggregates k-anonymised at zone-level (min 5 transactions per group). BeneficialOwner data restricted to authenticated DLD staff only. | Vendor Privacy Lead |
| DA-04 | Outlier transactions skew price/yield indices | L3 | I3 | Medium | ±5σ outlier flagging with DLD review queue. Median (not mean) for yield aggregates. Min-sample threshold (≥5) before publishing a derived metric. | Vendor Analytics Lead |

## 4. Security risks

| ID | Risk | Likelihood | Impact | Severity | Mitigation | Owner |
|---|---|---|---|---|---|---|
| S-01 | DESC-authorised VAPT finds Critical/High before Phase 3 | L3 | I4 | High | Internal OWASP ZAP scans on every staging deploy. `DESC_ISR_V3_COMPLIANCE.md` self-assessment captures known PI/P controls. VAPT engagement scheduled Phase 3 week 6 to leave a remediation window. | Vendor Security Lead |
| S-02 | DDoS attack against External Portal during GRETI announcement | L2 | I4 | Medium | WAF + CDN + rate-limiting middleware. Burst handling tested in load-test plan. Incident response per `DESC_ISR_V3_COMPLIANCE.md §8.1`. | Vendor Security Lead + DLD CISO |
| S-03 | Compromised admin credentials | L2 | I5 | Medium | MFA mandatory for internal users; PIM JIT for SystemAdmin; append-only audit log (Pass 19e) means a compromised admin cannot rewrite history; AdminAPI bound to private VNET. | Vendor Security Lead |
| S-04 | Third-party AI provider breach exposes prompt logs | L2 | I3 | Low | UAE-resident inference endpoints only. Prompts redacted (no PII per `KeywordAdvisoryGuardrail` + DTO filter). Provider security clauses in vendor contracts. | Vendor AI Lead |
| S-05 | OWASP A10 SSRF via AI-orchestrator outbound HTTP | L1 | I4 | Low | All outbound clients allow-listed by host; AdminAPI unreachable from public WebAPI request context. See `docs/OWASP_TOP_10_MAPPING.md` A10. | Vendor Security Lead |

## 5. Operational risks

| ID | Risk | Likelihood | Impact | Severity | Mitigation | Owner |
|---|---|---|---|---|---|---|
| O-01 | P1 incident during 99.9% SLA period | L3 | I3 | Medium | RFP §15.2 SLA — 1 h response 24/7, 4 h resolution. Vendor on-call schedule confirmed in support plan. P1 root-cause + corrective action reported within 3 BD. | Vendor Ops Lead |
| O-02 | Notification SLA breach (email > 5 min / SMS > 3 min) | L2 | I3 | Low | `NotificationSlaHealthCheck` (Pass 17) probes the last hour every minute; P95 breach pages oncall via OTLP alert rule. | Vendor Ops Lead |
| O-03 | DR drill exposes RTO > 4 h or RPO > 1 h | L2 | I4 | Medium | Quarterly drill per RFP §11.3, results reported to DLD. App Service slot-swap + SQL geo-replication + OneLake geo-redundancy keep recovery inside the budget. | Vendor Ops Lead |
| O-04 | 12-month support pool exhausted before year-end | L3 | I2 | Low | Pool tracking against monthly burn rate visible in support pack. Excess hours quoted separately per RFP §15.1. | Vendor PM |
| O-05 | Knowledge-transfer handover incomplete at contract expiry | L1 | I4 | Low | Exit plan (`docs/EXIT_PLAN.md`) lists every deliverable with handover date. CI/CD self-contained — DLD receives source + IaC + docs + secrets-rotation runbook. | Vendor PM |

---

## Top risks (severity = High)

1. **D-01** — 120-day window vs UAT defect cycle — pre-built reference implementation is the primary mitigation
2. **D-02** — Pre-Publication Analytics Assessment failure — continuous-assessment cadence from week 2
3. **D-03** — DLD source-system access delay — kickoff in week 1 with written request pack
4. **T-02** — AI accuracy < 90% — `AiAccuracyHarness` + RAG grounding + deterministic stats layer
5. **DA-01** — Historical data quality — year-by-year baseline before publication
6. **S-01** — VAPT Critical/High findings before Phase 3 — internal ZAP + pre-assessment + remediation window

---

## Review log

| Date | Reviewer | Changes |
|---|---|---|
| 2026-05-18 | Vendor PM | Initial draft for RFP v1.3 submission |

---

*This document is reviewed monthly during delivery and quarterly post-go-live. Updates tracked in git history.*
