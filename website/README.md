# LostMemory 게임 공식 사이트

`https://lostmemory.duckdns.org/` 로 서빙되는 **외부 (플레이어) 대상 마케팅 사이트** — 공지 / 패치노트 / FAQ.

## 구조

- **MkDocs Material** 정적 사이트 생성기 + **게임 톤 custom CSS** (Galmuri9 픽셀 폰트 + 검은 배경 + 흰 텍스트 + 노란 hover)
- **`website-nginx`** 컨테이너 (internal-only, infra_public network) 가 정적 파일 서빙
- **`tools/infra/nginx`** (외부 HTTPS 진입점) 가 `server_name lostmemory.duckdns.org` 으로 매치 → website-nginx 로 `proxy_pass`
- ComfyUI 는 sibling 도메인 `comfyui-lostmemory.duckdns.org` 로 분리 (DuckDNS dot 미허용으로 dash 형식)

## 디렉토리

```
website/
├── docker-compose.yml             # website-nginx + mkdocs-builder
├── nginx/conf.d/default.conf      # 정적 서빙
├── mkdocs.yml                     # MkDocs config
├── docs/
│   ├── index.md                   # 랜딩
│   ├── notices.md                 # 공지사항
│   ├── patch-notes.md             # 패치노트
│   ├── faq.md                     # FAQ
│   └── stylesheets/extra.css      # 게임 톤 override
├── README.md                      # 본 문서
└── site/                          # .gitignore — `mkdocs build` 결과 (자동 생성)
```

## 콘텐츠 업데이트 흐름

1. `docs/*.md` 수정 (운영자가 git 으로 commit + push)
2. develop 머지
3. EC2#2 운영자 SSH:
   ```bash
   cd /home/ubuntu/lostmemory
   git pull --ff-only origin develop
   cd website
   docker compose --profile mkdocs run --rm mkdocs-builder
   ```
4. nginx recreate/reload 불요 — 마운트된 `site/` 만 갱신됨

## 로컬 미리보기 (옵션)

```bash
cd website
docker compose --profile mkdocs run --rm mkdocs-builder
# 결과: site/index.html — 브라우저에서 file:// 로 열어 확인 가능

# 또는 hot-reload dev server (port 8000)
docker run --rm -it -p 8000:8000 -v "$(pwd):/docs" \
  squidfunk/mkdocs-material:latest serve --dev-addr 0.0.0.0:8000
```

## 첫 배포 절차 (EC2#2)

운영자가 한 번만 수행:

1. **DuckDNS 도메인 등록** (이미 완료):
   - `lostmemory.duckdns.org` (기존)
   - `comfyui-lostmemory.duckdns.org` (신규 sibling 도메인)
2. **`.env` 갱신** (`tools/infra/.env`):
   ```
   WEBSITE_DOMAIN=lostmemory.duckdns.org
   PUBLIC_DOMAIN=lostmemory.duckdns.org
   COMFYUI_DOMAIN=comfyui-lostmemory.duckdns.org
   ```
3. **Let's Encrypt 인증서 발급** (comfyui-lostmemory.duckdns.org):
   ```bash
   cd tools/infra
   docker compose --profile certbot run --rm certbot certonly --webroot \
     -w /var/www/certbot \
     -d comfyui-lostmemory.duckdns.org \
     --email <EMAIL> --agree-tos --no-eff-email
   ```
4. **tools/infra nginx recreate** (template 변경 반영):
   ```bash
   docker compose up -d --force-recreate nginx
   docker compose exec nginx nginx -t
   ```
5. **website stack 신설**:
   ```bash
   cd ../../website
   docker compose --profile mkdocs run --rm mkdocs-builder
   docker compose up -d website-nginx
   ```
6. **검증**:
   ```bash
   curl -I https://lostmemory.duckdns.org/            # → HTTP/2 200
   curl -I https://comfyui-lostmemory.duckdns.org/    # → HTTP/2 200 (또는 401 Basic Auth)
   ```

## 가드

- **`infra_public` network 이름**: 운영자가 `cd tools/infra` 후 compose 실행 가정 (project name = infra). EC2#2 에서 `docker network ls | grep public` 으로 정확한 이름 확인 후 `docker-compose.yml` 의 `name:` 일치 보장.
- **콘텐츠 수정 시 nginx 재기동 불요**: `site/` 만 마운트된 read-only 볼륨 — mkdocs-builder 가 site/ 갱신하면 자동 반영.
- **ComfyUI 도메인 swap**: 기존 사용자 (게임 디자이너 등) 에게 새 URL (`comfyui-lostmemory.duckdns.org`) 안내 필수.
- **DuckDNS dot 미허용**: `comfyui.lostmemory.duckdns.org` 형식 안 됨 (`valid chars: A-Z, 0-9, -`). 사용자가 dash 형식 sibling 도메인으로 우회.
