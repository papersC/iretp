# IRETP User Acceptance Test Plan

**RFP reference:** DLD-IRETP-2026-001 v1.3, §17 (Acceptance Criteria) + §17.3 (10 Universal UAT Categories)
**Document purpose:** Provide a phase-by-phase, category-by-category test plan that DLD can execute against the IRETP platform. Each category states the pass criterion from the RFP and a concrete script for verification.
**Audience:** DLD UAT lead, DLD test team, vendor QA lead.

---

## 1. Phase-level acceptance gates (RFP §17.1)

Each phase is accepted independently. The phase is accepted only when **all six** conditions hold:

1. ☐ All Section 13 deliverables completed and live in the production environment
2. ☐ All functional requirements applicable to that phase pass structured UAT with DLD stakeholders + end users
3. ☐ All performance SLAs in §10.1 met under load
4. ☐ Data accuracy criterion met (§17.2 — 500 random transactions × 99.5% accuracy)
5. ☐ Zero open Critical or High-severity defects at sign-off
6. ☐ DLD Project Sponsor signs the phase acceptance form

---

## 2. Data Accuracy Acceptance Criterion (§17.2)

For Phase 1 and any phase introducing new data categories:

| Step | Action | Pass condition |
|---|---|---|
| 1 | DLD Data Team selects a random sample of 500 transaction records from the External Portal | Sample is stratified across property types, zones, and transaction types |
| 2 | For each record, verify every displayed field against the authoritative DLD source database | Field-level accuracy ≥ 99.5% |
| 3 | Pre-Publication Analytics Assessment report signed off by DLD Data Liaison Officer | Sign-off recorded before the phase goes live with public data |

Sampling tool: a SQL/Python script using `Random(seed)` for reproducibility. Disagreements logged in `docs/familiarisation/03_data_quality_baseline.md`.

---

## 3. The 10 Universal UAT Categories (§17.3)

### 3.1 — Functional Testing

| Pass criterion | 100% of functional requirements applicable to the phase implemented and verified. Zero Critical or High-severity open defects at phase sign-off. |
|---|---|

**Test script (Phase 1 example):**

| # | Requirement | Test step | Expected | Status |
|---|---|---|---|---|
| FR-001 | Non-technical CMS edit | Log in as DLD Operator, edit a homepage banner, publish | Content live within 30 s | ☐ |
| FR-002 | Staging → preview → production | Create staging change, request preview link, approve | Rollback to prior version in ≤ 2 clicks | ☐ |
| FR-003 | KPI cards | Trigger transaction in source DB, wait ≤ 15 min | KPI value reflected on homepage | ☐ |
| FR-004 | 4 interactive charts | Apply 6M / 12M / 36M / Custom filter | All 4 charts re-render in ≤ 2 s | ☐ |
| FR-005 | Language + currency switch | Click EN ↔ AR, switch AED → USD | Switch takes ≤ 1 s, no reload | ☐ |
| FR-006 | 5-year transaction registry | Search transactions from 2021 | Table loads ≤ 3 s for ≤ 10k rows | ☐ |
| FR-007 | Multi-dimensional filters + URL persistence | Apply filter, share URL, open in new tab | Filter state restored | ☐ |
| FR-008 | Export 50k rows in Excel / CSV / PDF | Click Export → all 3 formats | Download begins ≤ 10 s | ☐ |
| FR-009 | 3 heatmap layers | Toggle Activity / Avg PSF / Yield | Layer switches ≤ 2 s | ☐ |
| FR-010 | Zone detail panel | Click any zone | Panel renders ≤ 1 s | ☐ |
| FR-011 | Project pins + clustering | Zoom in/out | Pins cluster + uncluster correctly | ☐ |
| FR-012 | Project detail panel | Click a pin | Developer + completion % from DLD official records | ☐ |
| FR-013 | Price Per Sqft Index + 5-zone overlay | Select 5 zones, draw overlay | Chart renders, weekly update visible | ☐ |
| FR-014 | Rental Index + Yield Calculator | Enter price + rent + size | Live gross yield with formula | ☐ |
| AN-001 to AN-006 | Slice & Dice — 4 dims, 3 metrics, 9 chart types, 12 saved views, 5-zone compare, 12-month shareable links | Build a saved view, share link, restore | All states restore with 100% fidelity | ☐ |
| AI-001 to AI-007 | AI Agent | Ask 100 standard questions in EN + AR | ≥ 90% correct, source citations on every fact | ☐ |
| §6 alerts | 6 alert types delivered via correct channels | Trigger each type | Email ≤ 5 min, SMS ≤ 3 min, In-Platform instant | ☐ |
| Open Data API | 5 endpoints + OpenAPI 3.0 docs | Issue API key, hit each endpoint | All return correct data with rate-limit headers | ☐ |

### 3.2 — Performance Testing

| Pass criterion | All P95 response time targets met under concurrent user loads specified in §10.1. |
|---|---|

| Metric | Target | Test |
|---|---|---|
| Homepage load | < 3 s P95, 10 Mbps | k6 load script with 5,000 concurrent users for 10 min |
| API response | < 500 ms P95 | k6 against `/api/dashboard/kpis`, `/api/transactions`, `/api/map/zones` |
| Map filter update | < 2 s | Playwright UI test toggling 3 heatmap layers |
| AI text response | < 8 s P90 | k6 against `/api/ai/query` with the 100-question seed set |
| AI chart generation | < 15 s P90 | same, filtering for chart-generation queries |
| 5,000 concurrent users + 500 internal | No performance degradation | combined k6 scenario |
| Uptime | 99.9% / month | Uptime monitor cross-checked against `/healthz/live` |
| KPI freshness | 15 min | Trigger source-DB row, time-to-KPI displayed |
| Transaction lag | 24 h | `/healthz/sla` ingests Fabric watermark — `SlaHealthCheckTests` |

### 3.3 — AI Agent Accuracy (Phase 1 + 2)

| Pass criterion | Agent tested against a standardised 100-question DLD data test set. Accuracy ≥ 90%. Zero fabricated data. Zero instances of investment advice. |
|---|---|

**Script:**

1. ☐ Vendor `AiAccuracyHarness` runs the 14-question seed catalog via `POST /api/admin/ai-models/accuracy-test`
2. ☐ DLD extends the catalog to 100 questions covering: data lookup (40), service navigation (30), adversarial guardrail (20), Arabic NLP (10)
3. ☐ Re-run the harness; assert ≥ 90 correct
4. ☐ Spot-check 10 responses for fabricated data (none allowed)
5. ☐ Submit 10 prompts attempting to elicit investment advice (forecast, recommendation, target price); all must refuse via guardrail

Evidence: harness JSON output + signed sign-off from DLD AI lead.

### 3.4 — Security Testing (Phase 3)

| Pass criterion | DESC-authorised VAPT report with zero open High or Critical findings. OWASP security scan: zero critical issues. |
|---|---|

**Script:**

1. ☐ Vendor engages DESC-approved assessor (named in proposal). VAPT scheduled Phase 3 week 6
2. ☐ Internal OWASP ZAP scan run on every push to `main` via `.github/workflows/security.yml`
3. ☐ All Critical / High findings remediated within 30 days; Medium within 90–120 days
4. ☐ VAPT report submitted to DLD CISO before phase sign-off

Evidence: VAPT report (DESC-approved), `.github/workflows/security.yml` run logs, `docs/DESC_ISR_V3_COMPLIANCE.md` updated with assessor findings.

### 3.5 — Accessibility Testing

| Pass criterion | Automated accessibility scan: zero Critical violations. WCAG 2.1 Level AA verified across all pages. |
|---|---|

**Script:**

1. ☐ Run axe-core via Playwright on every page (Public + Admin)
2. ☐ Manually verify focus rings visible on all interactive elements (Pass 20 fixed 3 invisible rings)
3. ☐ `prefers-reduced-motion: reduce` honoured (already implemented)
4. ☐ Keyboard-only navigation walk through Transactions / Map / AI Agent
5. ☐ Screen-reader test on at least Dashboard and Transactions in EN + AR

Evidence: axe-core JSON output, signed accessibility audit checklist.

### 3.6 — Language & RTL Testing

| Pass criterion | EN + AR fully correct in Phases 1–3. All 8 languages correct in Phase 4. RTL layout verified for AR and UR. No truncated or overlapping text. |
|---|---|

**Script:**

1. ☐ Visit every public page in AR — verify layout mirroring, icon flip, chart-axis flip, no truncation
2. ☐ Verify Arabic translation of every zone / project / developer name (validated via `/admin/name-validation`)
3. ☐ Phase 4: repeat for Simplified Chinese, Russian, Urdu (RTL), French, Hindi, German
4. ☐ Visit AI Agent in each language — verify graceful English fallback for complex queries

Evidence: screenshots per language per page, name-validation queue sign-off.

### 3.7 — Data Accuracy Verification

See [§2 above](#2-data-accuracy-acceptance-criterion-§172).

### 3.8 — End-User Usability Testing

| Pass criterion | Structured usability session with a representative group of DLD staff and investor users. System Usability Scale (SUS) score ≥ 75. All critical task completion rates ≥ 90%. |
|---|---|

**Script:**

1. ☐ Recruit 12 participants (6 investor-persona, 6 DLD-staff-persona)
2. ☐ Define 10 critical tasks (e.g., "find the average rental yield in Dubai Marina", "approve a Level 2 EWRS alert")
3. ☐ Observed session — measure task completion + time-on-task
4. ☐ Post-session SUS questionnaire
5. ☐ Aggregate score ≥ 75; task completion ≥ 90%

Evidence: SUS results, task-completion log, signed sign-off from usability moderator.

### 3.9 — CMS Non-Technical Edit Test

| Pass criterion | DLD non-technical staff members successfully publish a content change within 10 minutes without vendor assistance. |
|---|---|

**Script:**

1. ☐ Vendor trains 2 DLD non-technical staff members in a single 1-hour session
2. ☐ One week later, present each with an edit task (change a banner, update a KPI label, swap a hero image)
3. ☐ Time from login to live: must be < 10 min, no vendor in the room

Evidence: video recording (with consent) or signed observer log.

### 3.10 — Alert Delivery Testing

| Pass criterion | Test alerts triggered across all applicable alert levels and types. All delivered within specified SLA times to correct recipients. |
|---|---|

**Script:**

1. ☐ Trigger each of the 6 §6.1 alert types
2. ☐ Trigger each of the 4 §8.2 escalation levels (Operational / Managerial / Senior Leadership / Strategic)
3. ☐ Verify channel + recipient per RFP matrix (Email + SMS + In-Platform + briefing PDF)
4. ☐ Measure delivery time — must satisfy §6.2 SLA (Email ≤ 5 min, SMS ≤ 3 min, In-Platform instant)
5. ☐ `NotificationSlaHealthCheck` reports Healthy throughout

Evidence: `NotificationSlaHealthCheck` snapshot, recipient confirmation, latency log.

---

## 4. Sign-off form (per phase)

```
Phase _____ Acceptance Form

I, the DLD Project Sponsor, confirm that:
[ ] All RFP §13 deliverables for this phase are complete and operational
[ ] All UAT categories 3.1–3.10 (applicable to this phase) have passed
[ ] All P95 performance targets are met under §10.1 load
[ ] Data accuracy ≥ 99.5% on the 500-record sample
[ ] Zero Critical or High-severity open defects
[ ] Phase _____ is hereby accepted for go-live.

Signed: _____________________
Date: _______________________
Name: _______________________
Role: DLD Project Sponsor
```

---

## Cross-references

- [COMPLIANCE_MATRIX.md](COMPLIANCE_MATRIX.md) — every requirement → implementation
- [DESC_ISR_V3_COMPLIANCE.md](DESC_ISR_V3_COMPLIANCE.md) — security-control evidence (UAT 3.4)
- [RISK_REGISTER.md](RISK_REGISTER.md) — risks flagged during UAT
- [familiarisation/](familiarisation/README.md) — data-quality baseline + historical assessment

*Updated 2026-05-18.*
