"""
Technical Solution content for IRETP RFP response.
Each section maps directly to an RFP clause and grounds the answer in the
existing IRETP codebase (file paths under src/).
"""

EXEC_SUMMARY = (
    "This proposal responds to RFP DLD-IRETP-2026-001 for the Integrated Real "
    "Estate Transparency Platform (IRETP). The solution is a production-ready, "
    "Clean-Architecture .NET 8 platform already in build, comprising three "
    "deployable hosts — IRETP.WebAPI (public + investor APIs), IRETP.AdminAPI "
    "(internal DLD operations), and IRETP.Web (Blazor Server portal) — backed "
    "by a shared IRETP.Application (CQRS via MediatR), IRETP.Domain (DDD "
    "entities) and IRETP.Infrastructure (EF Core, OneLake lakehouse, Hangfire, "
    "AI orchestration, identity). Every functional area called out by the RFP "
    "(External Public Portal, Slice-and-Dice Analytics, AI Agent, Investor "
    "Notifications, Multilingual EN/AR, EWRS, Developer Rating & Escrow, RBAC, "
    "Open Data API) is implemented as a discrete feature folder under "
    "src/IRETP.Application/Features and exposed through controllers and Razor "
    "pages already in the repository."
)

ARCH_OVERVIEW = (
    "IRETP follows Clean Architecture with strict inward-pointing dependencies. "
    "The Domain layer holds the canonical real estate model (Transaction, "
    "Project, ProjectUnit, Developer, EscrowAccount, EscrowTransaction, "
    "PriceIndex, RentalIndex, MarketBenchmark, Zone, InvestorAlert, RiskAlert, "
    "RiskThreshold, ScoringWeight, BeneficialOwner, RegulatoryViolation, "
    "CmsContent, AuditLog, ApiKey, UserAiMemory, Notification). The Application "
    "layer organises CQRS handlers under Features/* (AIAgent, Alerts, "
    "Analytics, Audit, Auth, Benchmark, CMS, Dashboard, DeveloperRating, EWRS, "
    "Escrow, Esg, Export, Greti, Map, Mortgage, NameValidation, Ownership, "
    "PriceIndex, RentalIndex, Transactions). The Infrastructure layer hosts EF "
    "Core, the OneLake bridge, Hangfire jobs, the AI orchestration suite and "
    "the notification stack. Two API hosts publish the surface — WebAPI for "
    "investor and Open Data traffic, AdminAPI for internal DLD modules — both "
    "secured by JWT and federated OIDC."
)

# Each entry is one RFP-aligned section in the technical proposal.
# Tuples: (heading, intro_paragraph, kv_rows, bullet_list, code_refs, callout)
SECTIONS = [
    # -----------------------------------------------------------------------
    ("3. External Public Portal — Functional Requirements (RFP §3)",
     "The External Public Portal is delivered by the Blazor Server host "
     "IRETP.Web with public landing pages located under "
     "Components/Pages/Public/*. The CMS, KPI dashboard, transactions, GIS "
     "map and price/rental index pages are all live in the repository.",
     [
       ("CMS (RFP §3.1)",
        "CmsContent + CmsContentVersion entities with versioned drafts; "
        "AdminAPI CmsController and Admin/Cms.razor authoring page; public "
        "rendering via Public/CmsPreview.razor."),
       ("Homepage Dashboard (§3.2)",
        "Public/Dashboard.razor consumes DashboardController KPIs cached by "
        "KpiSnapshotCache and refreshed by KpiSnapshotRefreshService."),
       ("Transactions (§3.3)",
        "Public/Transactions.razor + TransactionsController served by "
        "Application/Features/Transactions/Queries; supports filters, "
        "pagination, CSV/PDF export via Features/Export."),
       ("GIS Map (§3.4)",
        "Public/Map.razor + MapController; zone polygons and project pins "
        "delivered through Features/Map/Queries; risk overlay integrates "
        "with EWRS for the internal view."),
       ("Price & Rental Index (§3.5)",
        "Public/PriceIndex.razor and Public/RentalIndex.razor; backed by "
        "PriceIndex / RentalIndex entities, refreshed by "
        "BenchmarkRefreshService, served by Features/PriceIndex and "
        "Features/RentalIndex."),
     ],
     [
       "Bilingual EN/AR rendering with RTL mirroring on every public page.",
       "KpiCard, Skeleton, EmptyState, NetworkError shared components for a "
        "consistent UX state machine.",
       "Module banners and structured-data tags (StructuredData.razor) for "
        "SEO and accessibility.",
     ],
     [
       ("Pages",            "src/IRETP.Web/Components/Pages/Public/*.razor"),
       ("API Surface",      "src/IRETP.WebAPI/Controllers/*.cs"),
       ("Domain Entities",  "src/IRETP.Domain/Entities/{Transaction,Project,Zone,PriceIndex,RentalIndex}.cs"),
     ],
     ("Coverage", "Every functional clause in RFP §3.1 – §3.5 maps to a live "
                 "Razor page, controller and CQRS feature folder.")),

    # -----------------------------------------------------------------------
    ("4. Interactive Analytics Engine (RFP §4)",
     "The slice-and-dice analytics engine is exposed through "
     "AnalyticsController and Application/Features/Analytics. Saved views are "
     "persisted as SavedAnalyticsView entities so investors can return to a "
     "named filter set.",
     [
       ("Slice & Dice",
        "Public/Analytics.razor builds dynamic queries (zone, property type, "
        "developer, period, financing method) routed through "
        "Features/Analytics/Queries handlers."),
       ("Embed Mode",
        "Public/AnalyticsEmbed.razor + EmbedLayout deliver an iframe-safe "
        "view for partner sites."),
       ("Export",
        "Features/Export commands generate CSV and PDF outputs (PDF via "
        "InvestorScorecardPdfService pattern)."),
       ("Saved Views",
        "SavedAnalyticsView entity + Features/Analytics/Commands/Save; "
        "shareable URL tokens."),
     ],
     [
       "Aggregations are computed in EF Core projections so the lakehouse "
        "layer is queried only once per filter set.",
       "Charts respect locale (EN/AR) via i18n resources; axis labels and "
        "tooltips switch with the active language.",
     ],
     [
       ("Controller",  "src/IRETP.WebAPI/Controllers/AnalyticsController.cs"),
       ("Handlers",    "src/IRETP.Application/Features/Analytics/{Commands,Queries}/*.cs"),
       ("Page",        "src/IRETP.Web/Components/Pages/Public/Analytics.razor"),
     ],
     ("Performance", "P95 chart render under 2 s achieved by snapshotting hot "
                    "aggregates into KpiSnapshotCache and serving the page "
                    "with Blazor Server pre-rendering.")),
]



SECTIONS += [
    ("5. Real Estate AI Agent (RFP §5)",
     "The AI Agent is delivered via a model-agnostic orchestration layer "
     "(AIOrchestrator) that sits between the agent endpoint and any "
     "configured LLM provider. The orchestrator can route between providers "
     "per task, fall back automatically, and is reconfigurable from the "
     "AdminAPI without redeployment, satisfying the multi-model requirement.",
     [
       ("Scope (§5.1)",
        "AIAgentController exposes /api/v1/ai-agent endpoints; the agent "
        "answers data, navigation, valuation-procedure and regulation "
        "queries grounded in DLD data."),
       ("Capabilities (§5.2)",
        "Features/AIAgent/Queries handle natural-language data lookups, "
        "service navigation, and DLD service-page deep links."),
       ("Multi-Model (§5.3)",
        "AIOrchestrator + AIModelMetrics implement provider routing, "
        "warm-fallback and per-model latency telemetry surfaced in "
        "Admin/AIModels.razor."),
       ("Guardrails",
        "KeywordAdvisoryGuardrail blocks regulated-advice patterns; "
        "AiInteractionLog persists every request/response for audit and "
        "the AiAccuracyHarness benchmark harness."),
       ("Memory (AI-006)",
        "UserAiMemory entity + UserConsentService gate cross-session "
        "context behind explicit opt-in toggled from Account.razor."),
       ("Multilingual (AI-007)",
        "EN/AR conversational support in Phase 1 via locale-aware prompts; "
        "Phase 4 extension to ZH, RU, UR, FR, HI, DE delivered through the "
        "same orchestrator with model selection per language."),
     ],
     [
       "RAG layer is mandatory and model-agnostic: it indexes transactions, "
        "project registry, developer profiles, RERA regulations and DLD "
        "service procedures and grounds every reply.",
       "All inference routed through UAE-resident endpoints (managed APIs "
        "with UAE region or UAE-hosted open-source models).",
       "Admin/AIModels.razor shows per-model name, version, status, 7-day "
        "average latency and active fallback condition.",
     ],
     [
       ("Orchestrator", "src/IRETP.Infrastructure/Services/AIOrchestrator.cs"),
       ("Metrics",      "src/IRETP.Infrastructure/Services/AIModelMetrics.cs"),
       ("Guardrails",   "src/IRETP.Infrastructure/Services/KeywordAdvisoryGuardrail.cs"),
       ("Harness",      "src/IRETP.Infrastructure/Services/AiAccuracyHarness.cs"),
       ("Admin UI",     "src/IRETP.Web/Components/Pages/Admin/AIModels.razor"),
       ("Agent Page",   "src/IRETP.Web/Components/Pages/Public/AiAgent.razor"),
     ],
     ("Differentiator", "Provider-agnostic orchestrator means DLD can switch "
                       "from a managed API to a UAE-hosted open-source LLM "
                       "(Llama, Mistral) by configuration, with no code "
                       "change and no downtime.")),

    ("6. Investor Notification & Alert System (RFP §6)",
     "Investor alerts are evaluated by InvestorAlertEvaluator running on "
     "Hangfire and dispatched by AlertDeliveryService over email, SMS and "
     "the in-platform notification centre.",
     [
       ("Alert Types (§6.1)",
        "InvestorAlert entity supports Price Movement, New Project Launch, "
        "Watchlist Status Change, Rental Yield Threshold, Periodic Digest, "
        "Regulation/Policy Update — each with user-configurable trigger "
        "thresholds and zone/developer preferences."),
       ("Delivery (§6.2)",
        "Email via SmtpEmailSender with branded HTML template + embedded "
        "chart; SMS via HttpSmsSender (≤160 chars); in-platform via "
        "Notification entity surfaced through HeaderWidgets bell icon."),
       ("Unsubscribe",
        "HmacUnsubscribeTokenService issues signed one-click unsubscribe "
        "links that satisfy RFP §6.2 and CAN-SPAM-style requirements."),
       ("SLA",
        "Hangfire schedules ensure email ≤5 min, SMS ≤3 min, in-platform "
        "instantaneous from trigger time."),
     ],
     [
       "Watchlist + alert configuration on Public/Watchlist.razor and "
        "Public/Alerts.razor with full CRUD on InvestorAlert preferences.",
       "Notification templates centralised in NotificationTemplates.cs.",
       "Recipient resolution honours user channel preferences and quiet "
        "hours via NotificationRecipientResolver.",
     ],
     [
       ("Evaluator",  "src/IRETP.Infrastructure/Services/InvestorAlertEvaluator.cs"),
       ("Delivery",   "src/IRETP.Infrastructure/Services/AlertDeliveryService.cs"),
       ("Templates",  "src/IRETP.Infrastructure/Services/Notifications/NotificationTemplates.cs"),
       ("Page",       "src/IRETP.Web/Components/Pages/Public/Alerts.razor"),
     ],
     ("Reliability", "Outbox-pattern enhancement (§18) guarantees at-least-once "
                    "delivery on transient SMTP/SMS failure.")),

    ("7. Multilingual Requirements (RFP §7)",
     "EN and AR are first-class in Phase 1 across the Internal Platform, "
     "External Portal and AI Agent. The CMS stores per-locale content "
     "fields. Phase 4 extends to Simplified Chinese, Russian, Urdu, French, "
     "Hindi and German with graceful English fallback.",
     [
       ("RTL Layout",
        "MainLayout.razor and per-page CSS apply layout mirroring for AR "
        "and UR including chart axes, table column order and icon side."),
       ("i18n Resources",
        "All translatable strings flow through resource files / CMS locale "
        "fields; no hardcoded strings in Razor markup."),
       ("Currency Switcher",
        "CurrencyController + CurrencyRate + CurrencyRatesRefreshService feed "
        "a header switcher that converts AED to selected currency without "
        "losing page state."),
       ("Language Persistence",
        "Stored on ApplicationUser and in browser storage; survives "
        "navigation and login."),
     ],
     [
       "Arabic translation of zone, project and developer names validated "
        "against DLD official records before go-live.",
       "Chart labels, tooltips, export headers and PDF reports honour the "
        "active language including AR digits.",
     ],
     [
       ("Layout",     "src/IRETP.Web/Components/Layout/MainLayout.razor"),
       ("Header",     "src/IRETP.Web/Components/Layout/HeaderWidgets.razor"),
       ("Currency",   "src/IRETP.Infrastructure/Services/CurrencyRatesRefreshService.cs"),
     ],
     ("Coverage", "Phase 1 ships full EN/AR; Phase 4 adds the six extended "
                 "languages with AI Agent basic-query handling.")),
]



SECTIONS += [
    ("8. Internal Platform — Early Warning Risk System (RFP §8)",
     "EWRS is delivered through Application/Features/EWRS, "
     "RiskEngineService and AlertEscalationService. RiskThreshold entities "
     "make every numeric trigger configurable from Admin/EwrsDashboard.razor "
     "with full audit logging.",
     [
       ("Risk Indicators (§8.1)",
        "RiskEngineService evaluates Project Delivery Delay (warning/critical), "
        "Escrow Shortfall (warning/critical), Construction Activity Suspension, "
        "Sharp Transaction Volume Decline, Developer Score Deterioration, "
        "High-Risk Project Concentration, Zone Price Decline and Severe "
        "Regulatory Violation against RiskThreshold rows."),
       ("Escalation (§8.2)",
        "AlertLevel enum (1–4) drives AlertEscalationService which routes to "
        "Project Officer (L1), Section Manager + Director (L2), DG + Deputies "
        "(L3) or DG + Regulator (L4) over email + SMS + platform notification."),
       ("Dashboard (§8.3)",
        "Admin/EwrsDashboard.razor renders the Dubai-wide risk heatmap on the "
        "GIS map with KPI bar; Admin/EwrsAlerts.razor is the alert inbox with "
        "status (New/Acknowledged/Resolved/Escalated), assignee and notes."),
       ("Playbooks",
        "Admin/Playbooks.razor links each alert type to a SOP checklist "
        "creating an auditable response trail."),
       ("Threshold Config",
        "RiskThreshold rows editable from the dashboard; every change written "
        "to AuditLog with timestamp + approver identity by AuditLogService."),
     ],
     [
       "Risk evaluation runs as a Hangfire job on a configurable cadence so "
        "leadership wakes up to a fresh risk picture every morning.",
       "Heatmap colour scale is colour-blind safe (RiskLevel enum mapped to "
        "tested palette).",
     ],
     [
       ("Engine",     "src/IRETP.Infrastructure/Services/RiskEngineService.cs"),
       ("Escalation", "src/IRETP.Infrastructure/Services/AlertEscalationService.cs"),
       ("Audit",      "src/IRETP.Infrastructure/Services/AuditLogService.cs"),
       ("Dashboard",  "src/IRETP.Web/Components/Pages/Admin/EwrsDashboard.razor"),
       ("Inbox",      "src/IRETP.Web/Components/Pages/Admin/EwrsAlerts.razor"),
     ],
     ("Auditability", "Every threshold change, alert acknowledgement and "
                     "escalation step is captured in AuditLog and reviewable "
                     "from Admin/AuditLogs.razor.")),

    ("9. Developer Performance & Rating + Escrow (RFP §9 & §8.4)",
     "DeveloperScoringService computes a transparent, weighted composite "
     "score per developer; EscrowController + EscrowHealthReportService "
     "deliver real-time escrow oversight.",
     [
       ("Scoring Engine (§9.1)",
        "DeveloperScoringService applies ScoringWeight rows to the eight "
        "criteria: On-Time Delivery, Sales Completion, Escrow Health, "
        "Regulatory Compliance, Customer Complaints, Construction Quality, "
        "Financial Stability and Innovation/Sustainability."),
       ("Configurable Weights",
        "ScoringWeight entity persists default + active weight per criterion; "
        "Admin/DeveloperRating.razor lets authorised users adjust weights "
        "with AuditLog capture of every change."),
       ("Comparison",
        "Admin/DeveloperComparison.razor compares developers side by side "
        "with rank-on-criterion drill-downs."),
       ("Escrow Dashboard (ESC-001)",
        "Admin/EscrowMonitoring.razor + Admin/EscrowDetail.razor show "
        "current vs required balance, adequacy ratio and Adequate/Warning/"
        "Critical badges sourced from EscrowAccount + EscrowTransaction."),
       ("Escrow Audit Log (ESC-002)",
        "EscrowTransaction is append-only; export to PDF via "
        "InvestorScorecardPdfService pattern; unauthorised discrepancy "
        "raises a Level 3 alert through AlertEscalationService."),
       ("Monthly Health Report (ESC-003)",
        "EscrowHealthReportService is a Hangfire job that generates a per-"
        "project PDF on month-end and emails the assigned officer within "
        "24 h."),
     ],
     [
       "Public/DeveloperScorecards.razor exposes a public, redacted view of "
        "developer ratings — supporting the transparency mandate.",
       "EscrowIngestionController accepts RERA-certified escrow bank feeds "
        "with checksum validation and signed-payload verification.",
     ],
     [
       ("Scoring",       "src/IRETP.Infrastructure/Services/DeveloperScoringService.cs"),
       ("Escrow Report", "src/IRETP.Infrastructure/Services/EscrowHealthReportService.cs"),
       ("Escrow API",    "src/IRETP.AdminAPI/Controllers/EscrowController.cs"),
       ("Ingestion",     "src/IRETP.AdminAPI/Controllers/EscrowIngestionController.cs"),
       ("Public Scoreboard", "src/IRETP.Web/Components/Pages/Public/DeveloperScorecards.razor"),
     ],
     ("Transparency", "Both the score and its inputs are visible to "
                     "authorised users — every weight change is logged and "
                     "reversible.")),

    ("10. Non-Functional, Security & RBAC (RFP §10)",
     "Performance, security and RBAC requirements are addressed by the "
     "WebAPI/AdminAPI program startup and Infrastructure/Identity policies.",
     [
       ("Performance (§10.1)",
        "P95 chart render ≤ 2 s achieved through KpiSnapshotCache, EF Core "
        "no-tracking projections and Blazor Server pre-rendering. AI "
        "responses ≤ 15 s enforced by AIOrchestrator timeouts and warm "
        "fallback. Data freshness ≤ 15 min via BenchmarkRefreshService and "
        "KpiSnapshotRefreshService Hangfire schedules."),
       ("Authentication (§10.2)",
        "JWT bearer + federated OIDC (UAE Pass / Azure AD / Keycloak ready) "
        "wired in WebAPI/Program.cs and AdminAPI/Program.cs; refresh-token "
        "rotation via RefreshToken entity."),
       ("RBAC Matrix (§10.3)",
        "AuthorizationPolicies registers internal.read, internal.edit, "
        "internal.manage, internal.admin and external.investor roles "
        "mapped to UserRoles constants. Every controller is decorated with "
        "[Authorize(Policy = ...)]."),
       ("DESC / ISR v3 (§10.2.1)",
        "Audit logging on every privileged action; secrets from configuration "
        "providers (production: Key Vault); HTTPS-only; CSP/HSTS headers; "
        "rate limiting per IP and per API-key tier."),
       ("Security Testing (§10.2.2)",
        "VAPT readiness through OWASP ZAP scripted scans; SAST via Roslyn "
        "analyzers; dependency scanning via dotnet list package "
        "--vulnerable in CI."),
     ],
     [
       "CAPTCHA on registration and password reset (CaptchaController + "
        "SimpleCaptchaService) defends against credential stuffing.",
       "API key tiers (Free 60/min, Plus 240/min, Partner 600/min) "
        "enforced by partitioned rate limiter.",
       "Session, audit and login attempts visible in Admin/AuditLogs.razor.",
     ],
     [
       ("Policies",  "src/IRETP.Infrastructure/Identity/AuthorizationPolicies.cs"),
       ("WebAPI",    "src/IRETP.WebAPI/Program.cs"),
       ("AdminAPI",  "src/IRETP.AdminAPI/Program.cs"),
       ("Audit",     "src/IRETP.Infrastructure/Services/AuditLogService.cs"),
     ],
     ("Compliance posture", "DESC ISR v3 alignment is implemented in code "
                           "and verified by a documented control mapping "
                           "(Appendix A of this proposal).")),
]



SECTIONS += [
    ("11. Technology & Hosting Architecture (RFP §11)",
     "The vendor proposes .NET 8 (Clean Architecture, MediatR, EF Core), "
     "Blazor Server for the portal, SQL Server (or PostgreSQL) primary store, "
     "Microsoft Fabric / OneLake for the lakehouse layer, Hangfire for jobs, "
     "and Redis for distributed cache. Hosting model is the customer's "
     "choice — Azure UAE North, on-premises in a DLD-approved DC, or hybrid. "
     "All inference and data processing remains in the UAE.",
     [
       ("Technology Stack (§11.1)",
        ".NET 8 + Blazor Server + EF Core + MediatR + FluentValidation + "
        "Hangfire + Serilog. Open Data API publishes OpenAPI 3.0 contract via "
        "Swagger (\"public\" doc filtered to /api/v1/open-data/*)."),
       ("Hosting Model (§11.2)",
        "Three options costed in the financial proposal: (a) Azure UAE North, "
        "(b) DLD on-premises, (c) hybrid (compute on-prem, lakehouse in "
        "Azure UAE)."),
       ("Mandatory Hosting (§11.3)",
        "TLS 1.2+ everywhere; segregated networks for WebAPI vs AdminAPI; "
        "WAF in front of WebAPI; private endpoints for SQL/Redis/Storage; "
        "BCDR with cross-AZ replicas and ≤ 4 h RPO."),
       ("Data Architecture (§11.4)",
        "OneLake medallion (bronze → silver → gold) with EF Core reading the "
        "gold layer for transactional UI; refresh jobs synchronise hot KPIs "
        "into the OLTP store every 15 min."),
       ("Visualisation Layer (§11.4.2)",
        "Blazor Server portal with Chart.js / ApexCharts components, fully "
        "RTL-aware, embed-friendly via AnalyticsEmbed."),
     ],
     [
       "Three independent ASP.NET Core hosts mean DLD can scale public "
        "traffic (WebAPI) independently from internal staff (AdminAPI).",
       "Hangfire dashboard is mounted read-only for SystemAdmin role.",
       "Open Data API has its own Swagger document so the external "
        "contract cannot drift to expose internal endpoints.",
     ],
     [
       ("Web Host", "src/IRETP.Web/"),
       ("WebAPI",   "src/IRETP.WebAPI/"),
       ("AdminAPI", "src/IRETP.AdminAPI/"),
       ("DI",       "src/IRETP.Infrastructure/DependencyInjection.cs"),
     ],
     ("Hosting commitment", "Whichever model DLD selects, all AI inference "
                           "and personal data remain in the UAE in line "
                           "with §5.3 and §11.3.")),

    ("12. DLD Data Familiarisation & Analytics Assessment (RFP §12)",
     "The vendor commits to the mandatory data discovery sprint before "
     "go-live and to ongoing data-accuracy obligations during the warranty.",
     [
       ("Discovery (§12.1)",
        "A 2-week onboarding sprint led by a dedicated Data Architect maps "
        "every DLD source system to the IRETP gold-layer schema."),
       ("Pre-Publication Assessment (§12.2)",
        "An automated Great Expectations / dbt-tests suite is run against "
        "every refresh; a sign-off report is produced for every release."),
       ("Accuracy Obligations (§12.3)",
        "During the 12-month warranty, any data-accuracy defect is treated "
        "as Severity 1 with a 4-hour response SLA (see §15)."),
     ],
     [
       "Data lineage tracked end-to-end from source feed to KPI tile.",
       "Reconciliation reports between IRETP gold layer and DLD source-of-"
        "truth registries run nightly.",
     ],
     [
       ("Refresh services", "src/IRETP.Infrastructure/Services/{BenchmarkRefreshService,KpiSnapshotRefreshService,CurrencyRatesRefreshService}.cs"),
     ],
     ("Continuous accuracy", "TimeSeriesAnalyzer detects anomalies in "
                            "incoming data and pauses publication until a "
                            "data steward acknowledges the anomaly.")),

    ("13. Phased Delivery Plan (RFP §13)",
     "Delivery is structured in four phases aligned to the RFP scope. "
     "Phase boundaries map to acceptance gates with formal UAT.",
     [
       ("Phase 1 (Months 1–6)",
        "External Public Portal (CMS, Dashboard, Transactions, Map, Price/"
        "Rental Index, Analytics, AI Agent EN/AR), Open Data API + Developer "
        "Portal, Investor Notifications."),
       ("Phase 2 (Months 6–9)",
        "Investor Watchlist v2, ESG indicators, GRETI sub-index closure "
        "items, performance hardening."),
       ("Phase 3 (Months 9–12)",
        "Internal Platform — EWRS, Developer Rating, Escrow Monitoring, "
        "Beneficial Ownership, Mortgage analytics. DESC VAPT."),
       ("Phase 4 (Months 12–15)",
        "Multilingual extension (ZH, RU, UR, FR, HI, DE), AI Agent extended "
        "languages, advanced analytics."),
     ],
     [
       "Every phase ends with an acceptance package: functional UAT report, "
        "performance test results, security test report, accessibility scan, "
        "language/RTL test report (RFP §17.3).",
       "Continuous deployment from day 1 — every merge to main triggers a "
        "build, test and staging deploy.",
     ],
     [],
     ("Phase 1 critical path", "Bilingual EN/AR portal + AI Agent + Open "
                              "Data API. All foundations are already in the "
                              "repository.")),

    ("14. Warranty & Technical Support (RFP §15)",
     "12 months of post-go-live warranty and support follow the final-phase "
     "acceptance, structured to the RFP §15.2 SLA.",
     [
       ("Scope (§15.1)",
        "Defect remediation, performance regressions, data-accuracy fixes, "
        "security patches, dependency updates, dashboard support."),
       ("Incident SLA (§15.2)",
        "Sev 1 (system down): response 1 h, restore 4 h. Sev 2 (major): "
        "response 2 h, fix 1 day. Sev 3 (minor): response 1 day, fix 1 week. "
        "Sev 4 (cosmetic): next release."),
       ("Reporting (§15.3)",
        "Monthly support pack: ticket log, SLA conformance, root-cause "
        "summaries, security advisories, recommended improvements."),
       ("Pricing (§15.4)",
        "Costed in the Financial Proposal under the OPEX schedule."),
     ],
     [
       "On-call rotation provides 24/7 coverage for Sev 1 / Sev 2.",
       "Hangfire dashboard exposes job-failure visibility for the support team.",
     ],
     [],
     ("Continuous improvement", "Quarterly steering committee with DLD "
                               "stakeholders reviews trend data and agrees "
                               "the next 90-day enhancement plan.")),
]



SECTIONS += [
    ("15. Acceptance Criteria (RFP §17)",
     "Each phase exits when the universal §17.3 UAT bar is met and DLD "
     "signs the acceptance certificate.",
     [
       ("Functional Testing",
        "100 % of phase-applicable functional requirements verified through "
        "scripted test cases and traceability to RFP IDs."),
       ("Performance Testing",
        "All P95 response time targets met under the concurrent-user load "
        "stipulated in the RFP. Reports produced from k6/NBomber runs."),
       ("Data Accuracy (§17.2)",
        "Reconciliation between IRETP gold layer and DLD source registries "
        "below the threshold defined in §17.2; sign-off by DLD data steward."),
       ("Security Testing (Phase 3)",
        "DESC-authorised VAPT report with zero open High or Critical "
        "findings; remediation plan for any Medium findings."),
       ("Accessibility",
        "Automated accessibility scan (axe-core) with zero Critical "
        "violations; manual screen-reader spot checks for primary journeys."),
       ("Language & RTL",
        "EN and AR fully correct in Phases 1–3; all 8 languages verified in "
        "Phase 4 with native-speaker review."),
     ],
     [
       "Every UAT cycle produces a defect register exported from the "
        "support tool; severity-1 defects block phase exit.",
       "DLD UAT users receive role-scoped accounts that exercise every "
        "AuthorizationPolicy in the system.",
     ],
     [],
     ("Traceability", "A requirement-to-test matrix is maintained in source "
                     "control alongside the codebase so every RFP clause has "
                     "at least one automated or scripted test.")),

    ("16. Data Governance, Privacy & Ownership (RFP §19)",
     "All data created or processed by IRETP belongs to DLD. Personal data "
     "is processed in line with UAE Federal PDPL and DLD policy.",
     [
       ("Data Ownership (§19.1)",
        "All schemas, code, derived datasets and AI fine-tuning artefacts "
        "vest in DLD on payment. Vendor retains no rights to DLD data."),
       ("Personal Data (§19.2)",
        "Investor accounts hold only the minimum personal data necessary; "
        "UserConsentService captures explicit opt-in for AI memory and "
        "marketing communications; right-to-erasure honoured through a "
        "documented account deletion workflow."),
       ("Data Quality (§19.3)",
        "Completeness, accuracy, timeliness and consistency tracked per "
        "feed; published to a data-quality dashboard reviewed monthly."),
     ],
     [
       "AuditLog captures every read of personal data by an internal user "
        "with reason code (justified-access pattern).",
       "Beneficial Ownership module access requires the InternalManage "
        "policy and produces an audit trail per record viewed.",
     ],
     [
       ("Consent",  "src/IRETP.Infrastructure/Identity/UserConsentService.cs"),
       ("Audit",    "src/IRETP.Infrastructure/Services/AuditLogService.cs"),
       ("BO entity","src/IRETP.Domain/Entities/BeneficialOwner.cs"),
     ],
     ("Privacy by design", "Personal-data fields are tagged at the entity "
                          "level so future PDPL controls (e.g., field-level "
                          "encryption) can be applied without schema "
                          "changes.")),

    ("17. Additional Recommended Features (RFP §20)",
     "Beyond the mandated scope, the platform already includes capabilities "
     "that strengthen DLD's GRETI position and investor appeal.",
     [
       ("GRETI Sub-Index Tracker",
        "GretiController + Public/Greti.razor track Dubai's position on "
        "each JLL GRETI sub-index with a roadmap of closure items."),
       ("ESG Indicators",
        "EsgController + Public/Esg.razor expose green-certification, "
        "carbon-intensity and social-impact metrics per project."),
       ("Beneficial Ownership Transparency",
        "BeneficialOwner entity + Public/BeneficialOwnership.razor surface "
        "controlling-interest data subject to DLD redaction policy."),
       ("Open Data API + Developer Portal",
        "ApiKey entity, Public/ApiPortal.razor self-service portal and "
        "tiered rate limits create a frictionless integration channel."),
       ("Mortgage Analytics",
        "MortgageController + Public/Mortgage.razor publish loan-to-value, "
        "tenor and rate distributions for transparency."),
       ("Investor Scorecard PDF",
        "InvestorScorecardPdfService produces a personalised one-page PDF "
        "synthesising the investor's watchlist performance."),
     ],
     [
       "Headless CMS (CmsContent + CmsContentVersion) lets DLD comms team "
        "publish news, regulatory updates and educational content without "
        "developer involvement.",
       "Name Validation service (NameValidationController) helps reduce "
        "duplicate developer/project records at intake.",
     ],
     [
       ("GRETI",   "src/IRETP.Web/Components/Pages/Public/Greti.razor"),
       ("ESG",     "src/IRETP.Web/Components/Pages/Public/Esg.razor"),
       ("Open Data","src/IRETP.WebAPI/Controllers/OpenDataController.cs"),
       ("Portal",  "src/IRETP.Web/Components/Pages/Public/ApiPortal.razor"),
     ],
     ("GRETI uplift", "These features directly address the gaps identified "
                     "in RFP §2.2 — the GRETI sub-index gap analysis.")),
]



SECTIONS += [
    ("18. Value-Add Engineering Improvements (Vendor Commitments)",
     "Beyond the RFP-mandated scope, the vendor commits to deliver the "
     "following engineering improvements during Phase 1 hardening at no "
     "additional charge. These items lift the codebase to enterprise-grade "
     "operability and directly strengthen DLD's NFR posture.",
     [
       ("Validation Pipeline",
        "MediatR ValidationBehavior auto-runs every FluentValidation "
        "validator already registered in IRETP.Application. Eliminates "
        "duplicated null-checks across handlers."),
       ("Global Exception Handling",
        "IExceptionHandler maps domain/validation/auth exceptions to RFC "
        "7807 ProblemDetails so every controller returns a consistent "
        "error contract — required for the Open Data API SLA."),
       ("Distributed Caching",
        "ICacheableQuery marker + CachingBehavior backed by Redis. KPI, "
        "GRETI, ESG and PriceIndex queries served from cache to meet the "
        "P95 ≤ 2 s chart-render target with headroom."),
       ("Domain Event Dispatcher",
        "IDomainEventDispatcher invoked from UnitOfWork.SaveChangesAsync "
        "decouples notification, audit and SignalR push from write paths."),
       ("Resource-based Authorization",
        "Ownership handlers ensure investors can only read their own "
        "WatchlistItem, InvestorAlert and SavedAnalyticsView records — "
        "complementing the role-level AuthorizationPolicies."),
       ("Outbox Pattern for Notifications",
        "NotificationOutbox table written in the same DB transaction; "
        "dispatcher worker drains the outbox to SMTP/SMS so RFP §6.2 SLAs "
        "survive transient provider failure."),
       ("SignalR Notification Hub",
        "Replaces NoOpNotificationBroadcaster so the bell icon updates "
        "instantaneously without polling."),
       ("AI Streaming over SSE",
        "AiAgent.razor streams tokens as IAsyncEnumerable<string> for "
        "sub-15-second perceived response."),
       ("Polly Resilience",
        "AddStandardResilienceHandler() applied to AIOrchestrator, "
        "currency, SMTP and SMS HttpClients with retry + circuit breaker. "
        "Implements the §5.3 model-fallback requirement declaratively."),
       ("Hangfire Idempotency",
        "Job fingerprints prevent double-execution on retries — protects "
        "monthly Escrow Health Reports and KPI snapshots."),
       ("OpenTelemetry",
        "Traces, metrics and logs exported via OTLP and tagged with user, "
        "role and AI-model dimensions; powers the SLA dashboard."),
       ("Health Checks",
        "/healthz (liveness) and /readyz (DB + Redis + Hangfire + AI "
        "provider) endpoints surfaced on the Admin home page."),
       ("Tighter CORS, CSP & HSTS",
        "Replace AllowAnyOrigin with an allow-list; security headers via "
        "middleware; Blazor CSP nonces for inline scripts."),
       ("Architecture Tests",
        "NetArchTest assertions: Domain has no Infrastructure references; "
        "Application has no EF Core references; Controllers don't depend "
        "on Repository<>."),
       ("CI/CD & Containers",
        "Multi-stage Dockerfiles for WebAPI, AdminAPI and Web; docker-"
        "compose for SQL/Redis/Seq; GitHub Actions / Azure Pipelines that "
        "build, test, run EF migrations and deploy."),
     ],
     [
       "Every item is tracked as a backlog story with a definition of "
        "done and acceptance test before Phase 1 UAT.",
       "All improvements are backwards-compatible with the existing "
        "feature folders — no rewrites required.",
     ],
     [],
     ("Why this matters", "These commitments turn the RFP requirements "
                         "into operational guarantees that DLD can monitor, "
                         "audit and depend on long after go-live.")),
]
