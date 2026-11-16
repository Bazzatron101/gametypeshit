"""Live ticket board for the .scratch issue tracker.

Run:  python scripts/ticket_board.py          (serves http://localhost:8765)
      python scripts/ticket_board.py --port N

The page re-reads the ticket files every few seconds, so edits to any
.scratch/<feature>/issues/*.md file show up without restarting.

Column rules (worked out from each ticket's Status line and checkboxes):
  Done         Status is done, or every checkbox is ticked and nothing is
               left for a human (status ready-for-agent).
  In progress  Status is in-progress or claimed, or some work is ticked but
               something is still open (unticked boxes, or ready-for-human:
               built, waiting on your check or your writing).
  Not started  Nothing ticked yet.
  wontfix tickets are listed separately.
"""

import argparse
import json
import re
import subprocess
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCRATCH = ROOT / ".scratch"

STATUS_RE = re.compile(r"^\*{0,2}Status:?\*{0,2}:?\s*(.+)$", re.I | re.M)
BLOCKED_RE = re.compile(r"^\*{0,2}Blocked by:?\*{0,2}:?\s*(.+)$", re.I | re.M)
TITLE_RE = re.compile(r"^#\s+(.+)$", re.M)
TICKED_RE = re.compile(r"^\s*- \[[xX]\]\s*(.+)$", re.M)
OPEN_RE = re.compile(r"^\s*- \[ \]\s*(.+)$", re.M)


def last_touched():
    """Map of ticket file path -> ISO date of the last commit that touched it."""
    try:
        out = subprocess.run(
            ["git", "log", "--name-only", "--format=@%cs", "--", ".scratch"],
            cwd=ROOT, capture_output=True, text=True, timeout=10,
        ).stdout
    except Exception:
        return {}
    dates, current = {}, None
    for line in out.splitlines():
        if line.startswith("@"):
            current = line[1:]
        elif line.strip() and current:
            dates.setdefault(line.strip().replace("\\", "/"), current)
    return dates


def parse(path: Path):
    text = path.read_text(encoding="utf-8", errors="replace")
    body = text.split("\n## Comments", 1)[0]
    title_m = TITLE_RE.search(text)
    title = title_m.group(1).strip() if title_m else path.stem
    num_m = re.match(r"(\d+)", path.name)
    num = int(num_m.group(1)) if num_m else 0
    title = re.sub(r"^\d+\s*[:.-]\s*", "", title)

    status_m = STATUS_RE.search(text)
    status_raw = status_m.group(1).strip() if status_m else "needs-triage"
    label = status_raw.split()[0].strip("`*,;:").lower()
    note_m = re.search(r"\((.*)\)\s*$", status_raw)
    note = note_m.group(1) if note_m else ""

    blocked_m = BLOCKED_RE.search(body)
    blocked = blocked_m.group(1).strip() if blocked_m else ""
    if blocked.lower() in ("none", "none - can start immediately", "-"):
        blocked = ""

    ticked = [t.strip() for t in TICKED_RE.findall(body)]
    open_ = [t.strip() for t in OPEN_RE.findall(body)]

    if label == "wontfix":
        column = "wontfix"
    elif label in ("done", "resolved", "closed"):
        column = "done"
    elif label in ("in-progress", "claimed"):
        column = "progress"
    elif ticked and not open_ and label == "ready-for-agent":
        column = "done"
    elif ticked:
        column = "progress"
    else:
        column = "todo"

    waiting_on_you = label in ("ready-for-human", "needs-info") and column != "done"
    return {
        "num": num,
        "title": title,
        "file": path.relative_to(ROOT).as_posix(),
        "feature": path.parent.parent.name,
        "label": label,
        "note": note,
        "blocked": blocked,
        "ticked": ticked,
        "open": open_,
        "column": column,
        "waitingOnYou": waiting_on_you,
    }


def collect():
    dates = last_touched()
    tickets = []
    for p in sorted(SCRATCH.glob("*/issues/*.md")):
        t = parse(p)
        t["touched"] = dates.get(t["file"], "uncommitted")
        tickets.append(t)
    return {"tickets": tickets}


PAGE = r"""<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Ticket Board</title>
<style>
:root{--bg:#f6f5f2;--card:#fff;--ink:#1d1c1a;--mute:#6b6862;--line:#e3e0da;
--done:#2f7d4f;--prog:#b5761a;--todo:#6b6862;--you:#7a4fd0;--bar:#ece9e3}
@media (prefers-color-scheme:dark){:root{--bg:#161514;--card:#201f1d;--ink:#ecebe8;
--mute:#9c988f;--line:#33312d;--done:#5cb784;--prog:#e0a548;--todo:#9c988f;--you:#a988ec;--bar:#2b2926}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);
font:14px/1.45 system-ui,-apple-system,"Segoe UI",sans-serif}
header{padding:20px 16px 8px;max-width:1400px;margin:auto}
h1{font-size:20px;margin:0 0 4px}.sub{color:var(--mute);font-size:12px}
.summary{display:flex;gap:10px;flex-wrap:wrap;margin:14px 0 6px}
.stat{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:8px 12px;min-width:110px}
.stat b{font-size:22px;display:block;font-variant-numeric:tabular-nums}
.stat span{font-size:12px;color:var(--mute)}
.meter{height:8px;border-radius:4px;background:var(--bar);overflow:hidden;display:flex;margin:6px 0 10px;max-width:640px}
.meter i{display:block;height:100%}
.tools{display:flex;gap:8px;flex-wrap:wrap;align-items:center}
input{font:inherit;padding:6px 10px;border:1px solid var(--line);border-radius:6px;background:var(--card);color:var(--ink);min-width:220px}
label.chk{font-size:13px;color:var(--mute);display:flex;gap:6px;align-items:center}
main{display:grid;grid-template-columns:repeat(3,1fr);gap:14px;padding:8px 16px 32px;max-width:1400px;margin:auto}
@media (max-width:860px){main{grid-template-columns:1fr}}
.col h2{font-size:13px;text-transform:uppercase;letter-spacing:.05em;margin:6px 0 10px;display:flex;gap:8px;align-items:center}
.dot{width:9px;height:9px;border-radius:50%}
.card{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:10px 12px;margin-bottom:8px;cursor:pointer}
.card:hover{border-color:var(--mute)}
.top{display:flex;gap:8px;align-items:baseline}
.num{font-variant-numeric:tabular-nums;color:var(--mute);font-weight:600;min-width:24px}
.t{font-weight:600}
.meta{display:flex;gap:6px;flex-wrap:wrap;margin-top:6px;font-size:11.5px;color:var(--mute)}
.pill{border:1px solid var(--line);border-radius:999px;padding:1px 8px}
.pill.you{color:var(--you);border-color:var(--you)}
.prog{height:4px;background:var(--bar);border-radius:2px;margin-top:8px;overflow:hidden}
.prog i{display:block;height:100%}
.detail{display:none;margin-top:8px;font-size:12.5px;border-top:1px solid var(--line);padding-top:8px}
.card.open .detail{display:block}
.detail ul{margin:4px 0;padding-left:18px}.detail li.x{color:var(--mute);text-decoration:line-through}
.note{color:var(--you)}.empty{color:var(--mute);font-size:13px}
.extra{max-width:1400px;margin:auto;padding:0 16px 32px;color:var(--mute);font-size:13px}
</style></head><body>
<header>
<h1>Ticket board</h1>
<div class="sub" id="sub">Loading…</div>
<div class="summary" id="summary"></div>
<div class="meter" id="meter"></div>
<div class="tools"><input id="q" placeholder="Filter by number or words…">
<label class="chk"><input type="checkbox" id="you" style="min-width:0"> Only tickets waiting on you</label></div>
</header>
<main>
<section class="col"><h2><span class="dot" style="background:var(--todo)"></span>Not started <span id="n-todo"></span></h2><div id="todo"></div></section>
<section class="col"><h2><span class="dot" style="background:var(--prog)"></span>In progress <span id="n-progress"></span></h2><div id="progress"></div></section>
<section class="col"><h2><span class="dot" style="background:var(--done)"></span>Done <span id="n-done"></span></h2><div id="done"></div></section>
</main>
<div class="extra" id="wontfix"></div>
<script>
const COLORS={todo:'var(--todo)',progress:'var(--prog)',done:'var(--done)'};
const openCards=new Set();let data=null;
const esc=s=>String(s).replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
function card(t){
  const total=t.ticked.length+t.open.length,pct=total?Math.round(100*t.ticked.length/total):0;
  const pills=[`<span class="pill">${esc(t.label)}</span>`];
  if(t.waitingOnYou)pills.push('<span class="pill you">waiting on you</span>');
  if(total)pills.push(`<span>${t.ticked.length}/${total} ticked</span>`);
  pills.push(`<span>${esc(t.touched)}</span>`);
  const list=[...t.ticked.map(x=>`<li class="x">${esc(x)}</li>`),...t.open.map(x=>`<li>${esc(x)}</li>`)].join('');
  return `<div class="card${openCards.has(t.num)?' open':''}" data-n="${t.num}">
  <div class="top"><span class="num">${String(t.num).padStart(2,'0')}</span><span class="t">${esc(t.title)}</span></div>
  <div class="meta">${pills.join('')}</div>
  ${total?`<div class="prog"><i style="width:${pct}%;background:${COLORS[t.column]}"></i></div>`:''}
  <div class="detail">${t.note?`<div class="note">${esc(t.note)}</div>`:''}
  ${t.blocked?`<div>Blocked by: ${esc(t.blocked)}</div>`:''}
  ${list?`<ul>${list}</ul>`:''}<div style="color:var(--mute)">${esc(t.file)}</div></div></div>`;
}
function render(){
  if(!data)return;
  const q=document.getElementById('q').value.trim().toLowerCase(),you=document.getElementById('you').checked;
  const all=data.tickets,counts={todo:0,progress:0,done:0};
  all.forEach(t=>{if(counts[t.column]!==undefined)counts[t.column]++});
  const shown=all.filter(t=>(!q||String(t.num)===q.replace(/^0+/,'')||(t.title+' '+t.note).toLowerCase().includes(q))&&(!you||t.waitingOnYou));
  for(const c of ['todo','progress','done']){
    const items=shown.filter(t=>t.column===c);
    if(c==='done')items.reverse();
    document.getElementById(c).innerHTML=items.map(card).join('')||'<div class="empty">Nothing here.</div>';
    document.getElementById('n-'+c).textContent=`(${counts[c]})`;
  }
  const n=counts.todo+counts.progress+counts.done,yours=all.filter(t=>t.waitingOnYou).length;
  document.getElementById('summary').innerHTML=
    `<div class="stat"><b>${n}</b><span>tickets</span></div>
     <div class="stat"><b style="color:var(--done)">${counts.done}</b><span>done</span></div>
     <div class="stat"><b style="color:var(--prog)">${counts.progress}</b><span>in progress</span></div>
     <div class="stat"><b>${counts.todo}</b><span>not started</span></div>
     <div class="stat"><b style="color:var(--you)">${yours}</b><span>waiting on you</span></div>`;
  document.getElementById('meter').innerHTML=['done','progress','todo'].map(c=>`<i style="width:${100*counts[c]/n}%;background:${COLORS[c]}"></i>`).join('');
  const wf=all.filter(t=>t.column==='wontfix');
  document.getElementById('wontfix').textContent=wf.length?'Won\'t fix: '+wf.map(t=>t.num+' '+t.title).join(' · '):'';
  document.getElementById('sub').textContent='Live from .scratch — refreshes every 5 s · last read '+new Date().toLocaleTimeString();
}
async function load(){try{data=await (await fetch('/data.json',{cache:'no-store'})).json();render()}
  catch(e){document.getElementById('sub').textContent='Board server not running — start it with: python scripts/ticket_board.py'}}
document.addEventListener('click',e=>{const c=e.target.closest('.card');if(!c)return;const n=+c.dataset.n;
  openCards.has(n)?openCards.delete(n):openCards.add(n);c.classList.toggle('open')});
document.getElementById('q').addEventListener('input',render);
document.getElementById('you').addEventListener('change',render);
load();setInterval(load,5000);
</script></body></html>"""


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith("/data.json"):
            body = json.dumps(collect()).encode()
            ctype = "application/json"
        elif self.path in ("/", "/index.html"):
            body = PAGE.encode()
            ctype = "text/html; charset=utf-8"
        else:
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header("Content-Type", ctype)
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *args):
        pass


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", type=int, default=8765)
    ap.add_argument("--print", action="store_true", help="print the columns and exit")
    args = ap.parse_args()
    if args.print:
        for col in ("done", "progress", "todo", "wontfix"):
            ts = [t for t in collect()["tickets"] if t["column"] == col]
            print(f"{col}: {len(ts)}  " + ", ".join(str(t["num"]) for t in ts))
        return
    print(f"Ticket board on http://localhost:{args.port}")
    ThreadingHTTPServer(("127.0.0.1", args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()
