# Generates IRETP_Compliance_Search.html — a single self-contained searchable
# webpage over docs/COMPLIANCE_MATRIX.md (same idea as IRETP_Searchable_Pack.html
# but for the requirement-by-requirement compliance matrix).
#
# Usage:  python tools/build_compliance_search.py
# Output: C:\Users\kalmi\Downloads\IRETP_Compliance_Search.html

import html
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SRC = REPO / "docs" / "COMPLIANCE_MATRIX.md"
OUT = Path.home() / "Downloads" / "IRETP_Compliance_Search.html"


def inline(text: str) -> str:
    """Escape HTML, then apply markdown inline formatting."""
    t = html.escape(text, quote=False)
    t = re.sub(r"`([^`]+)`", r"<code>\1</code>", t)
    t = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", t)
    t = re.sub(r"(?<!\w)\*([^*]+)\*(?!\w)", r"<em>\1</em>", t)
    return t


def split_row(line: str) -> list[str]:
    # split on | but honour \| escapes used in the matrix (e.g. xlsx\|csv\|pdf)
    cells = [p.replace("\\|", "|").strip() for p in re.split(r"(?<!\\)\|", line.strip())]
    if cells and cells[0] == "":
        cells = cells[1:]
    if cells and cells[-1] == "":
        cells = cells[:-1]
    return cells


def convert(md: str) -> tuple[str, list[tuple[str, str]], int]:
    """Return (body_html, [(anchor, section_title)], requirement_row_count)."""
    lines = md.splitlines()
    out: list[str] = []
    toc: list[tuple[str, str]] = []
    row_count = 0
    i = 0
    section_open = False
    anchor_n = 0

    def close_section():
        nonlocal section_open
        if section_open:
            out.append("</section>")
            section_open = False

    while i < len(lines):
        line = lines[i]

        if line.startswith("## ") and not line.startswith("###"):
            close_section()
            anchor_n += 1
            title = line[3:].strip()
            aid = f"s{anchor_n}"
            toc.append((aid, title))
            out.append(f'<section class="sec" id="{aid}">')
            out.append(f'<h2 class="unit">{inline(title)}</h2>')
            section_open = True
            i += 1
            continue

        if line.startswith("# "):
            i += 1  # page has its own header
            continue

        if line.startswith("### "):
            out.append(f'<h3 class="unit">{inline(line[4:].strip())}</h3>')
            i += 1
            continue

        if line.strip() == "---":
            i += 1
            continue

        if line.startswith("> "):
            out.append(f'<blockquote class="unit"><p>{inline(line[2:].strip())}</p></blockquote>')
            i += 1
            continue

        if line.lstrip().startswith("- "):
            out.append("<ul>")
            while i < len(lines) and lines[i].lstrip().startswith("- "):
                out.append(f'<li class="unit">{inline(lines[i].lstrip()[2:].strip())}</li>')
                i += 1
            out.append("</ul>")
            continue

        if line.strip().startswith("|"):
            # collect the table block
            block = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                block.append(lines[i])
                i += 1
            if len(block) >= 2 and re.match(r"^\s*\|[\s:|-]+\|\s*$", block[1]):
                header = split_row(block[0])
                out.append('<div class="twrap"><table><thead><tr>')
                out.extend(f"<th>{inline(h)}</th>" for h in header)
                out.append("</tr></thead><tbody>")
                for row in block[2:]:
                    cells = split_row(row)
                    while len(cells) < len(header):
                        cells.append("")
                    out.append('<tr class="unit">')
                    out.extend(f"<td>{inline(c)}</td>" for c in cells[: len(header)])
                    out.append("</tr>")
                    row_count += 1
                out.append("</tbody></table></div>")
            continue

        if line.strip():
            # merge consecutive plain lines into one paragraph
            para = [line.strip()]
            i += 1
            while i < len(lines) and lines[i].strip() and not re.match(
                r"^(#|\||>|-\s|---)", lines[i].strip()
            ):
                para.append(lines[i].strip())
                i += 1
            out.append(f'<p class="unit">{inline(" ".join(para))}</p>')
            continue

        i += 1

    close_section()
    return "\n".join(out), toc, row_count


def main() -> None:
    md = SRC.read_text(encoding="utf-8")
    body, toc, rows = convert(md)

    m = re.search(r"\*\*Last updated:\*\*\s*(\S+)", md)
    updated = m.group(1) if m else "?"

    toc_html = "".join(
        f'<button class="pill" data-target="{aid}">{html.escape(title)}</button>'
        for aid, title in toc
    )

    page = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>IRETP Compliance Search — RFP v1.3</title>
<style>
  :root {
    --green: #066735; --green-dark: #044a26;
    --ink: #1c211f; --muted: #5c6660; --line: #dde4df; --bg: #f4f7f5;
    --card: #ffffff; --mark: #ffe08a;
  }
  * { box-sizing: border-box; }
  body { margin: 0; background: var(--bg); color: var(--ink);
         font: 15px/1.55 "Segoe UI", system-ui, -apple-system, sans-serif; }
  header { background: linear-gradient(135deg, var(--green), var(--green-dark));
           color: #fff; padding: 26px 28px 18px; }
  header h1 { margin: 0 0 4px; font-size: 22px; font-weight: 600; }
  header p { margin: 0; opacity: .85; font-size: 13px; }
  .bar { position: sticky; top: 0; z-index: 10; background: var(--card);
         border-bottom: 1px solid var(--line); padding: 12px 28px;
         box-shadow: 0 2px 8px rgba(0,0,0,.05); }
  .bar input { width: 100%; max-width: 720px; font-size: 16px; padding: 10px 14px;
               border: 2px solid var(--line); border-radius: 8px; outline: none; }
  .bar input:focus { border-color: var(--green); }
  .meta { margin-top: 6px; font-size: 13px; color: var(--muted); }
  .ask { margin: 14px 28px 0; max-width: 1200px; background: var(--card);
         border: 1px solid var(--line); border-left: 4px solid var(--green);
         border-radius: 10px; padding: 14px 18px; }
  .ask-row { display: flex; gap: 8px; }
  .ask-row input { flex: 1; font-size: 15px; padding: 9px 13px;
                   border: 2px solid var(--line); border-radius: 8px; outline: none; }
  .ask-row input:focus { border-color: var(--green); }
  .ask-row button { background: var(--green); color: #fff; border: 0; border-radius: 8px;
                    padding: 9px 22px; font-size: 15px; font-weight: 600; cursor: pointer; }
  .ask-row button:hover { background: var(--green-dark); }
  .ask-row button:disabled { opacity: .55; cursor: wait; }
  .ask-note { margin-top: 6px; font-size: 12px; color: var(--muted); }
  .askout { margin-top: 12px; border-top: 1px solid var(--line); padding-top: 12px;
            font-size: 14.5px; }
  .askout .answer p { margin: 6px 0; }
  .askout .answer ul { margin: 6px 0; padding-left: 22px; }
  .askout .err { color: #a33; }
  .askout .srcs { margin-top: 10px; display: flex; flex-wrap: wrap; gap: 5px; }
  .askout .src { background: #eef4f0; border: 1px solid var(--line); border-radius: 999px;
                 padding: 2px 10px; font-size: 11.5px; color: var(--green-dark); }
  .askout .model { margin-top: 8px; font-size: 11.5px; color: var(--muted); }
  .spin { display: inline-block; width: 14px; height: 14px; border: 2px solid var(--line);
          border-top-color: var(--green); border-radius: 50%; vertical-align: -2px;
          animation: spin .8s linear infinite; margin-right: 8px; }
  @keyframes spin { to { transform: rotate(360deg); } }
  .pills { padding: 10px 28px 0; display: flex; flex-wrap: wrap; gap: 6px; }
  .pill { border: 1px solid var(--line); background: var(--card); color: var(--ink);
          border-radius: 999px; padding: 4px 12px; font-size: 12.5px; cursor: pointer; }
  .pill:hover { border-color: var(--green); color: var(--green); }
  main { padding: 18px 28px 60px; max-width: 1200px; }
  .sec { background: var(--card); border: 1px solid var(--line); border-radius: 10px;
         padding: 18px 22px; margin-bottom: 18px; }
  h2 { font-size: 18px; color: var(--green-dark); margin: 2px 0 10px;
       padding-bottom: 8px; border-bottom: 2px solid var(--line); }
  h3 { font-size: 15px; margin: 18px 0 8px; color: var(--ink); }
  .twrap { overflow-x: auto; }
  table { border-collapse: collapse; width: 100%; font-size: 13.5px; }
  th { text-align: left; background: #eef4f0; color: var(--green-dark);
       padding: 7px 10px; border: 1px solid var(--line); white-space: nowrap; }
  td { padding: 7px 10px; border: 1px solid var(--line); vertical-align: top; }
  tr.unit:nth-child(even) { background: #fafcfa; }
  td:first-child { font-weight: 600; white-space: nowrap; }
  code { background: #eef2ef; border: 1px solid #e2e8e3; border-radius: 4px;
         padding: 1px 5px; font-size: 12.5px;
         font-family: Consolas, "Cascadia Code", monospace; word-break: break-all; }
  blockquote { margin: 10px 0; padding: 8px 14px; border-left: 4px solid var(--green);
               background: #f2f7f3; border-radius: 0 6px 6px 0; }
  mark { background: var(--mark); border-radius: 3px; padding: 0 1px; }
  .hidden { display: none !important; }
  .noresults { display: none; color: var(--muted); font-size: 15px; padding: 30px 0;
               text-align: center; }
  footer { padding: 0 28px 40px; color: var(--muted); font-size: 12.5px; }
</style>
</head>
<body>
<header>
  <h1>IRETP Compliance Search</h1>
  <p>RFP DLD-IRETP-2026-001 v1.3 &middot; every requirement &rarr; implementation file &rarr; tests &rarr; verification &middot; matrix last updated __UPDATED__</p>
</header>
<div class="bar">
  <input id="q" type="search" placeholder="Search any requirement, file, feature&hellip; e.g. FR-008, escrow, unsubscribe, Fabric, RTL, guardrail" autofocus>
  <div class="meta" id="meta">__ROWS__ requirement rows &middot; type to filter</div>
</div>
<div class="ask">
  <div class="ask-row">
    <input id="askq" type="text" placeholder="Ask anything&hellip; How does the EWRS escalation work? How does X work? Why keyword guardrail? How is FR-008 implemented?">
    <button id="askbtn">Ask</button>
  </div>
  <div class="ask-note">Answered by the IRETP AI backend (gpt-4o) grounded on this matrix + the full technical docs pack (architecture, integration map, API reference&hellip;) &mdash; needs the app running (<code>start-iretp.bat</code>).</div>
  <div id="askout" class="askout hidden"></div>
</div>
<div class="pills" id="pills">__TOC__</div>
<main id="content">
__BODY__
<div class="noresults" id="noresults">No matches. Try fewer or shorter words &mdash; e.g. &ldquo;escrow&rdquo; instead of &ldquo;escrow account monitoring&rdquo;.</div>
</main>
<footer>Generated from <code>docs/COMPLIANCE_MATRIX.md</code> by <code>tools/build_compliance_search.py</code>. Regenerate after editing the matrix. Single file &mdash; works offline.</footer>
<script>
(function () {
  var q = document.getElementById('q');
  var meta = document.getElementById('meta');
  var noresults = document.getElementById('noresults');
  var sections = Array.prototype.slice.call(document.querySelectorAll('.sec'));
  var units = Array.prototype.slice.call(document.querySelectorAll('.unit'));
  var blocks = Array.prototype.slice.call(document.querySelectorAll('.twrap, ul, blockquote'));
  var totalRows = __ROWS__;

  units.forEach(function (u) { u.dataset.text = u.textContent.toLowerCase(); });

  function clearMarks() {
    Array.prototype.slice.call(document.querySelectorAll('mark')).forEach(function (m) {
      var p = m.parentNode;
      p.replaceChild(document.createTextNode(m.textContent), m);
      p.normalize();
    });
  }

  function markUnit(el, words) {
    var walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT, null);
    var nodes = [];
    while (walker.nextNode()) nodes.push(walker.currentNode);
    nodes.forEach(function (node) {
      var text = node.nodeValue, lower = text.toLowerCase();
      var hits = [];
      words.forEach(function (w) {
        var idx = 0;
        while ((idx = lower.indexOf(w, idx)) !== -1) { hits.push([idx, idx + w.length]); idx += w.length; }
      });
      if (!hits.length) return;
      hits.sort(function (a, b) { return a[0] - b[0]; });
      var merged = [hits[0]];
      for (var i = 1; i < hits.length; i++) {
        var last = merged[merged.length - 1];
        if (hits[i][0] <= last[1]) last[1] = Math.max(last[1], hits[i][1]);
        else merged.push(hits[i]);
      }
      var frag = document.createDocumentFragment(), pos = 0;
      merged.forEach(function (h) {
        if (h[0] > pos) frag.appendChild(document.createTextNode(text.slice(pos, h[0])));
        var mk = document.createElement('mark');
        mk.textContent = text.slice(h[0], h[1]);
        frag.appendChild(mk);
        pos = h[1];
      });
      if (pos < text.length) frag.appendChild(document.createTextNode(text.slice(pos)));
      node.parentNode.replaceChild(frag, node);
    });
  }

  function apply() {
    clearMarks();
    var words = q.value.toLowerCase().split(/\\s+/).filter(Boolean);
    if (!words.length) {
      units.forEach(function (u) { u.classList.remove('hidden'); });
      blocks.forEach(function (b) { b.classList.remove('hidden'); });
      sections.forEach(function (s) { s.classList.remove('hidden'); });
      noresults.style.display = 'none';
      meta.textContent = totalRows + ' requirement rows \\u00b7 type to filter';
      return;
    }
    var shown = 0;
    units.forEach(function (u) {
      var ok = words.every(function (w) { return u.dataset.text.indexOf(w) !== -1; });
      u.classList.toggle('hidden', !ok);
      if (ok) { shown++; markUnit(u, words); }
    });
    blocks.forEach(function (b) {
      b.classList.toggle('hidden', !b.querySelector('.unit:not(.hidden)'));
    });
    sections.forEach(function (s) {
      var any = s.querySelector('.unit:not(.hidden)');
      s.classList.toggle('hidden', !any);
      // keep the section heading visible as context when the section has matches
      var h = s.querySelector('h2');
      if (any && h) h.classList.remove('hidden');
    });
    noresults.style.display = shown ? 'none' : 'block';
    meta.textContent = shown + ' matching item' + (shown === 1 ? '' : 's');
  }

  var t;
  q.addEventListener('input', function () { clearTimeout(t); t = setTimeout(apply, 120); });

  // --- Ask how / why (POST to the local IRETP WebAPI) ---
  var askq = document.getElementById('askq');
  var askbtn = document.getElementById('askbtn');
  var askout = document.getElementById('askout');
  var API = 'http://localhost:5000/api/ai/compliance-ask';

  function esc(s) {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }

  function renderAnswer(text) {
    // minimal markdown: **bold**, `code`, bullet lists, paragraphs
    var lines = esc(text).split(/\\r?\\n/);
    var out = [], list = false;
    lines.forEach(function (ln) {
      ln = ln.replace(/\\*\\*([^*]+)\\*\\*/g, '<strong>$1</strong>')
             .replace(/`([^`]+)`/g, '<code>$1</code>');
      var h = ln.match(/^\\s*#{1,4}\\s+(.*)$/);
      if (h) {
        if (list) { out.push('</ul>'); list = false; }
        out.push('<p><strong>' + h[1] + '</strong></p>');
        return;
      }
      var m = ln.match(/^\\s*(?:[-*]|\\d+\\.)\\s+(.*)$/);
      if (m) {
        if (!list) { out.push('<ul>'); list = true; }
        out.push('<li>' + m[1] + '</li>');
      } else {
        if (list) { out.push('</ul>'); list = false; }
        if (ln.trim()) out.push('<p>' + ln + '</p>');
      }
    });
    if (list) out.push('</ul>');
    return out.join('');
  }

  function ask() {
    var question = askq.value.trim();
    if (!question) return;
    askbtn.disabled = true;
    askout.classList.remove('hidden');
    askout.innerHTML = '<span class="spin"></span>Asking gpt-4o&hellip;';
    fetch(API, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question: question })
    })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function (d) {
        var htmlOut = '<div class="answer">' + renderAnswer(d.answer || '') + '</div>';
        if (d.sources && d.sources.length) {
          htmlOut += '<div class="srcs">' + d.sources.slice(0, 10).map(function (s) {
            return '<span class="src">' + esc(s) + '</span>';
          }).join('') + '</div>';
        }
        if (d.modelUsed) htmlOut += '<div class="model">Answered by ' + esc(d.modelUsed) + ', grounded on the rows above.</div>';
        askout.innerHTML = htmlOut;
      })
      .catch(function (e) {
        askout.innerHTML = '<div class="err">Could not reach the IRETP backend (' + esc(String(e.message || e)) +
          '). Make sure the app is running &mdash; double-click <code>start-iretp.bat</code> &mdash; then try again.</div>';
      })
      .finally(function () { askbtn.disabled = false; });
  }

  askbtn.addEventListener('click', ask);
  askq.addEventListener('keydown', function (e) { if (e.key === 'Enter') ask(); });

  document.getElementById('pills').addEventListener('click', function (e) {
    var b = e.target.closest('.pill');
    if (!b) return;
    q.value = '';
    apply();
    var target = document.getElementById(b.dataset.target);
    if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });
})();
</script>
</body>
</html>
"""
    page = (
        page.replace("__UPDATED__", updated)
        .replace("__ROWS__", str(rows))
        .replace("__TOC__", toc_html)
        .replace("__BODY__", body)
    )
    OUT.write_text(page, encoding="utf-8")
    print(f"Wrote {OUT} ({OUT.stat().st_size:,} bytes, {rows} requirement rows, {len(toc)} sections)")


if __name__ == "__main__":
    main()
