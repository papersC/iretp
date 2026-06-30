# DESC ISR v3 Control Mapping — IRETP

**RFP reference:** DLD-IRETP-2026-001 v1.3, §10.2 + §16.2 deliverable #66 (Security & Compliance Plan)
**Document purpose:** Clause-by-clause mapping of the Dubai Electronic Security Centre (DESC) Information Security Regulation version 3 (ISR v3) controls to the IRETP platform's implementation. Serves as the vendor's pre-VAPT self-assessment and supports the DESC compliance submission required before Phase 3 acceptance.
**Audience:** DLD CISO, DESC compliance reviewer, DESC-authorised VAPT assessor.

---

## Status legend

| Code | Meaning |
|---|---|
| ✅ **I** — Implemented | Control is active in the codebase / hosting layer today |
| ⚠️ **PI** — Partially Implemented | Core control in place; process/config item documented for completion in the operational runbook (Phase 1) |
| 🔲 **P** — Planned | Control will be fully in place before Phase 3 VAPT. Owner + due date in project risk register |

---

## Control summary

| Domain | Implemented | Partial | Planned | Total |
|---|---|---|---|---|
| Governance | 4 | 1 | 0 | 5 |
| Asset Management | 2 | 1 | 0 | 3 |
| HR Security | 1 | 0 | 2 | 3 |
| Physical | 2 | 0 | 0 | 2 |
| Operations | 6 | 0 | 0 | 6 |
| Access Control | 6 | 1 | 0 | 7 |
| Cryptography | 2 | 0 | 0 | 2 |
| Incident Management | 3 | 0 | 0 | 3 |
| Business Continuity | 1 | 0 | 1 | 2 |
| Compliance | 3 | 0 | 1 | 4 |
| **Total** | **30** | **3** | **4** | **37** |

---

## 1. Governance

| Control | Title | Status | IRETP Implementation | Evidence |
|---|---|---|---|---|
| 1.1 | Information Security Policy | ✅ I | Security requirements documented in `PROPOSAL.md §5`. Policies enforced via `AuthorizationPolicies`, `AuditLogService`, and rate limiter. | `src/IRETP.Infrastructure/Identity/AuthorizationPolicies.cs`, `src/IRETP.Infrastructure/Services/AuditLogService.cs` |
| 1.2 | Roles & Responsibilities | ✅ I | RBAC matrix (`UserRoles` + `AuthorizationPolicies`) assigns least-privilege: DldViewer, DldOperator, DldSupervisor, SystemAdministrator, RegisteredInvestor. | `src/IRETP.Infrastructure/Identity/AuthorizationPolicies.cs`, `src/IRETP.Domain/Enums/UserRole.cs` |
| 1.3 | Risk Assessment Process | ✅ I | Risks identified during design; EWRS surfaces risk posture in real-time to DLD leadership. Annual risk review in support contract. | `src/IRETP.Infrastructure/Services/RiskEngineService.cs` |
| 1.4 | Security Exception Management | ⚠️ PI | Exceptions managed via `AuditLog` with timestamp + approver. Formal exception register documented in `docs/RUNBOOK.md` Phase 1. | `src/IRETP.Infrastructure/Services/AuditLogService.cs` |
| 1.5 | Third-party & Vendor Risk | ✅ I | AI provider UAE-residency enforced at infrastructure layer. All third-party libraries scanned (`dotnet list package --vulnerable` in CI). Contractual security clauses with SMS/SMTP providers. | `.github/workflows/security.yml`, `src/IRETP.WebAPI/Program.cs` |

## 2. Asset Management

| Control | Title | Status | IRETP Implementation | Evidence |
|---|---|---|---|---|
| 2.1 | Asset Inventory | ✅ I | Solution = 3 hosts (WebAPI, AdminAPI, Web), 1 SQL DB, 1 Redis, 1 OneLake workspace, 1 AI model endpoint, 1 SMTP relay, 1 SMS gateway. IaC manifests enumerate all assets. | `infra/bicep/main.bicep`, `infra/bicep/platform.bicep` |
| 2.2 | Data Classification | ✅ I | Data classified as Public / Internal / Confidential per RFP §10.2. `BeneficialOwner` and `ApplicationUser` entities tagged accordingly. | `src/IRETP.Domain/Entities/BeneficialOwner.cs`, `src/IRETP.Infrastructure/Identity/ApplicationUser.cs` |
| 2.3 | Media & Asset Disposal | ⚠️ PI | Database purge + right-to-erasure via account deletion endpoint. Formal media-destruction policy for on-premises option in `docs/RUNBOOK.md`. | `src/IRETP.WebAPI/Controllers/AccountController.cs` |

## 3. HR Security

| Control | Title | Status | IRETP Implementation | Evidence |
|---|---|---|---|---|
| 3.1 | Pre-employment Screening | 🔲 P | Vendor staff background-check process documented in vendor qualification dossier. Completed before project start. | Vendor HR policy (Annex E) |
| 3.2 | Security Awareness Training | 🔲 P | Mandatory security awareness training for all project staff + DLD platform administrators delivered in project kickoff. | Training schedule — Phase 1 Month 1 |
| 3.3 | Disciplinary Process | ✅ I | Every privileged action recorded in `AuditLog` with identity + timestamp, enabling accountability. | `src/IRETP.Infrastructure/Services/AuditLogService.cs` |

## 4. Physical Security

| Control | Title | Status | IRETP Implementation | Evidence |
|---|---|---|---|---|
| 4.1 | Secure Areas | ✅ I | Azure UAE North datacentre physical security (ISO 27001 certified). Administrative access via Azure Portal with MFA only. | Hosting design document |
| 4.2 | Equipment Security | ✅ I | All tiers run in managed services (PaaS); no bare-metal managed by vendor. Developer workstations: full-disk encryption + Defender for Endpoint. | Vendor workstation policy |

## 5. Operations

| Control | Title | Status | IRETP Implementation | Evidence |
|---|---|---|---|---|
| 5.1 | Change Management | ✅ I | All changes to production via CI/CD pipelines with peer-reviewed PRs and staged environments. No direct production access. | `.github/workflows/security.yml`, `docs/RUNBOOK.md` deploy section |
| 5.2 | Capacity Management | ✅ I | Azure App Service auto-scale rules + Redis Premium tier. Capacity reviewed against OpenTelemetry metrics monthly. | `infra/bicep/platform.bicep` |
| 5.3 | Malware Protection | ✅ I | Microsoft Defender for Cloud enabled on all Azure resources. Dependency scanning in CI. Container images scanned before push. | `.github/workflows/security.yml` |
| 5.4 | Backup & Recovery | ✅ I | SQL geo-redundant backups (35-day retention); Redis persistence; OneLake recycle-bin policy. RPO < 1 h; RTO < 4 h per RFP §11.3. | `docs/DISASTER_RECOVERY.md` |
| 5.5 | Logging & Monitoring | ✅ I | Structured logging via Serilog sinks → Seq / Azure Monitor. OpenTelemetry traces + metrics to OTLP. All privileged events in `AuditLog` (queryable from `/admin/audit`). | `src/IRETP.Infrastructure/Services/AuditLogService.cs`, `src/IRETP.Web/Components/Pages/Admin/AuditLogs.razor` |
| 5.6 | Vulnerability Management | ✅ I | OWASP ZAP scripted scans on every deployment to staging. DESC-authorised VAPT before Phase 3 go-live. Monthly dependency scan report. | `.github/workflows/security.yml` |

## 6. Access Control

| Control | Title | Status | IRETP Implementation | Evidence |
|---|---|---|---|---|
| 6.1 | Access Control Policy | ✅ I | Least-privilege RBAC via `AuthorizationPolicies` (internal.read, internal.edit, internal.manage, internal.admin, external.investor). No shared accounts. | `src/IRETP.Infrastructure/Identity/AuthorizationPolicies.cs` |
| 6.2 | User Registration & Provisioning | ✅ I | Registration via the public Register page with email verification. Role assignment by SystemAdministrator only via the User Admin endpoint. | `src/IRETP.AdminAPI/Controllers/UserAdminController.cs`, `src/IRETP.Web/Components/Pages/Auth/Register.razor` |
| 6.3 | Privileged Access Management | ✅ I | SystemAdmin policy restricts to `SystemAdministrator` role; Hangfire dashboard read-only for admin; AdminAPI not reachable from public internet (private network or VPN). | `src/IRETP.AdminAPI/Program.cs`, `src/IRETP.Infrastructure/Identity/AuthorizationPolicies.cs` |
| 6.4 | Authentication | ✅ I | JWT bearer (HS256, zero clock-skew) + federated OIDC (UAE Pass / Azure AD / Keycloak). Refresh-token rotation. CAPTCHA on registration + password reset. | `src/IRETP.WebAPI/Program.cs`, `src/IRETP.Infrastructure/Services/SimpleCaptchaService.cs` |
| 6.5 | Session Management | ✅ I | Short-lived access tokens (configurable TTL); sliding refresh tokens with family rotation; logout revokes the token family. | `src/IRETP.Domain/Entities/RefreshToken.cs`, `src/IRETP.WebAPI/Controllers/AuthController.cs` |
| 6.6 | Network Access Control | ✅ I | AdminAPI bound to private VNET only. WebAPI behind WAF (OWASP ruleset). TLS 1.2+ enforced everywhere. CORS allow-list (not wildcard in prod). | `src/IRETP.WebAPI/Program.cs`, `infra/bicep/platform.bicep` |
| 6.7 | Remote Access | ⚠️ PI | Developer access to production via Azure Bastion / jump-box with MFA; no direct SSH. Formal remote-access policy in `docs/RUNBOOK.md`. | `docs/RUNBOOK.md` |

## 7. Cryptography

| Control | Title | Status | IRETP Implementation | Evidence |
|---|---|---|---|---|
| 7.1 | Cryptographic Controls | ✅ I | JWT signed with HMAC-SHA256; passwords hashed with ASP.NET Identity (PBKDF2 + salt); HMAC-SHA256 unsubscribe tokens; TLS 1.2+; Azure Key Vault for prod secrets. | `src/IRETP.Infrastructure/Services/Notifications/HmacUnsubscribeTokenService.cs`, `src/IRETP.WebAPI/Program.cs` |
| 7.2 | Key Management | ✅ I | JWT signing key sourced from Azure Key Vault; key rotation supported without redeployment. Secrets never in `appsettings.json` in production. | `src/IRETP.WebAPI/Program.cs`, `infra/bicep/platform.bicep` |

## 8. Incident Management

| Control | Title | Status | IRETP Implementation | Evidence |
|---|---|---|---|---|
| 8.1 | Incident Response Procedure | ✅ I | EWRS L1–L4 escalation framework mirrors incident severity; P1/P2 alerting via `AlertEscalationService`. `Playbooks.razor` provides DLD staff step-by-step SOPs. | `src/IRETP.Infrastructure/Services/AlertEscalationService.cs`, `src/IRETP.Web/Components/Pages/Admin/Playbooks.razor` |
| 8.2 | Reporting & Learning | ✅ I | `AuditLog` records every security event; monthly support report includes incident register, root-cause summaries, and recommended improvements. | `src/IRETP.Infrastructure/Services/AuditLogService.cs`, `docs/RUNBOOK.md` |
| 8.3 | Evidence Preservation | ✅ I | `AuditLog` is append-only (enforced at DbContext boundary — Pass 19e). `EscrowTransaction` is immutable. Logs retained for 12 months on the logging sink. | `src/IRETP.Infrastructure/Data/IretpDbContext.cs`, `tests/IRETP.Tests/Infrastructure/AuditLogImmutabilityTests.cs` |

## 9. Business Continuity

| Control | Title | Status | IRETP Implementation | Evidence |
|---|---|---|---|---|
| 9.1 | BCM Planning | ✅ I | RTO < 4 h, RPO < 1 h (RFP §11.3). Azure cross-AZ replicas for SQL and Redis. OneLake geo-redundancy. App Service slot-swap deployment for zero-downtime releases. | `docs/DISASTER_RECOVERY.md`, `infra/bicep/platform.bicep` |
| 9.2 | DR Testing | 🔲 P | Annual DR drill scheduled in Q4 of each operating year. Quarterly backup-restoration drill per RFP §11.3. Results reported to DLD in the monthly support pack. | `docs/DISASTER_RECOVERY.md` |

## 10. Compliance

| Control | Title | Status | IRETP Implementation | Evidence |
|---|---|---|---|---|
| 10.1 | UAE Legal & Regulatory Compliance | ✅ I | UAE Federal PDPL honoured via `UserConsentService` (opt-in/out), minimum-data design, erasure workflow. All data + AI inference remain in UAE. | `src/IRETP.Infrastructure/Identity/UserConsentService.cs` |
| 10.2 | Intellectual Property | ✅ I | All platform IP vests in DLD on payment (RFP §19.1). Open-source components permissively licensed (MIT/Apache 2). License SBOM generated by CI. | `PROPOSAL.md §11`, `.github/workflows/security.yml` |
| 10.3 | Privacy & Personal Data Protection | ✅ I | `BeneficialOwner` and investor personal data tagged. Every read by internal user logged with reason code. Right-to-erasure endpoint on `AccountController`. | `src/IRETP.Infrastructure/Identity/UserConsentService.cs`, `src/IRETP.WebAPI/Controllers/AccountController.cs` |
| 10.4 | DESC Compliance Reporting | 🔲 P | DESC ISR v3 self-assessment submitted with proposal (this document). Annual external audit scheduled in operating year. | This document + DESC audit schedule |

---

## Residual risks & remediation plan

| ID | Risk | Current State | Likelihood | Remediation |
|---|---|---|---|---|
| R-1 | Partially Implemented controls | 3 controls (1.4, 2.3, 6.7) marked PI | Medium | Operational runbook + formal process completed by Phase 1 Month 1 |
| R-2 | Planned controls pre-VAPT | 4 controls (3.1, 3.2, 9.2, 10.4) marked P | Low | Completed before Phase 3 VAPT; tracked in proposal risk register |
| R-3 | AI model provider UAE residency | Depends on provider UAE endpoint availability | Medium | Provider contractually bound to UAE inference; UAE-hosted open-source fallback (Llama/Mistral) available |

---

## Acceptance statement

The vendor certifies that this ISR v3 self-assessment is accurate as of the proposal submission date (2026-05-18) and commits to resolving all Partial and Planned controls before the Phase 3 DESC-authorised VAPT engagement. The vendor will engage a DESC-approved penetration testing firm and submit the final VAPT report as a Phase 3 acceptance deliverable per RFP §10.2.2.

---

## Cross-references

- [PROPOSAL.md §5 — Compliance, Security & Data Residency](../PROPOSAL.md)
- [COMPLIANCE_MATRIX.md §10.2](COMPLIANCE_MATRIX.md)
- [docs/RUNBOOK.md — Security incident SIRP playbook](RUNBOOK.md)
- [docs/DISASTER_RECOVERY.md — RTO/RPO + quarterly drill](DISASTER_RECOVERY.md)

*Document classification: Confidential — Vendor Proposal. Updated 2026-05-18.*
