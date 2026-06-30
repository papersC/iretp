# RFP v1.0 → v1.3 Alignment Summary

**RFP reference:** DLD-IRETP-2026-001
**Document purpose:** Quick-start for DLD evaluators who want to understand what changed in the IRETP submission to track RFP v1.3 (vs the v1.0 baseline). One page (you are here).
**Audience:** DLD technical-evaluation panel, DLD CISO, DLD Data Liaison Officer.

---

## What v1.3 added vs v1.0

| § | New in v1.3 | Source |
|---|---|---|
| §11.4 | Microsoft Fabric / OneLake is the recommended data architecture; vendors must align with existing Silver+Gold layers, Data Factory pipelines, and semantic models | RFP §11.4 |
| §11.4.1 | Architecture Integration Map deliverable + justification register for non-Fabric components + Familiarisation Plan | RFP §11.4.1 |
| §11.4.2 | Visualisation Layer comparative analysis required for External Portal (Power BI vs. alternative); Power BI retained as preferred for Internal Platform | RFP §11.4.2 |
| §12.1 | Data Familiarisation activities now include explicit Microsoft Fabric environment assessment | RFP §12.1 |
| §16.2 | Mandatory Proposal Contents Checklist expanded — 15 numbered items including Risk Register (#69) and Exit Plan (#70) | RFP §16.2 |
| §16.1 | Vendor qualifications relaxed — no fixed minimum years/team sizes; flexible UAE-presence requirement | RFP §16.1 |

---

## How the IRETP submission addresses each v1.3 delta

### §11.4 — OneLake Gold-layer consumption

| Surface | Where |
|---|---|
| Code abstraction | `src/IRETP.Application/Interfaces/IFabricGoldDataSource.cs` |
| Reference implementation | `src/IRETP.Infrastructure/Services/Fabric/PassthroughFabricGoldDataSource.cs` |
| Mode config | `Fabric:Mode` in `appsettings.json` — `Sql` / `PassthroughMirror` / `OneLakeDirect` / `FabricSemanticModel` |
| Admin endpoints | `GET /api/admin/fabric/{status,semantic-models,freshness}` |
| Admin UI | `/admin/fabric` (`src/IRETP.Web/Components/Pages/Admin/FabricStatus.razor`) |
| SLA integration | `SlaHealthCheck` ingests Fabric Gold freshness — `/healthz/sla` flags transaction lag > 24 h |
| Tests | `PassthroughFabricGoldDataSourceTests` (6) + `SlaHealthCheckTests` (7) |
| Doc | [`docs/ARCHITECTURE_INTEGRATION_MAP.md`](ARCHITECTURE_INTEGRATION_MAP.md) |

### §11.4.1 — Architecture Integration Map + Justification Register

| Output | Where |
|---|---|
| Component-by-component alignment table | [Integration Map §2](ARCHITECTURE_INTEGRATION_MAP.md) |
| Mermaid + textual data-flow diagram | [Integration Map §3](ARCHITECTURE_INTEGRATION_MAP.md) |
| Justification register for non-Fabric components | [Integration Map §4](ARCHITECTURE_INTEGRATION_MAP.md) |
| Familiarisation Plan | [familiarisation/06_fabric_environment.md](familiarisation/06_fabric_environment.md) |

### §11.4.2 — Visualisation Layer Comparative Analysis

| Output | Where |
|---|---|
| Power BI vs custom-web 4-axis analysis | [`docs/VISUALISATION_LAYER_ANALYSIS.md`](VISUALISATION_LAYER_ANALYSIS.md) |
| Recommendation | Custom web (Chart.js + MapLibre) for External Portal; Power BI retained for Internal Platform |
| 5-year TCO impact | ~AED 12–15 m saved over Power BI Embedded floor |

### §12.1 — Familiarisation outputs

| Document | Template |
|---|---|
| Source System Inventory | [`docs/familiarisation/01_source_inventory.md`](familiarisation/01_source_inventory.md) |
| Field Mapping | [`docs/familiarisation/02_field_mapping.md`](familiarisation/02_field_mapping.md) |
| Data Quality Baseline | [`docs/familiarisation/03_data_quality_baseline.md`](familiarisation/03_data_quality_baseline.md) |
| Calculation Rules | [`docs/familiarisation/04_calculation_rules.md`](familiarisation/04_calculation_rules.md) |
| Historical Data Assessment | [`docs/familiarisation/05_historical_data.md`](familiarisation/05_historical_data.md) |
| Microsoft Fabric Environment Assessment (v1.3 addition) | [`docs/familiarisation/06_fabric_environment.md`](familiarisation/06_fabric_environment.md) |

### §16.2 — Mandatory Proposal Contents

All 15 items covered. The PROPOSAL.md §15 has the full coverage table. The new annexes added for v1.3 alignment:

| Annex | Doc | RFP item |
|---|---|---|
| I | [Architecture Integration Map](ARCHITECTURE_INTEGRATION_MAP.md) | #60 + §11.4.1 |
| J | [Visualisation Layer Analysis](VISUALISATION_LAYER_ANALYSIS.md) | #60 + §11.4.2 |
| K | [Familiarisation Plan](familiarisation/README.md) | #64 |
| L | [Compliance Matrix](COMPLIANCE_MATRIX.md) | self-validating; cross-cuts #60, #66, #67 |
| M | [DESC ISR v3 Mapping](DESC_ISR_V3_COMPLIANCE.md) | #66 |
| N | [OWASP Top 10 Mapping](OWASP_TOP_10_MAPPING.md) | #66 |
| O | [Risk Register](RISK_REGISTER.md) | #69 |
| P | [Knowledge Transfer & Exit Plan](EXIT_PLAN.md) | #70 |
| Q | [UAT Plan](UAT_PLAN.md) | covers §17.3 |

---

## State at submission

| Metric | Value |
|---|---|
| Build | 0 warnings, 0 errors |
| Tests | 106/106 passing |
| RFP v1.3 §16.2 mandatory items | 15 / 15 covered |
| Reference-build coverage of RFP functional requirements | ~100% |
| Self-validating compliance docs | 3 (Compliance Matrix, DESC ISR, OWASP) — drift fails CI |
| Doc pack | 17 markdown files in `docs/` |

---

## Where to start as a DLD evaluator

1. **5-minute read** — this document
2. **Compliance verification** — [`COMPLIANCE_MATRIX.md`](COMPLIANCE_MATRIX.md) maps every numbered requirement to its implementation file
3. **Fabric alignment** — [`ARCHITECTURE_INTEGRATION_MAP.md`](ARCHITECTURE_INTEGRATION_MAP.md) shows how the platform consumes the existing OneLake Gold layer
4. **Security posture** — [`DESC_ISR_V3_COMPLIANCE.md`](DESC_ISR_V3_COMPLIANCE.md) + [`OWASP_TOP_10_MAPPING.md`](OWASP_TOP_10_MAPPING.md) + [`THREAT_MODEL.md`](THREAT_MODEL.md)
5. **UAT readiness** — [`UAT_PLAN.md`](UAT_PLAN.md) walks the 10 §17.3 categories with concrete pass/fail scripts
6. **Live admin verification** — once the platform is deployed, visit `/admin/fabric` to see the active mode + freshness watermark + Gold semantic-model catalog

---

*Updated 2026-05-18.*
