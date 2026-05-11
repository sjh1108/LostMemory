#!/bin/sh
# Alertmanager 컨테이너 entrypoint.
#
# MATTERMOST_WEBHOOK_URL env 가 비어있으면 startup fail (silent skip 회피 — 메모리 룰
# reference_envvar_default_policy.md 와 정합).
# URL 은 /tmp/secrets/mattermost-webhook-url 파일로 작성 후 alertmanager.yml 의
# api_url_file 이 참조 — YAML 평문에 URL 노출 X.
#
# docker-compose.monitoring.yml 의 entrypoint 가 이 파일을 호출:
#   entrypoint: ["/bin/sh", "/etc/alertmanager/entrypoint.sh"]

set -eu

if [ -z "${MATTERMOST_WEBHOOK_URL:-}" ]; then
  echo "[alertmanager] ERROR: MATTERMOST_WEBHOOK_URL env 미설정 — server/.env 갱신 후 재기동" >&2
  exit 1
fi

mkdir -p /tmp/secrets
printf '%s' "$MATTERMOST_WEBHOOK_URL" > /tmp/secrets/mattermost-webhook-url
chmod 600 /tmp/secrets/mattermost-webhook-url

exec /bin/alertmanager \
  --config.file=/etc/alertmanager/alertmanager.yml \
  --storage.path=/alertmanager
