"""LostMemory Gallery — ComfyUI 내장 갤러리 페이지 (PromptServer routes).

기존 standalone `tools/ComfyUI/run/gallery_server.py`(port 8189)를 ComfyUI 본체 8188
포트의 `/gallery` 라우트로 통합. 별도 서버/포트/방화벽 룰 불필요.

엔드포인트:
- GET /gallery               그리드 (?q= filename 검색)
- GET /gallery/thumb/{name}  썸네일 (JPEG 200px 캐시)
- GET /gallery/file/{name}   원본 PNG 다운로드/표시
- GET /gallery/view/{name}   원본 + 메타데이터(prompt, workflow JSON) 페이지

운영 노출: comfyui-lostmemory.duckdns.org/gallery → ComfyUI Basic Auth 그대로 적용.
"""
from __future__ import annotations

import json
from datetime import datetime
from pathlib import Path

from aiohttp import web
from PIL import Image

try:
    import folder_paths  # ComfyUI 빌트인
except ImportError:
    folder_paths = None

try:
    from server import PromptServer  # ComfyUI 본체 서버
except ImportError:
    PromptServer = None


def _output_dir() -> Path:
    if folder_paths:
        return Path(folder_paths.get_output_directory())
    return Path(__file__).resolve().parents[2] / "output"


def _thumb_dir() -> Path:
    return _output_dir() / ".thumbs"


def list_pngs(prefix_filter: str = "") -> list[dict]:
    out = _output_dir()
    items: list[dict] = []
    for p in out.glob("*.png"):
        if prefix_filter and prefix_filter.lower() not in p.name.lower():
            continue
        st = p.stat()
        items.append({
            "name": p.name,
            "size_kb": round(st.st_size / 1024, 1),
            "mtime": st.st_mtime,
            "mtime_str": datetime.fromtimestamp(st.st_mtime).strftime("%m-%d %H:%M"),
        })
    items.sort(key=lambda x: x["mtime"], reverse=True)
    return items


def make_thumb(src: Path, thumb: Path, size: int = 200) -> Path:
    """원본 PNG → 200px JPEG 썸네일 캐시. mtime 비교로 stale 갱신."""
    if thumb.exists() and thumb.stat().st_mtime >= src.stat().st_mtime:
        return thumb
    thumb.parent.mkdir(parents=True, exist_ok=True)
    img = Image.open(src)
    img.thumbnail(
        (size, size),
        Image.Resampling.LANCZOS if max(img.size) > size * 4 else Image.Resampling.NEAREST,
    )
    if img.mode == "RGBA":
        bg = Image.new("RGB", img.size, (240, 240, 240))
        bg.paste(img, mask=img.split()[3])
        img = bg
    img.save(thumb, "JPEG", quality=80)
    return thumb


def extract_metadata(src: Path) -> dict:
    """ComfyUI가 PNG에 박아둔 prompt/workflow 메타데이터."""
    try:
        img = Image.open(src)
        meta = img.info or {}
        result: dict = {}
        for key in ("prompt", "workflow"):
            if key in meta:
                try:
                    result[key] = json.loads(meta[key])
                except Exception:
                    result[key] = str(meta[key])[:200]
        return result
    except Exception as e:
        return {"_error": str(e)}


def short_prompt(meta: dict) -> str:
    """workflow JSON에서 CLIPTextEncode positive prompt 한 줄 추출."""
    try:
        prompt_data = meta.get("prompt", {})
        if isinstance(prompt_data, dict):
            for node_id, node in prompt_data.items():
                if node.get("class_type") == "CLIPTextEncode":
                    text = node.get("inputs", {}).get("text", "")
                    if text and "negative" not in node_id.lower():
                        t = text.strip().replace("\n", " ")
                        if not any(w in t.lower() for w in ["blurry", "low quality", "realistic, 3d"]):
                            return t[:120] + ("..." if len(t) > 120 else "")
    except Exception:
        pass
    return ""


HTML_INDEX = """<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<title>LostMemory ComfyUI Gallery</title>
<style>
  body { background:#1a1a1a; color:#e6e6e6; font-family: -apple-system, "Segoe UI", "Noto Sans KR", sans-serif; margin:0; }
  header { padding:16px 24px; background:#222; border-bottom:1px solid #333; display:flex; align-items:center; gap:24px; flex-wrap:wrap; }
  header h1 { margin:0; font-size:18px; font-weight:600; }
  header form { display:flex; gap:8px; }
  header input { background:#111; color:#fff; border:1px solid #444; padding:6px 10px; border-radius:4px; width:240px; }
  header button { background:#3a5; color:#fff; border:0; padding:6px 14px; border-radius:4px; cursor:pointer; }
  header a.home { color:#7cf; align-self:center; text-decoration:none; }
  .meta { color:#888; font-size:13px; }
  .grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap:14px; padding:16px; }
  .card { background:#222; border-radius:6px; overflow:hidden; transition:transform .1s; }
  .card:hover { transform:scale(1.02); background:#2a2a2a; }
  .card a.thumb { display:block; background:#000; }
  .card img { width:100%; height:200px; object-fit:contain; display:block; image-rendering:pixelated; }
  .card .info { padding:8px 10px; font-size:12px; color:#bbb; }
  .card .name { color:#e6e6e6; font-weight:500; word-break:break-all; margin-bottom:4px; font-size:11px; }
  .card .footer { display:flex; justify-content:space-between; align-items:center; margin-top:4px; }
  .card .footer a { color:#7cf; text-decoration:none; font-size:11px; }
  footer { text-align:center; padding:24px; color:#666; font-size:12px; }
</style>
</head>
<body>
<header>
  <h1>LostMemory ComfyUI 갤러리</h1>
  <form method="get" action="/gallery">
    <input type="text" name="q" value="{q}" placeholder="filename 검색 (예: Skill, ZIT, pixel)">
    <button type="submit">검색</button>
    <a class="home" href="/gallery">전체</a>
  </form>
  <span class="meta">{count}장 표시</span>
  <a class="home" href="/" style="margin-left:auto;">← ComfyUI</a>
</header>
<div class="grid">
{cards}
</div>
<footer>output/ 폴더 자동 스캔 — 최신순 정렬 — 클릭하면 원본 + 메타데이터</footer>
</body>
</html>
"""

CARD_TPL = """<div class="card">
  <a class="thumb" href="/gallery/view/{name}"><img loading="lazy" src="/gallery/thumb/{name}"></a>
  <div class="info">
    <div class="name">{name}</div>
    <div class="footer">
      <span>{mtime} · {size_kb}KB</span>
      <a href="/gallery/file/{name}" download>다운</a>
    </div>
  </div>
</div>"""


async def _index(request: web.Request) -> web.Response:
    q = request.query.get("q", "").strip()
    items = list_pngs(q)
    cards_list = []
    for it in items:
        c = CARD_TPL
        c = c.replace("{name}", it["name"]).replace("{mtime}", it["mtime_str"]).replace("{size_kb}", str(it["size_kb"]))
        cards_list.append(c)
    cards = "\n".join(cards_list)
    if not cards:
        cards = "<p style='padding:40px;color:#888;'>결과 없음.</p>"
    html = HTML_INDEX.replace("{q}", q).replace("{count}", str(len(items))).replace("{cards}", cards)
    return web.Response(text=html, content_type="text/html")


async def _thumb(request: web.Request) -> web.StreamResponse:
    name = request.match_info["name"]
    if "/" in name or "\\" in name or ".." in name:
        return web.Response(status=400, text="invalid name")
    src = _output_dir() / name
    if not src.exists() or not src.is_file():
        return web.Response(status=404, text="not found")
    th = _thumb_dir() / (name + ".jpg")
    make_thumb(src, th)
    return web.FileResponse(th)


async def _file(request: web.Request) -> web.StreamResponse:
    name = request.match_info["name"]
    if "/" in name or "\\" in name or ".." in name:
        return web.Response(status=400, text="invalid name")
    src = _output_dir() / name
    if not src.exists() or not src.is_file():
        return web.Response(status=404, text="not found")
    return web.FileResponse(src)


async def _view(request: web.Request) -> web.Response:
    name = request.match_info["name"]
    if "/" in name or "\\" in name or ".." in name:
        return web.Response(status=400, text="invalid name")
    src = _output_dir() / name
    if not src.exists():
        return web.Response(status=404, text="not found")
    meta = extract_metadata(src)
    short = short_prompt(meta)
    workflow_json = meta.get("workflow", {})
    prompt_json = meta.get("prompt", {})
    html = f"""<!doctype html>
<html><head><meta charset="utf-8"><title>{name}</title>
<style>
  body {{ background:#1a1a1a; color:#e6e6e6; font-family: -apple-system, sans-serif; margin:0; padding:20px; }}
  a {{ color:#7cf; }}
  .wrap {{ max-width:1400px; margin:0 auto; display:grid; grid-template-columns: 1fr 1fr; gap:20px; }}
  img {{ max-width:100%; height:auto; background:#000; image-rendering:pixelated; }}
  pre {{ background:#111; padding:12px; border-radius:6px; overflow:auto; max-height:80vh; font-size:11px; }}
  .top {{ grid-column: 1/-1; display:flex; gap:16px; align-items:center; }}
  .prompt {{ background:#2a3; color:#fff; padding:10px 14px; border-radius:6px; margin:14px 0; font-size:13px; }}
</style></head><body>
<div class="top">
  <a href="/gallery">← 갤러리로</a>
  <h2 style="margin:0">{name}</h2>
  <a href="/gallery/file/{name}" download style="margin-left:auto;background:#3a5;color:#fff;padding:6px 14px;border-radius:4px;text-decoration:none;">다운로드</a>
</div>
{"<div class='prompt'>" + short + "</div>" if short else ""}
<div class="wrap">
  <div><img src="/gallery/file/{name}"></div>
  <div>
    <details open><summary><b>prompt (API 포맷)</b></summary>
    <pre>{json.dumps(prompt_json, indent=2, ensure_ascii=False) if isinstance(prompt_json, dict) else prompt_json}</pre>
    </details>
    <details><summary><b>workflow (UI 포맷)</b> — PNG를 ComfyUI에 드래그하면 워크플로 복원됨</summary>
    <pre>{json.dumps(workflow_json, indent=2, ensure_ascii=False)[:5000] if isinstance(workflow_json, dict) else workflow_json}</pre>
    </details>
  </div>
</div>
</body></html>"""
    return web.Response(text=html, content_type="text/html")


# PromptServer에 라우트 등록 (ComfyUI 시작 시 자동 등록됨).
# /gallery 와 /gallery/ 둘 다 등록 — 외부 nginx(또는 reverse proxy)가
# 디렉토리 형태로 trailing slash 를 추가하는 경우에도 동작하도록.
if PromptServer is not None:
    routes = PromptServer.instance.routes
    routes.get("/gallery")(_index)
    routes.get("/gallery/")(_index)
    routes.get("/gallery/thumb/{name}")(_thumb)
    routes.get("/gallery/file/{name}")(_file)
    routes.get("/gallery/view/{name}")(_view)


# ComfyUI 노드 등록 mapping은 비움 (이 패키지는 노드가 아니라 web route만 추가)
NODE_CLASS_MAPPINGS: dict = {}
NODE_DISPLAY_NAME_MAPPINGS: dict = {}
