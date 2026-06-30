# Detailed Field Mapping Document

**RFP §12.1 output #2 — to be approved by DLD IT**

For each source system → Silver → Gold mapping, document field names, data types, allowable values, null conventions, and any DLD coding scheme (transaction type codes, property category codes, zone identifiers, developer licence number formats).

## Transaction Registry → `GoldTransactionFacts`

| Source Field | Source Type | Silver Field | Gold Measure / Dimension | Allowable Values | Null Convention |
|---|---|---|---|---|---|
| `TXN_ID` | NVARCHAR(36) | `transaction_id` | (key) | UUID v4 | NOT NULL |
| `TXN_DATE` | DATETIME2 | `transaction_date` | DateKey | ISO 8601 | NOT NULL |
| `ZONE_CODE` | INT | `zone_id` | Zone | DLD zone codes 1–245 (validated against DM GIS) | NOT NULL |
| `PROP_TYPE_CODE` | CHAR(2) | `property_type` | PropertyType | AP, VL, TH, PL, CM | NOT NULL — unknown becomes "Other" with EWRS-flagged review |
| `TXN_TYPE_CODE` | CHAR(2) | `transaction_type` | TransactionType | SA, GF, MT, AU, IN | NOT NULL |
| `AREA_SQFT` | DECIMAL(12,2) | `area_sqft` | (measure base) | > 0 | NOT NULL |
| `AREA_SQM` | DECIMAL(12,2) | `area_sqm` | (measure base) | computed from sqft | NOT NULL |
| `TXN_VALUE_AED` | DECIMAL(18,2) | `transaction_value_aed` | TotalValueAed | > 0; AED currency only | NOT NULL |
| `PRICE_PER_SQFT` | DECIMAL(12,2) | `price_per_sqft` | AvgPricePerSqft | computed; outliers > 5σ flagged | NULL if AREA_SQFT = 0 (excluded from aggregates) |
| `FIN_METHOD_CODE` | CHAR(2) | `financing_method` | (dimension) | CA, MT, IS | NULL allowed |
| `IS_OFF_PLAN` | BIT | `is_off_plan` | (dimension) | 0 / 1 | NOT NULL |

## Ejari Rental → `GoldRentalYieldSemantic`

| Source Field | Source Type | Silver Field | Gold Measure / Dimension | Allowable Values | Null Convention |
|---|---|---|---|---|---|
| `EJARI_ID` | NVARCHAR(36) | `contract_id` | (key) | UUID v4 | NOT NULL |
| `ZONE_CODE` | INT | `zone_id` | Zone | matches DLD zone codes | NOT NULL |
| `UNIT_TYPE_CODE` | CHAR(2) | `unit_type` | UnitType | AP, VL, TH, ST, OF, RT | NOT NULL |
| `ANNUAL_RENT_AED` | DECIMAL(18,2) | `annual_rent_aed` | MedianRentAed | > 0 | NOT NULL |
| `CONTRACT_TERM` | NVARCHAR(20) | `contract_term` | (dimension) | LONG, SHORT | NOT NULL |
| `START_DATE` | DATETIME2 | `contract_start` | (filter) | ISO 8601 | NOT NULL |

## Coding scheme references

- **Property type codes:** maintained by DLD Real Estate Registration. Source of truth: `dm.lookup_property_types`.
- **Transaction type codes:** maintained by DLD. Source of truth: `dm.lookup_transaction_types`.
- **Zone identifiers:** issued by Dubai Municipality. Latest snapshot loaded into `dm.zones_authoritative`.
- **Developer licence number format:** RERA-issued numeric prefix + sequence (e.g., `R-12345-DEV`).

## Approval

| Role | Name | Date | Signature |
|---|---|---|---|
| Vendor Data Engineer | _____ | _____ | _____ |
| DLD IT | _____ | _____ | _____ |
