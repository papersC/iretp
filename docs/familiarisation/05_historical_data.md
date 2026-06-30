# Historical Data Assessment Report

**RFP §12.1 output #5 — to be submitted to DLD for approval**

Assesses the completeness and consistency of DLD's historical transaction data for the 5-year period that will be published on the External Portal (RFP FR-006).

## Coverage requirement

Minimum 5 full calendar years of historical data, accessible via the Transactions Page, with 99.5% accuracy against DLD source systems (RFP §17.2).

## Assessment dimensions

| Dimension | Question | Method |
|---|---|---|
| Coverage | Is every transaction in the 5-year window represented in OneLake Silver? | Source-system count vs. Silver count per month, per zone, per property type |
| Consistency | Do schema definitions hold across the 5 years? | Snapshot of the schema each year; diff |
| Quality drift | Are completeness rates stable, or do older years have more nulls? | Per-field completeness time-series |
| Coding-scheme drift | Have property-type codes / zone IDs / financing methods changed? | Lookup-table change log |
| Anomalies | Are there months with implausibly low counts (gaps)? | Outlier detection on monthly counts |

## Findings — to be populated during Phase 1

| Year | Total Source Records | Total Silver Records | Coverage % | Completeness % | Notable Issues |
|---|---|---|---|---|---|
| 2021 | _____ | _____ | _____ | _____ | _____ |
| 2022 | _____ | _____ | _____ | _____ | _____ |
| 2023 | _____ | _____ | _____ | _____ | _____ |
| 2024 | _____ | _____ | _____ | _____ | _____ |
| 2025 | _____ | _____ | _____ | _____ | _____ |
| 2026 (YTD) | _____ | _____ | _____ | _____ | _____ |

## Coding-scheme change log

| Date | Coding scheme | Change | Backward-compat handling |
|---|---|---|---|
| _____ | _____ | _____ | _____ |

## Remediation plan (agreed with DLD)

Any historical data that fails the 99.5% accuracy criterion must be remediated *before* public publication. The remediation plan logs each issue, the owner, and the target resolution.

| Issue ID | Description | Owner | Target Date | Blocking Public Publication? |
|---|---|---|---|---|
| _____ | _____ | _____ | _____ | Yes/No |

## Approval

| Role | Name | Date | Signature |
|---|---|---|---|
| Vendor Data Engineer | _____ | _____ | _____ |
| DLD Data Team Lead | _____ | _____ | _____ |
