# IRETP — Operational Runbook

Day-to-day operational procedures for DLD staff running the Integrated Real Estate Transparency Platform after go-live. Companion to [`ARCHITECTURE.md`](ARCHITECTURE.md) and [`DISASTER_RECOVERY.md`](DISASTER_RECOVERY.md).

---

## 1. Service contacts

| Role | On call | Escalation |
|---|---|---|
| Vendor primary | 24/7 via the P1 hotline documented in the SLA addendum. | Vendor on-call director after 30 min of P1 inaction. |
| DLD CISO | Security incidents only. | CISO deputy after 2 h. |
| DESC SOC | Via the ticketing portal configured under the SOC contract. | DESC duty manager by phone. |

Raise every incident in the shared incident-log spreadsheet (`docs/incidents/` path, tracked per DLD ops SOP). Mirror the entry into Hangfire via the "Notes" field on the relevant alert so the audit trail lives next to the data.

---

## 2. Daily operations

Run through this checklist at 09:00 UAE every business day.

1. **Health**
   - `GET https://iretp.dld.ae/healthz/live` — must return `Healthy`.
   - `GET https://iretp.dld.ae/healthz/ready` — must return `Healthy`.
   - `GET https://iretp.dld.ae/healthz/sla` — `Healthy` or `Degraded` acceptable; `Unhealthy` opens a P2.
2. **Hangfire dashboard** (`/hangfire`, local-only) — check for failed jobs in the Failed queue; retry once if transient, escalate if persistent.
3. **KPI freshness** — the homepage's "Refreshed at" timestamp must be within 20 min. Older than 30 min triggers a check of `KpiSnapshotRefreshService`.
4. **EWRS inbox** — review new alerts at `/ewrs`. Acknowledge what you'll action; escalations over 4 business hours auto-roll to Level 2 (`AlertEscalationService`).
5. **AI accuracy** — `POST /api/admin/ai-models/accuracy-test` once per week; results should stay ≥ 90%. Anything below 85% pages the vendor.
6. **Pending notifications** — `GET /api/admin/notifications/pending-count` should be near zero; values above 500 suggest the SMS/email gateway is throttling.

---

## 3. Incident response

### 3.1 Priority definitions (SLA §15.2)

| Priority | Definition | Response | Resolution | Coverage |
|---|---|---|---|---|
| P1 | Platform down or displaying materially incorrect data. | 1 h | 4 h | 24/7/365 |
| P2 | A core functional area (AI, map, exports, alerts) is unavailable. | 2 h | 24 h | Business hours + on-call escalation |
| P3 | Non-critical functionality impaired; minor data display issues. | 4 bus-h | 5 bus-d | Business hours |
| P4 | Minor usability issues or change requests. | 1 bus-d | 20 bus-d or next release | Business hours |

### 3.2 P1 triage order

1. Confirm scope from `/healthz/*`, App Insights live metrics, and ACE traffic graph.
2. Capture an incident ID and open the bridge line.
3. If SQL is the cause, fail over to the read replica (see DISASTER_RECOVERY §3).
4. If a hot-deploy broke something, execute slot-swap rollback (§4 below).
5. Notify DLD CISO within 2 h if the cause looks like a security incident.
6. Write-up in `docs/incidents/YYYY-MM-DD-<id>.md` within 3 business days of resolution — the vendor owes DLD a formal report per SLA §15.3.

### 3.3 Notification delivery failure

The dispatcher (`AlertDeliveryService`) runs every 2 minutes. If recipients report missing alerts:

1. `/healthz/sla` → inspect `notificationBacklog`.
2. Check SMTP / Twilio gateway status pages.
3. If upstream is green, re-enqueue by posting to `/api/admin/notifications/redeliver` with an alert id.
4. Root-cause any dispatch failure that breached the 5-minute email / 3-minute SMS SLA; file a P2.

### 3.4 AI fallback triggered

1. AIModels dashboard (`/admin/ai-models`) — check `FallbackActive` and `LastError` for each tier.
2. If the primary's rate-limit is exhausted, open a ticket with the provider and keep the secondary active in the meantime.
3. If Arabic accuracy drops below English parity by > 5 percentage points (AI-007 guardrail), switch `AI:primary:Arabic` to the fine-tuned build via `PUT /api/admin/ai-models/switch`.

### 3.5 EWRS indicator false positive

Authorised admins edit thresholds at `PUT /api/admin/ewrs/thresholds/{id}`. Every change writes `ModifiedBy` + `ModifiedAt` on the `RiskThreshold` row — DLD internal audit can recover the full history from SQL.

---

## 4. Deploy, rollback, roll-forward

All three App Services (Web, WebAPI, AdminAPI) run with two deployment slots: `staging` and `production`.

**Deploy** — CI pushes to the `staging` slot. After smoke tests pass, execute a slot-swap.

```
az webapp deployment slot swap \
  --resource-group iretp-uaenorth --name iretp-webapi \
  --slot staging --target-slot production
```

**Rollback** — swap the slots back. The EF migration layer is forward-only, so if a rollback requires a schema revert, restore the DB to a point-in-time snapshot (see DISASTER_RECOVERY §4).

**Roll-forward for hotfix** — create a new build from the `hotfix/*` branch, push to `staging`, swap. Tag the release in git once verified.

---

## 5. Routine maintenance

### 5.1 Weekly

- **Backup restoration test** — DISASTER_RECOVERY §5. Execute quarterly at minimum; log every run in the DR register.
- **Hangfire retry cleanup** — any job in the Failed queue > 7 days should be triaged or deleted.
- **TLS cert check** — Front Door auto-rotates; verify the App Services have the managed cert bound and expiry > 30 days.

### 5.2 Monthly

- **DESC SOC review** — the SOC report ships on the 5th. DLD CISO acknowledges and files under `docs/compliance/soc-reports/`.
- **VAPT patch review** — apply remediation guidance from the latest pen-test report; track every finding to closure.
- **Escrow-health PDF spot-check** — pull a sample from `/admin/escrow` and confirm the generated PDF is readable and bears the DLD header.

### 5.3 Quarterly

- **Developer scoring rerun** — triggered automatically on the 1st of Jan / Apr / Jul / Oct at 00:00 UAE. Check the run completed without errors.
- **Benchmark refresh audit** — verify that LON / SGP / NYC / PAR / HKG rows in `MarketBenchmarks` have `UpdatedAt` within the last quarter.
- **Penetration test** — DESC-approved assessor. Zero Critical / High must remain open.

### 5.4 Annually

- **DESC ISR re-certification**.
- **ISO 27001 surveillance audit**.
- **DR failover drill** — full failover to the secondary region documented in DISASTER_RECOVERY §6.

---

## 6. Common support tasks

### 6.1 Reset MFA for an internal user

The account owner opens a DLD helpdesk ticket. An administrator with the `SystemAdministrator` role runs:

```
POST /api/admin/users/{userId}/reset-mfa
```

The user then re-enrols via `/api/auth/2fa/setup` on their next login.

### 6.2 Adjust an EWRS threshold

1. Open `/admin/ewrs/thresholds`.
2. Edit the indicator's `ThresholdValue` (keep `ThresholdUnit` intact).
3. Save. The row's `ModifiedBy` + `ModifiedAt` + the audit log capture the change.

Rule of thumb: any threshold change that would fire more than 20% more alerts must be reviewed with the Risk Committee before saving.

### 6.3 Rotate AI model API key

1. Get the new key from the provider portal.
2. Store it in Key Vault as `AI--primary--ApiKey` (or the relevant tier).
3. Restart the WebAPI app slot to pick up the new secret (no code change needed — keys are resolved from Key Vault via MSI at startup).
4. Trigger `POST /api/admin/ai-models/accuracy-test` to confirm parity.

### 6.4 Revoke an investor account (PDPL right-to-erasure)

```
POST /api/admin/users/{userId}/delete
```

Deletes the user, cascades `WatchlistItem`s, `InvestorAlert`s, `Notification`s, `UserAiMemory` rows, and anonymises `AiInteractionLog.UserId`. File the erasure confirmation under `docs/compliance/dsar/<userId>.json`.

### 6.5 Publish a CMS change

`DldOperator` or above:

1. Edit the draft on `/admin/cms`.
2. Preview via the staging link emitted in the editor.
3. Publish. The change is live within 30 s. A previous version remains for 12 months — use the "Rollback" button to revert.

### 6.6 Add a language (Phase 4)

The plumbing is already in place — see `ARCHITECTURE.md` §11. Concretely:

1. Extend `UiStateService.SupportedLanguages` and `LanguageNames`.
2. Add a map entry in `LocalizationService._map` for each existing key.
3. Add an AI-model route if the new language needs a dedicated tier.
4. Re-run the i18n coverage test at `/admin/i18n-coverage` — must hit 100%.

---

## 7. Audit trail

Every RBAC-sensitive action writes to the `AuditLogs` table. Query via:

```
GET /api/admin/audit?actor=<userId>&entity=<RiskThreshold|ScoringWeight|CmsContent|...>&fromDate=...&toDate=...
```

Results are paged. DLD internal audit can export any range to CSV for compliance reviews.

---

## 8. Decommissioning a component

Do not remove any entity, endpoint, or job without (a) a documented migration plan, (b) a rollback window, and (c) sign-off from the DLD product owner. The RFP commitment is that DLD receives full platform ownership at contract expiry (`docs/exit-plan.md`), so nothing in the codebase should reach a state that requires vendor-only knowledge to operate.
