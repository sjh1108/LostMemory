#!/bin/sh
# =====================================================================
# Docker 자원 자동 정리 wrapper — root crontab 에서 호출
#
# 정책:
#   1) docker image prune -f         : dangling image (untagged + 컨테이너 미참조) 만 제거
#                                       → 태그 붙은 server-app:N rollback 이력 보존
#   2) docker builder prune --filter "until=720h"
#                                     : 30일 이상된 BuildKit 캐시만 제거
#                                       → 30일 미만은 다음 빌드 캐시 hit 유지
#   3) volume / network / running container 절대 미건드림
#      (postgres_data / redis_data / certbot_etc / certbot_webroot 보호)
#
# crontab 등록 예시 (sudo crontab -e):
#   30 3 * * 0 /home/ubuntu/lostmemory/server/scripts/docker-prune.sh \
#     >> /var/log/docker-prune.log 2>&1
# =====================================================================

set -eu

echo "[prune] $(date -Iseconds) docker prune 시작"
echo "[prune] before: $(df -h / | awk 'NR==2 {print $3" used / "$2" total / "$5" full"}')"

echo "[prune] dangling image 제거"
docker image prune -f

echo "[prune] 720h 이상 build cache 제거"
docker builder prune -f --filter "until=720h"

echo "[prune] after:  $(df -h / | awk 'NR==2 {print $3" used / "$2" total / "$5" full"}')"
echo "[prune] $(date -Iseconds) 완료"
