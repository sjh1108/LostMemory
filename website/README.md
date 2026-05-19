# LostMemory 게임 공식 사이트 — Spring Boot 동적 CMS

`https://lostmemory.duckdns.org/` 로 서빙되는 게임 공식 사이트. **Spring Boot 3.5 + PostgreSQL + Thymeleaf SSR** 동적 CMS. 운영자가 `/admin/` 에서 로그인 후 공지/패치노트/FAQ 를 직접 작성/편집.

## 구조

```
website/
├── backend/                            # Spring Boot project (Gradle)
│   ├── build.gradle, settings.gradle, Dockerfile, .dockerignore
│   ├── gradlew, gradlew.bat, gradle/
│   └── src/main/
│       ├── java/com/lostmemory/website/
│       │   ├── WebsiteApplication.java
│       │   ├── global/{config,security,startup,time}
│       │   ├── admin/{entity,repository,controller}     # AdminUser + Login/Dashboard
│       │   ├── home/controller                          # GET /
│       │   ├── notice/{entity,repository,service,controller,dto}
│       │   ├── patchnote/...
│       │   └── faq/...
│       └── resources/
│           ├── application.yaml
│           ├── db/migration/V[1-4]__*.sql               # Flyway
│           ├── templates/                               # Thymeleaf
│           │   ├── fragments/layout.html
│           │   ├── home/index.html
│           │   ├── notice/{list,detail}.html
│           │   ├── patchnote/{list,detail}.html
│           │   ├── faq/list.html
│           │   └── admin/{login,dashboard}.html + notice/, patchnote/, faq/ CRUD
│           └── static/css/site.css                      # 게임 톤 (Galmuri9 + 다크 + 노란 hover)
├── docker-compose.yml                  # cms-postgres + website-backend
├── .env.example                        # 운영 환경변수 template
└── README.md                           # 본 문서
```

## 아키텍처 (EC2#2)

```
host nginx (systemd, lostmemory.duckdns.org HTTPS 종단)
  └── proxy_pass http://127.0.0.1:8081
        └── website-backend (Spring Boot, port 8081)
              └── cms-postgres (Docker network 안)
```

ComfyUI (`comfyui-lostmemory.duckdns.org`) 는 별도 server block 으로 영향 X.

## 첫 배포 절차 (EC2#2 SSH)

```bash
ssh ubuntu@lostmemory.duckdns.org
cd /home/ubuntu/lostmemory
git pull --ff-only origin develop

# 1. 운영 .env 작성
cd website
cp .env.example .env
vi .env   # POSTGRES_PASSWORD, ADMIN_PASSWORD 운영 비밀번호로 변경

# 2. Docker 빌드 + 기동
docker compose build website-backend
docker compose up -d
docker compose logs --tail=80 website-backend   # Flyway 4 migration 성공 + "[AdminSeed] admin 'XXX' seeded" 확인

# 3. 내부 검증
curl -fsS http://127.0.0.1:8081/actuator/health   # → {"status":"UP"}
curl -I    http://127.0.0.1:8081/                  # → 200

# 4. host nginx config swap (정적 root → backend proxy)
sudo cp /etc/nginx/sites-enabled/lostmemory-site /home/ubuntu/lostmemory-site.bak.$(date +%Y%m%d)
sudo tee /etc/nginx/sites-enabled/lostmemory-site > /dev/null <<'EOF'
server {
    server_name lostmemory.duckdns.org;

    location / {
        proxy_pass http://127.0.0.1:8081;
        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_redirect off;
    }

    listen 443 ssl;
    ssl_certificate /etc/letsencrypt/live/lostmemory.duckdns.org/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/lostmemory.duckdns.org/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;
}

server {
    if ($host = lostmemory.duckdns.org) {
        return 301 https://$host$request_uri;
    }
    listen 80;
    server_name lostmemory.duckdns.org;
    return 404;
}
EOF

sudo nginx -t
sudo systemctl reload nginx

# 5. 외부 검증
curl -I https://lostmemory.duckdns.org/                # → 200 (Spring 랜딩)
curl -I https://lostmemory.duckdns.org/admin/login     # → 200 (로그인 폼)
curl -I https://comfyui-lostmemory.duckdns.org/        # → 401 (ComfyUI, 변경 0)

# 6. 기존 정적 site archive (안전 안전망)
sudo mv /var/www/lostmemory-site /var/www/lostmemory-site.archive.$(date +%Y%m%d)
```

## 콘텐츠 운영 흐름

1. 운영자가 `https://lostmemory.duckdns.org/admin/login` 접속
2. `.env` 의 `ADMIN_USERNAME` / `ADMIN_PASSWORD` 로 로그인
3. 대시보드에서 공지/패치노트/FAQ 중 선택 → 새 항목 작성 또는 기존 편집
4. "공개" 체크박스로 published 토글 — published=true 만 외부 사이트에 노출
5. 변경 즉시 반영 (DB write 만, 별도 빌드/배포 step 없음)

## 운영 명령

```bash
cd /home/ubuntu/lostmemory/website

# 컨테이너 상태
docker compose ps
docker compose logs --tail=100 website-backend
docker compose logs --tail=100 cms-postgres

# 재시작
docker compose restart website-backend

# 코드 업데이트 후 재배포
git pull --ff-only origin develop
docker compose build website-backend
docker compose up -d   # depends_on healthy 가 잡혀있어 무중단에 가깝게 swap

# DB 백업 (매일 권장 — cron 등록)
docker compose exec -T cms-postgres pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" \
  > /home/ubuntu/backup/lostmemory_cms-$(date +%F).sql

# DB 복구
gunzip -c backup.sql.gz | docker compose exec -T cms-postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"
```

## Admin 비밀번호 변경

1. 임시 권장: SQL 직접 update (BCrypt hash 새로 생성 후)
   ```bash
   # BCrypt hash 생성 (별도 Spring Boot console 또는 온라인 tool)
   docker compose exec -T cms-postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c \
     "UPDATE admin_user SET password_hash='<NEW_BCRYPT_HASH>' WHERE username='admin';"
   ```
2. 향후 enhancement — admin UI 의 비밀번호 변경 form 추가 후속 PR.

## 로컬 개발 (옵션)

```bash
cd website
cp .env.example .env   # 로컬 dummy 비밀번호

# 옵션 a: docker compose 로 전체 (postgres + backend)
docker compose up -d

# 옵션 b: postgres 만 docker, backend 는 IntelliJ 에서 실행
docker compose up -d cms-postgres
# IntelliJ Run Configuration 에 환경변수 동일하게 주입
```

## 가드

- **`.env` git 커밋 금지** — 루트 `.gitignore` 의 `.env` 패턴 차단됨.
- **PostgreSQL 데이터 볼륨 보호** — `cms_postgres_data` named volume. `docker volume prune` 절대 금지.
- **첫 부팅 admin seed** — `.env` 의 `ADMIN_USERNAME` / `ADMIN_PASSWORD` 가 첫 부팅 시점의 초기 계정. seed 후 username 변경 시 새 계정 생성 (이전 계정과 별개).
- **세션 단일 instance** — in-memory session, backend 재시작 시 로그아웃. Redis 도입은 후속.
- **CSRF 활성화** — Thymeleaf `<form th:action>` 사용 시 자동 hidden field 삽입. 직접 fetch/XHR 호출 시 token 누락 시 403.
