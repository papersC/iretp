# Calculation Rules Validation Document

**RFP §12.1 output #4 — to be approved by DLD Analytics team**

Validates the business logic used to compute every derived metric published on the platform. Any ambiguity must be resolved before development begins.

## Price per sqft

| Layer | Formula | Notes |
|---|---|---|
| Per-transaction | `TXN_VALUE_AED / AREA_SQFT` | Excluded when `AREA_SQFT = 0` or `TXN_VALUE_AED = 0` (data quality flag) |
| Per-zone aggregate (Gold) | Weighted average: `Σ(price * area) / Σ(area)` over all in-zone transactions in the period | Weighted by area to prevent micro-units skewing the average |
| Outlier handling | Per-zone z-score > ±5σ → flagged, included in raw table, excluded from public Gold aggregates pending DLD review | Outlier review log accessible to DLD analytics |

## Gross rental yield (%)

| Layer | Formula | Notes |
|---|---|---|
| Per-unit | `(annual_rent_aed / nearest_sale_price_aed) * 100` | Sale price matched on zone + unit_type + same quarter |
| Per-zone aggregate | Median of per-unit yields | Median (not mean) to mute outliers from luxury or distressed sales |
| Minimum sample | ≥ 5 paired rent+sale samples per zone-quarter | Below threshold = "insufficient data" instead of a misleading value |

## Escrow adequacy ratio

| Formula | `current_escrow_balance / required_minimum_balance` |
|---|---|
| `required_minimum_balance` | `construction_completion_pct × total_project_cost` |
| Status badges | Adequate ≥ 100% / Warning 80–99% / Critical < 80% (matches RFP ESC-001) |

## Developer composite score

| Criterion | Default Weight | Calculation |
|---|---|---|
| On-Time Project Delivery Rate | 25% | `(projects_delivered_on_or_before_declared_date / total_completed_projects) × 100`, weighted by project unit count |
| Unit Sales Completion Rate | 20% | `(total_units_sold / total_units_registered_across_all_projects) × 100` |
| Escrow Account Health Score | 20% | `(current_escrow_balance / required_min_balance) × 100`, averaged across all active projects |
| Regulatory Compliance Record | 15% | Starts at 100. Deductions: minor = −2, major = −10, critical = −25. Rolling 5-year window |
| Financial Soundness Indicator | 10% | Composite of liquidity ratios + debt-to-equity from most recent audited annuals. Normalised 0–100 |
| Historical Project Success Rate | 10% | `(successfully_completed_projects / total_projects_initiated_past_10_years) × 100` |

Composite = `Σ(criterion_score × weight)`. Weights are admin-configurable and **must always sum to 100%** (enforced at the API layer, audit-logged). Reference: `UpdateScoringWeightsCommandHandler`.

## EWRS risk indicator triggers

| Indicator | Default Threshold | Reference |
|---|---|---|
| Project Delivery Delay — Warning | > 6 months late AND < 80% complete | RFP §8.1 |
| Project Delivery Delay — Critical | > 12 months late AND < 90% complete | RFP §8.1 |
| Escrow Shortfall — Warning | balance < 80% of required minimum | RFP §8.1 |
| Escrow Shortfall — Critical | balance < 60% of required minimum | RFP §8.1 |
| Construction Suspension | ≥ 30 days no activity (Warning); ≥ 60 days (High) | RFP §8.1 |
| Transaction Volume Decline | Monthly count drops > 40% vs 12-mo rolling avg | RFP §8.1 |
| Developer Score Deterioration | Composite drops > 15 points in single quarter | RFP §8.1 |
| High-Risk Project Concentration | High-Risk share of portfolio > 30% | RFP §8.1 |
| Price Decline (Zone) | Avg PSF drops > 15% in single quarter | RFP §8.1 |
| Severe Regulatory Violation | Critical-category RERA violation issued | RFP §8.1 |

All thresholds are admin-configurable per RFP §8.1 final paragraph. Reference implementation: `UpdateRiskThresholdCommandHandler` + audit log.

## Approval

| Role | Name | Date | Signature |
|---|---|---|---|
| Vendor Analytics Lead | _____ | _____ | _____ |
| DLD Analytics Team Lead | _____ | _____ | _____ |
