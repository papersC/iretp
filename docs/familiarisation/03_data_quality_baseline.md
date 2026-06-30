# Data Quality Baseline Report

**RFP §12.1 output #3 — to be submitted to DLD for approval**

Quality profile per source system, structured by RFP §19.3 quality dimensions: accuracy, completeness, timeliness, consistency.

## Methodology

1. Pull a stratified sample of 1,000 records per source system, balanced across property type, zone, transaction year (last 5 years).
2. For each record, evaluate every field against the §12.1 #2 mapping spec.
3. Aggregate the per-record results into the dimensional baseline below.
4. Flag any anomaly above the threshold for DLD review.

## Baseline targets (per RFP §19.3)

| Dimension | Minimum Standard | Sample Method |
|---|---|---|
| Accuracy | ≥ 99.5% field-level match to source | 1,000 record stratified sample |
| Completeness | 100% of mandatory fields populated for last-24-months transactions | full-table coverage check |
| Timeliness | Transaction lag ≤ 24 h from source; KPI ≤ 15 min | pipeline watermark vs. now() |
| Consistency | Zero discrepancy between map data and transaction table totals | cross-module diff per zone |

## Findings — to be populated during Phase 1 familiarisation

| Source System | Accuracy | Completeness | Timeliness | Consistency | Known Issues |
|---|---|---|---|---|---|
| Transaction Registry | _____ | _____ | _____ | _____ | _____ |
| Project Database | _____ | _____ | _____ | _____ | _____ |
| Ejari | _____ | _____ | _____ | _____ | _____ |
| RERA | _____ | _____ | _____ | _____ | _____ |
| Escrow Bank Feeds | _____ | _____ | _____ | _____ | _____ |

## Remediation plan (agreed with DLD)

Each known issue is logged here with: owner, target resolution date, and impact on public publication.

| Issue ID | Description | Owner | Target Date | Impact on Phase 1 Go-Live |
|---|---|---|---|---|
| _____ | _____ | _____ | _____ | _____ |

## Approval

| Role | Name | Date | Signature |
|---|---|---|---|
| Vendor Data Analyst | _____ | _____ | _____ |
| DLD Data Team Lead | _____ | _____ | _____ |
