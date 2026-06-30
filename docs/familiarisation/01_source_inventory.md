# DLD Data Source Inventory

**RFP §12.1 output #1 — to be approved by the DLD Data Liaison Officer**

| Source System | Owning Department | Data Format | Update Frequency | API / Extract | IRETP Use |
|---|---|---|---|---|---|
| Transaction Registry | DLD Real Estate Registration | Relational (SQL Server) → OneLake Silver | Real-time CDC | OneLake Silver → Gold | Public transactions registry; price index; map heatmaps |
| Project Database | DLD Projects Department | Relational | Daily batch | Data Factory → OneLake | Project pins, status, completion %, escrow link |
| Ejari Rental System | RERA Tenancy Registration | Relational | Real-time CDC | Data Factory → OneLake | Rental index; yield calculator |
| RERA Regulatory Records | RERA | Hybrid (relational + document) | Daily | Data Factory → OneLake | Developer violations; compliance score |
| Escrow Bank Feeds | Authorised escrow banks (RERA-certified) | SFTP CSV + secure API | Daily | Data Factory → OneLake | Escrow balance; adequacy ratio; EWRS triggers |
| Project Certifications (LEED / Estidama) | DLD Sustainability + 3rd-party certifiers | Document + API | Monthly | Data Factory → OneLake | ESG module (Phase 4) |
| UAE Central Bank FX | UAE CB Open Data | REST API (JSON) | Daily | Direct fetch + cache | Currency switcher (RFP FR-005) |
| Dubai Municipality GIS | DM GeoSpatial Open Data Portal | GeoJSON / tile service | Quarterly | Direct fetch (static) | Zone boundaries on the GIS map |

## Required activities

- [ ] Catalogue all DLD source systems feeding the IRETP platform
- [ ] Document the data format, update frequency, API/extract availability for each
- [ ] Identify the responsible DLD department for each system
- [ ] Verify access credentials and connection method

## Approval

| Role | Name | Date | Signature |
|---|---|---|---|
| Vendor Data Lead | _____ | _____ | _____ |
| DLD Data Liaison Officer | _____ | _____ | _____ |
