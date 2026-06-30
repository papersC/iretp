# IRETP — Data Dictionary

Authoritative listing of every persisted entity, the business meaning of each field, and how it relates to the RFP. EF Core migrations are the source of truth for schema types; this document is the source of truth for **business semantics**.

Convention: every table inherits `Id (Guid)`, `CreatedAt (datetime2)`, `CreatedBy (nvarchar)`, `UpdatedAt (datetime2?)`, `UpdatedBy (nvarchar?)` from `BaseEntity`. Fields named below are in addition to those.

---

## Section 1 — Market data

### Zone

Dubai administrative zones as defined by Dubai Municipality GIS.

| Field | Meaning |
|---|---|
| `Name` | English zone name. |
| `NameAr` | Arabic zone name (validated against DLD official records pre-launch, RFP §13 #48). |
| `Code` | Internal zone code used by DLD source systems. |
| `Latitude`, `Longitude` | Centroid, used by `/map`. |
| `IsActive` | Soft-delete marker. |

### Transaction

Normalised transaction records ingested from the DLD registry.

| Field | Meaning |
|---|---|
| `TransactionDate` | Official transfer date. |
| `ZoneId` | FK to `Zone`. |
| `Community` | Sub-zone text label when DLD records include one. |
| `ProjectId`, `ProjectName` | Optional linkage to a `Project`. |
| `PropertyType` | `Apartment`, `Villa`, `Townhouse`, `Plot`, `Commercial`. |
| `TransactionType` | `Sale`, `Gift`, `Mortgage`, `Auction`, `Inheritance`. |
| `AreaSqft`, `AreaSqm` | Property area (dual unit). |
| `TransactionValue` | AED. |
| `PricePerSqft` | AED / sqft, derived pre-persistence. |
| `FinancingMethod` | `Cash`, `Mortgage`, `Developer` (off-plan financing). |
| `IsOffPlan` | Boolean flag. |

### Project

Project registry, sourced from the DLD project database + RERA licence records.

| Field | Meaning |
|---|---|
| `Name`, `NameAr` | Bilingual project name. |
| `DeveloperId` | FK to `Developer`. |
| `ZoneId` | FK to `Zone`. |
| `Status` | `Completed`, `UnderConstruction`, `FutureAnnounced`, `Stalled`. |
| `CompletionPercentage` | 0–100. Drives the §8.1 delay-detection logic. |
| `TotalUnits`, `SoldUnits`, `AvailableUnits` | Unit counts, invariant: Sold + Available ≤ Total. |
| `ExpectedDeliveryDate`, `ActualDeliveryDate` | Delivery dates. |
| `Latitude`, `Longitude` | Used by the FR-011 pin layer on `/map`. |
| `DldRegistrationNumber` | Official DLD reference. |
| `TotalProjectCost` | AED; feeds escrow adequacy. |

### ProjectUnit

Individual unit inventory under a `Project`, used by the detail panel in `/projects`.

### ProjectCertification

Sustainability certifications (LEED / Estidama Pearl / BREEAM) per project. Drives the ESG heatmap layer on `/map` + the ESG dashboard (RFP §20).

### Developer

Developer profiles, sourced from the RERA developer registry.

| Field | Meaning |
|---|---|
| `Name`, `NameAr` | Bilingual name. |
| `LicenceNumber` | RERA licence. |
| `IsActive` | Soft-delete marker. |
| Navigation: `Projects`, `Scores`, `Violations`, `BeneficialOwners` | One-to-many collections. |

### RegulatoryViolation

RERA violation log — drives the §8.1 Severe-Regulatory-Violation indicator.

| Field | Meaning |
|---|---|
| `DeveloperId` | FK. |
| `ViolationDate` | Official citation date. |
| `Severity` | `Minor`, `Major`, `Critical`. |
| `Description`, `DescriptionAr` | Bilingual summary. |
| `ReferenceNumber` | RERA incident number. |
| `IssuedBy` | Issuing authority. |

### BeneficialOwner

Beneficial-ownership transparency per developer. Drives `/beneficial-ownership`.

### PriceIndex

Zone × property-type × transaction-status × quarter price samples. Rendered on `/price-index` (FR-013).

### RentalIndex

Zone × property-type × short/long-term rent samples. Powers `/rental-index` and the yield calculator (FR-014).

### MarketBenchmark

Quarterly peer-city benchmarks for the GRETI comparison module (RFP §13 #51). Seeded with six cities: Dubai (DXB) plus London (LON), Singapore (SGP), New York (NYC), Paris (PAR), Hong Kong (HKG). `BenchmarkRefreshService` tops these up weekly.

---

## Section 2 — Escrow

### EscrowAccount

Project-level escrow balance snapshot.

| Field | Meaning |
|---|---|
| `ProjectId` | FK. |
| `AccountNumber`, `BankName` | Bank-side identifiers. |
| `CurrentBalance`, `RequiredMinimumBalance`, `AdequacyRatio` | RFP ESC-001 display fields. |
| `Status` | `Adequate` (≥ 100%), `Warning` (80–99%), `Critical` (< 80%). |
| `LastUpdated` | Feed timestamp. |

### EscrowTransaction

Immutable audit log per escrow account (RFP ESC-002). Read-only in the UI; `ExportPdf` is generated from the same rows.

---

## Section 3 — EWRS

### RiskThreshold

Admin-configurable thresholds for the ten §8.1 indicators. Each row carries `IndicatorKey`, `ThresholdValue`, `DefaultRiskLevel`, `DefaultAlertLevel`, `PlaybookStepsJson` (SOP checklist), plus `ModifiedBy` + `ModifiedAt` for the audit trail.

### RiskAlert

Alerts emitted by `RiskEngineService`.

| Field | Meaning |
|---|---|
| `IndicatorType` | One of the seeded `RiskThreshold.IndicatorKey` values. |
| `RiskLevel` | `Low`, `Medium`, `Warning`, `High`, `Critical`. |
| `AlertLevel` | `Level1_Operational` … `Level4_Strategic`. |
| `Status` | `New`, `Acknowledged`, `Resolved`, `Escalated`. |
| `ProjectId`, `DeveloperId`, `ZoneId` | Scoping — exactly one is typically set, depending on the indicator. |
| `Title`, `Description` | Localisable text, populated by the engine. |
| `AssignedTo`, `AcknowledgedBy`, `ResolvedBy`, `ActionNotes` | Owner tracking. |
| `AcknowledgeDeadline`, `ResolutionDeadline`, `LastEscalatedAt`, `AutoEscalated` | SLA timestamps (RFP §8.2). |
| `PlaybookProgressJson` | Per-alert checklist state. |

---

## Section 4 — Developer rating

### ScoringWeight

Per-criterion weights. `UpdateScoringWeightsCommandHandler` enforces the sum-to-100 rule.

### DeveloperScore

Quarterly composite scores per developer, with the component criterion scores and the weights that were in effect at the time of calculation. Drives `/developers` (leaderboard) and the public-facing `DeveloperScorecard`.

---

## Section 5 — Investor experience

### WatchlistItem

Per-user list of zones / projects / developers the investor is tracking. Consumed by `InvestorAlertEvaluator` when generating Watchlist-Project-Status-Change alerts.

### InvestorAlert

User-configured alert rules. `AlertType` values: `PriceMovement`, `NewProject`, `WatchlistChange`, `RentalYield`, `MarketDigest`, `RegulationUpdate`. `ThresholdValue`, `Period`, `ZoneId`, `DeveloperId` etc. vary by type.

### Notification

Per-user delivery record (every row represents one dispatch attempt, whether internal / email / SMS / push). Feeds the in-platform notification centre and DLD's compliance reporting.

### SavedAnalyticsView

User-saved analytics configurations. Capped at **12 per user** (RFP AN-003). If `IsPublic` is true, the row carries a `ShareToken` (GUID) and `ShareTokenExpiresAt` (12 months from creation, RFP AN-006).

---

## Section 6 — AI Agent

### AiInteractionLog

One row per AI query. Used for accuracy audits, guardrail triggers, and model-performance dashboards.

| Field | Meaning |
|---|---|
| `SessionId`, `UserId` | Correlation keys. `UserId` is null for anonymous sessions. |
| `Language` | `en` or `ar` (Phase 1) plus the six Phase-4 languages. |
| `Query`, `Answer` | Full content, truncated at 2000 / 8000 chars respectively. |
| `Topic` | Result of `AIOrchestrator.ClassifyTopic`. |
| `SourceCitation` | The citation injected into the answer. |
| `ModelUsed` | Tier (primary / secondary / nav / analytics) + model name. |
| `WasRefusal` | True when the answer matched a refusal pattern. |
| `LatencyMs`, `Success`, `ErrorMessage` | Runtime metrics. |

### UserAiMemory

Persistent cross-session memory for users who opted in via `ConsentAiMemory` (RFP AI-006). One row per (user, kind, key) tuple.

| Field | Meaning |
|---|---|
| `UserId` | FK (string, Identity user id). |
| `Kind` | `"zone"` or `"topic"`. |
| `Key` | Zone name or `AIOrchestrator.ClassifyTopic` bucket. |
| `Frequency` | Monotonically increasing count. |
| `LastUsedAt` | Updated on every mention. |

All rows are deleted when the user revokes consent (`AccountController.UpdateConsent`).

---

## Section 7 — CMS

### CmsContent / CmsContentVersion

Main content + version history (12-month retention per RFP §3.1). Each row stores Markdown + a staging-vs-production flag; the CMS editor at `/admin/cms` reads the latest draft and writes a new version on every publish.

---

## Section 8 — Platform

### AuditLog

RBAC-sensitive action trail. Written by `AuditLogService` from inside command handlers for EWRS threshold edits, scoring weight updates, CMS publishes, AI model switches, user role changes, and API key issuance.

### ApiKey

Keys issued to Open Data API consumers. Records `Name`, `KeyHash`, `RateLimitPerHour`, `ExpiresAt`, `LastUsedAt`, `IsRevoked`.

### RefreshToken

Rotating JWT refresh tokens. Single-use; revoked on logout or password reset.

### CurrencyRate

One row per (date, currency) pair. Refreshed daily from the UAE Central Bank (with drift-fallback in Dev). `Source` = `"UAECB"` or `"driftFallback"`.

### NameValidation

DLD-approved spellings for official zone / project / developer names — validated by DLD records before go-live (RFP §13 #48). Admin UI for DLD staff to maintain the canonical list.

---

## Appendix — Enumerations

| Enum | Values |
|---|---|
| `ProjectStatus` | Completed, UnderConstruction, FutureAnnounced, Stalled |
| `PropertyType` | Apartment, Villa, Townhouse, Plot, Commercial |
| `TransactionType` | Sale, Gift, Mortgage, Auction, Inheritance |
| `FinancingMethod` | Cash, Mortgage, Developer |
| `RiskLevel` | Low, Medium, Warning, High, Critical |
| `AlertLevel` | Level1_Operational, Level2_Managerial, Level3_SeniorLeadership, Level4_Strategic |
| `AlertStatus` | New, Acknowledged, Resolved, Escalated |
| `EscrowStatus` | Adequate, Warning, Critical |
| `ViolationSeverity` | Minor, Major, Critical |
| `CertificationScheme` | LEED, EstidamaPearl, BREEAM |
| `UserRole` | PublicVisitor, RegisteredInvestor, DldViewer, DldOperator, DldSupervisor, SystemAdministrator |
