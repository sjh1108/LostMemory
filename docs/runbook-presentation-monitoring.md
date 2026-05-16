# 운영 런북 — 발표 당일 모니터링 안전망 (S14P31C201-135)

- **대상**: 2026-05-18 (3차 MVP 발표일) 운영 EC2 `k14c201.p.ssafy.io`
- **목적**: 발표 중 서비스 사고 발생 시 빠르게 알아채고 진입 절차 follow 할 수 있도록 알림/대시보드/대응 매핑을 미리 점검
- **소요**: 사전(D-1) 점검 약 15분 + 발표 당일 watcher 상시 1명
- **선행 작업**: monitoring stack 운영 EC2 에 떠 있어야 함 (Jenkins 자동 배포 대상 아니므로 운영자가 수동 up — §1.1)
- **상태**: 초안. D-1(2026-05-17) 점검 결과는 부록 A 에 운영자 채움

> 발표 중 배포 시연을 하지 않기로 결정 → 본 런북은 **안전망** 중심. 발표 화면에 Grafana 띄우는 시연 시나리오는 후속 운영 인계 트랙으로 분리.

---

## 0. 환경 사실 (sticky)

| 항목 | 값 |
|---|---|
| 운영 EC2 호스트 | `k14c201.p.ssafy.io` |
| SSH 진입 | `ssh ubuntu@k14c201.p.ssafy.io` |
| compose 디렉토리 | `~/lostmemory/server` |
| 외부 health endpoint | `https://k14c201.p.ssafy.io/api/actuator/health` |
| Grafana URL | `https://k14c201.p.ssafy.io/grafana/` (login 필요, `GRAFANA_ADMIN_PASSWORD` env) |
| primary watcher 화면 | Grafana 대시보드 **LostMemory — Containers (cAdvisor)** (UID `lostmemory-containers`) |
| 알림 채널 | Mattermost (Jenkins 빌드 + Alertmanager 알림 공용 webhook) |
| Alertmanager API (internal) | `http://alertmanager:9093` (monitoring docker network 안) |
| 발표 일자 | 2026-05-18 |
| EC2 호스트 TZ | UTC |

### 0.1 모니터링 스택 구성

`server/docker-compose.monitoring.yml` 분리 파일 — **Jenkins 자동 배포가 건드리지 않음**.

| 서비스 | 이미지 | 역할 |
|---|---|---|
| prometheus | `prom/prometheus:v2.51.2` | scrape + 룰 평가 |
| node-exporter | `prom/node-exporter:v1.8.0` | 호스트 메트릭 |
| cadvisor | `gcr.io/cadvisor/cadvisor:v0.51.0` | 컨테이너 메트릭 |
| grafana | `grafana/grafana:10.4.3` | 대시보드 (nginx 통해 외부 노출) |
| alertmanager | `prom/alertmanager:v0.27.0` | 알림 라우팅 → Mattermost |

### 0.2 현재 알림 룰 (3개)

[`server/monitoring/prometheus/rules/server-health.yml`](../server/monitoring/prometheus/rules/server-health.yml)

| Alert | 임계 | 지속 | severity |
|---|---|---|---|
| `HighCpuUsage` | CPU > 80% | 5m | warning |
| `HighMemoryUsage` | Memory > 80% | 5m | warning |
| `HighDiskUsage` | rootfs `/` > 80% | 5m | warning |

> ⚠️ **현재 룰 gap**: app `/api/actuator/health` DOWN, postgres / redis 다운, nginx 5xx 급증 등 **service-level alert 가 없음**. 발표 당일에는 외부 health endpoint 수동 watch + 컨테이너 healthy 수동 watch 로 보완 (§3 watcher 화면).

### 0.3 사고 분류 → 진입 runbook 매핑

| 신호 | 의심 원인 | 진입 runbook / 액션 |
|---|---|---|
| Mattermost `FIRING: HighCpuUsage` | CPU spike — 무한 루프 / 다중 세션 부하 | EC2 SSH `top` / `docker stats` → 의심 컨테이너 식별 → restart 검토 |
| Mattermost `FIRING: HighMemoryUsage` | 메모리 누수 / heap OOM | `free -h` / `docker stats` → app restart 또는 rollback 검토 |
| Mattermost `FIRING: HighDiskUsage` | 로그 / image 적체 | `docker system df` → `server/scripts/docker-prune.sh` 수동 1회 |
| 외부 `/api/actuator/health` 200 → fail | 배포 회귀 / app 죽음 | [`docs/runbook-rollback.md`](runbook-rollback.md) 진입 검토 (build #N-1 으로 즉시 복귀) |
| 컨테이너 `(unhealthy)` / `Restarting` | healthcheck 실패 | `docker compose logs --tail=200 <svc>` → 로그 기반 결정 |
| DB 데이터 이상 의심 | schema / 데이터 손상 | [`docs/runbook-db-backup.md`](runbook-db-backup.md) §4 복원 절차 (BE 합의 후) |
| Mattermost 알림 자체 끊김 | webhook 만료 / alertmanager 다운 | §2.2 ping 절차로 재검증, 안 되면 발표 중에는 외부 health endpoint 수동 polling |

---

## 1. D-1 (2026-05-17) 사전 점검

발표 전날 진행. 약 15분.

### 1.1 monitoring stack 살아있는지

```bash
ssh ubuntu@k14c201.p.ssafy.io
cd ~/lostmemory/server

docker compose -f docker-compose.yml -f docker-compose.monitoring.yml --env-file .env ps \
  prometheus node-exporter cadvisor grafana alertmanager
```

기대: 5개 모두 `Up ... (healthy)` 또는 `Up`. 하나라도 down 이면 운영자가 직접 up:

```bash
docker compose -f docker-compose.yml -f docker-compose.monitoring.yml --env-file .env up -d \
  prometheus node-exporter cadvisor grafana alertmanager
```

### 1.2 Alertmanager 룰 / 라우팅 확인

```bash
# 룰 평가 상태 (Active / Pending / Inactive)
docker compose -f docker-compose.yml -f docker-compose.monitoring.yml --env-file .env \
  exec prometheus wget -qO- http://localhost:9090/api/v1/rules | head -50

# alertmanager 라우팅 트리
docker compose -f docker-compose.yml -f docker-compose.monitoring.yml --env-file .env \
  exec alertmanager wget -qO- http://localhost:9093/api/v2/status | head -50
```

### 1.3 Alertmanager → Mattermost ping (핵심 검증)

발표 전 가장 중요한 1회 검증. 가짜 alert 를 alertmanager API 에 직접 주입 → Mattermost 채널 도달 확인.

```bash
# alertmanager 컨테이너 안에서 자기 자신 API 호출 — monitoring network 안이라 가능
docker compose -f docker-compose.yml -f docker-compose.monitoring.yml --env-file .env \
  exec alertmanager wget -qO- \
    --header='Content-Type: application/json' \
    --post-data='[{
      "labels": {"alertname":"PresentationSmokeTest","severity":"warning","metric":"smoke"},
      "annotations": {"summary":"발표 D-1 알림 경로 검증 ping","description":"본 알람이 Mattermost 채널에 도달하면 OK — 운영 영향 없음"},
      "startsAt": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'"
    }]' \
    http://localhost:9093/api/v1/alerts
```

→ **Mattermost 채널에 "FIRING: PresentationSmokeTest (1건)" 메시지가 도착하는지 확인** (도달 시각 부록 A 에 기록).

24시간 이내 자동 해제되지 않으므로 ping 후 명시적으로 resolve:

```bash
docker compose -f docker-compose.yml -f docker-compose.monitoring.yml --env-file .env \
  exec alertmanager wget -qO- \
    --header='Content-Type: application/json' \
    --post-data='[{
      "labels": {"alertname":"PresentationSmokeTest","severity":"warning","metric":"smoke"},
      "annotations": {"summary":"발표 D-1 알림 경로 검증 ping (resolved)"},
      "startsAt": "'$(date -u -d '1 minute ago' +%Y-%m-%dT%H:%M:%SZ)'",
      "endsAt": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'"
    }]' \
    http://localhost:9093/api/v1/alerts
```

→ Mattermost 에 "RESOLVED: PresentationSmokeTest" 추가 도착 확인 (`send_resolved: true` 검증).

### 1.4 Grafana 대시보드 살아있는지

본인 PC 브라우저:
- `https://k14c201.p.ssafy.io/grafana/` 로그인 (admin / `GRAFANA_ADMIN_PASSWORD`)
- 좌측 메뉴 → Dashboards → **LostMemory — Containers (cAdvisor)** 클릭
- 5개 패널(컨테이너 CPU / 메모리 / 네트워크 RX/TX / 실행 컨테이너 수) 모두 데이터 표시되는지 확인

데이터 비어있으면 prometheus scrape 실패 — `prometheus` 컨테이너 재기동 또는 datasource 점검.

### 1.5 6 게임 서비스 컨테이너 healthy

```bash
docker compose --env-file .env ps
```

`server-app-1` / `server-postgres-1` / `server-redis-1` / `server-nginx-1` / `server-relay-1` / `jenkins` 모두 `Up ... (healthy)`.

---

## 2. 발표 당일 (2026-05-18) 1시간 전 점검

5분 안. §1 의 1.1 / 1.5 만 다시 확인 — alertmanager ping 은 D-1 에 한 번 했으면 OK.

```bash
ssh ubuntu@k14c201.p.ssafy.io
cd ~/lostmemory/server

# 6 게임 + 5 monitoring 컨테이너
docker compose --env-file .env ps
docker compose -f docker-compose.yml -f docker-compose.monitoring.yml --env-file .env ps \
  prometheus alertmanager grafana node-exporter cadvisor

# 외부 health
curl -sf https://k14c201.p.ssafy.io/api/actuator/health

# 디스크 여유
df -h ~
```

이상 발견 시 §0.3 매핑 표 따라 진입.

---

## 3. 발표 중 watcher (단독 솔로 OK)

발표 시간 동안 본인 PC 에 두 화면 띄움:

| 화면 | URL / 위치 | 무엇을 봐야 하나 |
|---|---|---|
| Mattermost 알림 채널 | Mattermost 데스크탑 / 웹 | `FIRING` 메시지 도달 시 즉시 §0.3 진입 |
| Grafana 대시보드 | `https://k14c201.p.ssafy.io/grafana/d/lostmemory-containers` | 실행 컨테이너 수가 갑자기 줄어들거나, CPU/메모리 spike 직관 확인 |

선택: 외부 health 폴링 1초 1회 (CPU 적 무관)

```bash
# 본인 PC git bash / WSL / macOS
while :; do
  printf '%(%H:%M:%S)T '
  curl.exe -sf -o /dev/null -w '%{http_code}\n' https://k14c201.p.ssafy.io/api/actuator/health \
    || echo "FAIL"
  sleep 5
done
```

> watcher 역할 본인 솔로 가정. BE/클라 측에서 발표 중 사고 신호 (게임 끊김 / API 5xx) 가 먼저 보일 수도 있으니 발표 직전 단톡 한 줄로 "사고 시 본인에게 즉시 핑" 합의 권장.

---

## 4. 발표 후 정리

- Mattermost 채널 사고 알람 없었는지 확인
- 부록 A 의 "결과" 칸 채움
- 이번 sprint 종료 + 후속 hygiene 트랙 분리:
  - **service-level alert 룰 추가** (app actuator DOWN / postgres unhealthy / 5xx 급증) — 별도 티켓
  - **on-call 다중 인원 합의** + 발표 후 운영 인수인계 — 별도 트랙
  - **Grafana 발표용 화면 시연 시나리오** — 후속 sprint 에서 검토

---

## 부록 A — D-1 사전 점검 raw 결과 (운영자 채움)

| 항목 | 값 |
|---|---|
| 점검 일시 | _(YYYY-MM-DD HH:mm KST)_ |
| 점검자 | _(이름)_ |
| §1.1 5 monitoring 컨테이너 healthy | ✅ / ❌ _(상세)_ |
| §1.2 prometheus 룰 / alertmanager 라우팅 | ✅ / ❌ _(상세)_ |
| §1.3 Mattermost ping FIRING 도달 | ✅ / ❌ _(도달 시각, 지연)_ |
| §1.3 Mattermost ping RESOLVED 도달 | ✅ / ❌ _(도달 시각)_ |
| §1.4 Grafana lostmemory-containers 정상 | ✅ / ❌ _(상세)_ |
| §1.5 6 게임 컨테이너 healthy | ✅ / ❌ _(상세)_ |
| 결론 | **OK** / 이상 |

### A.1 명령 raw 출력 (선택)

```
(§1.x 명령 출력 paste — 분량 부담되면 ❌ 항목만)
```

### A.2 발견된 이슈 / 후속

- _(있다면 bullet)_

---

## 부록 B — 발표 당일 결과 (발표 후 채움)

| 항목 | 값 |
|---|---|
| 발표 시간대 | _(HH:mm ~ HH:mm KST)_ |
| Mattermost 사고 알람 도달 | 없음 / _(상세)_ |
| 외부 health endpoint 200 유지 | ✅ / ❌ |
| Grafana 이상 패턴 관측 | 없음 / _(상세)_ |
| 진입한 runbook | 없음 / _(어떤 runbook + 결과)_ |
| 발표 영향 | 없음 / _(상세)_ |
| 결론 | **OK** / 사고 |
