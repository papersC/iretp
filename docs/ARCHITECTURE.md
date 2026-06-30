# IRETP — System Architecture

Reference architecture for the **Integrated Real Estate Transparency Platform** delivered under tender `DLD-IRETP-2026-001`. This document is one of the four deliverables required by RFP §13 item 53 (technical documentation package); the others are [`API_REFERENCE.md`](API_REFERENCE.md), [`RUNBOOK.md`](RUNBOOK.md), [`DISASTER_RECOVERY.md`](DISASTER_RECOVERY.md), and [`DATA_DICTIONARY.md`](DATA_DICTIONARY.md).

## 1. Solution layout

The codebase follows a Clean Architecture split. Each project has a single, well-bounded responsibility:

| Project | Responsibility |
|---|---|
| `IRETP.Domain` | Framework-free entities, value objects, enums, and domain-logic helpers (`AlertSla`). Depends on nothing. |
| `IRETP.Application` | CQRS commands, queries, DTOs, and feature-level interfaces. Thin — no infrastructure or UI concerns. |
| `IRETP.Infrastructure` | EF Core `IretpDbContext`, migrations, repository implementations, ASP.NET Identity, Hangfire services, AI orchestration, notification senders. |
| `IRETP.WebAPI` | Public read/write HTTP surface. Hosts SignalR for real-time notifications and the Hangfire dashboard. |
| `IRETP.AdminAPI` | DLD-internal endpoints (EWRS threshold editing, scoring-weight configuration, AI model status, audit). Protected by the policy registry in `IRETP.Infrastructure/Identity/AuthorizationPolicies.cs`. |
| `IRETP.Web` | Blazor Server frontend. Public portal + investor pages + admin pages. Routes WebAPI + AdminAPI calls through `WebApiClient`/`AdminApiClient`. |
| `IRETP.Tests` | xUnit coverage for domain rules, application handlers, and infrastructure services. |

## 2. Runtime topology

```
                        ┌─────────────────────────────────┐
                        │   Azure Front Door (Premium)    │
                        │   WAF + CDN + TLS 1.2 min       │
                        └───────────────┬─────────────────┘
                                        │
             ┌──────────────────────────┼───────────────────────────┐
             │                          │                           │
             ▼                          ▼                           ▼
     ┌─────────────┐            ┌──────────────┐          ┌───────────────┐
     │ Blazor Web  │            │  WebAPI      │          │  AdminAPI     │
     │  (:5010)    │──────────▶ │  (:5000)     │          │  (:5002)      │
     └─────┬───────┘            └──────┬───────┘          └───────┬───────┘
           │                           │                           │
           │  SignalR hub              │  EF Core                  │  EF Core
           │                           │                           │
           └──────────┬────────────────┴───────────────┬───────────┘
                      │                                │
                      ▼                                ▼
             ┌──────────────────┐            ┌────────────────────┐
             │ SQL Server (GP)  │            │  Azure Key Vault   │
             │ + ElasticPool    │            │ (secrets, MSI)     │
             └─────────┬────────┘            └────────────────────┘
                       │
                       │
      ┌────────────────┼─────────────────┬────────────────────┐
      ▼                ▼                 ▼                    ▼
┌───────────┐   ┌─────────────┐   ┌────────────────┐   ┌────────────────┐
│ Hangfire  │   │ Blob Storage │  │  App Insights  │  │  Log Analytics │
│ schema    │   │ (GRS)        │  │  (P95/health)  │  │  (logs/traces) │
└───────────┘   └──────────────┘  └────────────────┘  └────────────────┘
```

Infrastructure provisioning lives under `infra/bicep/`:

- `main.bicep` — subscription-scope wrapper. Pinned to **UAE North**.
- `platform.bicep` — the components above. All resources are MSI-enabled and disallow public blob access; Key Vault has purge-protection on; App Service Plan is P1v3 with three sites (public web, WebAPI, AdminAPI) behind the same plan.

## 3. Request flow

### 3.1 Public query (anonymous)

```
Browser ─▶ Front Door ─▶ Blazor Web ─▶ WebAPI /api/v1/open-data/* ─▶ MediatR handler ─▶ IRepository<T> ─▶ SQL
                                  │
                                  ├─▶ SignalR hub (for real-time alerts)
                                  └─▶ CurrencyService (cached UAECB rates)
```

Open-data endpoints are **PII-free** (RFP §19.2); the OpenAPI spec is published at `/openapi/public.json`.

### 3.2 Authenticated investor

```
Browser ─▶ Front Door ─▶ Blazor Web ─▶ WebAPI ─▶ AuthController (JWT+MFA)
                                                │
                                                ├─▶ /api/account/* (profile, consent, data-export)
                                                ├─▶ /api/alerts/*  (watchlist, 6 alert types)
                                                └─▶ /api/ai/query  (AIOrchestrator, personalised if opted-in)
```

MFA is enforced at the controller/policy level — see §5 below.

### 3.3 Internal DLD user

```
Browser ─▶ Front Door ─▶ Blazor Web (admin routes) ─▶ AdminAPI
                                                 ├─▶ /api/admin/ewrs/*           (DldSupervisor, SystemAdministrator)
                                                 ├─▶ /api/admin/developers/*    (SystemAdministrator for weight changes)
                                                 ├─▶ /api/admin/ai-models/*     (SystemAdministrator)
                                                 └─▶ /api/admin/cms/*           (DldOperator+)
```

## 4. Data layer

SQL Server is the OLTP store. EF Core migrations (under `src/IRETP.Infrastructure/Data/Migrations/`) are the only source of truth for schema — there is no hand-maintained DDL. Every `BaseEntity` row carries `CreatedAt` / `UpdatedAt`, enforced by the `SaveChangesAsync` interceptor on `IretpDbContext`.

Key tables and their business function are catalogued in [DATA_DICTIONARY.md](DATA_DICTIONARY.md).

Caching:

- **KPI snapshot cache** (`IKpiSnapshotCache`) — in-memory singleton refreshed every 15 minutes by `KpiSnapshotRefreshService`. Satisfies FR-003 data-freshness.
- **Currency rates** — persisted per-day via `CurrencyRatesRefreshService`; `CurrencyService` reads today's row from cache.

## 5. Authentication & authorization

ASP.NET Identity (`ApplicationUser`) extends `IdentityUser` with:
- Preference fields: `PreferredLanguage`, `PreferredCurrency`.
- PDPL consent flags: `ConsentMarketing`, `ConsentAiMemory`, `ConsentUsageAnalytics`, `ConsentUpdatedAt`.
- `IsInternalUser` for the DLD/investor split.

Authentication paths:
- External: email+password with 2FA (TOTP authenticator app). Endpoints: `/api/auth/login`, `/api/auth/login-2fa`, `/api/auth/2fa/setup`, `/api/auth/2fa/enable`.
- Internal: same flow, but **MFA is mandatory** and sessions expire after **30 minutes** of inactivity (RFP §10.2.1). `inactivity-watch.js` enforces the idle ceiling client-side.

Authorization policies are declared centrally in `IRETP.Infrastructure/Identity/AuthorizationPolicies.cs` and registered by both `WebAPI` and `AdminAPI` start-up code. The six RFP §10.3 roles:
- `PublicVisitor` — no auth; CAPTCHA gates exports.
- `RegisteredInvestor` — self-service alerts, watchlist, saved analytics views.
- `DldViewer` — read-only internal.
- `DldOperator` — CMS content + alert acknowledgement.
- `DldSupervisor` — EWRS threshold edit + audit log.
- `SystemAdministrator` — weight configuration, AI-model switching, RBAC.

## 6. Background jobs

Hangfire schedules live in `src/IRETP.WebAPI/Program.cs` (around line 333):

| Cron | Service | Purpose |
|---|---|---|
| `*/2 * * * *` | `AlertDeliveryService.DeliverPendingAlertsAsync` | Dispatch email + SMS for pending notifications (RFP §6.2 SLA). |
| `*/5 * * * *` | `AlertEscalationService.EscalateBreachedAlertsAsync` | Auto-escalate EWRS alerts past their ack deadline (§8.2). |
| `*/15 * * * *` | `RiskEngineService.EvaluateRiskIndicatorsAsync` | Scan the ten RFP §8.1 indicators and emit new `RiskAlert`s. |
| `*/15 * * * *` | `KpiSnapshotRefreshService.RefreshAsync` | Rebuild homepage KPI cache (FR-003). |
| `7 * * * *` | `InvestorAlertEvaluator.EvaluateAsync` | Price-movement / yield / new-project / watchlist + digest sweep. |
| `30 3 * * 0` | `BenchmarkRefreshService.RefreshAsync` | Weekly tick for the 5 GRETI benchmark cities. |
| `0 0 1 1,4,7,10 *` | `DeveloperScoringService.CalculateQuarterlyScoresAsync` | Quarterly developer composite scores. |
| `2 15 1 * *` | `EscrowHealthReportService` | Monthly per-project escrow PDF (`ESC-003`). |
| `45 1 * * *` | `CurrencyRatesRefreshService.RefreshAsync` | Daily UAECB pull (with drift-fallback). |

## 7. AI Agent architecture (RFP §5.3)

```
                  ┌──────────────────────────────────────────┐
                  │      AIOrchestrator (Infrastructure)     │
                  │                                          │
 query ─▶ RAG ─▶ │   ┌──────────┐    ┌──────────┐           │
                  │   │ Primary  │    │ Secondary │   ───────┼──▶ Model Performance Dashboard
                  │   │ model    │ ─▶ │ model    │           │    (name / latency / active / fallback)
                  │   └──────────┘    └──────────┘           │
                  │        │              │                 │
                  │    nav tier      analytics tier          │
                  └────────┼──────────────┼─────────────────┘
                           │              │
                           ▼              ▼
                    DLD service links   TimeSeriesAnalyzer
                                        (z-score + trend + Pearson)
```

- **Tier routing** (`AIOrchestrator.TierOrderFor`): service-navigation → `nav` → `primary`; trend/anomaly → `analytics` → `primary`; default → `primary` → `secondary`.
- **Guardrail** (`IAdvisoryGuardrail`) blocks investment-advice patterns before they reach the user; the raw model output is still logged for audit (RFP §5.1).
- **Session memory**: in-memory `SessionMemory` dictionary keyed by `sessionId`, capped at 20 turns.
- **Cross-session memory** (AI-006): `UserAiMemory` rows persist zone + topic frequency per user, gated by `ConsentAiMemory` and wiped on revocation.
- **UAE residency**: every configured model tier carries a `Region` metadata field (`AIModelMetrics.RecordSuccess`); the `/healthz/sla` endpoint surfaces non-UAE inference as a warning.

## 8. Observability

- `/healthz/live` — liveness (used by Kubernetes / App Service probes).
- `/healthz/ready` — readiness (DB + Hangfire).
- `/healthz/sla` — composite SLO health: AI P95 latency, UAE-residency state, KPI freshness. Returns `Degraded` within the thresholds and `Unhealthy` past them.
- App Insights (`appInsightsConnection` in Bicep) captures request + dependency + exception telemetry.
- Audit logs (`AuditLog` entity) record every RBAC-sensitive admin action with actor identity + timestamp.

## 9. Security controls

Aligned to DESC ISR v3 + RFP §10.2:

| Control | Where |
|---|---|
| TLS 1.2 minimum | Bicep `platform.bicep` App Service `minTlsVersion` |
| MFA mandatory (internal) | `AuthController.LoginAsync` rejects internal login without `requiresTwoFactor` |
| Session timeout 30 min | `inactivity-watch.js` + `SimpleCaptchaService` token TTL |
| OWASP Top-10 scan | `.github/workflows/security.yml` OWASP ZAP baseline on `main` |
| Vulnerability scan | `dotnet list package --vulnerable` in the same workflow (fails on Critical/High) |
| Bicep lint | Same workflow, `bicep build` step |
| Key Vault purge-protection | `platform.bicep` |
| GRS storage | `platform.bicep` with geo-redundant blob |
| No public blob access | `platform.bicep` `allowBlobPublicAccess: false` |
| RBAC at API layer | `[Authorize(Policy = ...)]` attributes + `AuthorizationPolicies.cs` registry |

## 10. Deployment pipeline

1. **PR build** — `dotnet build --nologo` and `dotnet test` run in CI. Zero warnings are required (`/warnaserror` in shared props).
2. **Security gate** — `.github/workflows/security.yml` must pass.
3. **Bicep validation** — `bicep build` + `what-if` preview against the target subscription.
4. **Deploy** — slot swap for WebAPI / AdminAPI / Web. Hangfire schema migration runs automatically via `EnsureCreated` on startup under a distributed lock.

## 11. Extension points

Commonly-requested changes and the files to touch:

- **Add a new risk indicator** — seed key in `DbSeeder.SeedRiskThresholdsAsync`, add detection method in `RiskEngineService`, cover with `RiskEngineServiceTests`.
- **Add an AI model tier** — new config section `AI:yourTier:*` (Endpoint / Model / Apikey), adjust `TierOrderFor` to route to it when appropriate. No code redeployment required to swap models at the same tier.
- **Add a language** — extend the `SupportedLanguages` list in `UiStateService` + add a map entry in `LocalizationService._map`.
- **Add a saved-view attribute** — extend `SavedAnalyticsView` + DTO + EF migration; shareable links are already expiry-safe (`ShareTokenExpiresAt`).

## 12. Shared UI components (design system)

Ported from the Corporate-Blue design system (ESEMS / ejraa360) and re-skinned to DLD green via `wwwroot/app.css`. All live under `src/IRETP.Web/Components/Shared/`.

- **`<ModuleBanner>`** — compact page header (icon + title + subtitle + optional primary CTA or arbitrary `ActionsContent` slot). Use in place of bespoke `<h2 class="mb-1"><i class="bi bi-…"></i>Title</h2>` blocks. Renders the `.esems-animate-in` fade-up on first render automatically.
- **`<Skeleton>` / `<SkeletonTable>` / `<SkeletonKpiGrid>`** — loading-state placeholders that mimic the upcoming layout (sized to match real cards / tables / KPI grids). Replaces centered `<div class="spinner-border">` patterns. The shimmer animation comes from `.esems-skeleton` in `wwwroot/css/design-system.css`.
- **`<NetworkError>`** — standardised "API unreachable" warning. Pass `Service="Mortgage"` for an auto-titled message, or `Title` / `Detail` for full custom copy. Optional `OnRetry` callback wires up a Retry button.
- **`<EmptyState>`** — empty-collection placeholder (icon + title + message + optional CTA via `ChildContent`). Use when a filter returns zero rows or a feed is empty.
- **`<BackToTop>`** — floating circular button at `bottom: 96px` (above the AI FAB), wired site-wide via `MainLayout.razor`. Appears after the user scrolls past 320 px; clicking smooth-scrolls to the top.

The design-system stylesheets (`material-design-3.css` + `design-system.css`) load BEFORE `app.css` so the DLD-green brand override in `app.css` wins. Wizard chrome (`.wiz-*` / `.pc-*`) is opt-in via `wwwroot/css/wizard.css` — link it from `App.razor` only on multi-step wizard pages.

## 13. Microsoft Fabric / OneLake data plane (RFP v1.3 §11.4)

The IRETP runtime is built to consume DLD's existing OneLake Gold layer as the single analytics source of truth. The integration is mediated by `IFabricGoldDataSource` in `IRETP.Application/Interfaces/`, with `PassthroughFabricGoldDataSource` (in `IRETP.Infrastructure/Services/Fabric/`) as the default implementation. Production deployments swap the mode through the `Fabric:Mode` config value — no code changes.

| Mode | Implementation | Used in |
|---|---|---|
| `Sql` | EF Core direct | Local OLTP-only dev |
| `PassthroughMirror` | Surfaces the Fabric contract over EF | Reference build / non-Fabric environments |
| `OneLakeDirect` | OneLake SQL endpoint / DirectLake | UAT / Production |
| `FabricSemanticModel` | XMLA / DAX against the published semantic model | UAT / Production |

Admin verification: `GET /api/admin/fabric/{status,semantic-models,freshness}` and the Blazor admin page at `/admin/fabric`. The Gold-layer freshness watermark feeds into `/healthz/sla` so a stale pipeline trips the platform SLA health check.

Full integration detail (per-component map, justification register for components outside the Fabric ecosystem, semantic-model catalogue, Data Factory pipeline alignment) lives in [ARCHITECTURE_INTEGRATION_MAP.md](ARCHITECTURE_INTEGRATION_MAP.md). The mandatory v1.3 §11.4.2 visualisation-layer comparative analysis (Power BI vs. custom-web for the External Portal) is in [VISUALISATION_LAYER_ANALYSIS.md](VISUALISATION_LAYER_ANALYSIS.md). Phase-1 Data Familiarisation output templates (§12.1) are in [familiarisation/](familiarisation/README.md).
