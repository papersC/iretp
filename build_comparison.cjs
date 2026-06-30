// Build a 1-slide comparison: UnifyApps proposal vs. our IRETP system
const Pptx = require('C:/nvm4w/nodejs/node_modules/pptxgenjs');

const pres = new Pptx();
pres.layout = 'LAYOUT_WIDE'; // 13.333 x 7.5"
pres.title = 'IRETP — UnifyApps vs Our System';

// DLD-aligned palette
const C = {
  bg:     'FFFFFF',
  ink:    '0F1B2D', // near-black for headings
  ours:   '066735', // DLD green (our side)
  theirs: '3F4753', // charcoal (UnifyApps side)
  rowAlt: 'F4F6F4',
  rule:   'D9DDD7',
  muted:  '6A7280',
  accent: 'C99A2E',  // gold accent
  good:   '066735',
  warn:   'B85042',
};

const slide = pres.addSlide();
slide.background = { color: C.bg };

// ── Title strip ────────────────────────────────────────────────
slide.addShape('rect', { x:0, y:0, w:13.333, h:0.85, fill:{color:C.ink}, line:{type:'none'} });
slide.addText(
  [
    { text:'IRETP — Side-by-Side Comparison  ', options:{ bold:true, fontSize:22, color:'FFFFFF' } },
    { text:'UnifyApps Proposal vs Our Delivered System', options:{ fontSize:16, color:'CDD3D9' } },
  ],
  { x:0.4, y:0.05, w:12.5, h:0.5, fontFace:'Calibri', valign:'middle' }
);
slide.addText('RFP DLD-IRETP-2026-001  •  Same scope, two delivery models', {
  x:0.4, y:0.48, w:12.5, h:0.32,
  fontSize:11, italic:true, color:'9AA2AC', fontFace:'Calibri', valign:'middle',
});

// ── Three highlight stats ──────────────────────────────────────
const statY = 1.00;
const statH = 1.05;
const gap   = 0.15;
const statX0 = 0.40;
const statW  = (12.45 - 2*gap)/3; // ≈ 4.05" — matches table span 0.40..12.85

function statCard(idx, big, small, foot, color){
  const x = statX0 + idx*(statW+gap);
  slide.addShape('rect', { x, y:statY, w:statW, h:statH, fill:{color:'F8F9F8'}, line:{color:C.rule, width:0.75} });
  slide.addShape('rect', { x, y:statY, w:0.08, h:statH, fill:{color}, line:{type:'none'} });
  slide.addText(big, { x:x+0.20, y:statY+0.05, w:statW-0.30, h:0.42, fontSize:22, bold:true, color, fontFace:'Calibri', valign:'middle' });
  slide.addText(small, { x:x+0.20, y:statY+0.42, w:statW-0.30, h:0.30, fontSize:11, bold:true, color:C.ink, fontFace:'Calibri' });
  slide.addText(foot,  { x:x+0.20, y:statY+0.66, w:statW-0.30, h:0.30, fontSize:9.5, italic:true, color:C.muted, fontFace:'Calibri' });
}
statCard(0, '~3× lower TCO',        '5-yr: AED 5.1M (ours) vs AED 14.8M (UnifyApps)', 'Sources: this proposal §7.4; our internal cost deck.', C.ours);
statCard(1, 'Live, not Day-90',     '88/88 xUnit pass; 28+ pages render today',       'UnifyApps prototype = mock screens only.',                C.ours);
statCard(2, '100% source-owned',    'No platform subscription, no per-seat fees',     'UnifyApps = platform license + PS each year.',           C.ours);

// ── Comparison table ───────────────────────────────────────────
const tableY = 2.20;
const cellH  = 0.36;
const col = { dim:[0.40, 2.55], theirs:[2.95, 4.95], ours:[7.90, 4.95] }; // right edge = 12.85"

// Header row
slide.addShape('rect', { x:col.dim[0],    y:tableY, w:col.dim[1],    h:0.46, fill:{color:C.ink},    line:{type:'none'} });
slide.addShape('rect', { x:col.theirs[0], y:tableY, w:col.theirs[1], h:0.46, fill:{color:C.theirs}, line:{type:'none'} });
slide.addShape('rect', { x:col.ours[0],   y:tableY, w:col.ours[1],   h:0.46, fill:{color:C.ours},   line:{type:'none'} });
slide.addText('Dimension',         { x:col.dim[0]+0.10,    y:tableY, w:col.dim[1]-0.20,    h:0.46, fontSize:11.5, bold:true, color:'FFFFFF', fontFace:'Calibri', valign:'middle', margin:0 });
slide.addText('UnifyApps Proposal',{ x:col.theirs[0]+0.10, y:tableY, w:col.theirs[1]-0.20, h:0.46, fontSize:11.5, bold:true, color:'FFFFFF', fontFace:'Calibri', valign:'middle', margin:0 });
slide.addText('Our IRETP System (already built)', { x:col.ours[0]+0.10, y:tableY, w:col.ours[1]-0.20, h:0.46, fontSize:11.5, bold:true, color:'FFFFFF', fontFace:'Calibri', valign:'middle', margin:0 });

const rows = [
  { dim:'Delivery model',
    theirs:'SaaS platform (UnifyApps OS for AI) + Professional Services',
    ours:'Custom .NET 9 / Blazor Server / EF Core — purpose-built, 100% owned source' },
  { dim:'State today',
    theirs:'Public-portal screen prototype on DLD design language',
    ours:'Live working system: 18 public + 10 admin pages return 200; build 0/0' },
  { dim:'Timeline',
    theirs:'90 calendar days from contract signing',
    ours:'4-month build plan (M1–M4); core features already implemented' },
  { dim:'Tech stack',
    theirs:'UnifyApps OS + Mongo + StarRocks + Kafka + OpenSearch + Redis on K8s',
    ours:'.NET 9, Blazor Server, EF Core, SQL Server, Hangfire, SignalR, Redis' },
  { dim:'AI Agent',
    theirs:'Multi-model orchestration, RAG via OpenSearch, system-prompt non-advisory guardrail',
    ours:'Tier-aware orchestrator + RAG w/ deterministic stats + regex+keyword guardrail + 14-Q accuracy harness' },
  { dim:'Maps & GIS',
    theirs:'Mapbox GL JS, Dubai zone GeoJSON, 3 heatmap layers',
    ours:'MapLibre + 4 heatmap modes (vol / PSF / yield / ESG) + 183 individual project pins' },
  { dim:'EWRS (Phase 2)',
    theirs:'Automations rules engine, configurable thresholds, multi-level escalation',
    ours:'10-indicator engine + L1–L4 auto-escalation (Hangfire) + immutable audit log §10.2' },
  { dim:'GRETI beyond-brief',
    theirs:'Beneficial Ownership + Mortgage Transparency — proposed roadmap',
    ours:'Already shipping: BeneficialOwnership page (Arabic owners) + Mortgage page w/ 5 KPIs' },
  { dim:'Compliance posture',
    theirs:'DESC ISR v3 roadmap; ISO 27001 target +6 mo; VAPT pre-engaged Day 1',
    ours:'Audit-log immutability, security CI (vuln pkg + OWASP ZAP + Bicep lint), SLO health probe' },
  { dim:'Cost (5-yr TCO)',
    theirs:'AED 14.8 M (UnifyApps scope, on-prem) + AED 440K/yr warranty',
    ours:'AED ≈5.1 M  (CAPEX 2.58M + 4× OPEX 629K) — no platform license' },
  { dim:'IP / lock-in',
    theirs:'Platform IP retained by UnifyApps; configs/data exit on request',
    ours:'Full source + IaC + docs handed over Day 1 — zero vendor lock-in' },
];

// Draw rows
rows.forEach((r, i) => {
  const y = tableY + 0.46 + i*cellH;
  const bgFill = (i % 2 === 0) ? 'FFFFFF' : C.rowAlt;
  slide.addShape('rect', { x:col.dim[0],    y, w:col.dim[1],    h:cellH, fill:{color:'E8ECEF'}, line:{color:C.rule, width:0.5} });
  slide.addShape('rect', { x:col.theirs[0], y, w:col.theirs[1], h:cellH, fill:{color:bgFill},  line:{color:C.rule, width:0.5} });
  slide.addShape('rect', { x:col.ours[0],   y, w:col.ours[1],   h:cellH, fill:{color:bgFill},  line:{color:C.rule, width:0.5} });

  slide.addText(r.dim,    { x:col.dim[0]+0.10,    y, w:col.dim[1]-0.20,    h:cellH, fontSize:10, bold:true,  color:C.ink,   fontFace:'Calibri', valign:'middle', margin:0 });
  slide.addText(r.theirs, { x:col.theirs[0]+0.10, y, w:col.theirs[1]-0.20, h:cellH, fontSize:9.5,   color:C.ink,   fontFace:'Calibri', valign:'middle', margin:0 });
  slide.addText(r.ours,   { x:col.ours[0]+0.10,   y, w:col.ours[1]-0.20,   h:cellH, fontSize:9.5,   bold:true, color:C.ours, fontFace:'Calibri', valign:'middle', margin:0 });
});

// ── Header + body table right edge: col.ours[0] + col.ours[1] = 12.85" ──
// Bottom takeaway strap (aligned to same span)
const stripY = tableY + 0.46 + rows.length*cellH + 0.07;
const stripX = col.dim[0];
const stripW = (col.ours[0] + col.ours[1]) - col.dim[0]; // 12.45"
slide.addShape('rect', { x:stripX, y:stripY, w:stripW, h:0.46, fill:{color:C.ours}, line:{type:'none'} });
slide.addText(
  [
    { text:'Bottom line: ', options:{ bold:true, color:'FFFFFF' } },
    { text:'a working IRETP today at ~⅓ the 5-year TCO of the SaaS proposal, with full source ownership and zero per-user fees.',
      options:{ color:'FFFFFF' } },
  ],
  { x:stripX+0.15, y:stripY, w:stripW-0.30, h:0.46, fontSize:12, fontFace:'Calibri', valign:'middle', margin:0 }
);

// Footer attribution
slide.addText('Sources: DLD-IRETP-2026 UnifyApps Technical Proposal (May 2026) §3.4 / §6 / §7.4 / §12  ·  IRETP internal cost deck  ·  project_iretp_rfp_progress.md',
  { x:stripX, y:stripY+0.50, w:stripW, h:0.22, fontSize:8, italic:true, color:C.muted, fontFace:'Calibri', margin:0 });

pres.writeFile({ fileName: 'C:/Users/kalmi/IRETP/IRETP_vs_UnifyApps_1Slide.pptx' })
  .then(p => console.log('WROTE', p));
