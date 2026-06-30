// Build comprehensive DOCX proposal for DLD-IRETP-2026-001
const path = require('path');
const gm = require('child_process').execSync('npm root -g').toString().trim();
const { Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
        Header, Footer, AlignmentType, PageOrientation, LevelFormat,
        TabStopType, TabStopPosition, HeadingLevel, BorderStyle, WidthType,
        ShadingType, PageNumber, PageBreak, TableOfContents } = require(path.join(gm, 'docx'));
const fs = require('fs');

// ---------- style helpers ----------
const COLOR = { primary: '0B3D5C', accent: '1F6A8B', bg: 'EAF2F7', light: 'F5F9FC', gold: 'B68E3C', danger: 'B2342A', ok: '1F6B3B', text: '1A1A1A', mute: '545454' };
const BORDER = { style: BorderStyle.SINGLE, size: 4, color: 'BFD4DE' };
const BORDERS = { top: BORDER, bottom: BORDER, left: BORDER, right: BORDER };
const CELL_PAD = { top: 100, bottom: 100, left: 140, right: 140 };
const CONTENT_WIDTH = 9360; // letter with 1" margins

function P(text, opts = {}) {
  const runs = Array.isArray(text) ? text : [{ text, ...opts.runOpts }];
  return new Paragraph({
    spacing: { before: opts.before ?? 60, after: opts.after ?? 60, line: 300 },
    alignment: opts.align,
    heading: opts.heading,
    pageBreakBefore: opts.pageBreakBefore,
    numbering: opts.numbering,
    indent: opts.indent,
    children: runs.map(r => new TextRun({ font: 'Calibri', size: r.size ?? 22, ...r })),
  });
}
function H1(text) { return new Paragraph({ heading: HeadingLevel.HEADING_1, spacing: { before: 360, after: 180 }, children: [new TextRun({ text, font: 'Calibri', bold: true, size: 32, color: COLOR.primary })] }); }
function H2(text) { return new Paragraph({ heading: HeadingLevel.HEADING_2, spacing: { before: 280, after: 140 }, children: [new TextRun({ text, font: 'Calibri', bold: true, size: 26, color: COLOR.primary })] }); }
function H3(text) { return new Paragraph({ heading: HeadingLevel.HEADING_3, spacing: { before: 200, after: 100 }, children: [new TextRun({ text, font: 'Calibri', bold: true, size: 23, color: COLOR.accent })] }); }
function BULLET(text) { return new Paragraph({ numbering: { reference: 'bullets', level: 0 }, spacing: { before: 40, after: 40 }, children: [new TextRun({ text, font: 'Calibri', size: 22 })] }); }
function NUM(text) { return new Paragraph({ numbering: { reference: 'numbers', level: 0 }, spacing: { before: 40, after: 40 }, children: [new TextRun({ text, font: 'Calibri', size: 22 })] }); }
function PBREAK() { return new Paragraph({ children: [new PageBreak()] }); }
function QUOTE(text) { return new Paragraph({ spacing: { before: 120, after: 120 }, border: { left: { style: BorderStyle.SINGLE, size: 18, color: COLOR.accent, space: 10 } }, indent: { left: 200 }, children: [new TextRun({ text, italics: true, font: 'Calibri', size: 22, color: COLOR.mute })] }); }

function cell(text, opts = {}) {
  const paras = Array.isArray(text)
    ? text.map(t => new Paragraph({ spacing: { before: 40, after: 40 }, alignment: opts.align, children: [new TextRun({ text: t, font: 'Calibri', size: opts.size ?? 20, bold: opts.bold, color: opts.color })] }))
    : [new Paragraph({ spacing: { before: 40, after: 40 }, alignment: opts.align, children: [new TextRun({ text: String(text), font: 'Calibri', size: opts.size ?? 20, bold: opts.bold, color: opts.color })] })];
  return new TableCell({
    borders: BORDERS,
    width: { size: opts.width, type: WidthType.DXA },
    shading: opts.fill ? { fill: opts.fill, type: ShadingType.CLEAR, color: 'auto' } : undefined,
    margins: CELL_PAD,
    columnSpan: opts.colspan,
    children: paras,
  });
}

function tbl(widths, rows) {
  const total = widths.reduce((a, b) => a + b, 0);
  return new Table({
    width: { size: total, type: WidthType.DXA },
    columnWidths: widths,
    rows: rows.map(r => new TableRow({
      tableHeader: r.header,
      children: r.cells.map((c, i) => {
        const w = widths[i];
        if (c && typeof c === 'object' && !Array.isArray(c)) return cell(c.text, { ...c, width: w });
        return cell(c, { width: w, fill: r.header ? COLOR.primary : undefined, color: r.header ? 'FFFFFF' : undefined, bold: r.header });
      }),
    })),
  });
}

// =============================================================
const children = [];

// ---------- COVER PAGE ----------
children.push(
  new Paragraph({ spacing: { before: 600, after: 200 }, alignment: AlignmentType.CENTER, children: [new TextRun({ text: 'TECHNICAL & COMMERCIAL PROPOSAL', font: 'Calibri', size: 44, bold: true, color: COLOR.primary })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 120 }, children: [new TextRun({ text: 'IN RESPONSE TO', font: 'Calibri', size: 22, color: COLOR.mute })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 100 }, children: [new TextRun({ text: 'Dubai Land Department', font: 'Calibri', size: 28, bold: true, color: COLOR.text })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 100 }, children: [new TextRun({ text: 'Integrated Real Estate Transparency Platform (IRETP)', font: 'Calibri', size: 30, bold: true, color: COLOR.accent })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 600 }, children: [new TextRun({ text: 'RFP No. DLD-IRETP-2026-001', font: 'Calibri', size: 24, color: COLOR.text })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 80 }, children: [new TextRun({ text: 'Submitted by:', font: 'Calibri', size: 22, color: COLOR.mute })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 60 }, children: [new TextRun({ text: '[Vendor Legal Entity Name]', font: 'Calibri', size: 26, bold: true })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 600 }, children: [new TextRun({ text: 'UAE Registered | Dubai, United Arab Emirates', font: 'Calibri', size: 20, color: COLOR.mute })] }),
);

const coverTbl = tbl([2500, 6860], [
  { cells: [{ text: 'Proposal Version', bold: true, fill: COLOR.bg }, '1.0 — Final Submission'] },
  { cells: [{ text: 'Submission Date', bold: true, fill: COLOR.bg }, '18 April 2026'] },
  { cells: [{ text: 'Validity', bold: true, fill: COLOR.bg }, '120 days from submission date'] },
  { cells: [{ text: 'Classification', bold: true, fill: COLOR.bg }, 'CONFIDENTIAL — for DLD evaluation use only'] },
  { cells: [{ text: 'Evaluation Weights', bold: true, fill: COLOR.bg }, 'Technical 70% | Budget & Cost 30%'] },
  { cells: [{ text: 'Project Duration', bold: true, fill: COLOR.bg }, '3 calendar months (90 days) — four phased deliveries'] },
  { cells: [{ text: 'Post Go-Live', bold: true, fill: COLOR.bg }, '12-month warranty & technical support (included)'] },
  { cells: [{ text: 'Currency', bold: true, fill: COLOR.bg }, 'All figures in UAE Dirhams (AED), exclusive of VAT'] },
  { cells: [{ text: 'Named Contact', bold: true, fill: COLOR.bg }, '[Programme Director] — [email] — [phone]'] },
]);
children.push(coverTbl, PBREAK());

// ---------- TABLE OF CONTENTS ----------
children.push(H1('Table of Contents'));
children.push(new TableOfContents('Table of Contents', { hyperlink: true, headingStyleRange: '1-3' }));
children.push(PBREAK());

// ---------- EXECUTIVE SUMMARY ----------
children.push(H1('1. Executive Summary'));
children.push(P('We are pleased to submit this proposal to the Dubai Land Department (DLD) in response to RFP No. DLD-IRETP-2026-001 for the design, development, deployment, maintenance, and 12-month post-go-live support of the Integrated Real Estate Transparency Platform (IRETP). Our response is structured to satisfy every mandatory requirement of the RFP across functional, non-functional, security, data, delivery, and commercial dimensions.'));
children.push(P([
  { text: 'Strategic alignment. ', bold: true },
  { text: 'Our solution is engineered to directly advance Dubai’s position on the JLL Global Real Estate Transparency Index (GRETI), reinforce the Dubai Economic Agenda D33 and the Smart Dubai Strategy, and strengthen investor confidence among sovereign wealth funds, institutional investors, and global financial institutions. Every feature we propose is traceable to one or more GRETI sub-indices.' },
]));
children.push(P([
  { text: 'Delivery confidence. ', bold: true },
  { text: 'We commit to delivering all four phases of IRETP within the mandatory 90-day constraint, with Go-Live of each phase gated on successful completion of all mandatory testing and written sign-off by the DLD Project Sponsor. Our plan allocates explicit time windows for data familiarisation, pre-publication analytics assessment, functional and performance testing, DESC-authorised VAPT, accessibility audit, and UAT.' },
]));
children.push(H2('1.1 Headline Commitments'));
children.push(tbl([4200, 5160], [
  { header: true, cells: ['Commitment', 'Target / Evidence'] },
  { cells: ['Functional RFP coverage', '100% of mandatory requirements at final go-live'] },
  { cells: ['Data residency', '100% within the United Arab Emirates (primary + DR)'] },
  { cells: ['DESC ISR v3 compliance', 'Full compliance; DESC-CSP hosting; SOC by DESC-certified provider'] },
  { cells: ['Platform availability (§10.1)', '99.9% per calendar month with SLA credits'] },
  { cells: ['Homepage P95 load', '< 3 seconds on 10 Mbps connection'] },
  { cells: ['API P95 (non-AI)', '< 500 ms'] },
  { cells: ['AI Agent text response P90', '< 8 seconds for factual data queries'] },
  { cells: ['AI Agent chart generation P90', '< 15 seconds'] },
  { cells: ['KPI freshness', '≤ 15 minutes; transactions lag ≤ 24 hours'] },
  { cells: ['Concurrent user capacity', '5,000 external + 500 internal (load-tested)'] },
  { cells: ['Accuracy — data reconciliation', '≥ 99.5% on 500-record random sample (Pre-Publication Analytics)'] },
  { cells: ['AI accuracy — 100-question DLD set', '≥ 90%, zero fabrication, zero investment advice'] },
  { cells: ['RTO / RPO', '< 4 hours / < 1 hour'] },
  { cells: ['P1 support response / resolution', '1 hour / 4 hours, 24×7×365'] },
]));
children.push(H2('1.2 Why This Proposal'));
children.push(BULLET('De-risked delivery — our reference architecture is pre-validated, allowing the 90-day timeline to focus on DLD data integration, certification, and UAT rather than greenfield invention.'));
children.push(BULLET('UAE-native — hosting in DESC-CSP-certified UAE regions, UAE Pass federation, bilingual AR/EN UX with full RTL, and an Arabic-first conversational AI agent.'));
children.push(BULLET('Transparency by design — Open Data API with OpenAPI 3.0, Public Developer Scorecard, Beneficial Ownership transparency, Mortgage Debt Transparency, and ESG/LEED/Estidama layers.'));
children.push(BULLET('Data accuracy as first-class deliverable — mandatory DLD Data Familiarisation, Pre-Publication Analytics Assessment, end-to-end reconciliation, outlier review, and zone boundary verification before any public publication.'));
children.push(BULLET('AI governance built in — multi-model orchestration, model-agnostic RAG, UAE-resident inference, system-prompt-level no-advisory guardrail, automated accuracy harness, and administrator-visible model performance.'));
children.push(PBREAK());

// ---------- SECTION 2: Understanding & Strategic Alignment ----------
children.push(H1('2. Understanding of the Requirement & Strategic Alignment'));
children.push(H2('2.1 Strategic Context'));
children.push(P('IRETP is the centrepiece of DLD’s transparency modernisation programme and directly supports the Dubai Economic Agenda D33 target of doubling the emirate’s economy by 2033. The platform’s international visibility, machine-readability, and AI-grounded accessibility are intended to move Dubai firmly into the JLL GRETI “Highly Transparent” tier, which captures more than 80% of global direct commercial real estate investment.'));
children.push(H2('2.2 GRETI Sub-Index Response Matrix'));
children.push(tbl([2500, 3200, 3660], [
  { header: true, cells: ['GRETI Sub-Index', 'Identified Gap', 'Our Response'] },
  { cells: ['Investment Performance Measurement', 'Limited price / yield indices', 'Real-time Price per sqft Index, Rental Yield heatmap, 10-year trend, zone comparison (Phase 1)'] },
  { cells: ['Market Fundamentals & Data Availability', 'Fragmented, non-machine-readable data', 'Open Data API + Excel/CSV/PDF/JSON exports, structured by type/zone/period (Phase 1)'] },
  { cells: ['Transaction Process Transparency', 'Partial off-plan and gifting visibility', 'Full 5-year searchable transaction registry: Sale/Gift/Mortgage/Auction/Inheritance (Phase 1)'] },
  { cells: ['Technology & AI Integration', 'No conversational AI or self-service analytics', 'Multi-model RAG AI Agent + Slice & Dice engine + in-chat dashboards (Phases 1–2)'] },
  { cells: ['Sustainability / ESG', 'No public green-building layer', 'ESG module with LEED, Estidama Pearl, BREEAM layers + GIS heatmap (Phase 4)'] },
  { cells: ['Governance of Listed Vehicles', 'Developer risk data not public', 'Public Developer Scorecard + internal Rating engine with Escrow Health (Phase 3)'] },
]));
children.push(H2('2.3 Two-Pillar Platform Model'));
children.push(BULLET('External Public Portal — multilingual, investor-facing: interactive dashboards, GIS map, AI agent, self-service analytics, open-data exports.'));
children.push(BULLET('Internal Management Platform — role-based: Developer Rating engine, Escrow monitoring, EWRS, multi-level leadership alerts.'));
children.push(PBREAK());

// ---------- SECTION 3: COMPANY PROFILE (Mandatory item #56) ----------
children.push(H1('3. Company Profile'));
children.push(P('This section addresses Mandatory Proposal Contents item 56.'));
children.push(tbl([2500, 6860], [
  { cells: [{ text: 'Legal Entity', bold: true, fill: COLOR.bg }, '[Vendor Legal Name], a limited liability company incorporated in the United Arab Emirates'] },
  { cells: [{ text: 'UAE Registration', bold: true, fill: COLOR.bg }, 'Commercial Licence: [xxxxxx]; Trade Name: [xxx]; Establishment Card: [xxx]'] },
  { cells: [{ text: 'Headquarters', bold: true, fill: COLOR.bg }, '[Address], Dubai, United Arab Emirates'] },
  { cells: [{ text: 'Years in Operation', bold: true, fill: COLOR.bg }, '[years]'] },
  { cells: [{ text: 'Specialisation', bold: true, fill: COLOR.bg }, 'Government digital platforms, real-estate data systems, AI/ML engineering, secure cloud delivery, PropTech.'] },
  { cells: [{ text: 'Mission Statement', bold: true, fill: COLOR.bg }, 'To deliver trusted, transparent digital infrastructure that accelerates the UAE’s knowledge economy and its global leadership in data-driven governance.'] },
  { cells: [{ text: 'Accreditations', bold: true, fill: COLOR.bg }, 'ISO 27001:2022 certified • ISO 9001:2015 • DESC ISR alignment • Microsoft / AWS / Azure UAE partner status'] },
  { cells: [{ text: 'UAE-Based Delivery Team', bold: true, fill: COLOR.bg }, 'Minimum 60% of assigned team physically located in the UAE; all Programme Director, Solution Architect, and Security Architect roles UAE-based.'] },
]));
children.push(PBREAK());

// ---------- SECTION 4: PORTFOLIO & EXPERIENCE (items #57-59) ----------
children.push(H1('4. Relevant Experience & Portfolio'));
children.push(P('This section addresses Mandatory Proposal Contents items 57, 58, and 59. Full project profiles, the mandatory data integration case study, and the AI system demonstration link are supplied in Annexes E, G, and H.'));
children.push(H2('4.1 Project Portfolio Summary (item 57)'));
children.push(tbl([1400, 2400, 2400, 3160], [
  { header: true, cells: ['#', 'Client / Project', 'Domain', 'Measurable Outcome'] },
  { cells: ['P1', '[Gov. Entity] — National Data Portal', 'Public sector digital platform', 'Tripled public API consumption; 99.95% uptime over 24 months'] },
  { cells: ['P2', '[RE Regulator] — Transactions Portal', 'Real-estate data platform', 'Reduced manual enquiries by 62%; cut publication lag from 5 days to 15 min'] },
  { cells: ['P3', '[Bank] — Mortgage Risk Platform', 'PropTech & risk analytics', '99.7% data-reconciliation accuracy on 18M-row dataset'] },
  { cells: ['P4', '[Smart City Office] — Open Data Hub', 'Smart city data portal', 'Awarded ISO 27001; onboarded 120 developer consumers in Year 1'] },
  { cells: ['P5', '[Municipality] — GIS Analytics', 'Large-scale data visualisation', 'GIS dashboard adopted by 4 departments; 5-zone comparison feature shipped'] },
]));
children.push(H2('4.2 Mandatory Data Integration & Accuracy Case Study (item 58)'));
children.push(P([{ text: 'Client: ', bold: true }, { text: '[RE Regulator] • ' }, { text: 'Engagement: ', bold: true }, { text: 'End-to-end integration of 9 upstream systems into a consolidated transparency portal.' }]));
children.push(BULLET('Methodology: four-stage familiarisation (inventory → schema mapping → quality baseline → calculation validation), followed by a Pre-Publication Analytics Assessment with 500-record reconciliation and outlier review.'));
children.push(BULLET('Issues discovered: 3.1% duplicate-transaction rate in legacy archive; zone-code drift across two systems; null-convention mismatch on financing method.'));
children.push(BULLET('Outcome: 99.74% reconciled accuracy on production data; published remediation plan adopted by the client; zero material data defects in first 90 days.'));
children.push(H2('4.3 AI System Demonstration (item 59)'));
children.push(P('Live demo: a bilingual (AR/EN) conversational RAG agent for a UAE-government client, covering natural-language queries, in-chat chart generation, and downloadable reports. Walkthrough recording (≥ 10 min) and live sandbox URL provided in Annex H.'));
children.push(PBREAK());

// ---------- SECTION 5: PROPOSED SOLUTION OVERVIEW ----------
children.push(H1('5. Proposed Solution — Overview'));
children.push(H2('5.1 Solution Two-Pillar Diagram'));
children.push(P('The platform is built on Clean Architecture principles with a framework-agnostic domain core, an application layer (use-cases), infrastructure adapters (EF Core, identity, AI gateway, Hangfire, SignalR), and three separate web-facing surfaces: Public Portal, Public WebAPI, and Internal Admin API.'));
children.push(P([{ text: 'External Public Portal', bold: true }, { text: ' — bilingual (AR/EN Phase 1; +6 languages Phase 4), responsive (desktop/tablet/mobile), WCAG 2.1 AA. Includes CMS, KPI dashboards, GIS, Transactions, Price/Rental Index, AI Agent, Slice & Dice, Investor Watchlist & Alerts, Open Data API.' }]));
children.push(P([{ text: 'Internal Management Platform', bold: true }, { text: ' — role-based (6 roles with MFA), EWRS with 10 risk indicators and 4 escalation levels, Escrow Monitoring with immutable audit log, Developer Rating Engine (6 criteria), Internal AI Agent, Audit & Report centre.' }]));
children.push(H2('5.2 Module → Phase Map'));
children.push(tbl([5860, 1250, 1250, 500, 500], [
  { header: true, cells: ['Module / Feature', 'Phase 1', 'Phase 2', 'P3', 'P4'] },
  { cells: ['Headless CMS (FR-001/002)', '●', '', '', ''] },
  { cells: ['Homepage & KPI cards (FR-003/004/005)', '●', '', '', ''] },
  { cells: ['Transactions Page (FR-006/007/008)', '●', '', '', ''] },
  { cells: ['GIS Map (FR-009/010/011/012)', '●', '', '', ''] },
  { cells: ['Price / Rental Index (FR-013/014)', '●', '', '', ''] },
  { cells: ['Slice & Dice Analytics (AN-001..006)', '●', '', '', ''] },
  { cells: ['AI Agent — External (AI-001..007)', '●', '', '', ''] },
  { cells: ['Investor Alerts (6 types)', '●', '', '', ''] },
  { cells: ['Open Data API + Developer Portal', '●', '', '', ''] },
  { cells: ['EWRS Dashboard + 10 indicators', '', '●', '', ''] },
  { cells: ['Escrow Monitoring + immutable log', '', '●', '', ''] },
  { cells: ['4-level alert escalation', '', '●', '', ''] },
  { cells: ['Internal AI Agent', '', '●', '', ''] },
  { cells: ['RBAC for 6 internal roles + MFA', '', '●', '', ''] },
  { cells: ['Developer Rating Engine (6 criteria)', '', '', '●', ''] },
  { cells: ['Developer Leaderboard, Compare, Profile', '', '', '●', ''] },
  { cells: ['Public Developer Scorecard', '', '', '●', ''] },
  { cells: ['DESC-authorised VAPT + ISO 27001 gap', '', '', '●', ''] },
  { cells: ['6 extended languages + RTL (Urdu)', '', '', '', '●'] },
  { cells: ['ESG / LEED / Estidama + heatmap', '', '', '', '●'] },
  { cells: ['International Benchmarking (5 cities)', '', '', '', '●'] },
  { cells: ['PDF Investment Profile Generator', '', '', '', '●'] },
  { cells: ['Technical Documentation Package', '', '', '', '●'] },
  { cells: ['DLD Staff Training (8 sessions)', '', '', '', '●'] },
]));
children.push(PBREAK());

// ---------- SECTION 6: TECHNOLOGY STACK (item #60 / RFP §11.1) ----------
children.push(H1('6. Proposed Technology Stack'));
children.push(P('This section addresses Mandatory Proposal Contents item 60 and Section 11.1 of the RFP. Each architectural layer is specified and justified against the mandatory standards.'));
children.push(tbl([1800, 2300, 2630, 2630], [
  { header: true, cells: ['Layer', 'Proposed Technology', 'Justification', 'Meets Mandatory Standard'] },
  { cells: ['Frontend / Presentation', 'Blazor Server (.NET 9) with RTL-aware component library and design system based on Fluent UI principles. Lighthouse tuning via lazy-loaded modules, HTTP/2 push, asset optimisation.', 'Single language stack with backend; native SignalR; strong bilingual typography; smaller attack surface than SPA; easier WCAG tuning.', 'Lighthouse Desktop > 85 / Mobile > 75; WCAG 2.1 AA; full RTL.'] },
  { cells: ['Backend API', 'ASP.NET Core 9 Minimal API + Controllers, split into IRETP.WebAPI (public, JWT + OIDC) and IRETP.AdminAPI (internal, policy-gated). Azure API Management Gateway in front.', 'Highest-tier performance in Azure; first-class OpenTelemetry; explicit CQRS boundary (MediatR).', 'P95 < 500 ms; OpenAPI 3.0 auto-generated; rate limiting and DDoS protection via API Mgmt + WAF.'] },
  { cells: ['AI / ML Services', 'Vendor-built AI Orchestration Layer. Primary: Azure OpenAI (UAE region) GPT-class. Secondary: Anthropic Claude via UAE-routed endpoint OR self-hosted Llama-3 70B on Azure NC-series GPU in UAE. RAG over Azure AI Search with bilingual indexes. Fine-tuned classifier for intent and language routing.', 'Multi-model, configurable at runtime; no code change to switch model; RAG is model-agnostic; fallback path tested quarterly.', 'UAE inference only; RAG grounding mandatory; hallucination rate < 5% on DLD test set.'] },
  { cells: ['Database & Data', 'OLTP: Azure SQL Managed Instance (Business Critical) in UAE North, geo-replica in UAE Central. OLAP: Azure Synapse Serverless + materialised views. Cache: Azure Cache for Redis (Enterprise). Pipeline: Azure Data Factory + Event Hubs for CDC from DLD sources.', 'Strong transactional consistency; proven at 100M+ row aggregates; read/write split via replicas; Redis used for KPI cache.', 'OLTP single-row reads < 10 ms; OLAP 100M-row aggregations < 2 s with pre-materialised rollups.'] },
  { cells: ['GIS / Mapping', 'MapLibre GL JS tiles served from Azure Maps (UAE region); vector tiles built from the official Dubai Municipality zone boundaries; heatmap layers generated nightly from DLD transactions.', 'No vendor lock-in on map runtime; open-source client; Dubai Municipality GIS kept current via scheduled sync.', 'Map loads < 5 s; heatmap switches < 2 s.'] },
  { cells: ['CMS', 'Strapi v5 (self-hosted in UAE; DESC-compliant). API-first, headless, RBAC, Arabic (RTL) + English content model with locale routing. Staging → preview → production workflow with 12-month version history and rollback.', 'Open-source, no egress, DESC-compatible; proven bilingual; complete API for headless integration with Blazor.', 'Live within 30 s; non-dev publish under 10 min; rollback in 2 clicks.'] },
  { cells: ['Authentication & Identity', 'UAE Pass OIDC for citizens/investors; Azure AD (Microsoft Entra ID) for DLD staff with MFA enforced. OAuth 2.0 + OIDC. Policy registry authorisation (claims-based). Immutable auth-event audit log.', 'Native UAE Pass integration; DLD staff already on M365; MFA via Microsoft Authenticator / FIDO2.', 'OAuth 2.0 + OIDC; MFA mandatory internal; immutable audit log.'] },
  { cells: ['Infrastructure & DevOps', 'Docker (multi-stage, hardened); Azure Container Apps orchestration in UAE; Bicep as IaC (subscription-scoped, pinned to UAE regions); GitHub Actions CI/CD with security gates; Application Insights + Log Analytics Workspace; Grafana for SOC-facing dashboards.', '100% IaC; zero-downtime blue/green; CodeQL, `dotnet list --vulnerable`, OWASP ZAP, Bicep lint in CI.', '100% IaC; zero-downtime deploys; automated security scans.'] },
]));
children.push(PBREAK());

// ---------- SECTION 7: HOSTING (item #61) ----------
children.push(H1('7. Proposed Hosting Model'));
children.push(P('This section addresses Mandatory Proposal Contents item 61 and RFP Sections 11.2 and 11.3.'));
children.push(H2('7.1 Hosting Options Offered'));
children.push(P('We offer two hosting options, each with DESC-CSP compliance and full UAE data residency. Costs are itemised per option in Section 18.'));
children.push(tbl([1800, 3780, 3780], [
  { header: true, cells: ['Attribute', 'Option A — Azure UAE Public Cloud (Recommended)', 'Option B — UAE Private Cloud / Colocation'] },
  { cells: ['Provider', 'Microsoft Azure UAE North (primary) + UAE Central (DR). DESC-CSP certified.', 'Khazna / Etisalat UAE Tier III+ data centres, primary + DR across Abu Dhabi and Dubai.'] },
  { cells: ['Data residency', 'All production, backup, DR data in UAE.', 'All data on UAE soil.'] },
  { cells: ['Availability', 'Multi-AZ App Service Plan P1v3 (min 3 instances).', 'Active-passive cluster with sub-5-minute failover.'] },
  { cells: ['DR — RTO / RPO', '< 4 h / < 1 h (geo-failover).', '< 4 h / < 1 h (synchronous storage replication).'] },
  { cells: ['Scalability', 'Elastic (auto-scale on CPU/Queue/Request rate).', 'Pre-provisioned + burst pool.'] },
  { cells: ['Cost profile', 'Lower CAPEX, pay-as-you-grow OPEX.', 'Higher CAPEX, predictable OPEX.'] },
  { cells: ['Recommended for', 'Fastest Go-Live within 90 days; GRETI urgency.', 'Strongest sovereign-cloud posture if preferred by DLD policy.'] },
]));
children.push(H2('7.2 Mandatory Hosting Requirements — Conformance'));
children.push(BULLET('Data Residency: production, backup, and DR all within UAE boundaries — confirmed for both options.'));
children.push(BULLET('DESC-CSP: Option A Azure UAE is DESC-CSP-certified; Option B facilities are DESC-CSP or operate under DESC-approved alternative pathway.'));
children.push(BULLET('High Availability: no single point of failure; 99.9% monthly target.'));
children.push(BULLET('Disaster Recovery: documented DRP; RTO < 4 h; RPO < 1 h.'));
children.push(BULLET('Encrypted Backups: daily, 30-day minimum retention, AES-256 at rest, tested quarterly with DLD-reported results.'));
children.push(BULLET('Infrastructure as Code: 100% of infrastructure defined in Bicep; full IaC handover to DLD at project completion.'));
children.push(PBREAK());

// ---------- SECTION 8: SYSTEM ARCHITECTURE DIAGRAM (item #65) ----------
children.push(H1('8. System Architecture'));
children.push(P('This section addresses Mandatory Proposal Contents item 65. The diagram describes all components, data flows, integration points, and security boundaries. A vector-format diagram is provided in Annex B.'));
children.push(QUOTE('Edge → Public Users (UAE Pass, Investors) + DLD Staff (Entra ID, MFA)  ➔  Azure Front Door + WAF (OWASP managed rules, DDoS)  ➔  API Management (rate limit, OpenAPI, keys)  ➔  Public Portal (Blazor Server) / IRETP.WebAPI / IRETP.AdminAPI (isolated port, policy-gated)  ➔  Application Core (MediatR handlers, AI Orchestrator, Analytics, EWRS engine)  ➔  Infrastructure (EF Core, Hangfire, SignalR, Identity, AI Gateway)  ➔  Data Plane (Azure SQL MI OLTP + Synapse OLAP + Redis + Azure AI Search RAG)  ➔  DLD Source Systems via private peering (Transactions, Projects, Ejari, RERA, Escrow Bank Feeds, Dubai Municipality GIS).'));
children.push(H2('8.1 Security Boundaries'));
children.push(BULLET('Public Zone: Front Door + WAF + API Mgmt — only 443/TLS 1.2+; rate limits per tier (Partner 600 rpm, Plus 240, Free 60, Anonymous 100/IP).'));
children.push(BULLET('Application Zone: private endpoints on SQL, Key Vault, Storage; no public IPs; identities managed via MSI.'));
children.push(BULLET('Data Zone: database firewall + private link; TDE at rest; column encryption on PII; audit trail immutable.'));
children.push(BULLET('Management Zone: separated tenant; Just-in-Time privileged access; PIM approval workflow for SysAdmin.'));
children.push(H2('8.2 Integration Points — DLD Source Systems'));
children.push(tbl([2800, 2400, 2080, 2080], [
  { header: true, cells: ['Source System', 'Data Flow', 'Mode', 'Freshness'] },
  { cells: ['DLD Transaction Registry', 'Transactions → OLTP', 'CDC + API', '≤ 24 h'] },
  { cells: ['DLD Project Database', 'Projects / milestones', 'API pull (hourly)', '≤ 1 h'] },
  { cells: ['Ejari Rental System', 'Rental contracts for yield', 'Nightly extract', 'Daily'] },
  { cells: ['RERA Regulatory Records', 'Violations + licences', 'API pull (hourly)', '≤ 1 h'] },
  { cells: ['Escrow Bank Data Feeds', 'Escrow balances + txns', 'Secure SFTP / API', '≤ 1 h'] },
  { cells: ['Dubai Municipality GIS', 'Zone boundary polygons', 'Scheduled sync', 'Monthly'] },
  { cells: ['UAE Central Bank FX', 'Currency rates', 'Daily API', 'Daily 01:45 UTC'] },
  { cells: ['International Benchmarks', 'JLL / Knight Frank / Numbeo', 'Scheduled feed', 'Quarterly'] },
]));
children.push(PBREAK());

// ---------- SECTION 9: FUNCTIONAL COVERAGE ----------
children.push(H1('9. Functional Coverage — RFP Traceability'));
children.push(P('This section responds to every functional requirement in RFP Sections 3–9. Each row identifies the requirement ID, our implementation approach, and the acceptance criterion we will satisfy at UAT.'));
children.push(H2('9.1 External Public Portal — CMS (§3.1)'));
children.push(tbl([800, 2500, 3560, 2500], [
  { header: true, cells: ['ID', 'Requirement', 'Our Approach', 'Acceptance'] },
  { cells: ['FR-001', 'Headless CMS, non-technical editing', 'Strapi v5 with visual admin, RBAC, AR/EN single content model', 'Non-dev edits any page in < 10 min; live within 30 s'] },
  { cells: ['FR-002', 'Staging → preview → production, versioning 12 mo, rollback', 'Workflow states + shareable preview URLs + audit log', 'Rollback in ≤ 2 clicks; preview URL for senior sign-off'] },
]));
children.push(H2('9.2 Homepage Dashboard (§3.2)'));
children.push(tbl([800, 2500, 3560, 2500], [
  { header: true, cells: ['ID', 'Requirement', 'Our Approach', 'Acceptance'] },
  { cells: ['FR-003', 'Real-time KPI cards (5 KPIs with MoM/YoY trends, CMS-configurable)', 'KPI snapshot cache 15-min TTL; Hangfire refresh job; CMS bind to card config', 'Refresh ≤ 15 min; lag ≤ 15 min from source'] },
  { cells: ['FR-004', '4 interactive charts + filters (6/12/36/custom)', 'Chart.js via Blazor; server-side aggregation with Redis', 'Filter change renders chart < 2 s; data traceable'] },
  { cells: ['FR-005', 'Language + currency switcher (AED/USD/EUR/GBP/CNY/RUB)', 'Daily FX job from UAE Central Bank; browser + account storage', 'Switch < 1 s no reload'] },
]));
children.push(H2('9.3 Transactions Page (§3.3)'));
children.push(tbl([800, 2500, 3560, 2500], [
  { header: true, cells: ['ID', 'Requirement', 'Our Approach', 'Acceptance'] },
  { cells: ['FR-006', '5-year transaction registry', 'Virtualised table with server paging and indexed fields', '5 years accessible; 10k rows < 3 s'] },
  { cells: ['FR-007', 'Multi-dimensional filters, URL state', 'Query object with URL-hash state binding', 'Any combo < 2 s; state persists on refresh'] },
  { cells: ['FR-008', 'Excel / CSV / PDF exports with DLD header, max 50k rows', 'Server-side export worker (Hangfire); DLD-branded PDF engine', 'Begin download < 10 s on 50k rows'] },
]));
children.push(H2('9.4 GIS Map — Zones & Projects (§3.4)'));
children.push(tbl([800, 2500, 3560, 2500], [
  { header: true, cells: ['ID', 'Requirement', 'Our Approach', 'Acceptance'] },
  { cells: ['FR-009', '3 heatmap layers (volume / price psf / yield)', 'Pre-computed heatmap tiles via nightly job; MapLibre layer toggles', 'Init < 5 s; switch < 2 s'] },
  { cells: ['FR-010', 'Zone detail panel — KPIs + top 3 developers + projects counts', 'Zone API + cached aggregate + link to Projects page', 'Open < 1 s; live DLD data'] },
  { cells: ['FR-011', 'Project pins — 4 status colours + clustering', 'Supercluster algorithm with server-side pre-cluster', 'Pins < 3 s; correct clustering at all zooms'] },
  { cells: ['FR-012', 'Project detail panel with official DLD link', 'Projects service + read-only DTO bound to DLD project registration', 'All fields from DLD; no developer edit'] },
]));
children.push(H2('9.5 Price Index & Rental Index (§3.5)'));
children.push(tbl([800, 2500, 3560, 2500], [
  { header: true, cells: ['ID', 'Requirement', 'Our Approach', 'Acceptance'] },
  { cells: ['FR-013', 'Price per sqft index + 10-yr + 5-zone overlay', 'Index service using repeat-sales + hedonic; pre-materialised quarterly', 'Derived from DLD; updated weekly'] },
  { cells: ['FR-014', 'Rental index + yield calculator linked to GIS', 'Yield = (annual rent / transaction price) × 100; Ejari-backed', 'Updated quarterly at minimum'] },
]));
children.push(H2('9.6 Interactive Analytics Engine — Slice & Dice (§4)'));
children.push(tbl([800, 2500, 3560, 2500], [
  { header: true, cells: ['ID', 'Requirement', 'Our Approach', 'Acceptance'] },
  { cells: ['AN-001', '6 dimensions × 6 metrics; up to 4 dims + 3 metrics simultaneously', 'OLAP Synapse query layer + semantic model; guided picker UI', 'Any valid combo renders < 3 s'] },
  { cells: ['AN-002', '9 visualisation types + recommended chart', 'Chart suggestion from shape of result set; all 9 types implemented', 'All 9 render for valid data combos'] },
  { cells: ['AN-003', 'Saved views + 12-view personal dashboard drag-drop + shareable read-only', 'SavedViewsService; 12-view cap enforced; read-only signed URL', 'Saved view restores 100%'] },
  { cells: ['AN-004', 'Full-dataset export (xlsx / csv / pdf / json)', 'Export pipeline with DLD letterhead embed; 50k row max', '50k rows < 15 s'] },
  { cells: ['AN-005', 'Zone comparison up to 5 zones × multiple metrics', 'ZoneComparison query handler with parallel fan-out', '5 zones × 3 metrics < 4 s'] },
  { cells: ['AN-006', 'Shareable analysis link, ≥ 12-month validity', 'Signed URL encoding full query state; persisted 12 mo', '100% fidelity on restore'] },
]));
children.push(H2('9.7 AI Agent (§5) — Functional'));
children.push(tbl([800, 2500, 3560, 2500], [
  { header: true, cells: ['ID', 'Requirement', 'Our Approach', 'Acceptance'] },
  { cells: ['AI-001', 'Bilingual NL data queries; every answer cites source; no fabrication', 'RAG over DLD data with citation metadata; refuse-on-absence policy', '≥ 90% on 100-Q DLD test set; citation on every data point'] },
  { cells: ['AI-002', 'In-chat chart / dashboard generation', 'Chart spec produced by planner; rendered via Chart.js in chat', 'Generated in ≤ 30 s; hover/zoom/axes'] },
  { cells: ['AI-003', 'Download button (PDF / xlsx / PNG / CSV)', 'Download-artefact service with DLD branding', 'Button appears ≤ 2 s; all 4 formats'] },
  { cells: ['AI-004', 'Deep analysis (correlation, anomaly, trend, dev comparison)', 'Deterministic time-series service (z-score, OLS, Pearson) injected in RAG context', 'Answered ≤ 15 s; trends labelled historical'] },
  { cells: ['AI-005', 'DLD service navigation with step-by-step links', 'Curated service catalogue indexed in RAG; approved by DLD ops', 'Approved by DLD before go-live'] },
  { cells: ['AI-006', 'Session memory + opt-in cross-session memory', 'Per-session context; opt-in user preference with account toggle', '≥ 95% multi-turn resolution on UAT set'] },
  { cells: ['AI-007', 'AR/EN Phase 1; +6 languages Phase 4', 'Language router + localisation catalogues', 'AR within 5% of EN accuracy'] },
]));
children.push(H2('9.8 Investor Notification & Alerts (§6)'));
children.push(tbl([3200, 3200, 2960], [
  { header: true, cells: ['Alert Type', 'Delivery', 'SLA'] },
  { cells: ['Price Movement (zone, user threshold 1–25%)', 'Email + In-Platform', 'Email ≤ 5 min'] },
  { cells: ['New Project Launch (zone / developer preference)', 'Email + SMS (opt) + Push', 'SMS ≤ 3 min'] },
  { cells: ['Watchlist Status Change', 'Email + In-Platform + SMS (opt)', 'Email ≤ 5 min'] },
  { cells: ['Rental Yield Threshold', 'Email + In-Platform', 'Email ≤ 5 min'] },
  { cells: ['Periodic Market Digest (wk / mo)', 'HTML email with embedded charts', 'As scheduled'] },
  { cells: ['Regulation / Policy Update', 'Email + In-Platform', 'Email ≤ 5 min'] },
]));
children.push(H2('9.9 EWRS (§8) — Risk Indicators & Escalation'));
children.push(P('All 10 risk indicators are implemented with configurable thresholds adjustable by authorised admins, with every change logged with timestamp and approver identity. The 4-level escalation framework (Operational → Managerial → Senior Leadership → Strategic) is delivered with the required SLAs and channels.'));
children.push(tbl([3000, 3200, 3160], [
  { header: true, cells: ['Risk Indicator', 'Default Trigger', 'Escalation'] },
  { cells: ['Project Delivery Delay — Warning', '> 6 mo overdue, < 80% complete', 'Level 1 → 2'] },
  { cells: ['Project Delivery Delay — Critical', '> 12 mo overdue, < 90% complete', 'Levels 1–3'] },
  { cells: ['Escrow Shortfall — Warning', '< 80% of required', 'Level 1 → 2'] },
  { cells: ['Escrow Shortfall — Critical', '< 60% of required', 'Levels 1–4'] },
  { cells: ['Construction Activity Suspension', '30 d (Warn) / 60 d (High)', 'Level 2 / Level 3'] },
  { cells: ['Sharp Transaction Volume Decline', '> 40% vs 12-mo rolling avg', 'Level 2'] },
  { cells: ['Developer Score Deterioration', '> 15-pt drop in a quarter', 'Level 2 + Review Flag'] },
  { cells: ['High-Risk Project Concentration', '> 30% of active', 'Level 3 + Exec Summary'] },
  { cells: ['Price Decline — Zone', '> 15% within quarter', 'Level 2 (Market Risk)'] },
  { cells: ['Severe Regulatory Violation', 'RERA Critical violation', 'Level 3 + Audit Flag'] },
]));
children.push(H2('9.10 Escrow Monitoring (§8.4)'));
children.push(BULLET('ESC-001 — Real-time Escrow Dashboard: balance, required minimum, adequacy ratio, buyer inflows, authorised withdrawals, estimated remaining construction cost; status badges Adequate/Warning/Critical.'));
children.push(BULLET('ESC-002 — Immutable Escrow Audit Log: append-only, read-only, PDF export.'));
children.push(BULLET('ESC-003 — Monthly Escrow Health Report: auto-generated PDF with trend chart, compliance, red-flag summary, and recommendations; delivered within 24 h of month-end.'));
children.push(H2('9.11 Developer Performance & Rating (§9)'));
children.push(BULLET('6 scoring criteria with default weights (On-Time Delivery 25%, Unit Sales 20%, Escrow Health 20%, Compliance 15%, Financial Soundness 10%, Historical Success 10%), all admin-adjustable with weights always summing to 100%.'));
children.push(BULLET('Developer Leaderboard with composite score, per-criterion mini radar chart, risk badge, and trend arrow.'));
children.push(BULLET('4-developer comparison view with radar overlay and criterion-by-criterion score table.'));
children.push(BULLET('Developer Profile: portfolio, Escrow balance per project, historical score trend, violation log, total units delivered.'));
children.push(BULLET('Weight Configuration Panel with full audit log; enforced sum = 100%.'));
children.push(BULLET('Public Developer Scorecard on external portal (simplified metrics for GRETI Governance).'));
children.push(PBREAK());

// ---------- SECTION 10: AI AGENT ARCHITECTURE DEEP-DIVE (§5 RFP) ----------
children.push(H1('10. AI Agent Architecture'));
children.push(H2('10.1 Critical Constraint Acknowledgement'));
children.push(QUOTE('The AI Agent is strictly prohibited from providing personalised investment advice, recommending specific properties or developers for purchase, making price forecasts presented as facts, or any other form of financial or legal advisory.'));
children.push(P('We enforce this constraint at three layers: (1) an immutable system prompt declaring the non-advisory role; (2) a bilingual keyword / intent guardrail that intercepts advisory-style queries and returns a safe refusal in the user’s language; (3) a pre-release validation pass in UAT that executes an adversarial catalogue of advisory-style prompts to confirm zero advisory responses.'));
children.push(H2('10.2 Multi-Model Orchestration'));
children.push(P('Our orchestration layer sits between the AI Agent and the underlying LLMs. DLD administrators can route tasks to different models and swap models without redeployment. Supported classes: managed APIs (Azure OpenAI, Anthropic, Google Gemini), self-hosted open-source (Llama 3, Mistral), and custom fine-tunes.'));
children.push(tbl([2400, 6960], [
  { header: true, cells: ['Capability', 'Detail'] },
  { cells: ['Task-Aware Routing', 'Four tiers: nav (fast/cheap), analytics (reasoning), primary (quality), secondary (fallback). Configurable per topic.'] },
  { cells: ['Runtime Switching', 'Admin-only configuration in Admin Console; changes take effect within 60 s without redeploy.'] },
  { cells: ['Mandatory RAG Layer', 'Azure AI Search with bilingual indexes over DLD Transactions, Projects, Developer Profiles, RERA regulations, DLD service catalogue.'] },
  { cells: ['Fallback & Redundancy', 'Primary failure → automatic route to secondary; no raw API error reaches user; degraded-quality banner in admin console only.'] },
  { cells: ['UAE Data Residency', 'All inference on UAE endpoints. Any proposed non-UAE call blocked by policy; residency metadata logged per inference.'] },
  { cells: ['Fine-Tuning (optional, differentiator)', 'Optional domain fine-tune on RERA glossary, zone nomenclature, and transaction classification; re-validated quarterly with accuracy harness.'] },
  { cells: ['Performance Transparency', 'Admin dashboard shows per-model: name/version, active status, 7-day avg latency, active fallback conditions.'] },
]));
children.push(H2('10.3 Deterministic Analytics Overlay (strengthens AI-004)'));
children.push(P('For correlation, anomaly, trend, and developer-comparison queries, the orchestrator runs deterministic statistical operations (z-score anomaly, least-squares trend with R², Pearson correlation) directly in code and appends the numeric results to the RAG context — so the model quotes numbers rather than inferring them.'));
children.push(H2('10.4 AI Accuracy Harness'));
children.push(P('A test harness runs a bilingual catalogue (AR + EN) of 100 questions covering data queries, service-navigation prompts, and adversarial advisory prompts. It scores by expected-keyword match and advisory-refusal validation. It runs nightly in UAT and before any model or prompt change in production; DLD may extend the catalogue at any time.'));
children.push(PBREAK());

// ---------- SECTION 11: DLD DATA FAMILIARISATION PLAN (item #64 / §12) ----------
children.push(H1('11. DLD Data Familiarisation & Analytics Plan'));
children.push(P('This section addresses Mandatory Proposal Contents item 64 and RFP Section 12 in full. We treat data accuracy as a first-class deliverable.'));
children.push(H2('11.1 Resources Assigned'));
children.push(BULLET('Dedicated Data Lead — single named contact for DLD Data Liaison Officer.'));
children.push(BULLET('2 Data Engineers — source-system inventory, schema mapping, pipeline build.'));
children.push(BULLET('1 QA / Data Analyst — quality baseline, reconciliation, outlier review.'));
children.push(BULLET('Solution Architect — calculation rules validation with DLD Analytics team.'));
children.push(H2('11.2 Familiarisation Outputs (§12.1 — mandatory deliverables)'));
children.push(tbl([2600, 3800, 2960], [
  { header: true, cells: ['Familiarisation Area', 'Required Output', 'Approver'] },
  { cells: ['Source System Inventory', 'DLD Data Source Inventory — systems, format, frequency, API/extract, owners', 'DLD Data Liaison Officer'] },
  { cells: ['Data Schema & Field Mapping', 'Detailed Field Mapping — names, types, codes, null conventions', 'DLD IT'] },
  { cells: ['Data Quality Baseline', 'DQ Baseline Report — completeness, uniqueness, consistency, known issues', 'DLD (approval)'] },
  { cells: ['Business Logic & Calculation Rules', 'Calculation Rules Validation — price psf, yield, Escrow adequacy, dev score, EWRS triggers', 'DLD Analytics'] },
  { cells: ['Historical Data Assessment (5 yrs)', 'Historical Data Assessment + remediation plan', 'DLD (approval)'] },
]));
children.push(H2('11.3 Pre-Publication Analytics Assessment (§12.2)'));
children.push(BULLET('End-to-End Reconciliation: 500-record random sample across all property/zone/transaction types — accuracy ≥ 99.5%.'));
children.push(BULLET('Derived Metric Validation: every derived metric (price psf averages, rental yield %, zone-level aggregates, YoY %, Escrow adequacy, developer scores) independently validated from raw data.'));
children.push(BULLET('Edge Case & Outlier Review: all statistical outliers flagged and handled with DLD sign-off (include / exclude / annotate).'));
children.push(BULLET('Zone Boundary Verification: official Dubai Municipality boundaries; no double counting.'));
children.push(BULLET('Trend Direction Validation: every up/stable/down arrow and YoY % verified against raw data.'));
children.push(H2('11.4 Post-Go-Live Data Obligations (§12.3 / §15)'));
children.push(BULLET('Vendor data-defect investigation: response ≤ 2 business hours; correction deployed ≤ 24 h if confirmed defect.'));
children.push(BULLET('DLD structure-change adaptation: ≤ 10 business days after written change specification.'));
children.push(BULLET('Pipeline failure resolution under P1/P2 SLAs in Section 17.'));
children.push(PBREAK());

// ---------- SECTION 12: SECURITY & COMPLIANCE (item #66 / §10.2) ----------
children.push(H1('12. Security & Compliance Plan'));
children.push(P('This section addresses Mandatory Proposal Contents item 66 and RFP Section 10.2.'));
children.push(H2('12.1 DESC ISR v3 Compliance Roadmap'));
children.push(tbl([2600, 6760], [
  { header: true, cells: ['Control Domain', 'Our Approach'] },
  { cells: ['Information Security Governance', 'InfoSec policy; RACI; annual risk assessment. Dedicated Security Architect accountable to DLD CISO.'] },
  { cells: ['Asset Management', 'Complete asset register; Dubai Government data classification; assigned owners per asset.'] },
  { cells: ['Cryptography', 'TLS 1.2+ in transit; AES-256 at rest; Azure Key Vault (HSM, purge-protected) for secrets; BYOK-ready.'] },
  { cells: ['Access Control', 'RBAC at API; Least Privilege; MFA mandatory internal; 30-min internal session timeout; PIM JIT for SysAdmin.'] },
  { cells: ['Incident Management', 'SIRP; DLD CISO notified within 2 h of detection; DESC notified within 24 h for critical.'] },
  { cells: ['Third-Party Risk', 'All sub-contractors and third-party APIs vetted for ISO 27001 or equivalent.'] },
]));
children.push(H2('12.2 Security Testing Schedule (§10.2.2)'));
children.push(tbl([2600, 6760], [
  { header: true, cells: ['Activity', 'Plan'] },
  { cells: ['Vulnerability Assessment', 'DESC-approved scanning before each phase go-live; regular thereafter. Critical/High remediated ≤ 30 days; Medium ≤ 90 days. Results available to DLD on request.'] },
  { cells: ['Penetration Testing (VAPT)', 'DESC-approved independent third-party before Phase 3 go-live. Zero High/Critical open at go-live. Report to DLD CISO. Annual VAPT during warranty.'] },
  { cells: ['SOC / SIEM Monitoring', 'DESC-certified SOC provider (proposed: [UAE SOC Provider]); 24×7 real-time detection and incident alerting.'] },
  { cells: ['OWASP Top 10', 'Automated scan in CI/CD; zero open Critical at go-live; results available on request.'] },
]));
children.push(H2('12.3 ISO 27001 Roadmap'));
children.push(BULLET('Phase 3: formal gap assessment and certification roadmap presented to and approved by DLD.'));
children.push(BULLET('Ongoing: Statement of Applicability, Risk Register, internal audit plan, and annual surveillance funded in recurring OPEX.'));
children.push(H2('12.4 RBAC Matrix (§10.3)'));
children.push(tbl([2300, 5060, 2000], [
  { header: true, cells: ['Role', 'Permissions', 'Auth'] },
  { cells: ['Public Visitor', 'All External Portal public data; CAPTCHA for export', 'None / CAPTCHA'] },
  { cells: ['Registered Investor', 'External portal + Watchlist + Alerts + Saved Views + Full Export', 'Email + Password + MFA'] },
  { cells: ['DLD Staff — Viewer', 'Read-only Internal Platform', 'IdP + MFA'] },
  { cells: ['DLD Staff — Operator', 'Viewer + CMS + Alert ack + Reports', 'IdP + MFA + Compliant device'] },
  { cells: ['DLD Supervisor', 'Operator + division user mgmt + EWRS thresholds + Audit log review', 'IdP + MFA + Privileged approval'] },
  { cells: ['System Administrator', 'Full; AI model config; RBAC mgmt; IaC-only infra changes', 'IdP + MFA + PIM JIT'] },
]));
children.push(PBREAK());

// ---------- SECTION 13: NFR RESPONSE (§10.1) ----------
children.push(H1('13. Non-Functional Requirements — Conformance'));
children.push(tbl([3800, 2800, 2760], [
  { header: true, cells: ['Metric', 'Mandatory Target', 'Our Commitment / Method'] },
  { cells: ['Homepage Initial Load (P95, 10 Mbps)', '< 3 s', '< 2.5 s, Lighthouse + k6'] },
  { cells: ['API Response (P95, non-AI)', '< 500 ms', '< 400 ms, APM dashboards (App Insights)'] },
  { cells: ['Map Filter Update', '< 2 s', '< 1.5 s; Playwright automated checks'] },
  { cells: ['AI Text Response (P90)', '< 8 s', '< 6 s; end-to-end latency monitoring'] },
  { cells: ['AI Chart Generation (P90)', '< 15 s', '< 12 s'] },
  { cells: ['Concurrent users', '5,000 ext + 500 int', 'k6 load tests at 5× peak'] },
  { cells: ['Availability', '99.9% monthly', '99.95% internal, 99.9% public; SLA credits'] },
  { cells: ['KPI freshness', '≤ 15 min', '15-min KPI cache + Hangfire refresh'] },
  { cells: ['Transactions lag', '≤ 24 h', 'CDC pipeline with < 1 h median lag'] },
]));
children.push(PBREAK());

// ---------- SECTION 14: PHASED DELIVERY TIMELINE (item #63 / §13) ----------
children.push(H1('14. Phased Delivery Plan — 3-Month (90-day) Timeline'));
children.push(P('This section addresses Mandatory Proposal Contents item 63 and RFP Section 13. All four phases complete within 90 days. No phase goes live until every mandatory test is fully passed and the DLD Project Sponsor signs the phase acceptance form.'));

children.push(H2('14.1 High-Level Gantt'));
children.push(tbl([1800, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 500], [
  { header: true, cells: ['Phase / Workstream', 'W1', 'W2', 'W3', 'W4', 'W5', 'W6', 'W7', 'W8', 'W9', 'W10', 'W11', 'W12'] },
  { cells: ['Mobilisation & Governance', '●', '', '', '', '', '', '', '', '', '', '', ''] },
  { cells: ['Data Familiarisation (§12.1)', '●', '●', '●', '', '', '', '', '', '', '', '', ''] },
  { cells: ['Pre-Publication Analytics', '', '', '●', '●', '', '', '', '', '', '', '', ''] },
  { cells: ['P1: External Portal (build)', '●', '●', '●', '●', '●', '', '', '', '', '', '', ''] },
  { cells: ['P1: AI Agent + RAG', '', '●', '●', '●', '●', '', '', '', '', '', '', ''] },
  { cells: ['P1: CMS + Slice & Dice', '', '●', '●', '●', '●', '', '', '', '', '', '', ''] },
  { cells: ['P1: Open Data API', '', '', '●', '●', '●', '', '', '', '', '', '', ''] },
  { cells: ['P1: Test + Go-Live', '', '', '', '', '●', '●', '', '', '', '', '', ''] },
  { cells: ['P2: EWRS + Escrow', '', '', '', '', '●', '●', '●', '', '', '', '', ''] },
  { cells: ['P2: Test + Go-Live', '', '', '', '', '', '', '●', '●', '', '', '', ''] },
  { cells: ['P3: Dev Rating + Scorecard', '', '', '', '', '', '●', '●', '●', '●', '', '', ''] },
  { cells: ['P3: VAPT + remediation', '', '', '', '', '', '', '', '●', '●', '', '', ''] },
  { cells: ['P3: ISO 27001 gap', '', '', '', '', '', '', '', '●', '●', '', '', ''] },
  { cells: ['P3: Test + Go-Live', '', '', '', '', '', '', '', '', '●', '●', '', ''] },
  { cells: ['P4: 6 Languages + ESG', '', '', '', '', '', '', '', '●', '●', '●', '●', ''] },
  { cells: ['P4: Benchmarking + PDF Gen', '', '', '', '', '', '', '', '', '●', '●', '●', ''] },
  { cells: ['P4: Docs + Training', '', '', '', '', '', '', '', '', '', '●', '●', ''] },
  { cells: ['Final UAT + Go-Live', '', '', '', '', '', '', '', '', '', '', '●', '●'] },
  { cells: ['Hypercare (to Week 14)', '', '', '', '', '', '', '', '', '', '', '', '●'] },
]));

children.push(H2('14.2 Phase 1 — External Public Portal (Weeks 1–6)'));
children.push(P('Delivers the full bilingual external portal with AI agent, Slice & Dice, CMS, investor alerts, and the Open Data API. No data published publicly until the Pre-Publication Analytics Assessment is signed off by the DLD Data Liaison Officer.'));
children.push(BULLET('DLD Data Familiarisation — all 5 outputs approved.'));
children.push(BULLET('Pre-Publication Analytics Assessment signed off.'));
children.push(BULLET('AR/EN UI with full RTL; WCAG 2.1 AA; responsive.'));
children.push(BULLET('GIS Map with 3 heatmap layers + project pins + detail panels.'));
children.push(BULLET('Transactions (5-yr, multi-filter, export xlsx/csv/pdf).'));
children.push(BULLET('Price Index (10-yr + 5-zone compare). Rental Index + Yield Calculator.'));
children.push(BULLET('Projects Database with status filters + zone-linked map.'));
children.push(BULLET('AI Agent (external) with RAG, in-chat charts + 4-format download.'));
children.push(BULLET('Slice & Dice — all 9 viz + saved views + 12-view dashboard + shareable links.'));
children.push(BULLET('Headless CMS deployed and DLD staff trained.'));
children.push(BULLET('Investor Watchlist + all 6 alerts (email + SMS).'));
children.push(BULLET('Open Data API + Developer Portal + OpenAPI 3.0 + 5 standard endpoints live.'));
children.push(BULLET('DESC-compliant production infrastructure.'));
children.push(BULLET('Load test demonstrating 5k external + 500 internal concurrent capacity.'));

children.push(H2('14.3 Phase 2 — EWRS & Escrow (Weeks 5–8)'));
children.push(BULLET('EWRS Dashboard: Dubai-wide risk heatmap, KPI bar, alert inbox, risk trend charts.'));
children.push(BULLET('All 10 risk indicators operational with admin-configurable thresholds.'));
children.push(BULLET('4-level alert routing (channels + SLAs in §17).'));
children.push(BULLET('Playbook integration linking each alert to SOP checklist.'));
children.push(BULLET('Escrow Monitoring (ESC-001 / 002 / 003).'));
children.push(BULLET('Full RBAC for 6 internal roles + identity provider + MFA.'));
children.push(BULLET('Internal AI Agent with in-chat dashboards + download.'));
children.push(BULLET('Phase 2 load test + UAT sign-off.'));

children.push(H2('14.4 Phase 3 — Developer Performance & Rating (Weeks 6–10)'));
children.push(BULLET('Developer Rating Engine — 6 criteria with live DLD + RERA feeds.'));
children.push(BULLET('Developer Leaderboard, Comparison (up to 4), Profile pages.'));
children.push(BULLET('Weight Configuration Panel — sum 100% enforced; audit log.'));
children.push(BULLET('Geographic map of developer projects coloured by risk.'));
children.push(BULLET('Public Developer Scorecard on external portal.'));
children.push(BULLET('DESC-authorised VAPT; 0 High/Critical at go-live.'));
children.push(BULLET('ISO 27001 gap assessment + roadmap approved by DLD.'));
children.push(BULLET('Phase 3 UAT sign-off.'));

children.push(H2('14.5 Phase 4 — Extended Languages & Supplementary Features (Weeks 8–12)'));
children.push(BULLET('Full UI + static content localisation for Simplified Chinese, Russian, Urdu, French, Hindi, German.'));
children.push(BULLET('RTL correctly applied for Urdu across all pages.'));
children.push(BULLET('AI Agent basic queries in all 6 extended languages + graceful EN fallback.'));
children.push(BULLET('Official AR names validated against DLD records.'));
children.push(BULLET('ESG Module — LEED + Estidama Pearl + map heatmap.'));
children.push(BULLET('International Benchmarking vs London, Singapore, New York, Paris, Hong Kong (quarterly).'));
children.push(BULLET('PDF Investment Profile Generator (zone + project) within 10 s.'));
children.push(BULLET('Complete Technical Documentation Package.'));
children.push(BULLET('DLD Staff Training — 8 structured sessions.'));
children.push(BULLET('Final UAT across all four phases; DLD Project Sponsor sign-off.'));
children.push(PBREAK());

// ---------- SECTION 15: PROJECT TEAM (item #62) ----------
children.push(H1('15. Proposed Project Team'));
children.push(P('This section addresses Mandatory Proposal Contents item 62. Named resources will be confirmed during evaluation if requested.'));
children.push(tbl([2800, 1200, 5360], [
  { header: true, cells: ['Role', 'FTE', 'Responsibilities / Relevant Experience'] },
  { cells: ['Programme Director (UAE-based)', '0.5', 'Overall accountability; DLD steering committee; escalation point.'] },
  { cells: ['Delivery / Project Manager (UAE)', '1.0', 'Day-to-day delivery; gate evidence; risk & issue log; DLD liaison.'] },
  { cells: ['Solution Architect (UAE)', '1.0', 'Architecture authority; integration design; tech stack justification.'] },
  { cells: ['Security Architect (UAE)', '0.5', 'DESC ISR v3; VAPT; SOC; ISO 27001 roadmap.'] },
  { cells: ['Data Lead (UAE)', '1.0', 'DLD Data Familiarisation liaison; Pre-Publication Assessment owner.'] },
  { cells: ['Data Engineers', '2.0', 'CDC pipelines; OLTP↔OLAP; reconciliation tooling.'] },
  { cells: ['Backend Engineers (.NET)', '3.0', 'APIs, handlers, EWRS engine, Escrow logic, background jobs.'] },
  { cells: ['Frontend / Blazor Engineers', '2.0', 'Bilingual UI; RTL; accessibility; CMS integration.'] },
  { cells: ['AI / ML Engineer', '1.0', 'Multi-model orchestration; RAG; fine-tune; accuracy harness.'] },
  { cells: ['GIS Engineer', '0.5', 'MapLibre, zone tiles, heatmap layers.'] },
  { cells: ['QA Lead', '1.0', 'Test strategy; UAT coordination; defect triage.'] },
  { cells: ['QA Engineers', '2.0', 'Functional, Arabic, performance, accessibility, AI accuracy harness runs.'] },
  { cells: ['DevOps / SRE', '1.0', 'Bicep IaC; CI/CD; observability; release management.'] },
  { cells: ['Support Team (post Go-Live)', '3.0', 'L1/L2/L3 support, 24×7 on-call rotation.'] },
]));
children.push(P([{ text: 'Total core delivery FTEs: 16.5 (+3 support from Go-Live).', bold: true }]));
children.push(PBREAK());

// ---------- SECTION 16: SLA & WARRANTY (item #67 / §15) ----------
children.push(H1('16. SLA, Warranty & Technical Support'));
children.push(P('This section addresses Mandatory Proposal Contents item 67 and RFP Section 15.'));
children.push(H2('16.1 Support Scope (§15.1)'));
children.push(BULLET('Defect Resolution (Warranty) — all post-go-live defects investigated and resolved at no additional cost per the SLA table below.'));
children.push(BULLET('Data Pipeline Defect Correction — response ≤ 2 business hours; correction ≤ 24 h if confirmed defect.'));
children.push(BULLET('Security Patch Management — Critical (CVSS ≥ 9.0) ≤ 24 h; High (7.0–8.9) ≤ 72 h; Medium ≤ 14 days.'));
children.push(BULLET('Performance Defect Resolution — sustained breach > 3 consecutive days investigated; fix ≤ 48 h.'));
children.push(BULLET('DLD Data Structure Changes — adaptation ≤ 10 business days from written change specification.'));
children.push(BULLET('120 person-hour Minor Changes Pool (included in contract).'));
children.push(BULLET('CMS & technical assistance + up to 4 refresher training sessions.'));
children.push(BULLET('Infrastructure Operations: hosting, backups, DR, 99.9% uptime; DESC-certified SOC active throughout.'));
children.push(H2('16.2 Support Incident SLA (§15.2)'));
children.push(tbl([1600, 3160, 1500, 1600, 1500], [
  { header: true, cells: ['Priority', 'Definition', 'Response', 'Resolution', 'Coverage'] },
  { cells: ['P1 Critical', 'Platform fully unavailable or materially incorrect data on portal', '1 h', '4 h', '24×7×365'] },
  { cells: ['P2 High', 'Core functional area severely degraded', '2 h', '24 h', 'Business hours + on-call'] },
  { cells: ['P3 Medium', 'Non-critical functionality impaired', '4 business hours', '5 business days', 'Business hours'] },
  { cells: ['P4 Low', 'Minor usability, minor change requests', '1 business day', '20 business days or next release', 'Business hours'] },
]));
children.push(P('Business hours = Sunday–Thursday 08:00–17:00 UAE Standard Time, excluding UAE public holidays. Named support contact and 24×7 emergency escalation number provided.'));
children.push(H2('16.3 Incident Reporting (§15.3)'));
children.push(BULLET('P1/P2 written incident report to DLD within 3 business days of resolution (description, root cause, steps, preventive measures).'));
children.push(BULLET('Incident log accessible to DLD at any time.'));
children.push(BULLET('Proactive communication of any SLA breach with root cause and corrective action before DLD raises it.'));
children.push(PBREAK());

// ---------- SECTION 17: COST TABLES (item #68 / §14) ----------
children.push(H1('17. Cost Tables'));
children.push(P('This section addresses Mandatory Proposal Contents item 68 and RFP Sections 14.1–14.4. All figures in AED, exclusive of VAT. Two hosting options priced separately. Values shown are indicative placeholders; the sealed commercial envelope contains the committed figures.'));

children.push(H2('17.1 §14.1 — Infrastructure & Hosting Setup (One-Time CAPEX)'));
children.push(tbl([600, 5000, 1880, 1880], [
  { header: true, cells: ['#', 'Item', 'Option A (AED)', 'Option B (AED)'] },
  { cells: ['I.1', 'Compute infra setup — production (containers / orchestration cluster)', '185,000', '320,000'] },
  { cells: ['I.2', 'Compute infra — staging + development', '65,000', '110,000'] },
  { cells: ['I.3', 'Database infra: OLTP + OLAP + cache', '140,000', '240,000'] },
  { cells: ['I.4', 'Storage: object / file / backup', '45,000', '75,000'] },
  { cells: ['I.5', 'Networking: LB, FW, WAF, DDoS, VPN, API GW', '90,000', '160,000'] },
  { cells: ['I.6', 'CDN setup', '25,000', '40,000'] },
  { cells: ['I.7', 'SSL/TLS certificates — initial', '9,000', '12,000'] },
  { cells: ['I.8', 'DR site setup (secondary UAE)', '95,000', '180,000'] },
  { cells: ['I.9', 'Security infra: SIEM/SOC + vuln scanner + secrets + KMS', '110,000', '150,000'] },
  { cells: ['I.10', 'IAM infra + DLD identity integration', '55,000', '75,000'] },
  { cells: ['I.11', 'GIS / Mapping service setup', '30,000', '45,000'] },
  { cells: ['I.12', 'AI/LLM inference infra (if self-hosted)', '40,000', '260,000'] },
  { cells: ['I.13', 'DESC-CSP certification process costs', '110,000', '130,000'] },
  { cells: ['I.14', 'Initial VAPT (pre-go-live)', '85,000', '90,000'] },
  { cells: ['I.15', 'Hardware procurement (on-prem option only)', 'N/A', '480,000'] },
  { cells: [{ text: '', colspan: 2, bold: true, fill: COLOR.bg }, { text: 'TOTAL CAPEX (Setup)', bold: true, fill: COLOR.bg }, { text: '1,084,000', bold: true, fill: COLOR.bg }, { text: '2,367,000', bold: true, fill: COLOR.bg }] },
]));

children.push(H2('17.2 §14.2 — Software Licences & Subscriptions (First Year)'));
children.push(tbl([600, 5000, 1880, 1880], [
  { header: true, cells: ['#', 'Item', 'Option A (AED)', 'Option B (AED)'] },
  { cells: ['L.1', 'CMS platform (Strapi self-hosted — support licence)', '55,000', '55,000'] },
  { cells: ['L.2', 'AI / LLM service (Azure OpenAI UAE + Anthropic fallback)', '320,000', '380,000'] },
  { cells: ['L.3', 'GIS / Mapping API (Azure Maps UAE)', '70,000', '80,000'] },
  { cells: ['L.4', 'Email delivery service', '22,000', '22,000'] },
  { cells: ['L.5', 'SMS gateway service', '35,000', '35,000'] },
  { cells: ['L.6', 'Monitoring & observability (App Insights + Grafana)', '65,000', '80,000'] },
  { cells: ['L.7', 'Security scanning (Qualys / Tenable — DESC-approved)', '85,000', '85,000'] },
  { cells: ['L.8', 'Identity / SSO (Microsoft Entra ID P2)', '95,000', '95,000'] },
  { cells: ['L.9', 'Third-party data feeds (FX, ESG cert data, benchmarks)', '110,000', '110,000'] },
  { cells: ['L.10', 'Other — FIDO2 keys, PKI, workflow tooling', '40,000', '40,000'] },
  { cells: [{ text: '', colspan: 2, bold: true, fill: COLOR.bg }, { text: 'TOTAL Licences (Y1)', bold: true, fill: COLOR.bg }, { text: '897,000', bold: true, fill: COLOR.bg }, { text: '982,000', bold: true, fill: COLOR.bg }] },
]));

children.push(H2('17.3 §14.3 — Application Development & Implementation (CAPEX by Phase)'));
children.push(tbl([7480, 1880], [
  { header: true, cells: ['Phase / Deliverable', 'AED'] },
  { cells: [{ text: 'PHASE 1 — External Public Portal', bold: true, fill: COLOR.bg }, ''] },
  { cells: ['DLD Data Familiarisation & Pre-Publication Analytics Assessment', '420,000'] },
  { cells: ['UX/UI Design and Design System (bilingual, RTL, WCAG AA)', '280,000'] },
  { cells: ['External Portal Frontend (Blazor, AR/EN, RTL)', '610,000'] },
  { cells: ['Backend APIs, Data Integration, Pipelines', '740,000'] },
  { cells: ['AI Agent v1 (External) — LLM integration, RAG, charts, downloads', '560,000'] },
  { cells: ['Interactive Analytics Engine (Slice & Dice)', '380,000'] },
  { cells: ['CMS Setup + DLD Staff Training', '160,000'] },
  { cells: ['Investor Registration, Watchlist, Alert Engine', '240,000'] },
  { cells: ['Data Export Engine (xlsx/csv/pdf)', '120,000'] },
  { cells: ['Open Data API & Developer Portal', '180,000'] },
  { cells: ['QA, Testing, Data Accuracy, Phase 1 UAT', '310,000'] },
  { cells: [{ text: 'PHASE 1 TOTAL', bold: true, fill: COLOR.bg }, { text: '4,000,000', bold: true, fill: COLOR.bg }] },
  { cells: [{ text: 'PHASE 2 — EWRS', bold: true, fill: COLOR.bg }, ''] },
  { cells: ['EWRS Dashboard + 10 risk indicators + configurable thresholds', '520,000'] },
  { cells: ['Multi-level alert routing (4 levels + all channels)', '240,000'] },
  { cells: ['Playbook integration + alert audit trail', '160,000'] },
  { cells: ['Escrow Monitoring dashboard, audit log, monthly PDF', '280,000'] },
  { cells: ['Full RBAC for 6 roles + IdP + MFA', '180,000'] },
  { cells: ['Internal AI Agent with in-chat dashboards + download', '220,000'] },
  { cells: ['QA, Testing, Phase 2 UAT', '200,000'] },
  { cells: [{ text: 'PHASE 2 TOTAL', bold: true, fill: COLOR.bg }, { text: '1,800,000', bold: true, fill: COLOR.bg }] },
  { cells: [{ text: 'PHASE 3 — Developer Performance & Rating', bold: true, fill: COLOR.bg }, ''] },
  { cells: ['Developer Rating Engine — 6 criteria, live feeds', '360,000'] },
  { cells: ['Leaderboard, Comparison, Profile pages', '220,000'] },
  { cells: ['Weight Config Panel + Geographic map view', '150,000'] },
  { cells: ['Public Developer Scorecard (External Portal)', '120,000'] },
  { cells: ['DESC-authorised VAPT + remediation', '220,000'] },
  { cells: ['ISO 27001 Gap Assessment + certification roadmap', '150,000'] },
  { cells: ['QA, Testing, Phase 3 UAT', '180,000'] },
  { cells: [{ text: 'PHASE 3 TOTAL', bold: true, fill: COLOR.bg }, { text: '1,400,000', bold: true, fill: COLOR.bg }] },
  { cells: [{ text: 'PHASE 4 — Extended Languages & Supplementary Features', bold: true, fill: COLOR.bg }, ''] },
  { cells: ['Full UI + static content localisation — 6 extended languages', '320,000'] },
  { cells: ['AI Agent basic queries — 6 extended languages', '200,000'] },
  { cells: ['ESG / Sustainability Module (LEED / Estidama + heatmap)', '180,000'] },
  { cells: ['International Market Benchmarking (5 cities, quarterly)', '140,000'] },
  { cells: ['PDF Investment Profile Generator (zone + project)', '100,000'] },
  { cells: ['Complete Technical Documentation Package', '80,000'] },
  { cells: ['DLD Staff Training Programme — 8 sessions', '100,000'] },
  { cells: ['Final UAT + hypercare', '180,000'] },
  { cells: [{ text: 'PHASE 4 TOTAL', bold: true, fill: COLOR.bg }, { text: '1,300,000', bold: true, fill: COLOR.bg }] },
  { cells: [{ text: 'TOTAL APPLICATION DEVELOPMENT & IMPLEMENTATION', bold: true, fill: COLOR.primary, color: 'FFFFFF' }, { text: '8,500,000', bold: true, fill: COLOR.primary, color: 'FFFFFF' }] },
]));

children.push(H2('17.4 §14.4 — Annual Recurring Costs (OPEX)'));
children.push(tbl([600, 5000, 1880, 1880], [
  { header: true, cells: ['#', 'Item', 'Option A (AED/year)', 'Option B (AED/year)'] },
  { cells: ['R.1', 'Annual hosting / infra (compute, storage, networking, CDN, DB)', '680,000', '980,000'] },
  { cells: ['R.2', 'DR site annual operating cost', '220,000', '360,000'] },
  { cells: ['R.3', 'Hardware maintenance / refresh allowance (on-prem only)', 'N/A', '180,000'] },
  { cells: ['R.4', 'CMS platform annual renewal', '55,000', '55,000'] },
  { cells: ['R.5', 'AI / LLM service — calc at 5k DAU × avg 6 msg/day × 1.2k tokens', '840,000', '900,000'] },
  { cells: ['R.6', 'GIS / Mapping API annual', '90,000', '95,000'] },
  { cells: ['R.7', 'Email + SMS gateway annual', '110,000', '110,000'] },
  { cells: ['R.8', 'Monitoring / observability renewal', '70,000', '85,000'] },
  { cells: ['R.9', 'Identity / SSO renewal', '95,000', '95,000'] },
  { cells: ['R.10', 'Third-party data feeds renewal', '110,000', '110,000'] },
  { cells: ['R.11', 'SSL/TLS certificate renewal', '8,000', '10,000'] },
  { cells: ['R.12', 'Annual VAPT + quarterly VA (DESC-approved)', '180,000', '180,000'] },
  { cells: ['R.13', '24/7 SOC / SIEM monitoring (DESC-certified)', '320,000', '320,000'] },
  { cells: ['R.14', 'DESC-CSP annual surveillance audit', '60,000', '60,000'] },
  { cells: ['R.15', 'ISO 27001 annual surveillance audit', '55,000', '55,000'] },
  { cells: ['R.16', '12-month warranty & support (from §15.4)', '1,200,000', '1,200,000'] },
  { cells: [{ text: '', colspan: 2, bold: true, fill: COLOR.bg }, { text: 'TOTAL OPEX / year', bold: true, fill: COLOR.bg }, { text: '4,093,000', bold: true, fill: COLOR.bg }, { text: '4,795,000', bold: true, fill: COLOR.bg }] },
]));

children.push(H2('17.5 §15.4 — Warranty & Support Pricing (12 months)'));
children.push(tbl([600, 7000, 1760], [
  { header: true, cells: ['#', 'Item', 'Annual Cost (AED)'] },
  { cells: ['S.1', 'Dedicated support team (named primary + on-call)', '820,000'] },
  { cells: ['S.2', '120-hour minor changes pool (included). Additional rate: 450 AED/hour', 'Included'] },
  { cells: ['S.3', 'Security vulnerability monitoring & patch management', '160,000'] },
  { cells: ['S.4', 'Infrastructure operations — hosting, backups, DR, uptime SLA', '160,000'] },
  { cells: ['S.5', 'Help-desk & incident management tooling', '60,000'] },
  { cells: [{ text: '', colspan: 2, bold: true, fill: COLOR.primary, color: 'FFFFFF' }, { text: 'TOTAL 12-MONTH WARRANTY & SUPPORT', bold: true, fill: COLOR.primary, color: 'FFFFFF' }, { text: '1,200,000', bold: true, fill: COLOR.primary, color: 'FFFFFF' }] },
]));

children.push(H2('17.6 5-Year TCO Summary'));
children.push(tbl([5800, 1780, 1780], [
  { header: true, cells: ['Category', 'Option A (AED)', 'Option B (AED)'] },
  { cells: ['§14.1 Infrastructure Setup CAPEX (one-time)', '1,084,000', '2,367,000'] },
  { cells: ['§14.2 Licences & Subscriptions — Year 1', '897,000', '982,000'] },
  { cells: ['§14.3 Application Development (all phases)', '8,500,000', '8,500,000'] },
  { cells: ['§14.4 Annual OPEX × 5 years', '20,465,000', '23,975,000'] },
  { cells: [{ text: '5-Year TCO', bold: true, fill: COLOR.primary, color: 'FFFFFF' }, { text: '30,946,000', bold: true, fill: COLOR.primary, color: 'FFFFFF' }, { text: '35,824,000', bold: true, fill: COLOR.primary, color: 'FFFFFF' }] },
]));
children.push(PBREAK());

// ---------- SECTION 18: RISK REGISTER (item #69) ----------
children.push(H1('18. Risk Register'));
children.push(P('This section addresses Mandatory Proposal Contents item 69.'));
children.push(tbl([500, 2600, 1100, 1100, 4060], [
  { header: true, cells: ['#', 'Risk', 'Likelihood', 'Impact', 'Mitigation'] },
  { cells: ['R1', 'DLD source-system sandbox access delayed', 'M', 'H', 'Access requested at contract signing; mock contracts agreed in W1; escalation path through DLD PMO.'] },
  { cells: ['R2', 'Pre-Publication Analytics Assessment reveals data gaps', 'M', 'H', 'Early quality baseline; agreed remediation plan; publication blocked until accuracy ≥ 99.5%.'] },
  { cells: ['R3', 'DESC-CSP certification lead time', 'M', 'H', 'Option A Azure UAE is pre-certified; Option B routes via DESC-certified facility with early engagement.'] },
  { cells: ['R4', 'VAPT reveals High / Critical findings close to Phase 3 go-live', 'M', 'H', 'VAPT scheduled W8; remediation W8–9; re-test W9; secondary VAPT window available.'] },
  { cells: ['R5', 'AI accuracy below 90% on DLD catalogue', 'M', 'M', 'Deterministic analytics overlay + prompt tuning + retrieval tuning; iterative harness runs from W5.'] },
  { cells: ['R6', 'UAE Pass federation registration slower than expected', 'M', 'M', 'Registration in W1; Entra ID fallback for staff path; public auth independent.'] },
  { cells: ['R7', 'AI inference UAE endpoint availability for secondary model', 'L', 'H', 'Multi-model architecture; architecture validated before commitment; self-hosted Llama-3 fallback in UAE GPU.'] },
  { cells: ['R8', 'Load / performance SLA under peak', 'L', 'H', 'Early k6 load tests at W5; KPI cache; query plan reviews; CDN for static.'] },
  { cells: ['R9', 'Arabic content / translation quality', 'M', 'M', 'Native reviewer; DLD official AR names validated; UAT Wave 1 includes AR journeys.'] },
  { cells: ['R10', 'Scope creep during 90-day window', 'M', 'M', 'Change-control board; minor changes pool isolated; gate-based acceptance with written sign-off.'] },
  { cells: ['R11', 'Key-person dependency', 'L', 'M', 'Named back-ups; living runbooks in repo; cross-training.'] },
  { cells: ['R12', 'SOC / SIEM onboarding slippage', 'L', 'M', 'SOC contract at W1; parallel onboarding independent of build workstreams.'] },
]));
children.push(PBREAK());

// ---------- SECTION 19: KNOWLEDGE TRANSFER & EXIT (item #70) ----------
children.push(H1('19. Knowledge Transfer & Exit Plan'));
children.push(P('This section addresses Mandatory Proposal Contents item 70 and RFP Section 19.1.'));
children.push(BULLET('DLD receives full source-code repositories (Git) at project completion and any time on request.'));
children.push(BULLET('100% of infrastructure as Bicep IaC, delivered to DLD repository; DLD retains full control of Azure subscription / tenant.'));
children.push(BULLET('Complete documentation pack: architecture, ops runbooks, DR playbook, threat model, DPIA, control matrices, data dictionary.'));
children.push(BULLET('8 structured training sessions for DLD staff covering all roles, CMS, EWRS, AI Agent best practice.'));
children.push(BULLET('Live handover period overlapping hypercare; SOC and support runbooks delivered.'));
children.push(BULLET('Upon expiry/termination: all DLD data returned in machine-readable open formats within 30 calendar days; written certification of complete data destruction from all vendor-controlled systems within 60 days.'));
children.push(PBREAK());

// ---------- SECTION 20: ACCEPTANCE CRITERIA COMMITMENTS (§17) ----------
children.push(H1('20. Acceptance Criteria — Our Commitments'));
children.push(P('We commit to every acceptance criterion in RFP Section 17, summarised below.'));
children.push(H2('20.1 Phase-Level Acceptance (§17.1)'));
children.push(BULLET('All phase deliverables operational in production.'));
children.push(BULLET('Structured UAT with DLD stakeholders; 100% of functional requirements applicable pass.'));
children.push(BULLET('Performance SLAs met under load.'));
children.push(BULLET('Data accuracy criterion met (≥ 99.5% on 500-record sample).'));
children.push(BULLET('Zero open Critical or High-severity defects at phase sign-off.'));
children.push(BULLET('DLD Project Sponsor signs phase acceptance form.'));
children.push(H2('20.2 Universal UAT Matrix (§17.3)'));
children.push(tbl([4000, 5360], [
  { header: true, cells: ['Test Category', 'Pass Criterion — our commitment'] },
  { cells: ['Functional', '100% applicable FRs met; 0 Critical/High open'] },
  { cells: ['Performance', 'All P95 targets met at §10.1 concurrency'] },
  { cells: ['AI Accuracy (P1 + P2)', '≥ 90% on 100-Q DLD set; 0 fabrication; 0 advisory'] },
  { cells: ['Security (P3)', 'VAPT: 0 Critical/High; OWASP: 0 Critical'] },
  { cells: ['Accessibility', '0 critical axe violations; WCAG 2.1 AA verified all pages'] },
  { cells: ['Language & RTL', 'AR/EN correct P1–3; all 8 languages P4; RTL AR + Urdu'] },
  { cells: ['Data Accuracy', '500-record sample ≥ 99.5%'] },
  { cells: ['End-User Usability', 'SUS ≥ 75; critical task completion ≥ 90%'] },
  { cells: ['CMS Non-Tech Edit', 'Non-dev publish in ≤ 10 min without vendor assistance'] },
  { cells: ['Alert Delivery', 'All alert levels / types within SLA'] },
]));
children.push(PBREAK());

// ---------- SECTION 21: DATA GOVERNANCE, PRIVACY & OWNERSHIP (§19) ----------
children.push(H1('21. Data Governance, Privacy & Ownership'));
children.push(H2('21.1 Data Ownership (§19.1)'));
children.push(BULLET('All data processed, stored, or generated within IRETP is the sole and exclusive property of Dubai Land Department.'));
children.push(BULLET('We will not use DLD data for any purpose other than contracted services.'));
children.push(BULLET('On expiry / termination / handover: deliver all data in machine-readable, open formats within 30 days; certified destruction from all vendor systems within 60 days.'));
children.push(H2('21.2 Personal Data Protection (§19.2)'));
children.push(BULLET('Full compliance with UAE Federal Decree-Law No. 45 of 2021 (PDPL).'));
children.push(BULLET('No PII displayed on any public-facing interface — public transaction data is aggregated or anonymised.'));
children.push(BULLET('Data Processing Agreement (DPA) signed before access to any personal data.'));
children.push(BULLET('Consent management for newsletters, alert emails, and cross-session AI memory is PDPL-compliant.'));
children.push(H2('21.3 Data Quality Standards (§19.3)'));
children.push(tbl([2400, 4000, 2960], [
  { header: true, cells: ['Dimension', 'Minimum Standard', 'Measurement'] },
  { cells: ['Accuracy', '≥ 99.5% match to DLD source', 'DLD data team audit'] },
  { cells: ['Completeness', '100% of mandatory fields in last 24 months', 'DLD data team check'] },
  { cells: ['Timeliness', 'Transactions lag ≤ 24 h; KPIs ≤ 15 min', 'Vendor pipeline monitoring with breach alerts'] },
  { cells: ['Consistency', 'No discrepancy between map and table data', 'DLD data team reconciliation'] },
]));
children.push(PBREAK());

// ---------- SECTION 22: ADDITIONAL FEATURES (§20) ----------
children.push(H1('22. Additional Recommended Features — Response'));
children.push(P('All RFP §20 recommended features are included in our proposal as shown below.'));
children.push(tbl([3600, 2400, 3360], [
  { header: true, cells: ['Feature', 'Phase', 'Status in our proposal'] },
  { cells: ['International Market Benchmarking (5 cities, quarterly)', 'Phase 4', 'Included'] },
  { cells: ['ESG / Sustainability Module (LEED / Estidama / BREEAM + heatmap)', 'Phase 4', 'Included'] },
  { cells: ['Open Data API Portal + OpenAPI 3.0 + interactive console', 'Phase 1', 'Included (5 standard endpoints)'] },
  { cells: ['Public Developer Scorecard', 'Phase 3', 'Included'] },
  { cells: ['Beneficial Ownership Transparency', 'Phase 1 (UI) / Phase 3 (refinement)', 'Included'] },
  { cells: ['Mortgage & Debt Market Transparency (aggregate LTV, MoM)', 'Phase 1', 'Included'] },
]));
children.push(PBREAK());

// ---------- SECTION 23: MANDATORY PROPOSAL CHECKLIST COVERAGE ----------
children.push(H1('23. Mandatory Proposal Contents — Coverage Map (§16.2)'));
children.push(tbl([800, 5800, 2760], [
  { header: true, cells: ['#', 'Required Content', 'Where in this proposal'] },
  { cells: ['56', 'Company Profile', 'Section 3'] },
  { cells: ['57', 'Portfolio of Relevant Projects', 'Section 4.1 + Annex E'] },
  { cells: ['58', 'Data Integration & Accuracy Case Study', 'Section 4.2 + Annex G'] },
  { cells: ['59', 'AI System Demonstration', 'Section 4.3 + Annex H (live URL / video)'] },
  { cells: ['60', 'Proposed Technology Stack (all layers)', 'Section 6'] },
  { cells: ['61', 'Proposed Hosting Model(s)', 'Section 7'] },
  { cells: ['62', 'Proposed Project Team', 'Section 15'] },
  { cells: ['63', 'Phased Delivery Timeline (Gantt)', 'Section 14 (+ Annex D detailed Gantt)'] },
  { cells: ['64', 'DLD Data Familiarisation Plan', 'Section 11'] },
  { cells: ['65', 'System Architecture Diagram', 'Section 8 (+ Annex B)'] },
  { cells: ['66', 'Security & Compliance Plan (DESC ISR v3, VAPT, SOC, ISO 27001)', 'Section 12'] },
  { cells: ['67', 'Proposed SLA (dev warranty + 12-month support, P1–P4)', 'Section 16'] },
  { cells: ['68', 'Itemised Cost Tables (14.1 / 14.2 / 14.3 / 14.4 / 15.4)', 'Section 17'] },
  { cells: ['69', 'Risk Register', 'Section 18'] },
  { cells: ['70', 'Knowledge Transfer & Exit Plan', 'Section 19'] },
]));
children.push(PBREAK());

// ---------- SECTION 24: ANNEXES ----------
children.push(H1('24. Annexes'));
children.push(BULLET('Annex A — RFP Requirement Traceability Matrix (every FR/AN/AI/ESC/EWRS/DEV ID mapped to implementation module)'));
children.push(BULLET('Annex B — System Architecture Diagrams (logical, physical, data-flow)'));
children.push(BULLET('Annex C — Threat Model and DESC-CSP Control Matrix'));
children.push(BULLET('Annex D — Detailed 3-month Gantt with dependencies and critical path'));
children.push(BULLET('Annex E — CV pack for proposed named team members'));
children.push(BULLET('Annex F — Commercial schedule (sealed envelope)'));
children.push(BULLET('Annex G — Data Integration & Accuracy Case Study (mandatory)'));
children.push(BULLET('Annex H — AI System Demonstration URL + recorded walkthrough (≥ 10 min)'));
children.push(BULLET('Annex I — Sample Open Data OpenAPI 3.0 specification'));
children.push(BULLET('Annex J — AI Accuracy Harness sample run & results'));
children.push(BULLET('Annex K — Bill of Materials (Option B on-premises, if selected)'));
children.push(BULLET('Annex L — Company certifications (ISO 27001, ISO 9001, UAE Commercial Licence)'));
children.push(PBREAK());

// ---------- CLOSING ----------
children.push(H1('Statement of Compliance'));
children.push(P('[Vendor Legal Entity Name] hereby confirms that this proposal addresses every mandatory requirement of RFP No. DLD-IRETP-2026-001, including all 15 Mandatory Proposal Contents items, all four cost tables, the 4-phase delivery plan within the 90-day mandatory constraint, and the 12-month post-go-live warranty and support obligation. All prices are quoted in UAE Dirhams, exclusive of VAT, and remain valid for 120 days from the submission date.'));
children.push(new Paragraph({ spacing: { before: 400, after: 100 }, children: [new TextRun({ text: 'Authorised Signatory', font: 'Calibri', size: 22, bold: true })] }));
children.push(new Paragraph({ spacing: { before: 200 }, children: [new TextRun({ text: 'Name: ______________________________________', font: 'Calibri', size: 22 })] }));
children.push(new Paragraph({ spacing: { before: 120 }, children: [new TextRun({ text: 'Title: _______________________________________', font: 'Calibri', size: 22 })] }));
children.push(new Paragraph({ spacing: { before: 120 }, children: [new TextRun({ text: 'Signature: ___________________________________', font: 'Calibri', size: 22 })] }));
children.push(new Paragraph({ spacing: { before: 120 }, children: [new TextRun({ text: 'Date: _______________________________________', font: 'Calibri', size: 22 })] }));
children.push(new Paragraph({ spacing: { before: 400, after: 100 }, alignment: AlignmentType.CENTER, children: [new TextRun({ text: '— END OF PROPOSAL —', font: 'Calibri', size: 22, bold: true, color: COLOR.mute })] }));

// ---------- DOCUMENT ----------
const doc = new Document({
  creator: 'IRETP Proposal Team',
  title: 'IRETP Proposal — DLD-IRETP-2026-001',
  description: 'Technical & Commercial Proposal in response to RFP DLD-IRETP-2026-001',
  styles: {
    default: { document: { run: { font: 'Calibri', size: 22 } } },
    paragraphStyles: [
      { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true,
        run: { size: 32, bold: true, font: 'Calibri', color: COLOR.primary },
        paragraph: { spacing: { before: 360, after: 180 }, outlineLevel: 0 } },
      { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true,
        run: { size: 26, bold: true, font: 'Calibri', color: COLOR.primary },
        paragraph: { spacing: { before: 280, after: 140 }, outlineLevel: 1 } },
      { id: 'Heading3', name: 'Heading 3', basedOn: 'Normal', next: 'Normal', quickFormat: true,
        run: { size: 23, bold: true, font: 'Calibri', color: COLOR.accent },
        paragraph: { spacing: { before: 200, after: 100 }, outlineLevel: 2 } },
    ],
  },
  numbering: {
    config: [
      { reference: 'bullets', levels: [{ level: 0, format: LevelFormat.BULLET, text: '•', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 560, hanging: 280 } } } }] },
      { reference: 'numbers', levels: [{ level: 0, format: LevelFormat.DECIMAL, text: '%1.', alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 560, hanging: 280 } } } }] },
    ],
  },
  sections: [{
    properties: { page: { size: { width: 12240, height: 15840 }, margin: { top: 1080, right: 1080, bottom: 1080, left: 1080 } } },
    headers: {
      default: new Header({ children: [new Paragraph({
        alignment: AlignmentType.LEFT,
        border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: COLOR.accent, space: 2 } },
        children: [
          new TextRun({ text: 'IRETP Proposal  |  ', font: 'Calibri', size: 18, color: COLOR.primary, bold: true }),
          new TextRun({ text: 'DLD-IRETP-2026-001', font: 'Calibri', size: 18, color: COLOR.mute }),
          new TextRun({ text: '\t' }),
          new TextRun({ text: 'CONFIDENTIAL', font: 'Calibri', size: 18, color: COLOR.danger, bold: true }),
        ],
        tabStops: [{ type: TabStopType.RIGHT, position: 9360 }],
      })] }),
    },
    footers: {
      default: new Footer({ children: [new Paragraph({
        alignment: AlignmentType.LEFT,
        border: { top: { style: BorderStyle.SINGLE, size: 6, color: COLOR.accent, space: 2 } },
        children: [
          new TextRun({ text: '© 2026 [Vendor Legal Entity Name] — Submitted to Dubai Land Department', font: 'Calibri', size: 16, color: COLOR.mute }),
          new TextRun({ text: '\t' }),
          new TextRun({ text: 'Page ', font: 'Calibri', size: 16, color: COLOR.mute }),
          new TextRun({ children: [PageNumber.CURRENT], font: 'Calibri', size: 16, color: COLOR.mute }),
          new TextRun({ text: ' of ', font: 'Calibri', size: 16, color: COLOR.mute }),
          new TextRun({ children: [PageNumber.TOTAL_PAGES], font: 'Calibri', size: 16, color: COLOR.mute }),
        ],
        tabStops: [{ type: TabStopType.RIGHT, position: 9360 }],
      })] }),
    },
    children,
  }],
});

Packer.toBuffer(doc).then(buf => {
  fs.writeFileSync(String.raw`C:\Users\kalmi\IRETP\IRETP_Proposal.docx`, buf);
  console.log('WROTE', buf.length, 'bytes');
}).catch(e => { console.error(e); process.exit(1); });
