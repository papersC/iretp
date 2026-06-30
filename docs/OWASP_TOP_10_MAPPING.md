# OWASP Top 10 (2021) Control Mapping — IRETP

**RFP reference:** DLD-IRETP-2026-001 v1.3, §10.2.2 — automated OWASP Top 10 security scan in CI/CD; zero open Critical findings at go-live.
**Document purpose:** Show how the IRETP codebase mitigates each OWASP Top 10 (2021) category. Cross-referenced from `docs/DESC_ISR_V3_COMPLIANCE.md` §5.6 (Vulnerability Management) and `docs/COMPLIANCE_MATRIX.md` §10.2.

---

## A01:2021 — Broken Access Control

| Mitigation | Where |
|---|---|
| Centralised RBAC policy registry — every internal endpoint requires a named policy | `src/IRETP.Infrastructure/Identity/AuthorizationPolicies.cs` |
| `[Authorize(Roles = …)]` on every AdminAPI controller; default-deny for unauthenticated routes | `src/IRETP.AdminAPI/Controllers/*` |
| AdminAPI bound to private VNET; not reachable from the public internet | `infra/bicep/platform.bicep` |
| Public Open Data API gated by API key middleware with path-prefixed check (`/api/v1/open-data`) — Pass 19h fixed the original prefix mismatch | `src/IRETP.WebAPI/Middleware/` |
| Anti-CSRF tokens on all non-idempotent Razor form posts (Blazor Server default) | Framework default |

## A02:2021 — Cryptographic Failures

| Mitigation | Where |
|---|---|
| TLS 1.2+ enforced everywhere; HTTP redirects to HTTPS in production | `src/IRETP.WebAPI/Program.cs`, `infra/bicep/platform.bicep` |
| Passwords hashed via ASP.NET Identity (PBKDF2 + salt + 10k iterations) | Framework default |
| JWT signed with HMAC-SHA256; signing key from Azure Key Vault in production | `src/IRETP.WebAPI/Program.cs` |
| HMAC-SHA256 unsubscribe tokens (no PII in URL; rotation invalidates outstanding tokens) | `src/IRETP.Infrastructure/Services/Notifications/HmacUnsubscribeTokenService.cs` |
| Secrets never in `appsettings.json` in production — Key Vault binding via Managed Identity | `infra/bicep/platform.bicep` |

## A03:2021 — Injection

| Mitigation | Where |
|---|---|
| All database access via EF Core (parameterised by default) — no raw SQL string concatenation | `src/IRETP.Infrastructure/Repositories/Repository.cs` |
| Input validation via FluentValidation on every MediatR command | `src/IRETP.Application/Features/*/Commands/*Validator.cs` |
| Output encoding by Razor framework (HtmlEncode by default; explicit `MarkupString` only with sanitisation) | Framework default |
| API key middleware uses constant-time comparison to mitigate timing-based extraction | `src/IRETP.WebAPI/Middleware/` |

## A04:2021 — Insecure Design

| Mitigation | Where |
|---|---|
| Threat-model documented in `PROPOSAL.md` Annex C | `PROPOSAL.md` |
| Defence in depth: API key + rate limit + WAF + authentication + authorisation | `src/IRETP.WebAPI/Program.cs` |
| Append-only audit log enforced at DbContext boundary so a compromised admin cannot rewrite history | `src/IRETP.Infrastructure/Data/IretpDbContext.cs` |
| AI agent strict-no-advisory guardrail with both keyword list and forecast regex | `src/IRETP.Infrastructure/Services/KeywordAdvisoryGuardrail.cs` |
| EWRS 4-level escalation ensures critical risk signals reach the right authority without single-point intervention | `src/IRETP.Infrastructure/Services/AlertEscalationService.cs` |

## A05:2021 — Security Misconfiguration

| Mitigation | Where |
|---|---|
| Production CORS allow-list (not wildcard) — explicit origins only | `src/IRETP.WebAPI/Program.cs` |
| Strict transport security and security headers via WAF policy | `infra/bicep/platform.bicep` |
| Hangfire dashboard read-only for admin (no job-edit permission in production) | `src/IRETP.WebAPI/Program.cs` |
| Default-error pages disabled in production (`UseExceptionHandler("/error")`) — no stack traces leaked | `src/IRETP.WebAPI/Program.cs` |
| Configuration validated at startup; missing required keys throw fast instead of running with defaults | `src/IRETP.WebAPI/Program.cs` |

## A06:2021 — Vulnerable & Outdated Components

| Mitigation | Where |
|---|---|
| `dotnet list package --vulnerable` runs in every CI build; Critical/High fails the pipeline | `.github/workflows/security.yml` |
| Dependabot or equivalent auto-PR upgrade flow on the `main` branch (operational) | `.github/workflows/security.yml` |
| License/SBOM generation in CI for procurement audit | `.github/workflows/security.yml` |
| Defender for Cloud enabled on Azure resources for OS-level patching alerts | `infra/bicep/platform.bicep` |

## A07:2021 — Identification & Authentication Failures

| Mitigation | Where |
|---|---|
| ASP.NET Identity with strong password policy (8+ chars, digit + lower + upper + symbol) | `src/IRETP.Infrastructure/DependencyInjection.cs` |
| MFA mandatory for all internal users (TOTP via authenticator app) | `src/IRETP.WebAPI/Controllers/AuthController.cs` |
| CAPTCHA on registration + password reset to slow automated abuse | `src/IRETP.Infrastructure/Services/SimpleCaptchaService.cs` |
| Refresh-token family rotation with revocation on logout | `src/IRETP.Domain/Entities/RefreshToken.cs` |
| Session timeout — 30 min idle for internal users (RFP §10.2.1) | `src/IRETP.Infrastructure/Identity/` |
| OIDC federation for UAE Pass / Azure AD so primary identity is external | `src/IRETP.WebAPI/Program.cs` |

## A08:2021 — Software & Data Integrity Failures

| Mitigation | Where |
|---|---|
| EF migrations run on startup in every environment so prod can't drift from schema | `src/IRETP.WebAPI/Program.cs` (Pass 19i) |
| Append-only audit trail prevents post-hoc tampering even with admin credentials | `src/IRETP.Infrastructure/Data/IretpDbContext.cs` |
| EscrowTransaction marked immutable at the DbContext boundary | `src/IRETP.Infrastructure/Data/IretpDbContext.cs` |
| Container image scanning before push; deployed images signed | `.github/workflows/security.yml` |
| CI/CD requires PR review — no direct push to `main` | Branch protection rule |

## A09:2021 — Security Logging & Monitoring Failures

| Mitigation | Where |
|---|---|
| Structured Serilog logging to centralised sink (Seq / Azure Monitor) | `src/IRETP.WebAPI/Program.cs` |
| OpenTelemetry traces + metrics to OTLP for distributed correlation | `src/IRETP.WebAPI/Program.cs` |
| Audit log records every privileged action with actor + timestamp | `src/IRETP.Infrastructure/Services/AuditLogService.cs` |
| `/healthz/sla` aggregate health check surfaces AI / KPI / Fabric / Notification SLA breaches | `src/IRETP.Infrastructure/HealthChecks/SlaHealthCheck.cs` |
| DESC-certified SOC monitoring contracted (operational per RFP §10.2.2) | Operational — vendor RFP §10.2.2 |

## A10:2021 — Server-Side Request Forgery (SSRF)

| Mitigation | Where |
|---|---|
| All outbound HTTP via named `IHttpClientFactory` clients with explicit allow-listed URLs | `src/IRETP.Infrastructure/DependencyInjection.cs` |
| AI orchestrator HTTP client validates the configured endpoint host against an allow-list before request | `src/IRETP.Infrastructure/Services/AIOrchestrator.cs` |
| Currency-rates refresh and SMS-gateway clients hit fixed configured URLs only — no user-supplied URL inputs | `src/IRETP.Infrastructure/Services/CurrencyRatesRefreshService.cs`, `src/IRETP.Infrastructure/Services/Notifications/HttpSmsSender.cs` |
| AdminAPI behind private VNET; even if an SSRF gadget existed it cannot reach internal admin endpoints from a public-WebAPI request context | `infra/bicep/platform.bicep` |

---

## CI/CD enforcement

Every push to a feature branch runs the security workflow:

1. `dotnet list package --vulnerable` — Critical/High fails the build
2. OWASP ZAP baseline scan on the staging deployment of `main`
3. Bicep linting (`bicep build`)

The workflow definition is `.github/workflows/security.yml`. Per RFP §10.2.2, **zero open Critical findings for any OWASP Top 10 category are permitted at go-live**.

---

## Cross-references

- [DESC_ISR_V3_COMPLIANCE.md §5.6 Vulnerability Management](DESC_ISR_V3_COMPLIANCE.md)
- [COMPLIANCE_MATRIX.md §10.2 Security & DESC Compliance](COMPLIANCE_MATRIX.md)
- [RUNBOOK.md — security-incident SIRP playbook](RUNBOOK.md)

*Updated 2026-05-18. The same traceability test that locks the Compliance Matrix also runs against this doc — claimed file paths must resolve on disk.*
