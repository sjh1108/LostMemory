#!/bin/sh
# =====================================================================
# 배포 / 롤백 / 상태 조회 통합 스크립트
#
# Jenkins 자동 배포 와 운영자 SSH 수동 운영 모두 같은 스크립트로 처리한다.
#
# Sub-command:
#   deploy <build_number>    이미 빌드된 server-app:latest 를 server-app:<N> 으로 태깅 + up -d + healthy 폴링
#   rollback <build_number>  보존된 server-app:<N> 태그를 server-app:latest 로 재태깅 + recreate (image 빌드 X)
#   status                   현재 떠있는 app 컨테이너의 image / health
#   history                  보존된 server-app:<N> 태그 목록 (디스크 점유 같이)
#
# 실행 위치: 이 스크립트는 server/ 디렉토리에서 실행되어야 한다.
# =====================================================================

set -eu

cd "$(dirname "$0")/.."

ENV_FILE="${ENV_FILE:-.env}"
[ -f "$ENV_FILE" ] || { echo "[deploy][ERROR] $ENV_FILE 없음" >&2; exit 1; }

# ---------------------------------------------------------------------
# Sub-command 헬퍼
# ---------------------------------------------------------------------

# healthy 폴링 — 30회 × 3초 = 최대 90초.
# docker-compose.yml 의 app healthcheck (start_period 40s + retries 6 × interval 15s = ~130s) 와 정합.
wait_healthy() {
  i=1
  while [ "$i" -le 30 ]; do
    if docker compose --env-file "$ENV_FILE" ps app | grep -q "(healthy)"; then
      echo "[deploy] app healthy"
      return 0
    fi
    echo "[deploy] healthy 대기... ($i/30)"
    i=$((i+1))
    sleep 3
  done
  echo "[deploy][ERROR] app healthy 안 됨 — 마지막 100줄 로그:" >&2
  docker compose --env-file "$ENV_FILE" logs --tail=100 app >&2 || true
  exit 1
}

cmd_deploy() {
  N="${1:?[deploy] BUILD_NUMBER 인자 필수 — 예: ./scripts/deploy.sh deploy 14}"
  echo "[deploy] build #$N — server-app:latest -> :$N 태깅 + up -d + healthy 폴링"
  # Jenkins 의 Docker Build stage 가 먼저 server-app:latest 를 빌드해둔 상태를 전제.
  # 운영자가 SSH 에서 수동 실행 시 미리 'docker compose build app' 으로 :latest 를 만들어야 함.
  if ! docker image inspect server-app:latest > /dev/null 2>&1; then
    echo "[deploy][ERROR] server-app:latest 가 없음 — 'docker compose --env-file $ENV_FILE build app' 먼저 실행" >&2
    exit 1
  fi
  docker tag server-app:latest "server-app:$N" || true
  # relay 도 server-app:latest 공유 — app 새 image 시 relay 도 함께 갱신
  docker compose --env-file "$ENV_FILE" up -d app relay
  wait_healthy
  echo "[deploy] build #$N — 정상"
}

cmd_rollback() {
  N="${1:?[rollback] BUILD_NUMBER 인자 필수 — 예: ./scripts/deploy.sh rollback 13}"
  TAG="server-app:$N"
  if ! docker image inspect "$TAG" > /dev/null 2>&1; then
    echo "[rollback][ERROR] $TAG image 가 호스트에 없음. './scripts/deploy.sh history' 로 보존된 태그 확인" >&2
    exit 1
  fi
  echo "[rollback] $TAG -> server-app:latest 재태깅 + recreate"
  docker tag "$TAG" server-app:latest
  # relay 도 같은 image 공유 — 함께 force-recreate 해서 옛 image 잔존 차단
  docker compose --env-file "$ENV_FILE" up -d --force-recreate app relay
  wait_healthy
  echo "[rollback] build #$N 으로 복귀 완료"
}

cmd_status() {
  echo "[status] 현재 app 컨테이너:"
  docker compose --env-file "$ENV_FILE" ps app
  echo ""
  echo "[status] image:"
  if docker inspect server-app-1 > /dev/null 2>&1; then
    docker inspect server-app-1 --format 'Image ID:   {{.Image}}
Image Name: {{.Config.Image}}
State:      {{.State.Status}} (health: {{.State.Health.Status}})'
  else
    echo "(server-app-1 컨테이너 없음 — 한 번도 띄워진 적 없거나 stop+rm 됨)"
  fi
}

cmd_history() {
  echo "[history] 보존된 server-app 태그 (latest 제외):"
  docker images server-app --format '{{.Tag}}\t{{.Size}}\t{{.CreatedSince}}' \
    | awk '$1 != "latest" { print }' \
    | sort -t '	' -k1 -r -n
}

# ---------------------------------------------------------------------
# Sub-command dispatch
# ---------------------------------------------------------------------

case "${1:-}" in
  deploy)   shift; cmd_deploy "$@" ;;
  rollback) shift; cmd_rollback "$@" ;;
  status)   shift; cmd_status "$@" ;;
  history)  shift; cmd_history "$@" ;;
  *)
    cat >&2 <<'EOF'
사용법: ./scripts/deploy.sh <command> [args]

Commands:
  deploy <N>     server-app:latest 를 server-app:N 으로 태깅 + up -d + healthy 폴링
                 (Jenkins 자동 배포가 호출하는 경로)
  rollback <N>   보존된 server-app:N 태그로 즉시 복귀 (image 빌드 없이 ~10초)
  status         현재 app 컨테이너의 image / state / health
  history        보존된 server-app:* 태그 목록 (디스크 점유 같이)
EOF
    exit 2 ;;
esac
