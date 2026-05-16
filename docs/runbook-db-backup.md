# 운영 런북 — PostgreSQL 정기 백업 (db-backup.sh)

- **대상**: 운영 EC2 `k14c201.p.ssafy.io` 의 `postgres` 컨테이너 (`postgres:16-alpine`)
- **소요**: 백업 1회 약 **수 초 ~ 수십 초** (DB 크기에 비례), 운영 트래픽 중단 없음
- **선행 도구**: [`server/scripts/db-backup.sh`](../server/scripts/db-backup.sh)
- **상태**: 초안 (S14P31C201-132). 첫 수동 실행 결과는 본 문서 부록 A 참조.

> 본 런북은 **논리 백업 (pg_dump → gzip)** 을 다룹니다. 물리 백업 / WAL 아카이빙 / PITR 은 본 런북 밖이며 운영 자원이 확보되면 별도 티켓으로 분리합니다.

---

## 0. 환경 사실 (sticky)

| 항목 | 값 |
|---|---|
| 운영 EC2 호스트 | `k14c201.p.ssafy.io` |
| SSH 진입 | `ssh ubuntu@k14c201.p.ssafy.io` |
| 운영 working tree 경로 | `~/lostmemory` |
| compose 디렉토리 | `~/lostmemory/server` |
| postgres 컨테이너 이름 | `server-postgres-1` |
| compose env file | `~/lostmemory/server/.env` (LF 유지) |
| 백업 출력 디렉토리 | `~/lostmemory/backups` (`BACKUP_DIR` 으로 override 가능) |
| 백업 파일명 규칙 | `lostmemory_YYYY-MM-DD_HHmmss.sql.gz` |
| 기본 retention | **14일** (`RETENTION_DAYS` 으로 override 가능) |
| cron 권장 시각 | `30 2 * * *` (매일 02:30, docker-prune / certbot 과 충돌 없음) |
| cron 로그 | `/var/log/db-backup.log` |

---

## 1. 언제 / 무엇을 하는가

| 시점 | 액션 |
|---|---|
| 운영 EC2 초기 1회 | 본 런북 §2 (1회 수동 실행) → §3 (cron 등록) |
| 매일 02:30 자동 | cron 이 `db-backup.sh` 호출, 로그를 `/var/log/db-backup.log` 에 append |
| 발표/배포 리허설 직전 | `§2` 의 수동 실행을 한 번 더 — 최신 스냅샷 확보 |
| 데이터 사고 의심 시 | `§4` 복원 절차 진입 검토 (BE 와 합의 후) |
| Mattermost cron 알림 실패 | `§5` 검증 / 모니터링 진입 |

**하지 말아야 할 케이스**:
- postgres 컨테이너가 `(unhealthy)` 상태인 동안의 백업 (corrupt dump 위험)
- 운영 .env 미동기화 (`POSTGRES_PASSWORD` 가 실제 컨테이너 비번과 다른 상태)
- 디스크 가용량 < 1GB 인 EC2 (백업 자체가 OOM 디스크 유발)

---

## 2. 사전조건 체크리스트 + 1회 수동 실행

### 2.1 사전조건

```bash
ssh ubuntu@k14c201.p.ssafy.io
cd ~/lostmemory/server

# (a) develop sync 확인 — db-backup.sh 가 working tree 에 있어야 함
git fetch origin develop
LOCAL=$(git rev-parse HEAD)
REMOTE=$(git rev-parse origin/develop)
[ "$LOCAL" = "$REMOTE" ] && echo "OK: synced" || echo "WARN: pull 검토"

# (b) 스크립트 존재 + 실행권한
ls -l ./scripts/db-backup.sh
# -rwxr-xr-x ... 가 보여야 함. 'x' 빠지면 chmod +x ./scripts/db-backup.sh

# (c) postgres 컨테이너 healthy
docker compose --env-file .env ps postgres
# STATUS 가 (healthy) 이어야 함

# (d) 디스크 여유
df -h ~
# Avail 컬럼 1GB 이상이어야 안전
```

### 2.2 1회 수동 실행

```bash
ssh ubuntu@k14c201.p.ssafy.io
sudo /home/ubuntu/lostmemory/server/scripts/db-backup.sh
```

**기대 출력 (요약)**:

```
[db-backup] 2026-05-16T02:30:00+09:00 백업 시작
[db-backup] target: appdb (user: appuser)
[db-backup] output: /home/ubuntu/lostmemory/backups/lostmemory_2026-05-16_023000.sql.gz
[db-backup] before: <X>G used / <Y>G total / <Z>% full
[db-backup] OK: 백업 완료 (size: <NNN>K)
[db-backup] retention: 14일 이상 백업 제거
[db-backup] after:  ...
[db-backup] 2026-05-16T02:30:01+09:00 완료
```

### 2.3 결과 검증

```bash
ls -lh /home/ubuntu/lostmemory/backups/
# lostmemory_YYYY-MM-DD_HHmmss.sql.gz 파일이 보여야 함, size > 1KB

# dump 내용 sanity check — 헤더 확인
zcat /home/ubuntu/lostmemory/backups/lostmemory_*.sql.gz | head -5
# "-- PostgreSQL database dump" 로 시작해야 함

# 테이블 수 (참고 — 운영 schema 와 정합)
zcat /home/ubuntu/lostmemory/backups/lostmemory_*.sql.gz | grep -c "^CREATE TABLE"
```

---

## 3. cron 등록 (자동화)

### 3.1 root crontab 에 추가

```bash
sudo crontab -e
```

다음 한 줄 추가:

```
30 2 * * * /home/ubuntu/lostmemory/server/scripts/db-backup.sh >> /var/log/db-backup.log 2>&1
```

**기존 cron 과 충돌 확인** (참고용 — docker-prune, certbot 과 시각 분리):

| 스크립트 | cron |
|---|---|
| `db-backup.sh` | `30 2 * * *` (매일 02:30) |
| `docker-prune.sh` | `30 3 * * 0` (일요일 03:30) |
| `certbot-renew.sh` | `17 3 * * 1` (월요일 03:17) |

### 3.2 첫 자동 실행 확인 (다음 날)

```bash
# 로그 확인
sudo tail -50 /var/log/db-backup.log

# 파일이 정상 생성됐는지
ls -lh /home/ubuntu/lostmemory/backups/ | tail -5
```

---

## 4. 복원 절차

**⚠️ 운영 DB 복원은 데이터 손실 위험이 큰 작업입니다. 반드시 BE 트랙 (S14P31C201-153) 과 합의 후 진행하고, 본 절차는 *수행 가능한 절차의 문서화* 일 뿐 자동 실행 대상이 아닙니다.**

### 4.1 복원 대상 파일 선택

```bash
ls -lh /home/ubuntu/lostmemory/backups/
# 복원할 백업 파일명을 결정 — 예: lostmemory_2026-05-16_023000.sql.gz
```

### 4.2 부분 검증 (실행 전)

```bash
BACKUP=/home/ubuntu/lostmemory/backups/lostmemory_2026-05-16_023000.sql.gz

# 압축 무결성 검증
gzip -t "$BACKUP" && echo "gzip OK"

# 첫/끝 라인 확인
zcat "$BACKUP" | head -3
zcat "$BACKUP" | tail -3
# 마지막 줄이 "-- PostgreSQL database dump complete" 이어야 정상 종료
```

### 4.3 복원 실행

```bash
cd /home/ubuntu/lostmemory/server
. ./.env  # POSTGRES_USER / POSTGRES_DB 가져오기

# 컨테이너 안 psql 로 dump 주입 (--clean --if-exists 가 dump 에 포함되어 idempotent)
zcat "$BACKUP" | docker compose --env-file .env exec -T \
  -e PGPASSWORD="$POSTGRES_PASSWORD" \
  postgres \
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1
```

### 4.4 복원 후 검증

```bash
# app 컨테이너 강제 재기동 (캐시/connection pool 초기화)
docker compose --env-file .env restart app

# health endpoint 확인
curl -sf https://k14c201.p.ssafy.io/api/actuator/health
```

---

## 5. 검증 / 모니터링

### 5.1 일일 점검 (수동, 30초)

```bash
ssh ubuntu@k14c201.p.ssafy.io
ls -lh /home/ubuntu/lostmemory/backups/ | tail -3
sudo tail -20 /var/log/db-backup.log
```

기대값:
- 가장 최근 파일이 **오늘 02:30 이후** 타임스탬프
- 로그 마지막 라인이 `[db-backup] ... 완료` 로 끝남
- `[ERROR]` 또는 `pg_dump 실패` 가 없어야 함

### 5.2 디스크 점유 점검

```bash
du -sh /home/ubuntu/lostmemory/backups/
df -h ~
```

retention 14일 + 평균 dump 크기를 곱해 예상 점유 산정 (예: 10MB × 14 = 140MB).

### 5.3 알림 통합 (후속 작업)

현재 cron 실패는 `/var/log/db-backup.log` 만 기록합니다. Mattermost 알림 통합은 별도 티켓 (Alertmanager 또는 cron MAILTO).

---

## 6. 후속 작업

- [ ] **Mattermost 알림 통합**: cron 실패 시 webhook 발송 (별도 티켓)
- [ ] **백업 무결성 자동 검증**: 주 1회 `pg_restore --list` 또는 dry-run 복원 (별도 티켓)
- [ ] **오프사이트 복제**: 현재 백업은 EC2 같은 디스크 — S3 / 다른 호스트 복제 검토 (운영 자원 확보 시)
- [ ] **물리 백업 / PITR**: WAL 아카이빙 기반 PITR 도입 (운영 자원 확보 시)

---

## 부록 A — 첫 수동 실행 결과 (운영자 채움)

| 항목 | 값 |
|---|---|
| 실행 일시 | _(YYYY-MM-DD HH:mm)_ |
| 실행자 | _(이름)_ |
| 백업 파일 크기 | _(MB)_ |
| 소요 시간 | _(초)_ |
| 결과 | _(OK / 이슈)_ |
| 비고 | _()_ |

## 부록 B — 환경변수 override 예시

```bash
# 백업을 다른 경로로 (예: 임시 검증)
BACKUP_DIR=/tmp/backup-test sudo -E /home/ubuntu/lostmemory/server/scripts/db-backup.sh

# retention 을 7일로 단축
RETENTION_DAYS=7 sudo -E /home/ubuntu/lostmemory/server/scripts/db-backup.sh
```
