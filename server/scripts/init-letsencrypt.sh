#!/bin/sh
# =====================================================================
# Let's Encrypt 첫 발급 부트스트랩 스크립트 (1회 실행)
#
# 동작 순서:
#   1) .env 검증 (DOMAIN / LETSENCRYPT_EMAIL)
#   2) named volume `certbot_etc` 에 self-signed dummy cert 생성
#      → nginx 가 443 ssl 블록을 가진 채로 정상 기동
#   3) `docker compose up -d nginx` 로 nginx 기동
#   4) dummy cert 삭제 + Let's Encrypt staging 으로 dry-run (rate limit 보호)
#   5) staging 성공 시 prod 발급
#   6) `docker compose exec nginx nginx -s reload` 로 진짜 cert 반영
#
# 실행 위치: 이 스크립트는 server/ 디렉토리에서 실행되어야 한다.
# =====================================================================

set -eu

cd "$(dirname "$0")/.."

ENV_FILE="${ENV_FILE:-.env}"

if [ ! -f "$ENV_FILE" ]; then
  echo "[ERROR] $ENV_FILE 가 없다. server/.env.example 참고해서 작성." >&2
  exit 1
fi

# .env 로드 (export 안 된 KEY=VALUE 도 읽기)
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

: "${DOMAIN:?[.env] DOMAIN 미설정}"
: "${LETSENCRYPT_EMAIL:?[.env] LETSENCRYPT_EMAIL 미설정}"

echo "[init] DOMAIN=$DOMAIN, EMAIL=$LETSENCRYPT_EMAIL"

# ---------------------------------------------------------------------
# 1) dummy self-signed cert 생성
# ---------------------------------------------------------------------
# certbot 컨테이너의 entrypoint 를 sh 로 덮어써서 openssl 직접 실행.
# certbot/certbot 이미지에는 openssl 이 포함되어 있다.
# named volume 기준 경로이므로 호스트 경로는 신경 X.
echo "[init] dummy self-signed cert 생성"
docker compose --env-file "$ENV_FILE" --profile certbot run --rm \
  --entrypoint sh certbot -c "
    set -eu
    mkdir -p /etc/letsencrypt/live/${DOMAIN}
    openssl req -x509 -nodes -newkey rsa:2048 -days 1 \
      -keyout /etc/letsencrypt/live/${DOMAIN}/privkey.pem \
      -out    /etc/letsencrypt/live/${DOMAIN}/fullchain.pem \
      -subj   /CN=localhost
    echo 'dummy cert 생성 완료'
  "

# ---------------------------------------------------------------------
# 2) nginx 기동 (이미 떠있으면 reload 만)
# ---------------------------------------------------------------------
echo "[init] nginx 기동/재기동"
docker compose --env-file "$ENV_FILE" up -d nginx

# nginx 가 dummy cert 로 정상 기동했는지 잠깐 대기 후 확인
sleep 3
docker compose --env-file "$ENV_FILE" ps nginx

# ---------------------------------------------------------------------
# 3) dummy 삭제 + staging dry-run
# ---------------------------------------------------------------------
echo "[init] dummy cert 삭제"
docker compose --env-file "$ENV_FILE" --profile certbot run --rm \
  --entrypoint sh certbot -c "
    rm -Rf /etc/letsencrypt/live/${DOMAIN} \
           /etc/letsencrypt/archive/${DOMAIN} \
           /etc/letsencrypt/renewal/${DOMAIN}.conf
    echo 'dummy cert 삭제 완료'
  "

echo "[init] Let's Encrypt staging 으로 dry-run"
docker compose --env-file "$ENV_FILE" --profile certbot run --rm certbot \
  certonly --webroot -w /var/www/certbot \
  -d "${DOMAIN}" \
  --email "${LETSENCRYPT_EMAIL}" \
  --agree-tos --no-eff-email \
  --staging \
  --non-interactive

# staging 성공 직후 staging cert 도 정리해서 prod 발급에 충돌 안 나게 함
echo "[init] staging cert 정리"
docker compose --env-file "$ENV_FILE" --profile certbot run --rm \
  --entrypoint sh certbot -c "
    rm -Rf /etc/letsencrypt/live/${DOMAIN} \
           /etc/letsencrypt/archive/${DOMAIN} \
           /etc/letsencrypt/renewal/${DOMAIN}.conf
    echo 'staging cert 정리 완료'
  "

# ---------------------------------------------------------------------
# 4) prod 발급
# ---------------------------------------------------------------------
echo "[init] Let's Encrypt prod 발급"
docker compose --env-file "$ENV_FILE" --profile certbot run --rm certbot \
  certonly --webroot -w /var/www/certbot \
  -d "${DOMAIN}" \
  --email "${LETSENCRYPT_EMAIL}" \
  --agree-tos --no-eff-email \
  --non-interactive

# ---------------------------------------------------------------------
# 5) nginx reload — 진짜 cert 반영
# ---------------------------------------------------------------------
echo "[init] nginx -t 문법 검증"
docker compose --env-file "$ENV_FILE" exec nginx nginx -t

echo "[init] nginx -s reload"
docker compose --env-file "$ENV_FILE" exec nginx nginx -s reload

echo ""
echo "[init] 완료. 검증 명령:"
echo "  curl -I https://${DOMAIN}/api/actuator/health"
echo "  curl -I http://${DOMAIN}/api/actuator/health     # 301"
echo "  openssl s_client -connect ${DOMAIN}:443 -servername ${DOMAIN} </dev/null 2>/dev/null | openssl x509 -noout -issuer -dates"
