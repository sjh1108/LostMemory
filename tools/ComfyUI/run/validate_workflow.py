"""ComfyUI UI workflow JSON 검증 — link 타입/노드 매핑/중복 ID 체크"""
import io
import json
import sys
from pathlib import Path

try:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass

if len(sys.argv) < 2:
    print("usage: validate_workflow.py <path>")
    sys.exit(1)

p = Path(sys.argv[1])
d = json.load(open(p, "r", encoding="utf-8"))

links = d.get("links", [])
nodes = {n["id"]: n for n in d.get("nodes", [])}

print(f"file:           {p.name}")
print(f"nodes:          {len(nodes)}")
print(f"links:          {len(links)}")
print(f"last_link_id:   {d.get('last_link_id')}")

ids = [l[0] for l in links]
dupes = set([x for x in ids if ids.count(x) > 1])
print(f"unique link ids: {len(set(ids)) == len(ids)}  (dupes: {dupes if dupes else 'none'})")

print()
print("=== links (src → dst) ===")
problems = []
for l in links:
    lid, src_node, src_slot, dst_node, dst_slot, ltype = l
    src = nodes.get(src_node, {})
    dst = nodes.get(dst_node, {})
    src_out = src.get("outputs", [])
    dst_in = dst.get("inputs", [])
    src_type = src_out[src_slot]["type"] if src_slot < len(src_out) else "?"
    dst_type = dst_in[dst_slot]["type"] if dst_slot < len(dst_in) else "?"
    match = (src_type == ltype == dst_type)
    flag = "OK " if match else "BAD"
    print(f"  {flag} link {lid:>2}: {ltype:<13} {src.get('type','?'):>15}[{src_slot}]({src_type}) -> {dst.get('type','?'):<15}[{dst_slot}]({dst_type})")
    if not match:
        problems.append((lid, src_type, ltype, dst_type))

print()
if problems:
    print(f"❌ {len(problems)} link(s) with type mismatch")
    sys.exit(2)
else:
    print("✅ all links type-consistent")
