"""
DESC ISR v3 (Information Security Regulation version 3) control mapping
for IRETP RFP DLD-IRETP-2026-001.

Each row: (Control ID, Control Domain, Control Title, Status, IRETP Implementation, Evidence File/Service)
Status: I=Implemented, PI=Partially Implemented, P=Planned (before Phase 3 VAPT)
"""

CONTROLS = [
    # -----------------------------------------------------------------------
    # Domain 1: Governance & Risk Management
    # -----------------------------------------------------------------------
    ("1.1",  "Governance", "Information Security Policy",
     "I",
     "Security requirements documented in this proposal (§10). Policies "
     "enforced via AuthorizationPolicies, AuditLogService and rate limiter.",
     "AuthorizationPolicies.cs / AuditLogService.cs"),
    ("1.2",  "Governance", "Roles & Responsibilities",
     "I",
     "RBAC matrix (UserRoles + AuthorizationPolicies) assigns least-privilege "
     "roles: DldViewer, DldOperator, DldSupervisor, SystemAdministrator, "
     "RegisteredInvestor.",
     "AuthorizationPolicies.cs / UserRole.cs"),
    ("1.3",  "Governance", "Risk Assessment Process",
     "I",
     "Risks identified during design; EWRS surfaces risk posture in "
     "real-time to DLD leadership. Annual risk review in support contract.",
     "RiskEngineService.cs"),
    ("1.4",  "Governance", "Security Exception Management",
     "PI",
     "Exceptions managed via AuditLog with timestamp + approver. Formal "
     "exception register process to be formalised in the operational runbook.",
     "AuditLogService.cs"),
    ("1.5",  "Governance", "Third-party & Vendor Risk",
     "I",
     "AI provider UAE-residency requirement enforced at infrastructure level; "
     "all third-party libraries scanned (dotnet list package --vulnerable "
     "in CI); contractual security clauses with SMS/SMTP providers.",
     "WebAPI/Program.cs / AdminAPI/Program.cs"),

    # -----------------------------------------------------------------------
    # Domain 2: Asset Management
    # -----------------------------------------------------------------------
    ("2.1",  "Asset Management", "Asset Inventory",
     "I",
     "Solution comprises 3 hosts (WebAPI, AdminAPI, Web), 1 SQL DB, 1 Redis, "
     "1 OneLake workspace, 1 AI model endpoint, 1 SMTP relay, 1 SMS gateway. "
     "Infrastructure-as-code manifests enumerate all assets.",
     "Infrastructure deployment manifests"),
    ("2.2",  "Asset Management", "Data Classification",
     "I",
     "Data classified as: Public (price/rental index, transactions), "
     "Internal (risk scores, escrow details, audit logs), Confidential "
     "(beneficial ownership, personal investor data). "
     "BeneficialOwner and ApplicationUser entities tagged accordingly.",
     "Domain/Entities/* entity XML comments"),
    ("2.3",  "Asset Management", "Media & Asset Disposal",
     "PI",
     "Database purge procedures and right-to-erasure workflow in place via "
     "account deletion endpoint; formal media-destruction policy for "
     "on-premises option in operational runbook.",
     "AccountController.cs"),

    # -----------------------------------------------------------------------
    # Domain 3: Human Resources Security
    # -----------------------------------------------------------------------
    ("3.1",  "HR Security", "Pre-employment Screening",
     "P",
     "Vendor staff background-check process documented in vendor "
     "qualification dossier (RFP §16). Completed before project start.",
     "Vendor HR policy"),
    ("3.2",  "HR Security", "Security Awareness Training",
     "P",
     "Mandatory security awareness training for all project staff "
     "and DLD platform administrators delivered in project kickoff.",
     "Training schedule (P1 M1)"),
    ("3.3",  "HR Security", "Disciplinary Process",
     "I",
     "Every privileged action is recorded in AuditLog with identity and "
     "timestamp, enabling accountability and disciplinary investigation.",
     "AuditLogService.cs"),

    # -----------------------------------------------------------------------
    # Domain 4: Physical & Environmental Security
    # -----------------------------------------------------------------------
    ("4.1",  "Physical", "Secure Areas",
     "I",
     "Option A/C: Azure UAE North datacentre physical security (ISO 27001 "
     "certified). Option B: DLD-approved DC physical controls. "
     "Administrative access via Azure Portal with MFA only.",
     "Hosting design document"),
    ("4.2",  "Physical", "Equipment Security",
     "I",
     "All application tiers run in managed services (PaaS); no bare-metal "
     "managed by the vendor. Developer workstations protected by full-disk "
     "encryption + Defender for Endpoint.",
     "Vendor workstation policy"),

    # -----------------------------------------------------------------------
    # Domain 5: Communications & Operations
    # -----------------------------------------------------------------------
    ("5.1",  "Operations", "Change Management",
     "I",
     "All changes to production deployed via CI/CD pipelines (GitHub Actions "
     "/ Azure Pipelines) with peer-reviewed pull requests and staged "
     "environments. No direct production access.",
     "CI/CD pipeline definitions"),
    ("5.2",  "Operations", "Capacity Management",
     "I",
     "Azure App Service auto-scale rules and Redis Premium tier elasticity. "
     "Capacity reviewed against OpenTelemetry metrics monthly.",
     "WebAPI/AdminAPI App Service config"),
    ("5.3",  "Operations", "Malware Protection",
     "I",
     "Microsoft Defender for Cloud enabled on all Azure resources. "
     "Dependency scanning in CI (dotnet list package --vulnerable). "
     "Container images scanned before push.",
     "Azure Defender / CI pipeline"),
    ("5.4",  "Operations", "Backup & Recovery",
     "I",
     "SQL geo-redundant backups (35-day retention); Redis persistence (AOF); "
     "OneLake recycle-bin policy. RPO ≤ 4 h; RTO ≤ 8 h.",
     "Hosting design / Azure Backup"),
    ("5.5",  "Operations", "Logging & Monitoring",
     "I",
     "Structured logging via Serilog sinks to Seq / Azure Monitor. "
     "OpenTelemetry traces + metrics to OTLP. All privileged events in "
     "AuditLog (queryable from Admin/AuditLogs.razor). Alerts on anomalies.",
     "AuditLogService.cs / OpenTelemetry config"),
    ("5.6",  "Operations", "Vulnerability Management",
     "I",
     "OWASP ZAP scripted scans on every deployment to staging. "
     "DESC-authorised VAPT before Phase 3 go-live. Monthly dependency "
     "scan report.",
     "CI/CD pipeline / VAPT schedule"),

    # -----------------------------------------------------------------------
    # Domain 6: Access Control
    # -----------------------------------------------------------------------
    ("6.1",  "Access Control", "Access Control Policy",
     "I",
     "Least-privilege RBAC via AuthorizationPolicies (internal.read, "
     "internal.edit, internal.manage, internal.admin, external.investor). "
     "No shared accounts; each user has a named ApplicationUser identity.",
     "AuthorizationPolicies.cs"),
    ("6.2",  "Access Control", "User Registration & Provisioning",
     "I",
     "User registration via Auth/Register.razor with email verification. "
     "Role assignment by SystemAdministrator only via UserAdminController.",
     "UserAdminController.cs / Register.razor"),
    ("6.3",  "Access Control", "Privileged Access Management",
     "I",
     "SystemAdmin policy restricts to SystemAdministrator role; Hangfire "
     "dashboard read-only for admin; AdminAPI not reachable from the "
     "public internet (private network or VPN).",
     "AdminAPI network config / AuthorizationPolicies.cs"),
    ("6.4",  "Access Control", "Authentication",
     "I",
     "JWT bearer (HS256, zero clock-skew) + federated OIDC (UAE Pass / "
     "Azure AD / Keycloak). Refresh-token rotation. CAPTCHA on "
     "registration and password reset.",
     "WebAPI/Program.cs / CaptchaController.cs"),
    ("6.5",  "Access Control", "Session Management",
     "I",
     "Short-lived access tokens (configurable TTL); sliding refresh tokens "
     "with family rotation; logout revokes the token family.",
     "RefreshToken entity / AuthController.cs"),
    ("6.6",  "Access Control", "Network Access Control",
     "I",
     "AdminAPI bound to private VNET only (not reachable from public "
     "internet). WebAPI behind WAF (OWASP ruleset). TLS 1.2+ enforced "
     "everywhere. CORS allow-list (not wildcard in production).",
     "WebAPI/Program.cs (CORS) / hosting network config"),
    ("6.7",  "Access Control", "Remote Access",
     "PI",
     "Developer access to production via Azure Bastion / jump-box with "
     "MFA; no direct SSH from developer laptops. Formal remote-access "
     "policy in operational runbook.",
     "Hosting design / operational runbook (P1)"),

    # -----------------------------------------------------------------------
    # Domain 7: Cryptography
    # -----------------------------------------------------------------------
    ("7.1",  "Cryptography", "Cryptographic Controls",
     "I",
     "JWT signed with HMAC-SHA256; passwords hashed with ASP.NET Core "
     "Identity (PBKDF2 + salt); HMAC-SHA256 unsubscribe tokens; "
     "TLS 1.2+ for all transport; Azure Key Vault for secrets in "
     "production.",
     "WebAPI/Program.cs / HmacUnsubscribeTokenService.cs"),
    ("7.2",  "Cryptography", "Key Management",
     "I",
     "JWT signing key sourced from Azure Key Vault (IOptionsMonitor); "
     "key rotation supported without redeployment. "
     "Secrets never in appsettings.json in production.",
     "WebAPI/Program.cs / Key Vault binding"),

    # -----------------------------------------------------------------------
    # Domain 8: Incident Management
    # -----------------------------------------------------------------------
    ("8.1",  "Incident Mgmt", "Incident Response Procedure",
     "I",
     "EWRS L1–L4 escalation framework mirrors incident severity; "
     "Sev 1/2 alerting via AlertEscalationService. Playbooks.razor "
     "provides DLD staff step-by-step SOPs.",
     "AlertEscalationService.cs / Admin/Playbooks.razor"),
    ("8.2",  "Incident Mgmt", "Reporting & Learning",
     "I",
     "AuditLog records every security event; monthly support report includes "
     "incident register, root-cause summaries and recommended improvements.",
     "AuditLogService.cs / support SLA §15.3"),
    ("8.3",  "Incident Mgmt", "Evidence Preservation",
     "I",
     "AuditLog is append-only; EscrowTransaction is immutable. "
     "Logs retained for 12 months on the logging sink.",
     "AuditLogService.cs / EscrowTransaction entity"),

    # -----------------------------------------------------------------------
    # Domain 9: Business Continuity
    # -----------------------------------------------------------------------
    ("9.1",  "Business Continuity", "BCM Planning",
     "I",
     "RTO ≤ 8 h, RPO ≤ 4 h. Azure cross-AZ replicas for SQL and Redis. "
     "OneLake geo-redundancy. App Service slot-swap deployment for zero "
     "downtime releases.",
     "Hosting design / Azure SLA documentation"),
    ("9.2",  "Business Continuity", "DR Testing",
     "P",
     "Annual DR drill scheduled in Q4 of each operating year. "
     "Results reported to DLD in the monthly support pack.",
     "DR test plan (to be provided with operational runbook)"),

    # -----------------------------------------------------------------------
    # Domain 10: Compliance
    # -----------------------------------------------------------------------
    ("10.1", "Compliance", "UAE Legal & Regulatory Compliance",
     "I",
     "UAE Federal PDPL honoured through UserConsentService (opt-in/out), "
     "minimum-data design, erasure workflow. All data and AI inference "
     "remain in the UAE.",
     "UserConsentService.cs / hosting design"),
    ("10.2", "Compliance", "Intellectual Property",
     "I",
     "All platform IP vests in DLD on payment (RFP §19.1). "
     "Open-source components are permissively licensed (MIT/Apache 2). "
     "License SBOM generated by CI.",
     "Licence SBOM / Financial Proposal §19"),
    ("10.3", "Compliance", "Privacy & Personal Data Protection",
     "I",
     "BeneficialOwner and investor personal data tagged. Every read "
     "by an internal user logged with reason code. "
     "Right-to-erasure endpoint exposed on AccountController.",
     "UserConsentService.cs / AccountController.cs / AuditLogService.cs"),
    ("10.4", "Compliance", "DESC Compliance Reporting",
     "P",
     "DESC ISR v3 compliance self-assessment submitted with proposal "
     "(this document). Annual external audit scheduled in operating year.",
     "This document / DESC audit schedule"),
]
