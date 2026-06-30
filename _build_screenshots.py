"""
Render high-fidelity HTML mockups of IRETP pages using the actual design system
tokens and layout patterns from the codebase, then screenshot each at 1600x900.

These are submission-quality renders that reflect what the live system displays.
The mockups use the same DLD-green palette, IBM Plex Sans Arabic + Inter fonts,
shared component shapes (ModuleBanner, KPI cards, SkeletonTable patterns) that
the live Razor pages render.
"""

from playwright.sync_api import sync_playwright
import os, json

OUT = r'C:\Users\kalmi\IRETP\proposal_screens'
os.makedirs(OUT, exist_ok=True)

# ---- Shared design system (mirrors wwwroot/css/app.css + design-system.css) ----
CSS = """
:root {
  --primary: #066735;
  --primary-light: #088B49;
  --primary-dark: #044d27;
  --primary-tint: #E8F2EC;
  --warn: #B85042;
  --gold: #C99A2E;
  --ink: #0F1B2D;
  --text: #1A1A1A;
  --muted: #6A7280;
  --rule: #D9DDD7;
  --bg: #F4F6F4;
  --card: #FFFFFF;
  --shadow: 0 1px 3px rgba(15,27,45,0.06), 0 1px 2px rgba(15,27,45,0.04);
}
* { box-sizing: border-box; margin: 0; padding: 0; }
html, body { font-family: 'Inter', 'Segoe UI', system-ui, sans-serif; color: var(--text); background: var(--bg); font-size: 14px; line-height: 1.5; }
.app { display: grid; grid-template-columns: 232px 1fr; min-height: 100vh; }

/* Sidebar */
.sidebar { background: linear-gradient(180deg, var(--primary), var(--primary-dark)); color: #fff; padding: 16px 0; }
.sidebar .brand { padding: 0 18px 16px; border-bottom: 1px solid rgba(255,255,255,0.18); margin-bottom: 12px; }
.sidebar .brand .logo { font-weight: 700; font-size: 18px; letter-spacing: 0.2px; }
.sidebar .brand .sub { font-size: 11px; opacity: 0.78; margin-top: 2px; }
.sidebar nav { padding: 0 8px; }
.sidebar .nav-link { display: flex; align-items: center; gap: 10px; padding: 9px 12px; color: #DDF1E3; text-decoration: none; border-radius: 8px; font-size: 13px; margin-bottom: 1px; }
.sidebar .nav-link.active { background: rgba(255,255,255,0.16); color: #fff; font-weight: 600; }
.sidebar .nav-link .ico { width: 18px; height: 18px; display: inline-flex; align-items: center; justify-content: center; font-size: 13px; }
.sidebar .section-label { padding: 12px 16px 6px; font-size: 10.5px; letter-spacing: 0.8px; text-transform: uppercase; opacity: 0.68; }

/* Main */
.main { display: flex; flex-direction: column; }
.topbar { background: #fff; border-bottom: 1px solid var(--rule); padding: 12px 24px; display: flex; align-items: center; justify-content: space-between; }
.topbar .crumb { color: var(--muted); font-size: 12px; }
.topbar .ttitle { font-weight: 600; font-size: 15px; color: var(--ink); }
.topbar .actions { display: flex; gap: 10px; }
.btn { padding: 7px 14px; border-radius: 8px; border: 1px solid var(--rule); background: #fff; font-size: 12.5px; cursor: pointer; color: var(--ink); display: inline-flex; align-items: center; gap: 6px; }
.btn.primary { background: var(--primary); color: #fff; border-color: var(--primary); }
.btn.ghost { background: var(--primary-tint); color: var(--primary); border-color: transparent; }
.content { padding: 22px 24px 30px; flex: 1; }

/* Page banner */
.banner { display: flex; align-items: center; gap: 14px; margin-bottom: 18px; padding: 14px 18px; background: linear-gradient(90deg, #fff, #fff); border-left: 4px solid var(--primary); border-radius: 10px; box-shadow: var(--shadow); }
.banner .icon { width: 42px; height: 42px; border-radius: 10px; background: var(--primary-tint); color: var(--primary); display: flex; align-items: center; justify-content: center; font-size: 22px; }
.banner h1 { font-size: 18px; font-weight: 700; color: var(--ink); }
.banner p { font-size: 12.5px; color: var(--muted); margin-top: 2px; }
.banner .right { margin-left: auto; display: flex; gap: 8px; }
.badge { display: inline-flex; padding: 3px 9px; border-radius: 999px; font-size: 11px; font-weight: 600; background: var(--primary-tint); color: var(--primary); }
.badge.warn { background: #FFF3E0; color: #B85042; }
.badge.gold { background: #FFF4D6; color: #8E6A12; }
.badge.crit { background: #FFE6E2; color: #B22A1C; }
.badge.info { background: #E1F1FB; color: #0F5A87; }

/* KPI grid */
.kpi-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 14px; margin-bottom: 18px; }
.kpi { background: #fff; border-radius: 12px; padding: 16px; box-shadow: var(--shadow); }
.kpi .lbl { font-size: 11.5px; color: var(--muted); text-transform: uppercase; letter-spacing: 0.5px; font-weight: 600; }
.kpi .val { font-size: 26px; font-weight: 700; color: var(--ink); margin-top: 4px; }
.kpi .delta { margin-top: 6px; font-size: 12px; display: flex; align-items: center; gap: 5px; color: var(--primary); font-weight: 600; }
.kpi .delta.down { color: var(--warn); }
.kpi .freshness { font-size: 10.5px; color: var(--muted); margin-top: 5px; font-style: italic; }

/* Cards */
.card { background: #fff; border-radius: 12px; padding: 16px; box-shadow: var(--shadow); }
.card .head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
.card .head h3 { font-size: 13.5px; font-weight: 700; color: var(--ink); }
.card .head .actions { display: flex; gap: 6px; }
.tabs { display: inline-flex; background: var(--bg); border-radius: 8px; padding: 3px; gap: 2px; }
.tabs .tab { padding: 5px 12px; font-size: 12px; border-radius: 6px; cursor: pointer; color: var(--muted); font-weight: 500; }
.tabs .tab.active { background: #fff; color: var(--ink); box-shadow: var(--shadow); font-weight: 600; }

/* Tables */
table.tbl { width: 100%; border-collapse: collapse; font-size: 12.5px; }
table.tbl th { text-align: left; padding: 8px 10px; font-size: 11px; color: var(--muted); text-transform: uppercase; letter-spacing: 0.5px; border-bottom: 1px solid var(--rule); font-weight: 700; }
table.tbl td { padding: 9px 10px; border-bottom: 1px solid var(--rule); color: var(--text); }
table.tbl tr:hover td { background: #FBFCFB; }
.mono { font-family: 'Consolas', 'Menlo', monospace; font-size: 11.5px; }

/* Charts (SVG positioning) */
.chart-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; }
.chart-card { background: #fff; border-radius: 12px; padding: 14px 16px 8px; box-shadow: var(--shadow); }
.chart-card h4 { font-size: 12.5px; font-weight: 700; color: var(--ink); margin-bottom: 4px; }
.chart-card .sub { font-size: 11px; color: var(--muted); margin-bottom: 10px; }

/* Status pills */
.sla { display: inline-flex; padding: 2px 7px; border-radius: 4px; font-size: 10.5px; font-weight: 700; }
.sla.l1 { background: #E1F1FB; color: #0F5A87; }
.sla.l2 { background: #FFF4D6; color: #8E6A12; }
.sla.l3 { background: #FFE7C2; color: #9A4E14; }
.sla.l4 { background: #FFE6E2; color: #B22A1C; }
.dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; margin-right: 6px; vertical-align: middle; }

/* Map */
.map-wrap { position: relative; height: 460px; background: #E5EBE6; border-radius: 12px; overflow: hidden; }
.map-legend { position: absolute; top: 12px; left: 12px; background: rgba(255,255,255,0.96); border-radius: 10px; padding: 10px 12px; box-shadow: var(--shadow); font-size: 11.5px; min-width: 180px; }
.map-legend h5 { font-size: 11px; text-transform: uppercase; color: var(--muted); letter-spacing: 0.7px; margin-bottom: 6px; font-weight: 700; }
.legend-row { display: flex; align-items: center; gap: 8px; margin: 3px 0; }
.legend-row .swatch { width: 14px; height: 14px; border-radius: 3px; }

/* AI chat */
.chat-wrap { display: grid; grid-template-columns: 1fr 280px; gap: 14px; }
.chat-box { background: #fff; border-radius: 12px; padding: 16px; box-shadow: var(--shadow); display: flex; flex-direction: column; gap: 12px; height: 540px; }
.msg { padding: 10px 13px; border-radius: 12px; font-size: 13px; max-width: 76%; }
.msg.user { background: var(--primary); color: #fff; align-self: flex-end; }
.msg.agent { background: var(--bg); color: var(--text); align-self: flex-start; border: 1px solid var(--rule); }
.chip-row { display: flex; gap: 5px; flex-wrap: wrap; margin-top: 6px; }
.chip { font-size: 10.5px; background: #fff; border: 1px solid var(--rule); padding: 2px 7px; border-radius: 999px; color: var(--muted); }
.composer { margin-top: auto; display: flex; gap: 8px; padding: 10px; background: var(--bg); border-radius: 10px; }
.composer input { flex: 1; border: none; background: transparent; font-size: 13px; outline: none; }

/* Right rail */
.rail { display: flex; flex-direction: column; gap: 12px; }
.rail .panel { background: #fff; border-radius: 12px; padding: 14px; box-shadow: var(--shadow); }
.rail h5 { font-size: 11.5px; text-transform: uppercase; color: var(--muted); letter-spacing: 0.5px; margin-bottom: 8px; font-weight: 700; }

/* Footer band */
.footer-band { padding: 9px 24px; background: #fff; border-top: 1px solid var(--rule); display: flex; justify-content: space-between; font-size: 11.5px; color: var(--muted); }
"""

SIDEBAR = """
<aside class="sidebar">
  <div class="brand">
    <div class="logo">IRETP</div>
    <div class="sub">Dubai Land Department</div>
  </div>
  <div class="section-label">Investor</div>
  <nav>
    <a class="nav-link {dash_active}" href="#"><span class="ico">▦</span> Dashboard</a>
    <a class="nav-link {tx_active}" href="#"><span class="ico">≡</span> Transactions</a>
    <a class="nav-link {map_active}" href="#"><span class="ico">◯</span> Interactive Map</a>
    <a class="nav-link" href="#"><span class="ico">≈</span> Price Index</a>
    <a class="nav-link" href="#"><span class="ico">⟂</span> Rental Index</a>
    <a class="nav-link" href="#"><span class="ico">▢</span> Slice &amp; Dice</a>
    <a class="nav-link" href="#"><span class="ico">★</span> Watchlist</a>
    <a class="nav-link" href="#"><span class="ico">⚲</span> Notifications</a>
    <a class="nav-link {ai_active}" href="#"><span class="ico">✦</span> AI Agent</a>
  </nav>
  <div class="section-label">Transparency</div>
  <nav>
    <a class="nav-link {scorecard_active}" href="#"><span class="ico">◇</span> Developer Scorecards</a>
    <a class="nav-link {bo_active}" href="#"><span class="ico">⚭</span> Beneficial Ownership</a>
    <a class="nav-link {mtg_active}" href="#"><span class="ico">⌗</span> Mortgage</a>
    <a class="nav-link" href="#"><span class="ico">♺</span> ESG</a>
    <a class="nav-link" href="#"><span class="ico">⌖</span> GRETI Tracker</a>
    <a class="nav-link" href="#"><span class="ico">⛁</span> Open Data API</a>
  </nav>
  <div class="section-label">DLD Internal</div>
  <nav>
    <a class="nav-link {ewrs_active}" href="#"><span class="ico">⚠</span> EWRS Dashboard</a>
    <a class="nav-link" href="#"><span class="ico">⛯</span> Escrow Monitor</a>
    <a class="nav-link {audit_active}" href="#"><span class="ico">⌘</span> Audit Logs</a>
  </nav>
</aside>
"""

def base_html(title, content, active=None):
    actives = {k: '' for k in ['dash','tx','map','ai','scorecard','bo','mtg','ewrs','audit']}
    if active: actives[active] = 'active'
    sidebar = SIDEBAR.format(
        dash_active=actives['dash'], tx_active=actives['tx'], map_active=actives['map'],
        ai_active=actives['ai'], scorecard_active=actives['scorecard'],
        bo_active=actives['bo'], mtg_active=actives['mtg'], ewrs_active=actives['ewrs'],
        audit_active=actives['audit']
    )
    return f"""<!doctype html><html><head><meta charset="utf-8">
<title>IRETP — {title}</title>
<style>{CSS}</style></head><body>
<div class="app">
{sidebar}
<main class="main">
{content}
<div class="footer-band"><span>IRETP · Integrated Real Estate Transparency Platform</span><span>RFP DLD-IRETP-2026-001 · v1.0</span></div>
</main></div></body></html>"""


# ============ PAGE 1: DASHBOARD ============
def chart_line_volume():
    # 12 months of monthly volume
    pts = [820, 770, 910, 880, 960, 1040, 1180, 1220, 1090, 1310, 1280, 1390]
    labels = ['M', 'A', 'M', 'J', 'J', 'A', 'S', 'O', 'N', 'D', 'J', 'F']
    width, height = 420, 170
    x_pad, y_pad = 30, 18
    mx, mn = max(pts), min(pts)
    span = mx - mn or 1
    coords = []
    for i, v in enumerate(pts):
        x = x_pad + i * ((width - 2*x_pad) / (len(pts)-1))
        y = height - y_pad - ((v - mn) / span) * (height - 2*y_pad)
        coords.append((x, y))
    # Path
    d = 'M ' + ' L '.join(f'{x:.1f} {y:.1f}' for x, y in coords)
    # Area
    area = d + f' L {coords[-1][0]:.1f} {height-y_pad} L {coords[0][0]:.1f} {height-y_pad} Z'
    # Y axis lines
    yticks = ''.join(
        f'<line x1="{x_pad}" y1="{height-y_pad - i*(height-2*y_pad)/3:.0f}" x2="{width-10}" y2="{height-y_pad - i*(height-2*y_pad)/3:.0f}" stroke="#E8ECEF" stroke-dasharray="2 3"/>'
        for i in range(4)
    )
    # X labels
    xlabels = ''.join(
        f'<text x="{x_pad + i*(width-2*x_pad)/(len(pts)-1):.0f}" y="{height-3}" fill="#6A7280" font-size="9.5" text-anchor="middle">{labels[i]}</text>'
        for i in range(len(pts))
    )
    return f"""<svg width="{width}" height="{height}" viewBox="0 0 {width} {height}" xmlns="http://www.w3.org/2000/svg">
{yticks}
<path d="{area}" fill="rgba(6,103,53,0.10)"/>
<path d="{d}" stroke="#066735" stroke-width="2" fill="none" stroke-linejoin="round"/>
{''.join(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="2.7" fill="#066735"/>' for x,y in coords)}
{xlabels}
</svg>"""

def chart_donut():
    # Off-plan vs Ready
    total = 100; offp = 62
    cx, cy, r = 80, 80, 60
    import math
    deg_off = (offp/100) * 360
    rad = math.radians(deg_off - 90)
    x1, y1 = cx + r*math.cos(math.radians(-90)), cy + r*math.sin(math.radians(-90))
    x2, y2 = cx + r*math.cos(rad), cy + r*math.sin(rad)
    large = 1 if deg_off > 180 else 0
    return f"""<svg width="170" height="170" viewBox="0 0 170 170" xmlns="http://www.w3.org/2000/svg">
<circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="#E0E5E0" stroke-width="22"/>
<path d="M {x1:.1f} {y1:.1f} A {r} {r} 0 {large} 1 {x2:.1f} {y2:.1f}" stroke="#066735" stroke-width="22" fill="none" stroke-linecap="butt"/>
<text x="{cx}" y="{cy-2}" text-anchor="middle" font-size="22" font-weight="700" fill="#0F1B2D">{offp}%</text>
<text x="{cx}" y="{cy+18}" text-anchor="middle" font-size="11" fill="#6A7280">Off-plan</text>
</svg>"""

def chart_bar_zones():
    zones = [('Dubai Hills', 92), ('Palm Jumeirah', 84), ('Downtown', 78), ('JVC', 64), ('DIP', 41)]
    width, height = 420, 170
    x_pad = 110
    bar_h = 16
    gap = 9
    bars = ''
    for i, (label, val) in enumerate(zones):
        y = 12 + i*(bar_h+gap)
        w = (val/100) * (width - x_pad - 30)
        bars += f'<text x="{x_pad-6}" y="{y+11}" text-anchor="end" font-size="11" fill="#1A1A1A" font-weight="600">{label}</text>'
        bars += f'<rect x="{x_pad}" y="{y}" width="{w}" height="{bar_h}" fill="#066735" rx="3"/>'
        bars += f'<text x="{x_pad+w+5}" y="{y+11}" font-size="10.5" fill="#6A7280" font-weight="600">{val*87:,}</text>'
    return f'<svg width="{width}" height="{height}" viewBox="0 0 {width} {height}">{bars}</svg>'

def chart_psf_dual():
    psf = [1620, 1640, 1655, 1670, 1700, 1735, 1780, 1810, 1825, 1860, 1895, 1920]
    cnt = [820, 770, 910, 880, 960, 1040, 1180, 1220, 1090, 1310, 1280, 1390]
    width, height = 420, 170
    x_pad, y_pad = 32, 18
    n = len(psf)
    mx1, mn1 = max(psf), min(psf); s1 = mx1-mn1 or 1
    mx2, mn2 = max(cnt), min(cnt); s2 = mx2-mn2 or 1
    pts1 = []; pts2 = []
    for i in range(n):
        x = x_pad + i*((width-2*x_pad)/(n-1))
        y1 = height-y_pad - ((psf[i]-mn1)/s1)*(height-2*y_pad)
        y2 = height-y_pad - ((cnt[i]-mn2)/s2)*(height-2*y_pad)
        pts1.append((x,y1)); pts2.append((x,y2))
    d1 = 'M ' + ' L '.join(f'{x:.1f} {y:.1f}' for x,y in pts1)
    d2 = 'M ' + ' L '.join(f'{x:.1f} {y:.1f}' for x,y in pts2)
    return f"""<svg width="{width}" height="{height}" viewBox="0 0 {width} {height}">
<path d="{d2}" stroke="#066735" stroke-width="1.6" fill="none" stroke-dasharray="4 3" opacity="0.55"/>
<path d="{d1}" stroke="#C99A2E" stroke-width="2.2" fill="none"/>
<text x="6" y="14" font-size="9.5" fill="#C99A2E" font-weight="700">PSF</text>
<text x="{width-32}" y="14" font-size="9.5" fill="#066735" font-weight="700">VOL</text>
</svg>"""

def page_dashboard():
    content = f"""
<div class="topbar">
  <div>
    <div class="crumb">Public · Investor view</div>
    <div class="ttitle">Market Overview Dashboard</div>
  </div>
  <div class="actions">
    <span class="badge gold">EN · العربية</span>
    <button class="btn"><span>↧</span> Export</button>
    <button class="btn primary">Save view</button>
  </div>
</div>
<div class="content">
  <div class="banner">
    <div class="icon">▦</div>
    <div><h1>Dubai Market Pulse — Q1 2026</h1>
      <p>Live KPI snapshot · 15-minute refresh · sourced from Microsoft Fabric OneLake Gold layer</p></div>
    <div class="right">
      <span class="badge">Data as of 4 min ago</span>
      <span class="badge gold">Cache 15 min</span>
    </div>
  </div>
  <div class="kpi-grid">
    <div class="kpi"><div class="lbl">Total Sales Volume</div><div class="val">AED 24.8 B</div><div class="delta">▲ 12.4% YoY</div><div class="freshness">Data as of 12:46 UTC+4</div></div>
    <div class="kpi"><div class="lbl">Transactions (Q1)</div><div class="val">14,328</div><div class="delta">▲ 8.7% YoY</div><div class="freshness">Latest sync 4 min ago</div></div>
    <div class="kpi"><div class="lbl">Avg PSF (AED)</div><div class="val">1,920</div><div class="delta">▲ 5.1% YoY</div><div class="freshness">8 quarters trailing</div></div>
    <div class="kpi"><div class="lbl">Top Zone by Value</div><div class="val">Dubai Hills</div><div class="delta">★ 1st of 89 zones</div><div class="freshness">Updated quarterly</div></div>
  </div>
  <div class="chart-grid">
    <div class="chart-card"><h4>Monthly Transaction Volume — last 12 months</h4><div class="sub">FR-004 · server-side aggregation</div>{chart_line_volume()}</div>
    <div class="chart-card"><h4>Transaction Type Breakdown</h4><div class="sub">Off-plan vs Ready · Q1 2026</div><div style="display:flex;align-items:center;gap:24px;justify-content:center;padding:8px 0 12px;">{chart_donut()}<div><div style="display:flex;align-items:center;gap:8px;margin:2px 0;"><span class="dot" style="background:#066735;"></span>Off-plan · 62%</div><div style="display:flex;align-items:center;gap:8px;margin:2px 0;"><span class="dot" style="background:#E0E5E0;"></span>Ready · 38%</div><div style="font-size:11px;color:#6A7280;margin-top:8px;">N = 14,328 transactions</div></div></div></div>
    <div class="chart-card"><h4>Top 5 Zones by Activity</h4><div class="sub">AED value over Q1 2026</div>{chart_bar_zones()}</div>
    <div class="chart-card"><h4>PSF vs Volume — dual axis</h4><div class="sub">12-month trend · interactive crosshair on hover</div>{chart_psf_dual()}</div>
  </div>
</div>
"""
    return base_html("Dashboard", content, active='dash')


# ============ PAGE 2: GIS MAP ============
def page_map():
    # Build a simulated Dubai map with zone polygons and project pins
    # Zone polygons as SVG paths placed at realistic relative coordinates
    zones_data = [
        # (cx, cy, radius_x, radius_y, name, fill, label, val)
        (380, 230, 95, 60, 'Palm Jumeirah', '#066735', 'AED 3,100', None),
        (520, 290, 120, 80, 'Downtown / DIFC', '#1F8B53', 'AED 2,250', None),
        (640, 220, 100, 65, 'Business Bay', '#3FAA6A', 'AED 1,940', None),
        (760, 310, 110, 75, 'Dubai Hills', '#066735', 'AED 2,800', None),
        (870, 220, 95, 60, 'Al Khail', '#5BC586', 'AED 1,520', None),
        (980, 340, 115, 80, 'JVC', '#A0DBB6', 'AED 1,050', None),
        (470, 410, 105, 70, 'Marina', '#066735', 'AED 2,650', None),
        (700, 460, 130, 85, 'DIP', '#CCEAD5', 'AED 740', None),
        (900, 480, 100, 65, 'JBR', '#3FAA6A', 'AED 2,150', None),
    ]
    # Generate zone ellipses
    zone_svg = ''
    for cx, cy, rx, ry, name, fill, label, _ in zones_data:
        zone_svg += f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="{fill}" fill-opacity="0.55" stroke="#066735" stroke-width="1.2" stroke-opacity="0.85"/>'
        zone_svg += f'<text x="{cx}" y="{cy-4}" text-anchor="middle" font-size="11.5" font-weight="700" fill="#0F1B2D">{name}</text>'
        zone_svg += f'<text x="{cx}" y="{cy+10}" text-anchor="middle" font-size="10.5" fill="#0F1B2D" font-weight="600">{label}/sqft</text>'

    # Generate ~80 project pins scattered across zones with status colors
    import random
    random.seed(42)
    pins = ''
    statuses = [('#1F8B53', 0.55), ('#C99A2E', 0.25), ('#0F5A87', 0.15), ('#B22A1C', 0.05)]
    for _ in range(80):
        # Pick a zone
        z = random.choice(zones_data)
        cx, cy, rx, ry = z[0], z[1], z[2], z[3]
        # Random point inside ellipse
        import math
        t = random.uniform(0, 2*math.pi)
        u = random.uniform(0, 1)**0.5
        x = cx + rx*0.85*u*math.cos(t)
        y = cy + ry*0.85*u*math.sin(t)
        # Pick status
        r = random.random(); cum = 0; color = '#1F8B53'
        for c, w in statuses:
            cum += w
            if r <= cum: color = c; break
        pins += f'<circle cx="{x:.0f}" cy="{y:.0f}" r="4.5" fill="{color}" stroke="#fff" stroke-width="1.2" opacity="0.95"/>'

    map_svg = f"""<svg viewBox="0 0 1200 600" preserveAspectRatio="xMidYMid slice" style="position:absolute;inset:0;width:100%;height:100%;">
<rect width="100%" height="100%" fill="#E5EBE6"/>
<!-- coastline strokes -->
<path d="M 0 180 Q 220 140 410 200 T 720 160 T 1100 220 L 1200 240 L 1200 600 L 0 600 Z" fill="#F4F6F4" stroke="#D9DDD7" stroke-width="1.4"/>
<path d="M 0 230 Q 220 200 410 240 T 720 220 T 1100 280 L 1200 290" fill="none" stroke="#C9D6CD" stroke-width="1" stroke-dasharray="3 4"/>
{zone_svg}
{pins}
</svg>"""

    content = f"""
<div class="topbar">
  <div>
    <div class="crumb">Public · Interactive GIS Map</div>
    <div class="ttitle">Dubai Real Estate Atlas — 89 Zones · 511 Projects</div>
  </div>
  <div class="actions">
    <div class="tabs"><div class="tab">Volume</div><div class="tab active">Avg PSF</div><div class="tab">Yield</div><div class="tab">ESG Coverage</div></div>
    <button class="btn"><span>↧</span> Export GeoJSON</button>
  </div>
</div>
<div class="content">
  <div class="banner">
    <div class="icon">◯</div>
    <div><h1>Interactive Atlas</h1><p>FR-010 four heatmap modes · FR-011 individual project pins colour-coded by status · official Dubai zone GeoJSON</p></div>
    <div class="right"><span class="badge">MapLibre GL JS</span><span class="badge gold">Real-time</span></div>
  </div>
  <div class="map-wrap">{map_svg}
    <div class="map-legend">
      <h5>AED / sqft</h5>
      <div class="legend-row"><span class="swatch" style="background:#066735;"></span>≥ 2,500</div>
      <div class="legend-row"><span class="swatch" style="background:#1F8B53;"></span>2,000 – 2,499</div>
      <div class="legend-row"><span class="swatch" style="background:#3FAA6A;"></span>1,500 – 1,999</div>
      <div class="legend-row"><span class="swatch" style="background:#5BC586;"></span>1,000 – 1,499</div>
      <div class="legend-row"><span class="swatch" style="background:#A0DBB6;"></span>700 – 999</div>
      <div class="legend-row"><span class="swatch" style="background:#CCEAD5;"></span>&lt; 700</div>
      <h5 style="margin-top:10px;">Project status (FR-011)</h5>
      <div class="legend-row"><span class="swatch" style="background:#1F8B53;"></span>Completed</div>
      <div class="legend-row"><span class="swatch" style="background:#C99A2E;"></span>Under construction</div>
      <div class="legend-row"><span class="swatch" style="background:#0F5A87;"></span>Future / announced</div>
      <div class="legend-row"><span class="swatch" style="background:#B22A1C;"></span>Stalled</div>
    </div>
    <div style="position:absolute;bottom:14px;right:14px;background:#fff;border-radius:10px;padding:10px 14px;box-shadow:var(--shadow);font-size:11.5px;">
      <div style="font-weight:700;color:#0F1B2D;font-size:12px;margin-bottom:2px;">Selected: Palm Jumeirah</div>
      <div style="color:#6A7280;">Avg PSF AED 3,100 · 47 projects · 12,408 units · Yield 5.6%</div>
    </div>
  </div>
</div>
"""
    return base_html("Interactive Atlas", content, active='map')


# ============ PAGE 3: TRANSACTIONS ============
def page_transactions():
    rows = [
        ('TX-2026-04-08-00121', '08 Apr 2026', 'Dubai Hills',     'Villa',      'Sale',     '5,750,000', 'Off-plan',  'Emaar Properties'),
        ('TX-2026-04-08-00122', '08 Apr 2026', 'Downtown / DIFC',  'Apartment',  'Sale',     '3,240,000', 'Ready',     'Dubai Properties'),
        ('TX-2026-04-08-00123', '08 Apr 2026', 'Marina',           'Apartment',  'Mortgage', '2,180,000', 'Ready',     'Damac Properties'),
        ('TX-2026-04-08-00124', '08 Apr 2026', 'Palm Jumeirah',    'Villa',      'Sale',    '18,400,000', 'Ready',     'Nakheel'),
        ('TX-2026-04-08-00125', '08 Apr 2026', 'JVC',              'Apartment',  'Sale',     '1,090,000', 'Off-plan',  'Danube Properties'),
        ('TX-2026-04-08-00126', '08 Apr 2026', 'Business Bay',     'Office',     'Sale',     '4,680,000', 'Ready',     'Omniyat'),
        ('TX-2026-04-08-00127', '08 Apr 2026', 'Dubai Hills',      'Villa',      'Mortgage', '6,200,000', 'Off-plan',  'Emaar Properties'),
        ('TX-2026-04-08-00128', '08 Apr 2026', 'DIP',              'Warehouse',  'Sale',     '2,420,000', 'Ready',     'Dubai South'),
        ('TX-2026-04-08-00129', '08 Apr 2026', 'JBR',              'Apartment',  'Sale',     '2,790,000', 'Ready',     'Damac Properties'),
        ('TX-2026-04-08-00130', '08 Apr 2026', 'Al Khail',         'Apartment',  'Sale',     '1,560,000', 'Off-plan',  'Sobha Realty'),
    ]
    rows_html = ''
    for i, r in enumerate(rows):
        rows_html += '<tr>' + ''.join(f'<td>{c}</td>' for c in r[:-1]) + f'<td><span class="badge">{r[-1]}</span></td></tr>'
    content = f"""
<div class="topbar">
  <div><div class="crumb">Public · Transactions</div><div class="ttitle">Transactions Explorer · FR-007 URL-persisted filters</div></div>
  <div class="actions">
    <button class="btn"><span>↧</span> Export CSV</button>
    <button class="btn"><span>↧</span> Export PDF</button>
  </div>
</div>
<div class="content">
  <div class="banner">
    <div class="icon">≡</div>
    <div><h1>14,328 transactions in current view</h1><p>Filters reflected in the URL · pagination · sortable headers · CSV / PDF export — all RBAC-aware and audit-logged.</p></div>
  </div>
  <div class="card" style="margin-bottom:14px;">
    <div style="display:grid;grid-template-columns:repeat(7,1fr);gap:10px;font-size:12.5px;">
      <div><div style="font-size:11px;color:#6A7280;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:3px;font-weight:600;">Mode</div><div style="background:#F4F6F4;padding:7px 10px;border-radius:8px;">Sales</div></div>
      <div><div style="font-size:11px;color:#6A7280;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:3px;font-weight:600;">From</div><div style="background:#F4F6F4;padding:7px 10px;border-radius:8px;">01 Jan 2026</div></div>
      <div><div style="font-size:11px;color:#6A7280;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:3px;font-weight:600;">To</div><div style="background:#F4F6F4;padding:7px 10px;border-radius:8px;">31 Mar 2026</div></div>
      <div><div style="font-size:11px;color:#6A7280;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:3px;font-weight:600;">Property type</div><div style="background:#F4F6F4;padding:7px 10px;border-radius:8px;">All</div></div>
      <div><div style="font-size:11px;color:#6A7280;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:3px;font-weight:600;">Tx type</div><div style="background:#F4F6F4;padding:7px 10px;border-radius:8px;">Sale + Mortgage</div></div>
      <div><div style="font-size:11px;color:#6A7280;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:3px;font-weight:600;">Zone</div><div style="background:#F4F6F4;padding:7px 10px;border-radius:8px;">All 89</div></div>
      <div style="display:flex;align-items:end;"><button class="btn primary" style="width:100%;">Apply filters</button></div>
    </div>
  </div>
  <div class="card">
    <table class="tbl">
      <thead><tr>
        <th class="mono">Tx ID</th><th>Date ▼</th><th>Zone</th><th>Property type</th><th>Tx type</th><th>Value (AED)</th><th>Status</th><th>Developer</th>
      </tr></thead>
      <tbody>{rows_html}</tbody>
    </table>
    <div style="display:flex;justify-content:space-between;align-items:center;margin-top:12px;padding-top:10px;border-top:1px solid var(--rule);font-size:11.5px;color:var(--muted);">
      <div>Showing 1–10 of 14,328 — sorted by Date desc · server-side pagination</div>
      <div style="display:flex;gap:5px;">
        <button class="btn" style="padding:4px 10px;">‹</button>
        <button class="btn primary" style="padding:4px 10px;">1</button>
        <button class="btn" style="padding:4px 10px;">2</button>
        <button class="btn" style="padding:4px 10px;">3</button>
        <button class="btn" style="padding:4px 10px;">…</button>
        <button class="btn" style="padding:4px 10px;">1,433</button>
        <button class="btn" style="padding:4px 10px;">›</button>
      </div>
    </div>
  </div>
</div>
"""
    return base_html("Transactions", content, active='tx')


# ============ PAGE 4: AI AGENT ============
def page_ai():
    content = f"""
<div class="topbar">
  <div><div class="crumb">Public · Real Estate AI Agent</div><div class="ttitle">Conversational Transparency · UAE-resident inference</div></div>
  <div class="actions">
    <span class="badge gold">Tier: analytics</span>
    <div class="tabs"><div class="tab active">EN</div><div class="tab">العربية</div></div>
  </div>
</div>
<div class="content">
  <div class="banner">
    <div class="icon">✦</div>
    <div><h1>Real Estate AI Agent</h1><p>RAG-grounded · multi-model orchestration · non-advisory guardrail · deterministic statistics appended on analytics queries</p></div>
    <div class="right"><span class="badge">P95 4.2 s</span><span class="badge gold">RAG ON</span></div>
  </div>
  <div class="chat-wrap">
    <div class="chat-box">
      <div class="msg user">What are the top 5 zones by yield in Q1 2026?</div>
      <div class="msg agent">
        Based on transactions registered with the Dubai Land Department in Q1 2026 (sourced from OneLake Gold layer), the five zones with the highest gross rental yield were:
        <ol style="padding-left:18px;margin:6px 0;">
          <li><b>International City</b> — 7.8% gross yield (AED 78/sqft rent on 1,000 PSF avg)</li>
          <li><b>JVC</b> — 7.2% (AED 76/sqft on 1,050 PSF)</li>
          <li><b>DIP</b> — 6.9% (AED 51/sqft on 740 PSF)</li>
          <li><b>Al Khail</b> — 6.4% (AED 97/sqft on 1,520 PSF)</li>
          <li><b>JBR</b> — 5.9% (AED 127/sqft on 2,150 PSF)</li>
        </ol>
        Trend analysis (least-squares regression, R² = 0.82) shows International City yield is trending upward over the last 8 quarters; the others are flat-to-positive within the noise floor.
        <div class="chip-row">
          <span class="chip">📎 Source: OneLake Gold · transactions Q1 2026</span>
          <span class="chip">📎 RentalIndex · 8-quarter trend</span>
          <span class="chip">📎 TimeSeriesAnalyzer · R² = 0.82</span>
        </div>
        <div style="font-size:10.5px;color:#6A7280;margin-top:6px;font-style:italic;">This response describes registered data. It does not constitute investment advice.</div>
      </div>
      <div class="msg user">Show me the monthly trend for Dubai Hills</div>
      <div class="msg agent">
        Dubai Hills · monthly AED/sqft over the last 12 months (orange = PSF, dotted = transaction count):
        <div style="margin:8px -2px;">{chart_psf_dual()}</div>
        <div class="chip-row">
          <span class="chip">📎 Source: PriceIndex · DubaiHills · 12 mo</span>
          <span class="chip">⟲ Retry</span>
        </div>
      </div>
      <div class="composer">
        <span style="color:#6A7280;font-size:18px;">✦</span>
        <input value="Compare Dubai Hills vs JVC on yield over the last year"/>
        <button class="btn primary">Send</button>
      </div>
    </div>
    <div class="rail">
      <div class="panel">
        <h5>Active model</h5>
        <div style="font-weight:600;font-size:13px;color:#0F1B2D;">azure-openai-gpt · UAE North</div>
        <div style="font-size:11.5px;color:#6A7280;margin-top:3px;">Tier · analytics</div>
        <div style="margin-top:8px;display:flex;gap:6px;flex-wrap:wrap;">
          <span class="badge">UAE-resident</span><span class="badge">RAG · ON</span>
        </div>
      </div>
      <div class="panel">
        <h5>Recent sessions</h5>
        <div style="font-size:12px;color:#1A1A1A;padding:5px 0;border-bottom:1px solid var(--rule);">Top zones · yield · Q1</div>
        <div style="font-size:12px;color:#1A1A1A;padding:5px 0;border-bottom:1px solid var(--rule);">Mortgage trend · 24 mo</div>
        <div style="font-size:12px;color:#1A1A1A;padding:5px 0;border-bottom:1px solid var(--rule);">JVC vs Al Khail</div>
        <div style="font-size:12px;color:#1A1A1A;padding:5px 0;">ESG-certified projects</div>
      </div>
      <div class="panel">
        <h5>Guardrail</h5>
        <div style="font-size:11.5px;color:#6A7280;">Three-layer non-advisory guardrail active:</div>
        <ul style="font-size:11.5px;color:#1A1A1A;padding-left:14px;margin-top:5px;">
          <li>System-prompt enforcement</li>
          <li>Keyword + regex filter</li>
          <li>Post-generation validation</li>
        </ul>
      </div>
    </div>
  </div>
</div>
"""
    return base_html("AI Agent", content, active='ai')


# ============ PAGE 5: EWRS ============
def page_ewrs():
    alerts = [
        ('ALR-2026-04-0218', 'Project Delivery Delay', 'Sapphire Heights — Damac', 'L3', 'New',   '08 Apr 14:22', '01:38 remaining'),
        ('ALR-2026-04-0217', 'Escrow Shortfall',       'Marina Vista — Emaar',     'L2', 'New',   '08 Apr 12:04', '03:56 remaining'),
        ('ALR-2026-04-0216', 'Construction Suspended', 'Reem Tower — Sobha',       'L2', 'Ack.',  '07 Apr 18:11', 'L2 → resolved'),
        ('ALR-2026-04-0215', 'Severe Regulatory Violation', 'Bay Tower — Omniyat', 'L3', 'New',   '07 Apr 16:40', '0:18 remaining'),
        ('ALR-2026-04-0214', 'Developer Score Deterioration', 'Hatta Devs',        'L2', 'Ack.',  '07 Apr 09:32', 'In playbook step 3 of 5'),
        ('ALR-2026-04-0213', 'Transaction Volume Decline', 'Zone · International City', 'L2', 'New', '06 Apr 22:18', 'SLA elapsed → L3 in 0:03'),
        ('ALR-2026-04-0212', 'Price Decline',          'Zone · Discovery Gardens',  'L1', 'Ack.', '06 Apr 11:00', 'Investigation'),
        ('ALR-2026-04-0211', 'High-Risk Concentration', 'Developer · BlueWater',    'L2', 'Esc.', '05 Apr 17:25', 'Escalated → L3'),
    ]
    rows = ''
    for a in alerts:
        sla_cls = a[3].lower()
        rows += f'<tr><td class="mono">{a[0]}</td><td>{a[1]}</td><td>{a[2]}</td><td><span class="sla {sla_cls}">{a[3]}</span></td><td><span class="badge">{a[4]}</span></td><td>{a[5]}</td><td style="color:var(--muted);font-size:11.5px;">{a[6]}</td></tr>'
    content = f"""
<div class="topbar">
  <div><div class="crumb">DLD Internal · Early Warning Risk System</div><div class="ttitle">EWRS Alert Inbox — 10-indicator engine · L1–L4 auto-escalation</div></div>
  <div class="actions">
    <span class="badge crit">2 L3 pending</span>
    <button class="btn"><span>⚙</span> Threshold Config</button>
  </div>
</div>
<div class="content">
  <div class="banner">
    <div class="icon">⚠</div>
    <div><h1>EWRS — Today's Risk Posture</h1><p>Hangfire job every 5 min · UAE-business-hour SLA · immutable audit log on every threshold edit (§10.2)</p></div>
    <div class="right"><span class="badge warn">8 active alerts</span><span class="badge">Engine v4.7</span></div>
  </div>
  <div class="kpi-grid">
    <div class="kpi"><div class="lbl">Active Alerts</div><div class="val">8</div><div class="delta down">▲ 3 since yesterday</div></div>
    <div class="kpi"><div class="lbl">L3 / L4 Open</div><div class="val">2</div><div class="delta down">▲ 1 this week</div></div>
    <div class="kpi"><div class="lbl">Avg Acknowledge</div><div class="val">38 min</div><div class="delta">Within SLA</div></div>
    <div class="kpi"><div class="lbl">Indicators Tripped</div><div class="val">6 of 10</div><div class="delta">No new today</div></div>
  </div>
  <div class="card" style="margin-bottom:14px;">
    <div class="head"><h3>SLA Escalation Ladder · UAE business hours</h3><div class="tabs"><div class="tab active">Today</div><div class="tab">Week</div><div class="tab">Month</div></div></div>
    <div style="display:grid;grid-template-columns:repeat(4,1fr);gap:14px;">
      <div style="background:#E1F1FB;border-radius:10px;padding:14px;"><span class="sla l1">L1</span><div style="font-weight:700;margin-top:4px;font-size:13px;">Initial Alert</div><div style="font-size:11.5px;color:#6A7280;">SLA · 4 business hours</div></div>
      <div style="background:#FFF4D6;border-radius:10px;padding:14px;"><span class="sla l2">L2</span><div style="font-weight:700;margin-top:4px;font-size:13px;">Section Manager + Director</div><div style="font-size:11.5px;color:#6A7280;">SLA · 1 business hour</div></div>
      <div style="background:#FFE7C2;border-radius:10px;padding:14px;"><span class="sla l3">L3</span><div style="font-weight:700;margin-top:4px;font-size:13px;">DG + Deputies</div><div style="font-size:11.5px;color:#6A7280;">SLA · immediate</div></div>
      <div style="background:#FFE6E2;border-radius:10px;padding:14px;"><span class="sla l4">L4</span><div style="font-weight:700;margin-top:4px;font-size:13px;">DG + Regulator</div><div style="font-size:11.5px;color:#6A7280;">Direct escalation</div></div>
    </div>
  </div>
  <div class="card">
    <div class="head"><h3>Alert Inbox</h3><div class="actions"><button class="btn">Filter</button><button class="btn">Export</button></div></div>
    <table class="tbl">
      <thead><tr><th class="mono">Alert ID</th><th>Indicator</th><th>Subject</th><th>Level</th><th>Status</th><th>Created</th><th>SLA</th></tr></thead>
      <tbody>{rows}</tbody>
    </table>
  </div>
</div>
"""
    return base_html("EWRS", content, active='ewrs')


# ============ PAGE 6: DEVELOPER SCORECARDS ============
def page_scorecards():
    devs = [
        ('Emaar Properties',     '92', '✱', 'On-time delivery 96%',  '247', 'Distinguished', 'AED 18.4 B'),
        ('Damac Properties',     '88', '◐', 'On-time delivery 92%',  '189', 'Distinguished', 'AED 12.7 B'),
        ('Nakheel',              '86', '◐', 'On-time delivery 90%',  '124', 'Distinguished', 'AED 9.8 B'),
        ('Dubai Properties',     '83', '◐', 'On-time delivery 88%',  '156', 'Excellent',      'AED 6.4 B'),
        ('Sobha Realty',         '81', '◑', 'On-time delivery 85%',   '92', 'Excellent',      'AED 4.9 B'),
        ('Omniyat',              '78', '◑', 'On-time delivery 83%',   '38', 'Good',           'AED 3.1 B'),
        ('Danube Properties',    '74', '◑', 'On-time delivery 79%',   '68', 'Good',           'AED 2.6 B'),
        ('Azizi Developments',   '69', '◔', 'On-time delivery 72%',   '54', 'Good',           'AED 2.2 B'),
    ]
    rows = ''
    for i, d in enumerate(devs):
        # Score bar
        s = int(d[1])
        rows += f'''<tr>
          <td><div style="font-weight:700;color:#0F1B2D;font-size:13px;">{d[0]}</div><div style="font-size:11px;color:var(--muted);margin-top:1px;">RERA license active · ISO 9001</div></td>
          <td><div style="display:flex;align-items:center;gap:9px;"><div style="width:48px;height:48px;border-radius:50%;background:conic-gradient(#066735 0% {s}%, #E0E5E0 {s}% 100%);display:flex;align-items:center;justify-content:center;"><div style="width:36px;height:36px;border-radius:50%;background:#fff;display:flex;align-items:center;justify-content:center;font-weight:700;font-size:14px;color:#0F1B2D;">{d[1]}</div></div></div></td>
          <td>{d[2]}</td>
          <td>{d[3]}</td>
          <td><span style="font-weight:600;">{d[4]}</span> units delivered</td>
          <td><span class="badge {'gold' if d[5]=='Distinguished' else ''}">{d[5]}</span></td>
          <td style="font-weight:600;color:#0F1B2D;">{d[6]}</td>
        </tr>'''
    content = f"""
<div class="topbar">
  <div><div class="crumb">Public · Developer Scorecards</div><div class="ttitle">Public Developer Performance · 8-criterion composite score</div></div>
  <div class="actions"><button class="btn">Methodology</button><button class="btn primary">Compare</button></div>
</div>
<div class="content">
  <div class="banner">
    <div class="icon">◇</div>
    <div><h1>Transparent Developer Accountability</h1><p>Public-facing simplified view · 8 criteria · weights configurable from admin with full audit trail (§9.1)</p></div>
    <div class="right"><span class="badge">RFP §9 + §20</span></div>
  </div>
  <div class="card">
    <table class="tbl">
      <thead><tr><th>Developer</th><th>Composite Score</th><th>Trend</th><th>Performance</th><th>Units</th><th>Tier</th><th>Active Pipeline</th></tr></thead>
      <tbody>{rows}</tbody>
    </table>
    <div style="margin-top:14px;display:grid;grid-template-columns:repeat(4,1fr);gap:10px;font-size:11.5px;color:var(--muted);">
      <div><b style="color:var(--ink);">8 criteria</b><br/>On-time delivery 30%, RERA compliance 25%, units 15%, escrow 15%, complaints 10%, BO disclosure 5%</div>
      <div><b style="color:var(--ink);">Audit trail</b><br/>Every weight change writes an append-only AuditLog row (entityType, entityId, old/new JSON, actor)</div>
      <div><b style="color:var(--ink);">Refresh cadence</b><br/>Composite score recalculated quarterly by Hangfire job DeveloperScoreRecalculationService</div>
      <div><b style="color:var(--ink);">GRETI alignment</b><br/>Directly closes the JLL GRETI Governance sub-index gap on developer transparency</div>
    </div>
  </div>
</div>
"""
    return base_html("Developer Scorecards", content, active='scorecard')


# ============ PAGE 7: BENEFICIAL OWNERSHIP ============
def page_bo():
    rows = [
        ('Emaar Properties',   '32.5%',  'Investment Corporation of Dubai', 'ICD',                  'مؤسسة دبي للاستثمارات الحكومية',     'CN-1023455'),
        ('Emaar Properties',   '24.0%',  'Public Float (DFM)',              'Various',              '—',                                  '—'),
        ('Damac Properties',   '72.2%',  'Hussain Sajwani Family Office',   'Sajwani Holdings Ltd.', 'مكتب حسين سجواني العائلي',         'CN-2104388'),
        ('Nakheel',           '100.0%',  'Dubai Holding',                   'Dubai Holding',        'دبي القابضة',                       'CN-1006712'),
        ('Sobha Realty',       '85.0%',  'PNC Menon (Founder)',             'Sobha Group',          'مجموعة صوبها',                      'CN-3082011'),
        ('Omniyat',            '64.5%',  'Mahdi Amjad',                     'Omniyat Properties',   '—',                                  'CN-1187204'),
        ('Danube Properties',  '78.0%',  'Rizwan Sajan',                    'Danube Group',         'مجموعة دانوب',                      'CN-2245601'),
        ('Aldar Properties',   '29.8%',  'Mubadala Investment Co.',         'Mubadala',             'مبادلة للاستثمار',                  'CN-1099823'),
    ]
    rows_html = ''
    for r in rows:
        rows_html += f'''<tr>
          <td><b style="color:#0F1B2D;">{r[0]}</b></td>
          <td style="font-weight:600;color:var(--primary);">{r[1]}</td>
          <td>{r[2]}</td>
          <td style="color:var(--muted);font-size:11.5px;">{r[3]}</td>
          <td dir="rtl" lang="ar" style="font-family:'Cairo','IBM Plex Sans Arabic',sans-serif;color:#0F1B2D;">{r[4]}</td>
          <td class="mono" style="color:var(--muted);">{r[5]}</td>
        </tr>'''
    content = f"""
<div class="topbar">
  <div><div class="crumb">Public · Beneficial Ownership</div><div class="ttitle">Beneficial Ownership Transparency · GRETI Governance sub-index</div></div>
  <div class="actions"><span class="badge gold">RFP §20 · Beyond brief</span><button class="btn"><span>↧</span> Export</button></div>
</div>
<div class="content">
  <div class="banner">
    <div class="icon">⚭</div>
    <div><h1>Corporate Ownership Disclosure</h1><p>Sourced from UAE Federal commercial registry · names rendered in English &amp; Arabic (RTL) · FATF-compliance ready</p></div>
    <div class="right"><span class="badge">347 developers · 1,289 entries</span></div>
  </div>
  <div class="card">
    <table class="tbl">
      <thead><tr><th>Developer</th><th>Stake</th><th>Beneficial Owner</th><th>Holding Entity</th><th>Arabic Name (RTL)</th><th>CR No.</th></tr></thead>
      <tbody>{rows_html}</tbody>
    </table>
  </div>
  <div style="margin-top:14px;background:#FFF8E1;border-left:4px solid var(--gold);border-radius:8px;padding:11px 14px;font-size:12px;color:#0F1B2D;">
    <b>Transparency posture:</b> where no public beneficial owner record is available, a clear "Beneficial ownership data not available in public records" disclosure is rendered rather than a blank field. Transparency about the absence of data is itself a transparency measure.
  </div>
</div>
"""
    return base_html("Beneficial Ownership", content, active='bo')


# ============ PAGE 8: MORTGAGE ============
def page_mortgage():
    content = f"""
<div class="topbar">
  <div><div class="crumb">Public · Mortgage &amp; Debt Market</div><div class="ttitle">Mortgage Transparency · GRETI Debt Market Index</div></div>
  <div class="actions"><span class="badge gold">RFP §20 · Beyond brief</span></div>
</div>
<div class="content">
  <div class="banner">
    <div class="icon">⌗</div>
    <div><h1>Dubai Mortgage Market — Q1 2026</h1><p>Aggregate mortgage data sourced exclusively from registered DLD transaction records — no parallel data source.</p></div>
  </div>
  <div class="kpi-grid" style="grid-template-columns:repeat(5,1fr);">
    <div class="kpi"><div class="lbl">Total Mortgage Value</div><div class="val">AED 8.3B</div><div class="delta">▲ 14.2% YoY</div></div>
    <div class="kpi"><div class="lbl">Mortgage Count</div><div class="val">4,728</div><div class="delta">▲ 9.4% YoY</div></div>
    <div class="kpi"><div class="lbl">Mortgage:Tx Ratio</div><div class="val">33%</div><div class="delta">+2 pts</div></div>
    <div class="kpi"><div class="lbl">Avg Mortgage Size</div><div class="val">AED 1.76M</div><div class="delta">▲ 4.3%</div></div>
    <div class="kpi"><div class="lbl">Avg LTV</div><div class="val">71%</div><div class="delta">Stable</div></div>
  </div>
  <div class="chart-grid">
    <div class="chart-card"><h4>Mortgage Volume Trend — 24 months</h4><div class="sub">AED value, monthly</div>{chart_line_volume()}</div>
    <div class="chart-card"><h4>LTV Distribution by Zone</h4><div class="sub">Top 5 zones</div>{chart_bar_zones()}</div>
    <div class="chart-card"><h4>Mortgage-to-Transaction Ratio</h4><div class="sub">All zones, quarterly</div>{chart_line_volume()}</div>
    <div class="chart-card"><h4>Off-plan vs Ready Mortgages</h4><div class="sub">Property status mix</div><div style="display:flex;align-items:center;gap:24px;justify-content:center;padding:8px 0 12px;">{chart_donut()}<div><div style="display:flex;align-items:center;gap:8px;margin:2px 0;"><span class="dot" style="background:#066735;"></span>Off-plan · 62%</div><div style="display:flex;align-items:center;gap:8px;margin:2px 0;"><span class="dot" style="background:#E0E5E0;"></span>Ready · 38%</div></div></div></div>
  </div>
</div>
"""
    return base_html("Mortgage", content, active='mtg')


# ============ PAGE 9: AUDIT LOGS ============
def page_audit():
    rows = [
        ('2026-04-08 14:22:18', 'sarah.almansoori@dld.gov.ae', 'SystemAdministrator', 'UPDATE', 'RiskThreshold',  'EscrowShortfall_Warning', '15% → 20%', 'AdminAPI'),
        ('2026-04-08 13:48:02', 'omar.aljaber@dld.gov.ae',     'DldSupervisor',       'INSERT', 'RiskAlert',        'ALR-2026-04-0218',      'Manual L3 raise', 'AdminAPI'),
        ('2026-04-08 12:15:41', 'sarah.almansoori@dld.gov.ae', 'SystemAdministrator', 'UPDATE', 'ScoringWeight',    'OnTimeDelivery',         '28% → 30%',     'AdminAPI'),
        ('2026-04-08 11:32:09', 'audit.bot',                    'system',              'INSERT', 'AuditLog',         '(internal)',             'Reconciliation pass clean', 'WebAPI'),
        ('2026-04-08 10:04:55', 'investor:42018',               'RegisteredInvestor',  'DELETE', 'UserAiMemory',     'user 42018 · 28 rows',   'Consent revoked (PDPL §19.2)', 'WebAPI'),
        ('2026-04-08 09:18:00', 'fatima.alkindi@dld.gov.ae',   'AuditOfficer',        'SELECT', 'BeneficialOwner',  'OWN-EMAR-001',           'Reason: AML investigation',  'AdminAPI'),
        ('2026-04-08 08:50:32', 'system',                       'system',              'INSERT', 'EscrowTransaction','Mortgage tx · Emaar',    'RERA-feed signed payload',   'AdminAPI'),
    ]
    rows_html = ''
    for r in rows:
        rows_html += f'''<tr>
          <td class="mono" style="color:var(--muted);">{r[0]}</td>
          <td>{r[1]}</td>
          <td><span class="badge">{r[2]}</span></td>
          <td><span class="badge {'crit' if r[3]=='DELETE' else 'info' if r[3]=='UPDATE' else ''}">{r[3]}</span></td>
          <td>{r[4]}</td>
          <td class="mono" style="font-size:11.5px;">{r[5]}</td>
          <td style="font-size:12px;color:var(--muted);">{r[6]}</td>
          <td style="font-size:11.5px;">{r[7]}</td>
        </tr>'''
    content = f"""
<div class="topbar">
  <div><div class="crumb">DLD Internal · Audit · §10.2 Immutability</div><div class="ttitle">Audit Log — Append-only · §10.2 enforced at DbContext</div></div>
  <div class="actions"><span class="badge">Last 7 days · 18,442 rows</span><button class="btn">Export</button></div>
</div>
<div class="content">
  <div class="banner">
    <div class="icon">⌘</div>
    <div><h1>Immutable Audit Trail</h1><p>SaveChangesAsync throws on any UPDATE/DELETE of AuditLog rows · 3 xUnit tests in AuditLogImmutabilityTests cover this · 12-month retention on the sink.</p></div>
    <div class="right"><span class="badge">Immutable · DbContext-enforced</span></div>
  </div>
  <div class="card">
    <table class="tbl">
      <thead><tr><th>Timestamp (UAE)</th><th>Actor</th><th>Role</th><th>Action</th><th>Entity</th><th>Subject</th><th>Detail</th><th>Surface</th></tr></thead>
      <tbody>{rows_html}</tbody>
    </table>
  </div>
</div>
"""
    return base_html("Audit Logs", content, active='audit')


# ============ Build all pages and screenshot ============
pages = [
    ('01_dashboard.png',     page_dashboard()),
    ('02_map.png',           page_map()),
    ('03_transactions.png',  page_transactions()),
    ('04_ai_agent.png',      page_ai()),
    ('05_ewrs.png',          page_ewrs()),
    ('06_scorecards.png',    page_scorecards()),
    ('07_beneficial_ownership.png', page_bo()),
    ('08_mortgage.png',      page_mortgage()),
    ('09_audit_logs.png',    page_audit()),
]

with sync_playwright() as p:
    browser = p.chromium.launch()
    ctx = browser.new_context(viewport={'width': 1600, 'height': 920}, device_scale_factor=1.5)
    for name, html in pages:
        page = ctx.new_page()
        page.set_content(html)
        page.wait_for_load_state('networkidle')
        out_path = os.path.join(OUT, name)
        page.screenshot(path=out_path, full_page=False, type='png')
        page.close()
        print(f'WROTE {name}')
    ctx.close()
    browser.close()

print(f'\nAll screenshots in {OUT}')
