# IRETP — API Reference

Curated overview of the HTTP surface. The OpenAPI 3.0 specs at `/openapi/public.json` (public endpoints only) and `/openapi/v1.json` (full surface, dev-only) are the authoritative machine-readable reference. This file exists to give humans a readable map grouped by RFP capability.

**Base URLs**
- Public WebAPI: `https://api.iretp.dld.ae` (prod) / `http://localhost:5000` (dev)
- Admin AdminAPI: `https://admin-api.iretp.dld.ae` / `http://localhost:5002`
- Blazor Web:   `https://iretp.dld.ae` / `http://localhost:5010`

**Auth**
- Bearer JWT for authenticated endpoints. MFA-enforced for internal users (RFP §10.2.1).
- API key in `X-Api-Key` header for `/api/v1/open-data/*` when issued; anonymous otherwise (CAPTCHA-gated for exports).

---

## 1. Public Portal

### 1.1 Dashboard / KPIs

```
GET /api/dashboard/kpis
```
Returns cached KPI snapshot — refreshed every 15 min by `KpiSnapshotRefreshService` (RFP FR-003). Response includes `RefreshedAt` so clients can age the values.

### 1.2 Map

```
GET /api/map/zones/heatmap?mode=volume|pricesqft|yield|esg
GET /api/map/zones/{id}
GET /api/map/projects?zoneId=&status=&developerId=
GET /api/map/projects/{id}
GET /api/map/zones/compare?zoneIds=...   (max 5)
```

### 1.3 Transactions / Indices

```
GET  /api/transactions                   (filter: dateFrom/dateTo/zone/propertyType/transactionType/price/area/financing)
POST /api/transactions/export            (format: xlsx|csv|pdf)
GET  /api/price-index                    (trend, 10-year zoomable)
GET  /api/price-index/compare?zoneIds=
GET  /api/rental-index
GET  /api/rental-index/yield-calculator
```

### 1.4 Projects / Developers

```
GET /api/projects?status=&zoneId=
GET /api/developers                      (leaderboard, public subset)
GET /api/developers/{id}/scorecard       (public simplified view)
```

### 1.5 ESG / Benchmark / Mortgage / Ownership / GRETI

```
GET /api/esg/dashboard
GET /api/benchmark/dashboard             (Dubai vs. LON/SGP/NYC/PAR/HKG)
GET /api/mortgage/dashboard              (aggregate LTV, volume, trend)
GET /api/ownership/dashboard             (beneficial ownership summary)
GET /api/greti/dashboard                 (sub-index progress)
```

### 1.6 Analytics (Slice & Dice)

```
POST /api/analytics/execute              (dimensions, metrics, filters, chartType)
POST /api/analytics/export               (format: xlsx|csv|pdf|json)
GET  /api/analytics/saved-views          (current user's 12 max)
POST /api/analytics/saved-views          (capped server-side)
PUT  /api/analytics/saved-views/{id}     (reorder, rename)
GET  /api/analytics/shared/{shareToken}  (12-month validity — RFP AN-006)
```

### 1.7 AI Agent

```
POST /api/ai/query
  body: { query, language: "en"|"ar"|..., sessionId? }
  auth: optional — authenticated users get cross-session memory if opted in (RFP AI-006)
```

### 1.8 Account (authenticated investor)

```
GET  /api/account/profile
PUT  /api/account/profile                (first/last name, preferred lang/currency)
PUT  /api/account/consent                 (marketing, ai-memory, usage-analytics; revoking ai-memory purges UserAiMemory)
GET  /api/account/data-export             (PDPL DSAR — full JSON dump)
```

### 1.9 Alerts / Watchlist / Notifications

```
GET    /api/alerts
POST   /api/alerts                       (6 alert types supported)
DELETE /api/alerts/{id}
GET    /api/watchlist
POST   /api/watchlist
DELETE /api/watchlist/{id}
GET    /api/notifications
POST   /api/notifications/mark-read
POST   /api/notifications/mark-all-read
```

### 1.10 Investment Profile PDF

```
GET /api/investment-profile/zone/{zoneId}/pdf
GET /api/investment-profile/project/{projectId}/pdf
```

### 1.11 Auth

```
POST /api/auth/register
POST /api/auth/login                     (returns requiresMfaSetup / requiresTwoFactor)
POST /api/auth/login-2fa
POST /api/auth/2fa/setup
POST /api/auth/2fa/enable
POST /api/auth/refresh
POST /api/auth/logout
```

### 1.12 Open Data API

```
GET /api/v1/open-data/transactions
GET /api/v1/open-data/projects
GET /api/v1/open-data/price-index
GET /api/v1/open-data/zones
GET /api/v1/open-data/developers/scorecards
```

Rate-limited per API key. Swagger console at `/api-docs`.

---

## 2. Internal Platform (AdminAPI)

All endpoints require JWT + appropriate role. Authorisation policies are declared in `IRETP.Infrastructure/Identity/AuthorizationPolicies.cs`.

### 2.1 EWRS

```
GET /api/admin/ewrs/dashboard              (DldViewer+)
GET /api/admin/ewrs/alerts                 (DldViewer+)
POST /api/admin/ewrs/alerts/{id}/acknowledge   (DldOperator+)
POST /api/admin/ewrs/alerts/{id}/resolve       (DldOperator+)
POST /api/admin/ewrs/alerts/{id}/escalate      (DldSupervisor+)
GET  /api/admin/ewrs/thresholds                (DldViewer+)
PUT  /api/admin/ewrs/thresholds/{id}           (DldSupervisor, SystemAdministrator — audit-logged)
GET  /api/admin/ewrs/playbooks
PUT  /api/admin/ewrs/playbooks/{indicatorKey}
POST /api/admin/ewrs/alerts/{id}/playbook-step (progress checkbox)
```

### 2.2 Escrow

```
GET /api/admin/escrow/dashboard
GET /api/admin/escrow/projects/{projectId}
GET /api/admin/escrow/projects/{projectId}/audit-log
GET /api/admin/escrow/projects/{projectId}/health-report.pdf
```

### 2.3 Developer Rating

```
GET  /api/admin/developers/leaderboard
GET  /api/admin/developers/{id}/profile
GET  /api/admin/developers/compare?ids=...      (up to 4)
GET  /api/admin/developers/scoring-weights
PUT  /api/admin/developers/scoring-weights      (SystemAdministrator; sum-to-100 enforced; audit-logged)
```

### 2.4 CMS

```
GET    /api/admin/cms/content
POST   /api/admin/cms/content
PUT    /api/admin/cms/content/{id}               (creates a CmsContentVersion snapshot)
POST   /api/admin/cms/content/{id}/publish
GET    /api/admin/cms/content/{id}/versions
```

### 2.5 AI Models

```
GET  /api/admin/ai-models/status                 (name, version, latency, fallback state — RFP §5.3)
POST /api/admin/ai-models/switch                 (no-redeploy tier switching)
POST /api/admin/ai-models/accuracy-test          (runs the 100-question harness)
```

### 2.6 Users & Audit

```
GET  /api/admin/users
POST /api/admin/users/{userId}/reset-mfa
POST /api/admin/users/{userId}/delete            (PDPL erasure)
GET  /api/admin/audit?actor=&entity=&fromDate=&toDate=
```

### 2.7 Name validation (RFP §13 #48)

```
GET /api/admin/name-validations
PUT /api/admin/name-validations/{entityType}/{entityId}
```

---

## 3. Health & meta

```
GET /healthz/live           (liveness probe)
GET /healthz/ready          (liveness + DB + Hangfire)
GET /healthz/sla            (AI P95 / UAE residency / KPI freshness vs. §10.1)
GET /openapi/public.json    (public surface only)
GET /openapi/v1.json        (full; dev-only; blocked in prod)
GET /api-docs               (interactive Swagger console)
```

---

## 4. Response conventions

- All responses are JSON, UTF-8.
- Paged results: `{ items: [...], totalCount, page, pageSize }`.
- Errors: `{ error: string, code: string?, details: object? }` with RFC-7807-ish shape.
- Timestamps: ISO-8601 with `Z` suffix. All server timestamps are UTC.
- Currency: every response expresses money as AED decimals; the frontend converts via `CurrencyService`.

---

## 5. Rate limits

- Anonymous open-data: 60 req/min per IP.
- API-key holders: quota set on the `ApiKey` record (`RateLimitPerHour`).
- Authenticated investor: 600 req/min per user.
- AdminAPI: 1200 req/min per authenticated DLD user.

Breaches return HTTP 429 with a `Retry-After` header.
