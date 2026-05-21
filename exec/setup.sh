#!/usr/bin/env bash
# =====================================================================
# LostMemory (S14P31C201) 운영 EC2 부트스트랩 wrapper
#
# 0 상태의 Ubuntu 22.04 EC2 에서 메인 stack 까지 띄우는 idempotent wrapper.
# 운영 로직은 server/scripts/*.sh 가 전담 — 본 wrapper 는 호출만 함.
#
# 사용법:
#   exec/setup.sh [--with-monitoring] <step>
#
# Steps:
#   prerequisites   docker / compose / openssl / curl / git 설치 검사
#   env             4세트 .env 존재 확인 + 미생성 시 example 복사 + 필수 키 미채움 검출
#   game-stack      server/ 의 postgres + redis + app + relay up + healthcheck
#   cert            server/scripts/init-letsencrypt.sh 호출 (이미 cert 있으면 skip)
#   cms             website/ 의 cms-postgres + website-backend up
#   ai-tool         tools/infra/ 의 nginx + postgres up + ai-server bootRun 안내
#   monitoring      --with-monitoring 일 때만 prometheus + grafana + alertmanager up
#   cron            certbot-renew / db-backup / docker-prune crontab 라인 echo (자동 등록 X)
#   all             prerequisites → env → game-stack → cert → cms → ai-tool → (monitoring) → cron
#
# 안전 정책:
#   - destructive 명령 (down -v / crontab write / git force push) 미포함
#   - .env source 안 함 (dash 함정 회피) — grep 으로 한 줄씩 읽음
#   - bash 명시 (/bin/sh dash 호환성 회피)
# =====================================================================
set -euo pipefail

# ---------- 로깅 ----------
log()  { echo "[setup] $*"; }
warn() { echo "[setup][WARN] $*" >&2; }
err()  { echo "[setup][ERROR] $*" >&2; exit 1; }
step() { echo ""; echo "========== [$1] $2 =========="; }

# ---------- 경로 ----------
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"
SERVER_DIR="$REPO_ROOT/server"
WEBSITE_DIR="$REPO_ROOT/website"
AI_INFRA_DIR="$REPO_ROOT/tools/infra"
AI_SERVER_DIR="$REPO_ROOT/tools/ai_server"

# ---------- 옵션 ----------
WITH_MONITORING=0
STEP=""

usage() {
  cat <<'EOF'
사용법: exec/setup.sh [--with-monitoring] [--help] <step>

Steps:
  prerequisites   docker / compose / openssl / curl / git 설치 검사
  env             4세트 .env 존재 확인 + 미생성 시 example 복사 + 필수 키 검사
  game-stack      게임 stack 기동 (postgres + redis + app + relay) + healthcheck
  cert            Let's Encrypt 첫 발급 (이미 cert 있으면 skip)
  cms             CMS stack 기동 (cms-postgres + website-backend)
  ai-tool         AI 도구 인프라 nginx + postgres 기동 + ai-server bootRun 안내
  monitoring      모니터링 stack (--with-monitoring 필수)
  cron            cron 라인 echo (자동 등록 안 함)
  all             prerequisites → env → game-stack → cert → cms → ai-tool → (monitoring) → cron

Options:
  --with-monitoring   all 흐름에 monitoring step 포함
  --help, -h          본 도움말

예시:
  exec/setup.sh --help
  exec/setup.sh prerequisites
  exec/setup.sh all
  exec/setup.sh --with-monitoring all

검증 (all 완료 후):
  curl -I https://<DOMAIN>/api/actuator/health
  curl -I https://<WEBSITE_DOMAIN>/
EOF
}

# ---------- prerequisites ----------
prerequisites() {
  step 1/8 "필수 도구 설치 확인"
  command -v docker  >/dev/null || err "docker 미설치 — 'curl -fsSL https://get.docker.com | sudo bash' 후 재시도"
  docker compose version >/dev/null 2>&1 || err "docker compose plugin 미설치 — 'sudo apt install docker-compose-plugin' 후 재시도"
  command -v openssl >/dev/null || err "openssl 미설치"
  command -v curl    >/dev/null || err "curl 미설치"
  command -v git     >/dev/null || err "git 미설치"
  log "docker        $(docker --version 2>/dev/null | awk '{print $3}' | tr -d ,)"
  log "docker compose $(docker compose version --short 2>/dev/null)"
  log "필수 도구 OK"
}

# ---------- env ----------
ensure_env_file() {
  local target="$1"    # 절대 경로 .env
  local example="$2"   # 절대 경로 .env.example
  if [ -f "$target" ]; then
    log "$target 존재 — skip"
    return 0
  fi
  [ -f "$example" ] || err "$example 미존재 — repository tree 가 정상인지 확인"
  cp "$example" "$target"
  warn "$target 신규 생성 ($example 복사). 운영 비밀번호 / secret 으로 즉시 채워야 합니다."
  warn "  편집:  vi $target"
  warn "  생성:  openssl rand -base64 32   # JWT_SECRET 류"
  warn "  생성:  openssl rand -base64 24   # POSTGRES_PASSWORD 류"
}

# 필수 키가 비어있거나 placeholder 인지 검사. placeholder 면 다음 step 진입 차단.
validate_env_filled() {
  local file="$1"
  shift
  local keys="$*"
  local missing=""
  local k v
  for k in $keys; do
    v="$(grep -E "^${k}=" "$file" | head -1 | cut -d= -f2- | sed 's/[[:space:]]*$//')"
    if [ -z "$v" ]; then
      missing="$missing $k(empty)"
      continue
    fi
    # placeholder 패턴 — change-me / replace-me / <...> / CHANGE-ME / your-prod-domain
    case "$v" in
      change-me*|*change-me*|CHANGE-ME*|*CHANGE-ME*|replace-me*|*replace-me*|*"<"*">"*|your-prod-domain*|admin@example.com)
        missing="$missing $k(placeholder)"
        ;;
    esac
  done
  if [ -n "$missing" ]; then
    err "$file 의 다음 키가 미채움 또는 placeholder 그대로:$missing"
  fi
}

env_step() {
  step 2/8 "환경 변수 파일 (.env) 4세트 확인 / 생성"

  ensure_env_file "$SERVER_DIR/.env"      "$SERVER_DIR/.env.example"
  ensure_env_file "$WEBSITE_DIR/.env"     "$WEBSITE_DIR/.env.example"
  ensure_env_file "$AI_SERVER_DIR/.env"   "$AI_SERVER_DIR/.env.example"
  ensure_env_file "$AI_INFRA_DIR/.env"    "$AI_INFRA_DIR/.env.example"

  # 운영 차단 키만 검사 — secret 류는 운영자가 채웠는지만 sanity 확인
  validate_env_filled "$SERVER_DIR/.env" \
    POSTGRES_PASSWORD REDIS_PASSWORD JWT_SECRET \
    JWT_ACCESS_EXPIRATION JWT_REFRESH_EXPIRATION JWT_SESSION_EXPIRATION \
    DOMAIN LETSENCRYPT_EMAIL MATTERMOST_WEBHOOK_URL

  validate_env_filled "$WEBSITE_DIR/.env" \
    POSTGRES_PASSWORD ADMIN_PASSWORD ADMIN_BASE_PATH

  validate_env_filled "$AI_INFRA_DIR/.env" \
    PUBLIC_DOMAIN COMFYUI_DOMAIN LETSENCRYPT_EMAIL POSTGRES_PASSWORD

  log ".env 4세트 OK"
}

# ---------- game-stack ----------
game_stack() {
  step 3/8 "게임 서버 stack 기동 (postgres + redis + app + relay)"
  cd "$SERVER_DIR"

  if ! docker image inspect server-app:latest >/dev/null 2>&1; then
    log "server-app:latest image 미존재 — docker compose build app"
    docker compose --env-file .env build app
  else
    log "server-app:latest image 존재 — build 생략"
  fi

  log "postgres + redis + app + relay up -d"
  docker compose --env-file .env up -d postgres redis app relay

  wait_compose_healthy "$SERVER_DIR" app   30
  wait_compose_healthy "$SERVER_DIR" relay 20
  log "게임 stack healthy"
}

# ---------- cert ----------
cert_step() {
  step 4/8 "Let's Encrypt 인증서 (game) 첫 발급"
  cd "$SERVER_DIR"

  local domain
  domain="$(grep -E '^DOMAIN=' .env | head -1 | cut -d= -f2-)"
  [ -n "$domain" ] || err "server/.env 의 DOMAIN 비어있음"

  # 이미 발급된 cert 있는지 검사 — certbot_etc volume 안 live/<DOMAIN>/fullchain.pem
  if docker run --rm \
       -v server_certbot_etc:/etc/letsencrypt \
       alpine \
       test -s "/etc/letsencrypt/live/${domain}/fullchain.pem" 2>/dev/null; then
    log "이미 발급된 cert 존재 (live/${domain}/fullchain.pem) — skip"
    log "갱신은 cron 의 certbot-renew.sh 로 자동 (cron step 안내 참조)"
    docker compose --env-file .env up -d nginx
    return 0
  fi

  log "init-letsencrypt.sh 호출 — dummy cert → nginx up → staging dry-run → prod 발급"
  bash "$SERVER_DIR/scripts/init-letsencrypt.sh"
}

# ---------- cms ----------
cms_step() {
  step 5/8 "CMS stack 기동 (cms-postgres + website-backend)"
  cd "$WEBSITE_DIR"

  if ! docker image inspect lostmemory-website:latest >/dev/null 2>&1; then
    log "lostmemory-website:latest image 미존재 — docker compose build website-backend"
    docker compose --env-file .env build website-backend
  else
    log "lostmemory-website:latest image 존재 — build 생략"
  fi

  log "cms-postgres + website-backend up -d"
  docker compose --env-file .env up -d

  wait_compose_healthy "$WEBSITE_DIR" website-backend 30
  log "CMS stack healthy"
  log "host nginx 가 127.0.0.1:$(grep -E '^SERVER_PORT=' .env | cut -d= -f2-) 로 reverse proxy 설정되어 있는지 확인"
}

# ---------- ai-tool ----------
ai_tool_step() {
  step 6/8 "AI 도구 인프라 stack 기동 (nginx + postgres) + ai-server bootRun 안내"
  cd "$AI_INFRA_DIR"

  log "nginx + postgres up -d"
  docker compose --env-file .env up -d nginx postgres

  wait_compose_healthy "$AI_INFRA_DIR" postgres 20

  warn "ai-server 컨테이너는 tools/infra/docker-compose.yml 에 아직 정의되지 않았습니다 (백엔드 미통합)."
  warn "현재 ai-server 를 띄우려면 host 에서 직접 실행:"
  warn "  cd $AI_SERVER_DIR && cp .env.example .env && vi .env && ./gradlew bootRun"
  warn ""
  warn "ComfyUI 는 외부 GPU 호스트 / RunPod 에 별도 기동되어 있어야 합니다."
  warn "  tools/infra/.env 의 COMFYUI_UPSTREAM 이 reachable 인지 확인:"
  warn "    curl -fsS \$COMFYUI_UPSTREAM/system_stats"
  warn ""
  warn "AI 도구 도메인 (PUBLIC_DOMAIN / COMFYUI_DOMAIN) 인증서 발급은 별도:"
  warn "  cd $AI_INFRA_DIR"
  warn "  docker compose --env-file .env --profile certbot run --rm certbot \\"
  warn "    certonly --webroot -w /var/www/certbot -d <PUBLIC_DOMAIN> -d <COMFYUI_DOMAIN>"
}

# ---------- monitoring ----------
monitoring_step() {
  step 7/8 "모니터링 stack (선택)"
  if [ "$WITH_MONITORING" != "1" ]; then
    log "--with-monitoring 미지정 — skip"
    return 0
  fi

  cd "$SERVER_DIR"
  validate_env_filled "$SERVER_DIR/.env" GRAFANA_ADMIN_PASSWORD

  log "monitoring stack up -d"
  docker compose -f docker-compose.yml -f docker-compose.monitoring.yml --env-file .env up -d \
    prometheus grafana alertmanager node-exporter cadvisor

  local domain
  domain="$(grep -E '^DOMAIN=' .env | head -1 | cut -d= -f2-)"
  log "Grafana: https://${domain}/grafana/ (admin / GRAFANA_ADMIN_PASSWORD)"
}

# ---------- cron ----------
cron_step() {
  step 8/8 "운영 cron 등록 안내 (자동 등록 안 함)"
  cat <<EOF

다음 3 라인을 운영자 sudo crontab -e 로 직접 등록하세요.
경로는 운영자 home (보통 /home/ubuntu/lostmemory) 기준 절대경로로 작성합니다.

# Let's Encrypt 갱신 (매주 월 03:17)
17 3 * * 1 ${SERVER_DIR}/scripts/certbot-renew.sh >> /var/log/certbot-renew.log 2>&1

# PostgreSQL 백업 (매일 02:30)
30 2 * * * ${SERVER_DIR}/scripts/db-backup.sh >> /var/log/db-backup.log 2>&1

# Docker 자원 정리 (매주 일 03:30)
30 3 * * 0 ${SERVER_DIR}/scripts/docker-prune.sh >> /var/log/docker-prune.log 2>&1

본 wrapper 는 destructive 동작 (기존 crontab 덮어쓰기) 회피 정책으로
자동 등록하지 않습니다. 등록 후 확인은 sudo crontab -l 로.

EOF
}

# ---------- helpers ----------
wait_compose_healthy() {
  local dir="$1"
  local svc="$2"
  local max="$3"   # 시도 회수 (× 3초)
  cd "$dir"

  local i=1
  while [ "$i" -le "$max" ]; do
    if docker compose --env-file .env ps "$svc" 2>/dev/null | grep -qE '(healthy)|(Up)'; then
      # healthy 라벨 / Up 둘 중 하나면 OK (healthcheck 미정의 service 대비)
      if docker compose --env-file .env ps "$svc" 2>/dev/null | grep -q 'starting\|unhealthy'; then
        :  # 아직 시작 중 / unhealthy — 폴링 계속
      else
        log "$svc healthy"
        return 0
      fi
    fi
    log "$svc healthy 대기... ($i/$max)"
    i=$((i + 1))
    sleep 3
  done
  warn "$svc healthy timeout — 로그 마지막 50줄:"
  docker compose --env-file .env logs --tail=50 "$svc" >&2 || true
  return 1
}

final_verification() {
  echo ""
  echo "================================================================"
  echo "[setup] 전체 step 완료. 아래 명령으로 외부 도달 확인:"
  echo "================================================================"
  local server_domain website_domain public_domain
  server_domain="$(grep  -E '^DOMAIN='         "$SERVER_DIR/.env"     2>/dev/null | head -1 | cut -d= -f2-)"
  website_domain="$(grep -E '^WEBSITE_DOMAIN=' "$AI_INFRA_DIR/.env"   2>/dev/null | head -1 | cut -d= -f2-)"
  public_domain="$(grep  -E '^PUBLIC_DOMAIN='  "$AI_INFRA_DIR/.env"   2>/dev/null | head -1 | cut -d= -f2-)"

  cat <<EOF
  curl -I https://${server_domain:-<DOMAIN>}/api/actuator/health
  curl -I https://${website_domain:-<WEBSITE_DOMAIN>}/
  curl -I https://${public_domain:-<PUBLIC_DOMAIN>}/
  nc -uvz ${server_domain:-<DOMAIN>} 7777

다음 단계:
  - cron 라인 등록 (위 [8/8] 출력 참고)
  - Jenkins 컨테이너 별도 기동:
      docker compose -f $SERVER_DIR/docker-compose.yml -f $SERVER_DIR/docker-compose.jenkins.yml \\
        --env-file $SERVER_DIR/.env up -d jenkins
  - Jenkins UI 에서 Pipeline job + credential 설정 (docs/ci-jenkins-setup.md 참고)
EOF
}

# ---------- main dispatch ----------
while [ $# -gt 0 ]; do
  case "$1" in
    --with-monitoring) WITH_MONITORING=1; shift ;;
    --help|-h)         usage; exit 0 ;;
    prerequisites|env|game-stack|cert|cms|ai-tool|monitoring|cron|all)
      STEP="$1"; shift ;;
    *) err "알 수 없는 옵션 / step: $1 (--help 참고)" ;;
  esac
done
STEP="${STEP:-all}"

case "$STEP" in
  prerequisites) prerequisites ;;
  env)           env_step ;;
  game-stack)    game_stack ;;
  cert)          cert_step ;;
  cms)           cms_step ;;
  ai-tool)       ai_tool_step ;;
  monitoring)    WITH_MONITORING=1; monitoring_step ;;
  cron)          cron_step ;;
  all)
    prerequisites
    env_step
    game_stack
    cert_step
    cms_step
    ai_tool_step
    monitoring_step
    cron_step
    final_verification
    ;;
esac
