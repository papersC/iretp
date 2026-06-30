# IRETP — Integrated Real Estate Transparency Platform

Reference implementation of the **Dubai Land Department** tender
**DLD-IRETP-2026-001**. Delivers a unified public + internal platform for
Dubai's real-estate market: transparency indices, escrow monitoring, risk
analytics, investor tooling, an Open Data portal, and an Arabic-first AI
agent.

> Demo data is seeded in `Development`. No production credentials, no live
> DLD integrations — replace connection strings and OIDC configuration
> before any non-dev deployment.
>
> **Deploying?** See [`DEPLOYMENT.md`](DEPLOYMENT.md) — quick start, the
> dev-vs-production settings matrix, and the required env-var overrides.

---

## Solution layout

| Project | Purpose |
|---|---|
| `src/IRETP.Domain` | Enterprise entities, enums, domain interfaces |
| `src/IRETP.Application` | MediatR use-cases, DTOs, application services |
| `src/IRETP.Infrastructure` | EF Core, Identity, Hangfire jobs, outbound integrations |
| `src/IRETP.WebAPI` | Public JWT-secured API, Open Data portal, SignalR hub |
| `src/IRETP.AdminAPI` | Internal DLD staff API (separate port, policy-gated) |
| `src/IRETP.Web` | Blazor Server public portal + admin console |
| `tests/` | Test projects (placeholder — add `xUnit` projects here) |

Architecture follows Clean Architecture — outer layers depend inward;
`Domain` has zero framework references.

### Technical documentation (RFP §13 item 53)

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — runtime topology, request flow, security controls, extension points.
- [`docs/COMPLIANCE_MATRIX.md`](docs/COMPLIANCE_MATRIX.md) — every RFP v1.3 requirement mapped to implementation file + test coverage.
- [`docs/DESC_ISR_V3_COMPLIANCE.md`](docs/DESC_ISR_V3_COMPLIANCE.md) — clause-by-clause DESC ISR v3 pre-VAPT self-assessment (RFP §10.2).
- [`docs/OWASP_TOP_10_MAPPING.md`](docs/OWASP_TOP_10_MAPPING.md) — OWASP Top 10 (2021) control mapping (RFP §10.2.2).
- [`docs/RISK_REGISTER.md`](docs/RISK_REGISTER.md) — project risk register (RFP §16.2 #69).
- [`docs/EXIT_PLAN.md`](docs/EXIT_PLAN.md) — knowledge transfer and exit plan (RFP §16.2 #70).
- [`docs/THREAT_MODEL.md`](docs/THREAT_MODEL.md) — STRIDE threat model for the IRETP platform (PROPOSAL.md Annex C).
- [`docs/UAT_PLAN.md`](docs/UAT_PLAN.md) — phase-by-phase, category-by-category UAT plan (RFP §17.3 — 10 universal UAT categories).
- [`docs/V1_3_ALIGNMENT_SUMMARY.md`](docs/V1_3_ALIGNMENT_SUMMARY.md) — 1-page summary of what changed v1.0 → v1.3 and where each delta is addressed. **Start here as a DLD evaluator.**
- [`docs/ARCHITECTURE_INTEGRATION_MAP.md`](docs/ARCHITECTURE_INTEGRATION_MAP.md) — alignment with DLD's Microsoft Fabric / OneLake ecosystem (RFP v1.3 §11.4 / §11.4.1).
- [`docs/VISUALISATION_LAYER_ANALYSIS.md`](docs/VISUALISATION_LAYER_ANALYSIS.md) — Power BI vs custom-web comparative analysis for the External Portal (RFP v1.3 §11.4.2).
- [`docs/familiarisation/`](docs/familiarisation/README.md) — DLD Data Familiarisation output templates (RFP v1.3 §12.1).
- [`docs/API_REFERENCE.md`](docs/API_REFERENCE.md) — HTTP surface curated by RFP capability.
- [`docs/RUNBOOK.md`](docs/RUNBOOK.md) — daily ops, incident response, routine maintenance.
- [`docs/DISASTER_RECOVERY.md`](docs/DISASTER_RECOVERY.md) — RTO/RPO targets, regional-failover and PITR procedures.
- [`docs/DATA_DICTIONARY.md`](docs/DATA_DICTIONARY.md) — business-semantic reference for every persisted entity.

### Microsoft Fabric / OneLake integration

The IRETP runtime is configured to consume DLD's existing OneLake Gold layer
via the `IFabricGoldDataSource` abstraction. Mode is set via `Fabric:Mode` in
`appsettings.json`:

| Mode | Use case |
|---|---|
| `Sql` | Pure OLTP (local dev) |
| `PassthroughMirror` | Reference build — surfaces the Fabric contract over the local OLTP store |
| `OneLakeDirect` | UAT / Production — reads via OneLake SQL endpoint or DirectLake |
| `FabricSemanticModel` | UAT / Production — DAX queries via XMLA |

Admin verification endpoints (require `DldSupervisor` or `SystemAdministrator`):
- `GET /api/admin/fabric/status`
- `GET /api/admin/fabric/semantic-models`
- `GET /api/admin/fabric/freshness`

## Running locally

### With Docker Compose (recommended)

```bash
docker compose up -d --build
```

- Web portal — <http://localhost:5010>
- Public WebAPI — <http://localhost:5000> (Swagger at `/swagger`)
- Admin API — <http://localhost:5002>
- SQL Server — `localhost,1433` (`sa` / password in `docker-compose.yml`)

### With the .NET 9 SDK

```bash
dotnet restore IRETP.sln
dotnet build IRETP.sln
dotnet run --project src/IRETP.WebAPI
dotnet run --project src/IRETP.AdminAPI
dotnet run --project src/IRETP.Web
```

The APIs auto-migrate and seed fixture data on first run in `Development`.
Seeded Partner API key prefix: `iretp_live_3a9…` (600 req/min ceiling).

## Configuration

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience` | JWT signing + validation |
| `Jwt:ExpiryMinutes` | Public/investor access-token lifetime (default 60) |
| `Jwt:InternalExpiryMinutes` | Internal DLD staff lifetime (default 30, per RFP 10.2.1) |
| `Jwt:RefreshTokenExpiryDays` | Refresh-token lifetime (default 7) |
| `Oidc:Authority` | Optional — enables the federated OIDC scheme (UAE Pass, Azure AD…) |
| `Oidc:Audience` / `Oidc:NameClaim` / `Oidc:RoleClaim` | OIDC claim mapping |
| `ApiSettings:WebApiUrl` / `ApiSettings:AdminApiUrl` | Consumed by `IRETP.Web` |

## Health endpoints

| Endpoint | Meaning |
|---|---|
| `GET /healthz` | Aggregated JSON with per-check status + data |
| `GET /healthz/live` | Liveness (always 200 if process is up) |
| `GET /healthz/ready` | Readiness — fails when DB or Hangfire is Unhealthy |

## Rate limits

Partitioned by `X-API-Key` header when present, otherwise by remote IP.

| Tier | Ceiling | Typical use |
|---|---|---|
| Partner | 600 req/min | DLD strategic partners |
| Plus | 240 req/min | Paid developer tier |
| Free | 60 req/min | Default public key |
| Anonymous | 100 req/min per IP | No key supplied |

## SEO

- `wwwroot/robots.txt` — allow public, disallow `/admin`, `/auth/`, `/account`, etc.
- `GET /sitemap.xml` — dynamic; static routes + every `/projects/{id}` detail page.

## Background jobs (Hangfire)

| Schedule | Job |
|---|---|
| Every 15 min | Risk engine evaluator (EWRS indicators) |
| Every 2 min | Notification dispatcher (email/SMS/in-app) |
| Hourly at :07 | Investor alert evaluator |
| Daily 01:45 UTC | Currency rates refresh (UAE Central Bank) |
| Weekly Sun 03:30 | International benchmark refresh |
| Monthly 1st 02:15 | Escrow health report |
| Quarterly 1st Jan/Apr/Jul/Oct | Developer rating scores |

Dashboard — `/hangfire` (local-only by default).

## CI

`.github/workflows/build.yml` — restore, build `/warnaserror`, test, publish
all three services, upload artifacts on `main`, docker-compose sanity build.
`.github/workflows/codeql.yml` — weekly + per-PR CodeQL (csharp).

## License

Prepared for the DLD-IRETP-2026-001 tender response. Not licensed for
commercial redistribution.
