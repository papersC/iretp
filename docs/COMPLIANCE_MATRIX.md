# IRETP Compliance Matrix — RFP v1.3

**RFP reference:** DLD-IRETP-2026-001 v1.3
**Last updated:** 2026-05-18
**Purpose:** Maps every numbered RFP requirement to its implementation file, key tests, and verification approach so DLD evaluators can confirm compliance without spelunking the codebase.

Symbols: ✅ implemented · 📄 doc/process · 🔧 operational (handled by deployment, not code)

---

## §3 External Public Portal

### 3.1 Content Management System

| Req | Specification | Implementation | Tests | Verification |
|---|---|---|---|---|
| FR-001 | Headless CMS, RBAC, bilingual, rich text + structured | `src/IRETP.Domain/Entities/CmsContent.cs`, `src/IRETP.Web/Components/Pages/Admin/Cms.razor` | manual UAT | ✅ Edit/preview/publish flow in `/admin/cms` |
| FR-002 | Staging → preview → production with rollback | `CmsContent.Status` enum, version history in audit log | manual UAT | ✅ Rollback via Admin UI |

### 3.2 Homepage — Market Overview Dashboard

| Req | Specification | Implementation | Tests | Verification |
|---|---|---|---|---|
| FR-003 | Real-Time KPI Cards, 15-min refresh | `KpiSnapshotCache` + `KpiSnapshotRefreshService` Hangfire job. Dashboard.razor renders "Data as of HH:mm ago" badge | covered indirectly via SLA test | ✅ `GET /api/dashboard/kpis` |
| FR-004 | 4 interactive charts with 6M/12M/36M/Custom filter | `MarketDataService.GetMonthlySalesSeriesAsync` + Chart.js in Dashboard.razor (Pass 12) | preview-verified | ✅ All 4 chart canvases render |
| FR-005 | Language + Currency switcher | `LocalizationService`, `CurrencyService` + UCB feed via `CurrencyRatesRefreshService` | — | ✅ Header switcher; preference cookie + account-stored |

### 3.3 Transactions Page

| Req | Specification | Implementation | Tests | Verification |
|---|---|---|---|---|
| FR-006 | Full registry, 5 years | `TransactionsController` + `GetTransactionsQuery` | — | ✅ 5-yr seed via `DbSeeder` |
| FR-007 | Multi-dimensional filters + URL persistence | `Transactions.razor` (Pass 19a — `HydrateFromUrl` / `ApplyFilters`) | preview-verified | ✅ Filter state in `?mode=&from=&...` |
| FR-008 | Export Excel/CSV/PDF, ≤ 50k rows | `TransactionExportService` (ClosedXML + PdfSharpCore) | — | ✅ `GET /api/transactions/export?format=xlsx\|csv\|pdf` |

### 3.4 Interactive GIS Map

| Req | Specification | Implementation | Tests | Verification |
|---|---|---|---|---|
| FR-009 | 3 heatmap layers + Project pins | `Map.razor` + `dld-map-interop.js` (MapLibre); 3 layers volume/pricesqft/yield, 4th = ESG (Pass 7) | preview-verified | ✅ Layer-switch under 2 s |
| FR-010 | Zone detail panel | `Map.razor` zone-click handler → `GetZoneDetailQuery` | — | ✅ Panel within 1 s of click |
| FR-011 | Individual project pins, status-coded, clustering | `MapDataService.GetProjectPinsAsync` + `setProjectPins` in interop (Pass 13). Zoom-based radius substitutes for cluster worker | preview-verified (171 pins rendered) | ✅ 4 status colours |
| FR-012 | Project detail panel | `Map.razor` pin-click popup | — | ✅ Renders developer + completion % |

### 3.5 Price & Rental Index

| Req | Specification | Implementation | Tests | Verification |
|---|---|---|---|---|
| FR-013 | Price Per Sqft Index, up-to-5 zone overlay | `PriceIndex.razor` + `IPriceIndexService` | — | ✅ Pass 10 seeded 8 rolling quarters |
| FR-014 | Rental Index + Yield Calculator | `RentalIndex.razor` + `IRentalIndexService` (Pass 19d audit confirmed present) | — | ✅ Live gross-yield + formula displayed |

---

## §4 Interactive Analytics Engine — Slice & Dice

| Req | Specification | Implementation | Tests | Verification |
|---|---|---|---|---|
| AN-001 | Up to 4 dimensions + 3 metrics | `Analytics.razor` + `AnalyticsQueryService` | — | ✅ Combo dropdowns + live render |
| AN-002 | 9 chart types | Chart.js via `iretpChart.render` interop | — | ✅ Bar/StackedBar/Line/Area/Scatter/Donut/Treemap/Table/KPI |
| AN-003 | Saved views + 12-cap dashboard with drag-drop | `SavedViewsService.Reorder` + `MaxSavedViews = 12` (Pass 6) | `SaveAnalyticsViewCommandHandlerTests` | ✅ 12-cap enforced + drag-drop validated |
| AN-004 | Full-dataset export Excel/CSV/PDF/JSON | `AnalyticsExportService` | — | ✅ 50k rows within 15 s |
| AN-005 | Zone Comparison up to 5 zones | `GetZoneComparisonQuery` + `ZoneCompare.razor` (Pass 6) | — | ✅ `/analytics/compare` |
| AN-006 | Shareable links, ≥ 12-month validity | `SavedAnalyticsView.ShareTokenExpiresAt` + 365-day lifetime (Pass 14) | `SharedAnalyticsViewTests` (6 cases) | ✅ Expired tokens rejected |

---

## §5 Real Estate AI Agent

| Req | Specification | Implementation | Tests | Verification |
|---|---|---|---|---|
| 5.1 | NO INVESTMENT ADVISORY constraint | `IAdvisoryGuardrail` + `KeywordAdvisoryGuardrail` (incl. forecast regex layer, Pass 19f) | `KeywordAdvisoryGuardrailTests` | ✅ Bilingual refusal text |
| AI-001 | Natural Language Data Queries (EN+AR) | `AIOrchestrator.ProcessQueryAsync` with RAG | `AiAccuracyHarness` 14-question seed | ✅ Source citation mandatory |
| AI-002 | In-Chat Dashboard Generation | `AIOrchestrator` returns Chart.js config + interactive defaults (Pass 19b) | — | ✅ Tooltip + axis defaults merged |
| AI-003 | In-Chat Download (PDF/Excel/PNG/CSV) | `AiAgent.razor` download buttons | — | ✅ All 4 formats |
| AI-004 | Deep Data Analysis (correlation/anomaly/trend) | `ITimeSeriesAnalyzer` + `TimeSeriesAnalyzer` (Pass 5, deterministic stats) | `TimeSeriesAnalyzerTests` (14 cases) | ✅ Wired into RAG context |
| AI-005 | DLD Service Navigation | `AIOrchestrator` topic = "service-nav" routes to navigation playbook | — | ✅ Step-by-step instructions |
| AI-006 | Session Context Memory (cross-session opt-in) | `UserAiMemory` entity + `UserConsentService` (Pass 15) | — | ✅ Consent toggle deletes memory rows (PDPL §19.2) |
| AI-007 | Multilingual Support (EN/AR core + 6 Phase 4) | `LocalizationService` + `AIOrchestrator` language metadata | — | ✅ EN/AR full; 6 extended graceful fallback |

### §5.3 AI Model Architecture

| Req | Specification | Implementation | Tests | Verification |
|---|---|---|---|---|
| 5.3 multi-model | Switchable models via admin config | `AIOrchestrator` tier routing (`nav`/`analytics`/`primary`/`secondary`) | — | ✅ Config-driven, no redeploy |
| 5.3 RAG mandatory | Grounded retrieval before generation | `BuildRagContextAsync` | — | ✅ Every answer cites source |
| 5.3 model fallback | Auto-fallback without user-visible error | `AIOrchestrator` catches + retries secondary tier | — | ✅ No raw API errors surfaced |
| 5.3 UAE residency | All inference within UAE | `AIModelSnapshot.UaeResident` enforced + `/healthz/sla` flag | `SlaHealthCheckTests` | ✅ Non-UAE = breach |
| 5.3 performance transparency | Model dashboard with mode/latency/status | `IAIModelMetrics` + `/admin/ai-models` page | — | ✅ Live admin UI |

---

## §6 Investor Notification & Alert System

| Alert type | Implementation | Verification |
|---|---|---|
| Price Movement | `InvestorAlertEvaluator` evaluates per zone | ✅ |
| New Project Launch | `InvestorAlertEvaluator` watches `Project` adds | ✅ |
| Watchlist Project Status | Watchlist + `Project.Status` change detection | ✅ |
| Rental Yield Threshold | `InvestorAlertEvaluator` evaluates yield | ✅ |
| Periodic Market Digest | `MarketDigestJob` (Hangfire weekly + monthly) | ✅ |
| Regulation / Policy Update | `RegulatoryViolation` ingestion + alert | ✅ |

### §6.2 Notification Delivery Standards

| Standard | Implementation | Tests |
|---|---|---|
| Email HTML with embedded chart + unsubscribe | `SmtpEmailSender` + `NotificationTemplates.BuildInvestorEmailHtml` with RFC 8058 `List-Unsubscribe` (Pass 19c) | — |
| SMS plain-text < 160 chars | `HttpSmsSender` | — |
| In-Platform Notification Centre | `Notifications.razor` + SignalR `NotificationsHub` | — |
| Email ≤ 5 min, SMS ≤ 3 min, In-Platform instant | `NotificationSlaHealthCheck` (Pass 17) | `NotificationSlaHealthCheckTests` (5 cases) |
| HMAC unsubscribe tokens | `HmacUnsubscribeTokenService` (Pass 19c) | `HmacUnsubscribeTokenServiceTests` (6 cases) |

---

## §7 Multilingual Requirements

| Scope | Implementation | Status |
|---|---|---|
| EN + AR core (Phase 1) | `LocalizationService._map` covers every UI string | ✅ |
| Full RTL for Arabic | Bootstrap RTL bundle in `src/IRETP.Web/wwwroot/lib/bootstrap/dist/css/bootstrap.rtl.css` + `[dir="rtl"]` rules in `src/IRETP.Web/wwwroot/css/design-system.css` + `dir` attribute on `<html>` | ✅ |
| 6 extended languages (Phase 4) | `SupportedLanguages` list in `UiStateService`; AI fallback to English for complex queries | ✅ scaffold + Phase-4 fill |
| Unicode/UTF-8 throughout | EF Core defaults; CMS content NVARCHAR(MAX) | ✅ |
| Chart labels respect active language | `iretpChart.render` reads `data-lang` | ✅ |
| Zone/project/developer name AR validation | `src/IRETP.AdminAPI/Controllers/NameValidationController.cs` queue | ✅ |
| Language preference per user + browser | `UiStateService.SetLanguage` + cookie + account claim | ✅ |

---

## §8 EWRS — Early Warning Risk System

### §8.1 — 10 Risk Indicators

All 10 indicators in `RiskEngineService.EvaluateRiskIndicatorsAsync` (Pass 11 — full rewrite). Threshold keys exposed as `public const` on the service, matching `DbSeeder.SeedRiskThresholdsAsync` verbatim.

| Indicator | Threshold key | Detection method | Tests |
|---|---|---|---|
| Project Delivery Delay — Warning | `ProjectDelayWarningKey` | `EvaluateProjectLevelIndicators` (project-level pass) | `RiskEngineServiceTests` |
| Project Delivery Delay — Critical | `ProjectDelayCriticalKey` | same | same |
| Escrow Shortfall — Warning | `EscrowShortfallWarningKey` | same | same |
| Escrow Shortfall — Critical | `EscrowShortfallCriticalKey` | same | same |
| Construction Suspension | `ConstructionSuspensionKey` | same | same |
| Sharp Transaction Volume Decline | `TransactionVolumeDeclineKey` | `EvaluateTransactionVolumeDeclineAsync` | same |
| Developer Score Deterioration | `DeveloperScoreDeteriorationKey` | `EvaluateDeveloperScoreDeteriorationAsync` | same |
| High-Risk Project Concentration | `HighRiskConcentrationKey` | `EvaluateHighRiskConcentrationAsync` | same |
| Price Decline (Zone) | `PriceDeclineKey` | `EvaluatePriceDeclineAsync` | same |
| Severe Regulatory Violation | `SevereRegulatoryViolationKey` | `EvaluateSevereRegulatoryViolationAsync` | same |

10 tests in `RiskEngineServiceTests`.

### §8.2 — Multi-Level Alert Escalation

`AlertEscalationService.EscalateBreachedAlertsAsync` (Hangfire job, every 5 min) walks open `RiskAlert` rows with `SlaDeadline < now`, sets `AutoEscalated=true`, advances `AlertLevel`, and rewrites the deadline using `AlertSla.DeadlineFor(newLevel)`.

| Level | SLA | Verification |
|---|---|---|
| L1 Operational | ≤ 4 BH acknowledge / 2 BD action | `AlertSlaTests` business-hours / weekend cases |
| L2 Managerial | ≤ 2 BH acknowledge / 1 BD decision | `AlertEscalationServiceTests` L1→L2 case |
| L3 Senior Leadership | ≤ 1 h business hours / ≤ 4 h after | `AlertSlaTests` |
| L4 Strategic | immediate, no SLA | `AlertSlaTests` L4-immediate case |

5 tests in `AlertEscalationServiceTests`.

### §8.3 — Dashboard, Inbox, Playbook, Threshold Config

| Capability | Implementation |
|---|---|
| Main Risk Overview Dashboard | `/admin/ewrs` (`EwrsDashboard.razor`) — Dubai-wide heatmap + summary KPI bar |
| Alert Inbox | `/admin/ewrs/alerts` (`EwrsAlerts.razor`) — status filter, owner, action notes |
| Playbook Integration | `/admin/playbooks` + per-alert SOP checklist with audit trail (`UpdateAlertPlaybookProgressAsync`) |
| Threshold Config Panel | `UpdateRiskThresholdCommandHandler` — audit-logged (Pass 19e) |

3 tests in `UpdateRiskThresholdCommandHandlerTests`.

### §8.4 — Escrow Account Monitoring

| Req | Implementation |
|---|---|
| ESC-001 Real-Time Escrow Dashboard | `EscrowMonitoring.razor` + `EscrowController` |
| ESC-002 Immutable Audit Log | `EscrowTransaction` append-only at DbContext boundary (Pass 19e) |
| ESC-003 Monthly Escrow Health Report | `EscrowHealthReportService` (Hangfire monthly) |

---

## §9 Developer Performance & Rating

All 6 sub-scores computed inline inside `DeveloperScoringService.CalculateQuarterlyScoresAsync` (Hangfire job, quarterly) and persisted as columns on `DeveloperScore`. Composite = `Σ(score × weight)` where weights live in `ScoringWeight` (admin-configurable, audit-logged).

| §9.1 Criterion | Default weight | Source | DeveloperScore column |
|---|---|---|---|
| On-Time Project Delivery | 25% | DLD project records + RERA delivery certs | `OnTimeDeliveryScore` |
| Unit Sales Completion | 20% | DLD transaction registry | `UnitSalesCompletionScore` |
| Escrow Account Health | 20% | RERA-certified Escrow Bank feeds | `EscrowHealthScore` |
| Regulatory Compliance | 15% | RERA violations + DLD audit records | `RegulatoryComplianceScore` |
| Financial Soundness | 10% | Bank financial reports submitted to RERA | `FinancialSoundnessScore` |
| Historical Project Success | 10% | DLD project completion records (10-yr) | `HistoricalSuccessScore` |

Weight config audit-logged via `UpdateScoringWeightsCommandHandler` (Pass 18 + Pass 19e central audit). 7 tests in `UpdateScoringWeightsCommandHandlerTests`.

| §9.1.2 Interface | Implementation |
|---|---|
| Main Developer Leaderboard | `/admin/developers` (`DeveloperRating.razor`) — composite + radar mini-charts |
| Developer Comparison (up to 4) | `/admin/developers/compare` (`DeveloperComparison.razor`) — overlay radar |
| Developer Profile Page | `/admin/developers/{id}` |
| Weight Configuration Panel | `UpdateScoringWeightsCommandHandler` audit-logged |
| Public Developer Scorecard | `/developers/scorecards` (`DeveloperScorecards.razor`, Pass 2) |

---

## §10 Non-Functional Requirements

### §10.1 Performance

| Metric | Target | Implementation |
|---|---|---|
| Homepage load | <3 s P95 | Static assets via CDN, 15-min KPI cache |
| API response | <500 ms P95 | EF Core w/ no-tracking reads, AsNoTracking |
| Map filter update | <2 s | MapLibre client-side, prebuilt GeoJSON |
| AI text response | <8 s P90 | `SlaHealthCheck` enforces |
| AI chart generation | <15 s P90 | same |
| Concurrent users | 5k public + 500 internal | Hosting capacity per Bicep |
| Uptime | 99.9% / month | `/healthz/live` + `/healthz/ready` monitoring |
| KPI freshness | 15 min | `KpiSnapshotCache` + `SlaHealthCheck` |
| Transaction lag | 24 h | **v1.3 addition**: `SlaHealthCheck` ingests Fabric watermark (Pass 21) |

### §10.2 Security & DESC ISR v3

| Domain | Implementation |
|---|---|
| Info Security Governance | RACI in `docs/RUNBOOK.md`; annual risk assessment scheduled |
| Asset Management | Asset register stub; data classification at API layer |
| Cryptography | TLS 1.2+ enforced in Bicep; secrets via Key Vault MSI |
| Access Control | RBAC enforced via `AuthorizationPolicies` + MFA mandatory for internal |
| Incident Management | `docs/RUNBOOK.md` SIRP playbook; 2h CISO + 24h DESC notification |
| Third-Party Risk | Section in `docs/RUNBOOK.md` |
| VAPT | Automated OWASP scan in `.github/workflows/security.yml` |
| Audit Log Immutability | `IretpDbContext.SaveChangesAsync` rejects update/delete on `AuditLog` (Pass 19e) |

3 tests in `AuditLogImmutabilityTests`.

### §10.3 RBAC Matrix

| Role | Policy | Impl |
|---|---|---|
| Public Visitor | anonymous + CAPTCHA on export | `SimpleCaptchaService` |
| Registered Investor | `RegisteredInvestorPolicy` | ASP.NET Identity + MFA optional |
| DLD Staff — Viewer | `DldViewerPolicy` | `[Authorize(Roles)]` |
| DLD Staff — Operator | `DldOperatorPolicy` | same |
| DLD Supervisor | `DldSupervisorPolicy` | same + threshold/weight edit |
| System Administrator | `SystemAdministratorPolicy` | full + PIM JIT |

Policy registry at `src/IRETP.Infrastructure/Identity/AuthorizationPolicies.cs`.

---

## §11 Technology & Hosting

### §11.4 Microsoft Fabric / OneLake (v1.3)

| Req | Implementation | Tests |
|---|---|---|
| §11.4 OneLake Gold layer consumption | `IFabricGoldDataSource` + `PassthroughFabricGoldDataSource` (Pass 21) | `PassthroughFabricGoldDataSourceTests` (6 cases) |
| §11.4 No parallel data stores | Operational state CDC-replicated back into Silver (documented in Integration Map §3) | — |
| §11.4.1 Architecture Integration Map | `docs/ARCHITECTURE_INTEGRATION_MAP.md` | 📄 |
| §11.4.1 Justification register | Integration Map §4 | 📄 |
| §11.4.1 Familiarisation Plan | `docs/familiarisation/06_fabric_environment.md` | 📄 |
| §11.4.2 Visualisation comparative analysis | `docs/VISUALISATION_LAYER_ANALYSIS.md` | 📄 |
| Admin verification endpoints | `FabricController` — status / semantic-models / freshness | — |
| Admin UI | `/admin/fabric` (`FabricStatus.razor`) | preview-verified |

---

## §12 Data Understanding & Analytics Assessment

| Output | Template |
|---|---|
| Source System Inventory | `docs/familiarisation/01_source_inventory.md` |
| Field Mapping | `docs/familiarisation/02_field_mapping.md` |
| Data Quality Baseline | `docs/familiarisation/03_data_quality_baseline.md` |
| Calculation Rules | `docs/familiarisation/04_calculation_rules.md` |
| Historical Data Assessment | `docs/familiarisation/05_historical_data.md` |
| Microsoft Fabric Environment Assessment (v1.3) | `docs/familiarisation/06_fabric_environment.md` |

Pre-Publication Analytics Assessment gate documented in `docs/familiarisation/README.md`.

---

## §13 Phased Delivery + Phase 4 Extras

Phase 4 supplementary features:
- **ESG / Sustainability** — `Esg.razor` + 4th heatmap layer on `/map` (Pass 7) + `ProjectCertification` entity ✅
- **International Market Benchmarking** — `Benchmark.razor` + `MarketBenchmark` entity + `BenchmarkRefreshService` quarterly job ✅
- **PDF Investment Profile Generator** — `InvestorScorecardPdfService` (zone + project level) ✅
- **Complete Technical Documentation Package** — `docs/ARCHITECTURE.md`, `API_REFERENCE.md`, `RUNBOOK.md`, `DISASTER_RECOVERY.md`, `DATA_DICTIONARY.md`, `ARCHITECTURE_INTEGRATION_MAP.md`, `VISUALISATION_LAYER_ANALYSIS.md`, `familiarisation/*` ✅

---

## §15 Warranty & Support — operational coverage

| Category | Coverage |
|---|---|
| Defect Resolution (P1–P4 SLA) | `docs/RUNBOOK.md` P1–P4 triage |
| Data Pipeline Defect Correction | 2 BH response, 24 h fix — operational |
| Security Patch Management | Critical 24h / High 72h / Other 14d — `.github/workflows/security.yml` flags |
| Performance Defect Resolution | 48 h fix on 3-day breach — `/healthz/sla` alerting |
| DLD Data Structure Changes | 10 BD update window — operational |
| Minor Changes Pool | 120 person-hours — commercial |
| CMS & Technical Assistance | 4 × 1-hr refresher sessions — operational |
| Infrastructure Operations | DESC-certified SOC monitoring — 🔧 |

---

## §16.2 Mandatory Proposal Contents

See `PROPOSAL.md §15` for the complete 15-item coverage table.

---

## §17 Acceptance Criteria

| § | Criterion | Verification |
|---|---|---|
| 17.1 | Phase-level acceptance form | UAT process |
| 17.2 | 500-record accuracy ≥ 99.5% | DLD Data Liaison Officer reconciliation |
| 17.3 | 10 UAT categories (functional/performance/AI/security/a11y/lang/data/usability/CMS/alerts) | UAT plan in `docs/RUNBOOK.md` |

---

## §19 Data Governance, Privacy, Ownership

| Req | Implementation |
|---|---|
| Data ownership = DLD | License declaration in `PROPOSAL.md §11` |
| PDPL compliance | No PII in public APIs; aggregation enforced in DTOs |
| No PII in public interfaces | DTOs strip names/IDs; verified in code review |
| DPA between DLD and vendor | Operational |
| Cross-session AI memory consent | `IUserConsent` + opt-out deletes `UserAiMemory` rows (Pass 15) |
| Data accuracy ≥ 99.5% | Pre-Publication Analytics Assessment gate |
| Completeness 100% mandatory fields | Field validation + DbSeeder constraints |
| Timeliness — transaction lag ≤ 24h | `SlaHealthCheck` Fabric freshness probe (Pass 21) |
| Consistency across modules | Single Gold-layer source of truth |

---

## §20 Additional Recommended Features

| Feature | Status |
|---|---|
| International Market Benchmarking | ✅ Phase 4 ready |
| ESG / Sustainability Module | ✅ Phase 4 ready (Pass 7) |
| Open Data API Portal | ✅ Phase 1 — `/api/v1/open-data/*` + OpenAPI 3.0 (Pass 3) |
| Public Developer Scorecard | ✅ Phase 3 — `/developers/scorecards` (Pass 2) |
| Beneficial Ownership | ✅ `/beneficial-ownership` page with EN+AR names (Pass 8) |
| Mortgage & Debt Transparency | ✅ `/mortgage` page with 5 KPIs incl. AvgMortgageValueAed (Pass 8) |

---

## Test Coverage Summary

| Suite | Count |
|---|---|
| `KeywordAdvisoryGuardrailTests` | 18 |
| `TimeSeriesAnalyzerTests` | 14 |
| `RiskEngineServiceTests` | 10 |
| `UpdateScoringWeightsCommandHandlerTests` | 7 |
| `AlertSlaTests` | 6 |
| `HmacUnsubscribeTokenServiceTests` | 6 |
| `PassthroughFabricGoldDataSourceTests` | 6 |
| `SharedAnalyticsViewTests` | 6 |
| `SlaHealthCheckTests` | 7 |
| `AlertEscalationServiceTests` | 5 |
| `ComplianceMatrixTraceabilityTests` | 5 |
| `NotificationSlaHealthCheckTests` | 5 |
| `SaveAnalyticsViewCommandHandlerTests` | 4 |
| `UpdateRiskThresholdCommandHandlerTests` | 4 |
| `AuditLogImmutabilityTests` | 3 |
| **Total** | **106** |

Build: 0 warnings, 0 errors. All 106 tests pass.

> Note: `ComplianceMatrixTraceabilityTests` (in `tests/IRETP.Tests/Documentation/`) self-validates **this document** — every `src/...` file-path claim must resolve, every familiarisation doc must be linked from its README, and the required documentation pack must be on disk. If a file is renamed or removed and the matrix isn't updated, the test fails. This stops the Compliance Matrix from rotting silently.

---

*Living document. Updates tracked in git history. The DOCX-format compliance matrix (`ComplianceMatrix_IRETP.docx`) is regenerated from this Markdown via `tools/` scripts when DLD requests an offline copy.*
