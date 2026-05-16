# 발표 전 마지막 배포 sanity check (S14P31C201-134)

- **대상**: 운영 EC2 `k14c201.p.ssafy.io` 의 `server-app` (Spring Boot) + `relay` (Netty UDP) 컨테이너
- **목적**: 발표 전 어차피 한 번 일어날 마지막 자동 배포가 깨지지 않는다는 것만 가볍게 확인
- **소요**: 5분 안
- **선행 도구**: [`server/scripts/deploy.sh`](../server/scripts/deploy.sh), [`server/scripts/db-backup.sh`](../server/scripts/db-backup.sh), Jenkins UI
- **상태**: 초안 — 1회 실행 결과는 부록 A 에 운영자 채움

> **스코프 축소 근거**: 원래 티켓은 "배포 리허설 2회(팀 관찰)" 로 발표 중 배포 시연 안전성 검증이 의도였으나, 발표 중 배포 시연을 하지 않기로 결정해 1회 sanity check 로 축소. 후일자 sprint 에서 다시 정식 리허설이 필요해지면 git history 의 commit `74ff23e13` 의 framework 가 출발점.

---

## 0. 환경 사실 (sticky)

| 항목 | 값 |
|---|---|
| 운영 EC2 호스트 | `k14c201.p.ssafy.io` |
| SSH 진입 | `ssh ubuntu@k14c201.p.ssafy.io` |
| compose 디렉토리 | `~/lostmemory/server` |
| 외부 health endpoint | `https://k14c201.p.ssafy.io/api/actuator/health` |
| Jenkins URL | (운영자 노트 참고) |
| 발표 일자 | 2026-05-18 |
| EC2 호스트 TZ | UTC |

---

## 1. 통과 기준

다음 4개만 OK 이면 발표 진행:

| 항목 | OK 기준 |
|---|---|
| 외부 health endpoint | 배포 후 200 응답 복귀 |
| app 컨테이너 | `Up ... (healthy)` 도달 |
| 부수 컨테이너 (postgres / redis / nginx / relay) | 상태 변동 없음 (recreate 대상 아님) |
| DB 영향 | 데이터 손실 없음 (사전 백업 1회로 안전망 확보) |

다운타임 / 알림 도달 시각은 참고용 — 정량 임계치 안 둠 (발표 중 시연 대상 아님).

---

## 2. 실행 (한 paste, 단독 진행 OK)

### 2.1 사전 안전망 + T0 캡처

```bash
ssh ubuntu@k14c201.p.ssafy.io
cd ~/lostmemory/server

# develop sync 확인
git fetch origin develop
[ "$(git rev-parse HEAD)" = "$(git rev-parse origin/develop)" ] && echo "OK: synced" || echo "WARN"

# 6 컨테이너 healthy
docker compose --env-file .env ps

# deploy.sh history (보존 image 2개 이상이면 회귀 시 rollback 가능)
./scripts/deploy.sh history | head -5

# 백업 1회 (회귀 시 복원 안전망)
sudo /home/ubuntu/lostmemory/server/scripts/db-backup.sh

# === T0 시각 + 직전 image ID ===
date +"%H:%M:%S.%3N"
docker inspect server-app-1 --format 'image: {{.Image}} / health: {{.State.Health.Status}}'
```

### 2.2 ⚠️ Jenkins UI manual trigger (브라우저)

1. Jenkins URL 접속
2. 해당 job → **Build with Parameters** → 파라미터 그대로 → **Build**
3. T1 = Jenkins console "Started by user ..." 시각 (KST 환산해 부록 A 기록)

### 2.3 회복 확인 (EC2 SSH 별도 paste)

```bash
cd ~/lostmemory/server

# T2 시각 + 새 image
date +"%H:%M:%S.%3N"
./scripts/deploy.sh status
docker compose --env-file .env ps
docker compose --env-file .env logs --tail=30 app

# 외부 회복 확인
curl.exe -sf https://k14c201.p.ssafy.io/api/actuator/health
# 또는 EC2 안에서:
# curl -sf https://k14c201.p.ssafy.io/api/actuator/health
```

부수 컨테이너 (postgres / redis / nginx) `Up ... (healthy)` 유지 확인.

> Windows PowerShell 본인 PC 에서 실행 시 `curl` 이 `Invoke-WebRequest` alias 라 `-sf` 안 먹힘 → `curl.exe` 로 명시 호출.

---

## 3. 결과 정리 (부록 A 채운 뒤 본 섹션 한 줄)

- **결론**: OK / 이상 (이상이면 §4 발견 이슈 + fix → 재실행)

---

## 4. 발견 이슈 / 후속 액션

- (있다면 bullet 으로)

---

## 부록 A — 1회 sanity check raw 측정 (운영자 채움)

| 항목 | 값 |
|---|---|
| 실행 일시 | _(YYYY-MM-DD HH:mm KST)_ |
| 실행자 | _(이름)_ |
| T0 (직전 캡처 시각) | _(HH:MM:SS.mmm)_ |
| T1 (Jenkins 빌드 시작) | _(HH:MM:SS.mmm)_ |
| T2 (app healthy 도달) | _(HH:MM:SS.mmm)_ |
| 다운타임 (대략, 참고용) | _(s)_ |
| 외부 health 200 회복 | ✅ / ❌ |
| app `(healthy)` 도달 | ✅ / ❌ |
| 부수 컨테이너 상태 변동 | 없음 / _(상세)_ |
| DB 영향 | 없음 / _(상세)_ |
| Mattermost 알림 도달 (참고용) | _(HH:MM:SS)_ |
| 결론 | **OK** / 이상 |

### A.1 T0 / T2 raw 출력 (선택)

```
(2.1 / 2.3 명령 출력 전체 paste 가능 — 분량 부담되면 핵심 라인만)
```
