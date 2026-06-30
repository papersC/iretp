# IRETP — Disaster Recovery Playbook

Procedures for recovering the Integrated Real Estate Transparency Platform after a catastrophic failure. Covers the RFP §11.3 commitments: RTO < 4 h, RPO < 1 h, data residency within the UAE at all times, encrypted backups with ≥ 30-day retention.

This document is the **authoritative source** during a DR event. Print a copy of this file and keep it in the DLD ops binder; during an incident you may lose access to the codebase.

---

## 1. Recovery objectives

| Parameter | Target | How we achieve it |
|---|---|---|
| RTO (Recovery Time Objective) | < 4 h | UAE North + UAE Central active-passive, Front Door failover < 5 min once triggered, stateless app services recreate from Bicep in ~20 min. |
| RPO (Recovery Point Objective) | < 1 h | SQL Server — point-in-time restore (PITR) down to 5 min; Storage — GRS with automatic cross-region async copy. |
| Backup retention | 30 days minimum | SQL PITR retention = 35 days; blob soft-delete = 45 days. |
| Data residency | UAE only | Paired region pair UAE North ↔ UAE Central; GRS storage is UAE-to-UAE; no secondary in a non-UAE region. |

---

## 2. Failure scenarios

### 2.1 Single-instance app failure

Auto-handled by the App Service Plan (3 instances behind Front Door). No manual action required unless Front Door reports all three unhealthy for > 5 min — then treat as a regional failure and follow §3.

### 2.2 SQL transient

SQL handles transient errors via the retry policy in `DependencyInjection.cs` (`EnableRetryOnFailure(3, 10s)`). Persistent failures > 2 min: trigger a failover to the geo-secondary DB (§3).

### 2.3 Regional failure

UAE North region fails or is unreachable. Execute §4.

### 2.4 Data corruption / ransomware

Malicious or accidental write that has been committed. Do **not** fail over — the secondary will replicate the corruption. Execute §5 (point-in-time restore) instead.

### 2.5 Key Vault compromise

Rotate every secret in Key Vault, revoke the MSIs, and force re-authentication for all internal users. Escalate to DESC SOC immediately.

---

## 3. Regional failover (UAE North → UAE Central)

**Pre-conditions**: `/healthz/live` unreachable from two independent networks for > 5 min; App Insights region-health = Unavailable.

1. **Confirm scope.** Azure Service Health portal for UAE North. Open incident ID in DLD ops spreadsheet.
2. **Fail over SQL.**
   ```
   az sql failover-group set-primary \
     --resource-group iretp-uaenorth \
     --server iretp-sql-uaenorth \
     --name iretp-sql-fog
   ```
   The failover group promotes the UAE Central replica in < 60 s.
3. **Update Front Door origin group.** Set `iretp-uaecentral-webapi`, `iretp-uaecentral-adminapi`, `iretp-uaecentral-web` as the active origins. Health probes confirm readiness in < 3 min.
4. **Verify.** `/healthz/ready` from the DR region must return `Healthy`. Hangfire resumes from its persisted state automatically.
5. **Communicate.** Update the DLD status page and notify stakeholders per the comms matrix.
6. **Monitor.** Watch App Insights for the first 60 min — error rates should return to baseline.

**Expected duration**: ≤ 30 min end-to-end.

---

## 4. Point-in-time restore (PITR)

Use this when the primary is intact but the data has been corrupted.

1. Identify the last-known-good timestamp from audit logs + `AuditLogs` table.
2. Create a restore database alongside the current one:
   ```
   az sql db restore \
     --resource-group iretp-uaenorth \
     --server iretp-sql-uaenorth \
     --source-database iretp \
     --dest-database iretp-restore-YYYYMMDDHHmm \
     --time "2026-04-18T09:30:00Z"
   ```
3. Verify row counts and spot-check affected entities (`RiskAlert`, `Transaction`, `SavedAnalyticsView`, whichever was impacted).
4. Swap the restored DB in:
   ```
   az sql db rename \
     --resource-group iretp-uaenorth --server iretp-sql-uaenorth \
     --name iretp --new-name iretp-corrupt-YYYYMMDD
   az sql db rename \
     --resource-group iretp-uaenorth --server iretp-sql-uaenorth \
     --name iretp-restore-YYYYMMDDHHmm --new-name iretp
   ```
5. Re-run `RiskEngineService` + `DeveloperScoringService` to rebuild derived data.
6. Delete `iretp-corrupt-*` once the DLD data team signs off.

---

## 5. Backup restoration drill

DLD's compliance team runs this **quarterly** (RFP §11.3). Dry-run only — do not promote the restored DB.

1. Pick a random timestamp from the last 14 days.
2. Restore via the command in §4 to a temp DB (`iretp-drill-<timestamp>`).
3. Connect via SSMS and validate:
   - Row counts for the ten largest tables match the expected trajectory.
   - Sample 50 transactions and cross-check against the DLD source system.
   - `AuditLogs` for the drill window is intact.
4. Document the drill outcome in `docs/dr-drills/YYYY-Q#.md`.
5. Drop the temp DB.

A failed drill is a P1 against the vendor.

---

## 6. Annual DR failover drill

End-to-end failover exercise, documented ahead of time with DLD.

1. Announce a maintenance window.
2. Execute §3 steps 1–5 against UAE Central.
3. Run smoke tests + a shortened UAT script.
4. Fail back to UAE North the same day.
5. Retrospective within 7 days. Any deviation from RTO or RPO is a learning item.

---

## 7. Recovery data sources

| Asset | Source of truth | Retention |
|---|---|---|
| SQL transactional data | UAE North primary, UAE Central geo-replica, plus PITR. | PITR 35 days, long-term backups 10 years via export-to-storage. |
| Secrets | Azure Key Vault with purge-protection. Soft-delete 90 days. | Key rotation docs under `docs/compliance/key-rotation/`. |
| IaC state | `infra/bicep/` in the git repo; deployment history in the ops subscription. | Git history unbounded. |
| Application code | Git (private) + signed build artefacts. | Git unbounded; artefacts 24 months. |
| CMS content | `CmsContentVersion` table (12-month minimum history per RFP §3.1) + daily blob export. | 12-month rollback + 3-year archival. |
| Audit logs | `AuditLogs` table + Log Analytics workspace. | SQL-side 5 years, Log Analytics 2 years. |

---

## 8. Communications during an outage

1. **Internal** — DLD ops bridge. Update every 30 min with status.
2. **Investors** — public status page `https://status.iretp.dld.ae` (kept outside the failed region, maintained by DLD ops).
3. **DESC** — notify within 2 h of detection for any Critical incident (per §10.2.1). Report within 24 h of resolution.
4. **Post-mortem** — vendor owes a written report within 3 business days (SLA §15.3).

---

## 9. Post-recovery checklist

- [ ] `/healthz/live`, `/healthz/ready`, `/healthz/sla` all green from two independent networks.
- [ ] Latest 50 transactions reconciled against the DLD source system.
- [ ] Hangfire queue processed without failures in the last 60 min.
- [ ] Investor alerts generated in the last 24 h have delivery timestamps.
- [ ] AI accuracy harness re-run; ≥ 90% pass rate.
- [ ] All audit entries for the outage window present.
- [ ] DR event logged in `docs/incidents/` with root cause and corrective action.
