#!/bin/sh
# =====================================================================
# Let's Encrypt 자동 갱신 wrapper — root crontab 에서 호출
#
# 동작:
#   1) certbot renew 실행 (만료 30일 이내일 때만 실제 갱신, 평소엔 no-op)
#   2) nginx -s reload 로 갱신된 cert 반영
#
# crontab 등록 예시 (sudo crontab -e):
#   17 3 * * 1 /home/ubuntu/lostmemory/server/scripts/certbot-renew.sh \
#     >> /var/log/certbot-renew.log 2>&1
# =====================================================================

set -eu

# 실제 EC2 배포 경로 — 변경 시 cron 등록도 같이 갱신
SERVER_DIR="${SERVER_DIR:-/home/ubuntu/lostmemory/server}"
ENV_FILE="${ENV_FILE:-$SERVER_DIR/.env}"

cd "$SERVER_DIR"

if [ ! -f "$ENV_FILE" ]; then
  echo "[renew][ERROR] $ENV_FILE 없음" >&2
  exit 1
fi

echo "[renew] $(date -Iseconds) certbot renew 시작"
docker compose --env-file "$ENV_FILE" --profile certbot run --rm certbot \
  renew --quiet

echo "[renew] nginx reload"
docker compose --env-file "$ENV_FILE" exec nginx nginx -s reload

echo "[renew] $(date -Iseconds) 완료"
