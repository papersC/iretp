# Microsoft Fabric Environment Assessment

**RFP §12.1 (v1.3) addition — input to the [Architecture Integration Map](../ARCHITECTURE_INTEGRATION_MAP.md)**

Assesses DLD's existing Microsoft Fabric configuration so the IRETP integration is grounded in actual environment state, not assumptions.

## Activities

| # | Activity | Output |
|---|---|---|
| 1 | OneLake Lakehouse schema walkthrough — Silver layer | Schema diagram + table list with row counts + last-write watermarks |
| 2 | OneLake Lakehouse schema walkthrough — Gold layer | Schema diagram + measure inventory + semantic model list |
| 3 | Active Data Factory pipeline inventory | Pipeline list with: trigger schedule, source, sink, transformation summary, average run duration, failure rate |
| 4 | Published semantic model catalogue | List of all Power BI semantic models in the Fabric workspace, with measure inventory |
| 5 | Data exchange / integration mechanism inventory | List of all current connectors (CDC, REST, SFTP, etc.) feeding the lakehouse |
| 6 | Gaps register | Where IRETP requires capability the existing ecosystem doesn't yet have |

## OneLake — Silver layer inventory (to be filled in during discovery)

| Schema | Table | Row Count | Last Write | Owner |
|---|---|---|---|---|
| silver.dld | transactions | _____ | _____ | DLD Real Estate Registration |
| silver.dld | projects | _____ | _____ | DLD Projects |
| silver.dld | escrow_balances | _____ | _____ | RERA Escrow |
| silver.dld | developer_scorecards | _____ | _____ | RERA Compliance |
| silver.rera | ejari_contracts | _____ | _____ | RERA Tenancy |
| silver.rera | regulatory_violations | _____ | _____ | RERA |
| silver.dm | zones_authoritative | _____ | _____ | Dubai Municipality |

## OneLake — Gold layer inventory

Models the IRETP runtime **expects to consume** (cross-referenced against the Architecture Integration Map §7):

| Model | Layer | Measures Needed | Already Exists? | Action |
|---|---|---|---|---|
| `GoldTransactionFacts` | Gold | TransactionCount, TotalValueAed, AvgPricePerSqft | _____ | _____ |
| `GoldRentalYieldSemantic` | Gold | AvgGrossYield, MedianRentAed, YoYChangePct | _____ | _____ |
| `GoldDeveloperScorecard` | Gold | CompositeScore, OnTimeDeliveryPct, EscrowAdequacyPct | _____ | _____ |
| `GoldEwrsAlertFact` | Gold | OpenAlertCount, AvgTimeToAcknowledge, EscalationRate | _____ | _____ |
| `GoldEscrowHealth` | Gold | AdequacyRatio, BalanceAed, MonthOverMonthDelta | _____ | _____ |

For any model marked "doesn't exist": vendor proposes it as an **extension** to DLD's existing model structure (matches existing naming, governance, security boundaries) — never as a parallel definition.

## Data Factory pipelines

| Pipeline | Trigger | Source → Sink | Cadence | Pass / Fail rate (30-day) |
|---|---|---|---|---|
| _____ | _____ | _____ | _____ | _____ |

## Gaps register

For any IRETP capability that cannot be served by the existing ecosystem, log it here with a proposed compliant alternative.

| Gap | Why existing ecosystem cannot serve | Proposed alternative | DLD approval status |
|---|---|---|---|
| _____ | _____ | _____ | _____ |

## Approval

| Role | Name | Date | Signature |
|---|---|---|---|
| Vendor Fabric Lead | _____ | _____ | _____ |
| DLD Fabric Data Engineer | _____ | _____ | _____ |
| DLD Data Liaison Officer | _____ | _____ | _____ |
