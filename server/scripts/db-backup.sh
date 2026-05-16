#!/bin/sh
# =====================================================================
# PostgreSQL 백업 스크립트 — root crontab 에서 호출 + 수동 실행 모두 지원
#
# 정책:
#   1) docker compose exec 로 컨테이너 안 pg_dump 실행
#      postgres 의 backend network 가 internal: true 라 host TCP 접근 불가
#   2) .env 의 POSTGRES_USER / POSTGRES_DB / POSTGRES_PASSWORD 만 사용
#      운영 비번은 .env 에만 존재 — 스크립트에 hardcode 금지
#   3) 출력: $BACKUP_DIR/lostmemory_YYYY-MM-DD_HHmmss.sql.gz (gzip -9)
#   4) retention: RETENTION_DAYS (기본 14) 일 이상 백업 자동 제거
#      docker-prune.sh 와 동일 패턴
#   5) pg_dump exit code 와 결과 크기 둘 다 검증 — pipefail 미사용 (dash 호환)
#
# 실행 위치: 이 스크립트는 server/ 디렉토리 기준으로 동작한다.
#
# 1회 수동 실행:
#   /home/ubuntu/lostmemory/server/scripts/db-backup.sh
#
# crontab 등록 예시 (sudo crontab -e):
#   30 2 * * * /home/ubuntu/lostmemory/server/scripts/db-backup.sh \
#     >> /var/log/db-backup.log 2>&1
#
# 환경변수 override:
#   BACKUP_DIR        백업 출력 디렉터리 (기본 /home/ubuntu/lostmemory/backups)
#   RETENTION_DAYS    보존 일수 (기본 14)
#   ENV_FILE          .env 경로 (기본 ./.env — 즉 server/.env)
#
# 복원 절차 / 장애 대응: docs/runbook-db-backup.md 참조
# =====================================================================

set -eu

cd "$(dirname "$0")/.."

ENV_FILE="${ENV_FILE:-.env}"
[ -f "$ENV_FILE" ] || { echo "[db-backup][ERROR] $ENV_FILE 없음" >&2; exit 1; }

BACKUP_DIR="${BACKUP_DIR:-/home/ubuntu/lostmemory/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

# ---------------------------------------------------------------------
# .env source — set -a 동안 정의된 변수는 자동 export
# dash 의 `.` 은 변수 자동 export 안 하므로 set -a 로 강제
# ---------------------------------------------------------------------
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

: "${POSTGRES_USER:?[db-backup][ERROR] POSTGRES_USER 가 $ENV_FILE 에 없음}"
: "${POSTGRES_DB:?[db-backup][ERROR] POSTGRES_DB 가 $ENV_FILE 에 없음}"
: "${POSTGRES_PASSWORD:?[db-backup][ERROR] POSTGRES_PASSWORD 가 $ENV_FILE 에 없음}"

mkdir -p "$BACKUP_DIR"

TIMESTAMP=$(date +%Y-%m-%d_%H%M%S)
OUT_FILE="$BACKUP_DIR/lostmemory_${TIMESTAMP}.sql.gz"
TMP_SQL="${OUT_FILE%.gz}"

echo "[db-backup] $(date -Iseconds) 백업 시작"
echo "[db-backup] target: $POSTGRES_DB (user: $POSTGRES_USER)"
echo "[db-backup] output: $OUT_FILE"
echo "[db-backup] before: $(df -h "$BACKUP_DIR" | awk 'NR==2 {print $3" used / "$2" total / "$5" full"}')"

# 실패 시 임시 파일 정리
trap 'rm -f "$TMP_SQL" "$OUT_FILE.partial"' EXIT

# ---------------------------------------------------------------------
# pg_dump 실행
#   -T                       TTY 비할당 (cron 환경 호환)
#   -e PGPASSWORD            컨테이너 안 pg_dump 인증
#   --clean --if-exists      복원 시 기존 객체 DROP 후 재생성 (idempotent)
#   --no-owner --no-privileges  복원 환경 user/role 차이로 인한 실패 차단
# ---------------------------------------------------------------------
set +e
docker compose --env-file "$ENV_FILE" exec -T \
  -e PGPASSWORD="$POSTGRES_PASSWORD" \
  postgres \
  pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
    --clean --if-exists --no-owner --no-privileges \
  > "$TMP_SQL"
DUMP_RC=$?
set -e

if [ "$DUMP_RC" -ne 0 ]; then
  echo "[db-backup][ERROR] pg_dump 실패 (exit $DUMP_RC) — postgres 컨테이너 상태 확인 필요" >&2
  exit 2
fi

if [ ! -s "$TMP_SQL" ]; then
  echo "[db-backup][ERROR] pg_dump 결과 비어있음 — 인증/권한/네트워크 점검" >&2
  exit 2
fi

# gzip in-place: $TMP_SQL → $OUT_FILE, 원본 삭제
gzip -9 "$TMP_SQL"

# trap 정리 — 정상 완료
trap - EXIT

SIZE=$(du -h "$OUT_FILE" | awk '{print $1}')
echo "[db-backup] OK: 백업 완료 (size: $SIZE)"

# ---------------------------------------------------------------------
# retention: RETENTION_DAYS 이상 백업 파일 제거
# ---------------------------------------------------------------------
echo "[db-backup] retention: ${RETENTION_DAYS}일 이상 백업 제거"
find "$BACKUP_DIR" -maxdepth 1 -type f -name 'lostmemory_*.sql.gz' \
  -mtime "+${RETENTION_DAYS}" -print -delete || true

echo "[db-backup] after:  $(df -h "$BACKUP_DIR" | awk 'NR==2 {print $3" used / "$2" total / "$5" full"}')"
echo "[db-backup] $(date -Iseconds) 완료"
