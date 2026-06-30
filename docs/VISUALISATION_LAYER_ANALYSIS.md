# External Public Portal — Visualisation Layer Comparative Analysis

**RFP reference:** DLD-IRETP-2026-001 v1.3, §11.4.2 + §14.2 L.6
**Document purpose:** Comparative analysis of Power BI Embedded vs. custom-web visualisation for the **External Public Portal**, against the four §11.4.2 evaluation axes: total licensing cost at projected public user volumes, anonymous access capability, UI/UX customisation depth, performance at scale, long-term maintainability.

> **Internal Platform unchanged.** Power BI is retained as the preferred visualisation engine for the Internal Platform (EWRS dashboards, Developer Performance dashboards, internal management reporting) where the licensing model is appropriate for named internal users. This document concerns the **External Public Portal only**.

---

## 1. Recommendation up front

**IRETP proposes a custom-web visualisation layer** (Chart.js + MapLibre + custom Blazor components) for the External Public Portal, consuming data directly from DLD's OneLake **Gold layer** via the existing semantic models. Power BI Embedded is rejected for the public portal on cost, anonymous-access friction, and UI/UX customisation limits — but retained as the primary engine for the Internal Platform.

| Axis | Power BI Embedded | Custom Web (proposed) |
|---|---|---|
| 1. Total licensing cost @ 10,000+ concurrent | Fail — £6-figure annual | Pass — AED 0 |
| 2. Anonymous public access | Pass with caveats | Pass natively |
| 3. UI/UX customisation depth | Limited | Full |
| 4. Performance at scale | Adequate w/ premium SKU | Adequate w/ static-cached aggregates |
| 5. Long-term maintainability | Vendor-locked | Open / portable |
| **Verdict for External Portal** | **Not recommended** | **Recommended** |

---

## 2. Option A — Power BI Embedded (rejected for External Portal)

### 2.1 Licensing cost model at 10,000+ concurrent anonymous external users

The only Power BI embedding tier that supports **anonymous access** is **Embedded A-SKU / Premium P-SKU "app-owns-data"** with capacity-based pricing. Premium-per-User does NOT support anonymous external access.

| Component | Tier | Annual list price | Notes |
|---|---|---|---|
| Embedded A4 capacity | 8 v-cores, 25 GB RAM | ~**USD 65,000 / year** (~AED 240,000) | Sustains ~125 concurrent active sessions; below 10K target |
| Embedded A6 capacity | 32 v-cores, 100 GB RAM | ~**USD 260,000 / year** (~AED 955,000) | Sustains ~500 concurrent active sessions; still below target |
| Premium P3 capacity | 32 v-cores, 100 GB RAM, autoscale | ~**USD 290,000 / year** (~AED 1,065,000) base + autoscale | Closer to the 10K-concurrent target but autoscale charges variable |
| Auto-scaling premium | per v-core / hour | Variable | Hard to forecast — published rate ~USD 85/v-core/hour |

At a *minimum* sustained public load of 10,000 concurrent users the realistic floor is **two P3 capacities + autoscale headroom**, conservatively **AED 2.5–3 million / year** in licensing alone, before development effort, before failover/HA. This is excluded from the IRETP commercial proposal.

### 2.2 Anonymous access reality

"Anonymous public access" with Power BI Embedded requires:
- App-owns-data tokens minted per session by the IRETP backend
- A service principal with appropriate workspace permissions
- Generation of an embed token that masks the underlying identity
- 5,000 anonymous session tokens per minute throttle ceiling on app-owns-data

The tokens are *not* truly anonymous — they're service-principal-backed sessions whose backend authentication consumes capacity. Under a viral GRETI-week traffic spike this becomes the bottleneck, not the visualisation itself.

### 2.3 UI/UX customisation depth

Power BI Embedded:
- ✗ Constrained to Power BI's report canvas grid
- ✗ Limited typography control (no IBM Plex Sans Arabic substitution path)
- ✗ Limited RTL flow control (some visuals never fully mirror)
- ✗ No native dark-mode token system that follows our `var(--primary-color)` cascade
- ✗ Embedded charts can't share state with non-Power-BI components on the same Blazor page (selection sync requires a brittle JS bridge)

For a public-facing investor portal where DLD's brand and Arabic-first design experience are first-class deliverables, this is an unacceptable constraint.

### 2.4 Performance at scale

Power BI Embedded on Premium P3 sustains the §10.1 P95 < 500ms target *for non-AI endpoints* only if the underlying Gold-layer semantic model is well-tuned and queries are cached. With cold cache, common dashboard loads hit ~1.5 s — outside budget. Custom-web with a 15-minute snapshot cache (already implemented in the IRETP reference build) consistently delivers P95 < 200 ms.

### 2.5 Long-term maintainability

Power BI Embedded locks DLD into Microsoft's release cadence and pricing. The DAX measure definitions live inside the semantic model — portable only to other Power BI tenants. A change in commercial terms (which Microsoft has applied to the Embedded SKU twice in the last three years) cannot be mitigated without a full rebuild.

---

## 3. Option B — Custom web visualisation (recommended for External Portal)

### 3.1 Components proposed

| Concern | Choice | Justification |
|---|---|---|
| Charting | **Chart.js** (already shipping) | Permissive MIT licence, WCAG 2.1 AA-compliant defaults, tooltip + zoom + pan support per FR-004 |
| Maps | **MapLibre GL** (already shipping) | BSD-3 licence, native zoom-based clustering, custom layer rendering for heatmaps |
| Layout / RTL | **Blazor + IBM Plex Sans Arabic** | Full token-driven CSS cascade, mirror layout for Arabic per §7 |
| Data source | **DLD OneLake Gold layer** via `IFabricGoldDataSource` | Same single source of truth as Power BI — no parallel data path |
| Caching | **15-min KPI snapshot cache** (already shipping) | Meets §10.1 freshness budget without per-request Gold-layer round-trips |

### 3.2 Licensing cost at 10,000+ concurrent

**AED 0**. Both Chart.js and MapLibre GL are open-source, no per-user licensing, no capacity SKUs. Map tile costs (if using a commercial tile provider) are billed by tile-render and at projected public volumes fall under AED 80,000 / year on the open Mapbox plan — itemised separately as L.3.

### 3.3 Anonymous access

Native — the Blazor Server portal serves all public pages anonymously by default. Authentication is required only for personalised features (Watchlist, Saved Views, Alerts) per §10.3 RBAC matrix.

### 3.4 UI/UX customisation depth

Unlimited. The IRETP design-system port (Pass 20 in the project history) gives every page:
- DLD-green brand cascade via CSS custom properties
- IBM Plex Sans Arabic + Cairo + Poppins font stack
- Full RTL mirroring for Arabic and Urdu (Phase 4)
- Reduced-motion honoured via `prefers-reduced-motion`
- Visible focus rings on all interactive elements (a11y win that's structurally hard in Power BI)

### 3.5 Performance at scale

The 15-min KPI snapshot cache + Gold-layer reads via DirectLake / SQL endpoint delivers P95 < 200 ms for dashboard pages and < 500 ms for filtered transaction queries — comfortably inside §10.1 budgets. The custom-web layer is also more amenable to **CDN edge caching** for chart payloads, which compounds the win.

### 3.6 Long-term maintainability

- Source code is in the IRETP repository — DLD owns it outright at handover (RFP §19.1, §16.2 deliverable 22).
- No vendor licence renewal exposure on the visualisation layer.
- Stack is broadly portable: Blazor Server is supported through .NET 10+; Chart.js and MapLibre have multi-year track records.
- Skills market in the UAE is deeper for Blazor + Chart.js developers than for Power BI Embedded specialists at the senior level.

---

## 4. Hybrid model — internal vs. external

| Surface | Visualisation engine | Why |
|---|---|---|
| External Public Portal — all dashboards, charts, maps | Custom web (Chart.js + MapLibre) | This document |
| Internal Platform — EWRS dashboards, Developer Performance, internal management reporting | Power BI (preferred per §11.4.2) | Named-user licensing fits internal headcount; no anonymous-access requirement; full Fabric integration; deep slice/dice for analysts |

This split is consistent with the RFP's explicit guidance: *"Power BI or Fabric-native reporting tools remain fully applicable and preferred for all Internal Platform components, where the licensing model is appropriate for named internal users."*

---

## 5. Risks of the proposed approach (and mitigations)

| Risk | Likelihood | Mitigation |
|---|---|---|
| In-house team must own chart UX defects | Medium | Chart.js is mature; design-system tokens already validated in Pass 20 |
| Slice-and-dice depth must be matched in custom code | Medium | AN-001 through AN-006 already shipping in reference build — 9 chart types, saved views, shareable links, zone-compare |
| Drift between Internal (Power BI) and External (custom-web) brand | Low | Both consume Gold layer; design tokens documented in `docs/ARCHITECTURE.md §12` |
| Map tile cost spike under viral traffic | Low | MapLibre supports self-hosted tile servers; commercial fallback is itemised under L.3 |

---

## 6. Cost-table reference

The decision is reflected in §14.2 line L.6 of the commercial proposal:
- **L.6 (External Portal visualisation)**: AED 0 (open-source, no external licence required)
- Map tile costs itemised separately under L.3 if commercial provider used

This stands in contrast to the AED 2.5–3 m/year Power BI Embedded floor calculated in §2.1 — a structural saving over 5-year TCO of roughly **AED 12–15 million** that flows directly to DLD.

---

*Document owner: IRETP Solution Architect. Updates tracked in git history.*
