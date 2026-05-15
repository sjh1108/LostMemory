# 운영 런북 — 배포 롤백 (server-app / relay)

- **대상**: 운영 EC2 `k14c201.p.ssafy.io` 의 `server-app` (Spring Boot) + `relay` (Netty UDP) 컨테이너
- **소요**: 약 **5 ~ 15초 다운타임** (BUILD_NUMBER 별 image 가 호스트에 보존된 경우 한정)
- **선행 도구**: [`server/scripts/deploy.sh`](../server/scripts/deploy.sh) `rollback <N>`
- **상태**: 초안 (S14P31C201-129). 첫 리허설 결과는 본 문서 부록 A 참조.

> 본 런북은 **app/relay 컨테이너 image 만 되돌리는 경로** 를 다룹니다. DB 스키마 변경 / 데이터 정합 롤백은 본 런북 밖이며 BE 트랙 (S14P31C201-153) 으로 위임합니다.

---

## 1. 언제 사용하는가

다음 중 하나라도 해당하면 본 런북 진입을 검토합니다.

| 트리거 | 판단 근거 |
|---|---|
| 5xx 급증 | nginx access log `$status >= 500` 비율 > 5% (1분 윈도우) |
| `/api/actuator/health` UP 실패 | 60초 이상 `{"status":"DOWN"}` 또는 connection refused |
| 컨테이너 healthcheck red | `docker compose ps app` 가 `(unhealthy)` 또는 `Restarting` 반복 |
| BE / 게임 디자인 측 명시 요청 | 직전 배포가 명백한 회귀 (예: 매치메이킹 100% fail) |
| Mattermost 자동 알림에서 직전 빌드 실패 + 직전-1 빌드 정상 | history 상 N-1 가 명확히 stable |

**하지 말아야 할 케이스**:
- 직전 배포에 DB 마이그레이션이 동반된 경우 (현재 prod `ddl-auto: validate` 라 일반 배포에는 마이그레이션이 없지만, BE 가 의도적으로 ALTER 를 친 경우는 본 런북으로 되돌리면 application 이 schema-validation 으로 부팅 실패)
- 운영 EC2 working tree 가 develop 과 sync 되어 있지 않은 경우 (사전조건 1번 참조)

---

## 2. 사전조건 체크리스트

본 실행 전에 모두 ✅ 확인합니다. 하나라도 실패하면 본 런북 중단하고 운영자 단톡 (Mattermost) 에 보고.

### 2.1 운영 EC2 working tree 가 develop sync 인지

```bash
ssh ubuntu@k14c201.p.ssafy.io
cd <repo>      # 운영 working tree 위치
git fetch origin develop
LOCAL=$(git rev-parse HEAD)
REMOTE=$(git rev-parse origin/develop)
echo "local=$LOCAL"
echo "remote=$REMOTE"
[ "$LOCAL" = "$REMOTE" ] && echo "OK: synced" || echo "WARN: out of sync — pull 검토"
```

> ⚠️ EC2 working tree 가 develop 보다 뒤처져 있으면 `./scripts/deploy.sh` 자체가 없거나 옛 버전일 수 있습니다. (2차 검증 [docs/2nd-mvp-infra-verification.md](2nd-mvp-infra-verification.md) 결론에 명시된 함정.)

### 2.2 deploy.sh 존재 + 실행권한

```bash
cd <repo>/server
ls -l ./scripts/deploy.sh
# -rwxr-xr-x ... 가 보여야 함. 'x' 빠지면 chmod +x ./scripts/deploy.sh
```

### 2.3 보존된 image tag 가 충분한지

```bash
cd <repo>/server
./scripts/deploy.sh history
```

기대 출력 예시 (실측은 다를 수 있음):
```
[history] 보존된 server-app 태그 (latest 제외):
22	1.2GB	2 hours ago
21	1.2GB	1 day ago
20	1.2GB	2 days ago
```

- 현재 active build 번호와 **롤백 대상 (예: 직전-1)** 두 개 이상이 보여야 함.
- 만약 한 줄밖에 없으면 (디스크 정리로 옛 태그가 prune 된 경우) 본 런북으로 즉시 롤백 불가 → 직전 commit 기반 재빌드 재배포 경로 (`./scripts/deploy.sh deploy <N>` 후 별도 빌드) 로 우회.

### 2.4 현재 active 컨테이너 healthy 확인

```bash
cd <repo>/server
./scripts/deploy.sh status
```

기대 출력:
```
[status] 현재 app 컨테이너:
NAME           IMAGE                STATUS
server-app-1   server-app:latest    Up 2 hours (healthy)

[status] image:
Image ID:   sha256:...
Image Name: server-app:latest
State:      running (health: healthy)
```

healthy 가 아니면 본 롤백 진입 전에 어떤 상태인지 (Restarting / unhealthy) 먼저 운영자 판단 — healthcheck 가 일시 깜빡인 경우 vs 진짜 회귀.

### 2.5 외부 smoke test 한 번 더

본인 PC 에서:

```bash
curl -sf https://k14c201.p.ssafy.io/api/actuator/health
# {"status":"UP","groups":["liveness","readiness"]} 가 정상
```

### 2.6 Mattermost 사전 공지

운영 채널에 1줄 공지:
```
[운영] 롤백 진행 — server-app:N → server-app:N-1, 다운타임 ~10초 예상.
```

수동 SSH 롤백은 Jenkins 자동 알림 대상이 아닙니다 (자동 알림은 Jenkins 빌드 완료 시에만). 본인이 직접 채널 보고합니다.

---

## 3. 단계별 롤백 절차

> ⚠️ **3.1 (정상 상태 캡처) 와 3.2 (rollback 명령) 은 같은 paste 블록에 묶지 말 것.** 3.1 결과 확인 후 별도 paste 로 3.2 실행. (자동 paste 시 destructive 명령이 검증 없이 흘러 들어가는 사고 방지 — 메모리 패턴.)

### 3.1 직전 상태 캡처 (T0)

```bash
cd <repo>/server
date +"%H:%M:%S.%3N"   # T0 시작 시각 — 사람 읽기용
./scripts/deploy.sh status
./scripts/deploy.sh history
curl -sf https://k14c201.p.ssafy.io/api/actuator/health
```

→ 출력 전체를 따로 저장 (Mattermost 메모 또는 터미널 스크롤 캡처). 롤백 후 비교에 씁니다.

### 3.2 ⚠️ Rollback 실행 (T1 → T2)

**별도 paste — 의도적으로 분리합니다.** 다음 명령에서 `<N-1>` 은 **2.3 history 결과에서 직접 본 직전 build 번호**로 대체. 추측 금지.

```bash
cd <repo>/server
date +"%H:%M:%S.%3N"   # T1: rollback 시작
./scripts/deploy.sh rollback <N-1>
date +"%H:%M:%S.%3N"   # T2: 명령 종료 (= 컨테이너 healthy 도달)
```

기대 출력:
```
[rollback] server-app:<N-1> -> server-app:latest 재태깅 + recreate
[+] Running 2/2
 ⠿ Container server-app-1   Started
 ⠿ Container server-relay-1 Started
[deploy] healthy 대기... (1/30)
[deploy] healthy 대기... (2/30)
[deploy] app healthy
[rollback] build #<N-1> 으로 복귀 완료
```

**timeout 발생 시** (`[deploy][ERROR] app healthy 안 됨` + 마지막 100줄 로그 출력):
- 90초 안에 healthy 안 된 상황. **자동 재시도 X**.
- 곧바로 `docker compose --env-file .env logs --tail=200 app relay` 로 원인 진단.
- DB / Redis / .env 변수 누락 / image 자체 손상 등이 전형. 메모리의 [application.yaml envvar 기본값 미사용 정책] 참조 — `BindException` 으로 부팅 실패한다면 envvar 누락.
- **상황을 운영자 판단 영역으로 즉시 escalate** (단톡 보고 + BE 담당 호출).

### 3.3 외부 smoke test (T3)

본인 PC 에서 즉시:

```bash
curl -sf https://k14c201.p.ssafy.io/api/actuator/health
echo "---"
curl -sf -o /dev/null -w "%{http_code} %{time_total}s\n" https://k14c201.p.ssafy.io/api/actuator/health
```

- `{"status":"UP",...}` + `200` 이면 사용자 체감 복구. T3 시각 기록.
- `502` / `503` 가 잠시 보이면 nginx upstream 재연결 대기 중일 수 있음 — 5초 간격 3번 재시도 후에도 5xx 면 escalate.

### 3.4 Relay UDP 도달성 확인 (T4)

운영 EC2 안에서:

```bash
docker logs --tail=20 server-relay-1
# RelayApplication 시작 로그 + (가능하면) handshake/peer 로그가 보이면 OK.

# 컨테이너 안에서 프로세스 살아있음 확인
docker exec server-relay-1 sh -c 'pgrep -f RelayApplication && echo OK'
```

본인 PC (Windows PowerShell) 에서 UDP 도달성 ping (메모리 [EC2 UDP 외부 도달성 검증 절차] 참고):

```powershell
$udp = New-Object System.Net.Sockets.UdpClient
$udp.Connect("k14c201.p.ssafy.io", 7777)
$bytes = [Text.Encoding]::ASCII.GetBytes("ping")
$udp.Send($bytes, $bytes.Length) | Out-Null
$udp.Close()
```

EC2 안에서 (다른 SSH 세션) 패킷 수신 확인:
```bash
docker logs --tail=5 server-relay-1
# UDP packet received 류 로그가 있어야 함 (있는 경우 한정 — 없는 환경이면 본 step skip)
```

### 3.5 latest 복구 (T5)

문제가 해결된 새 빌드가 준비되면 일반 배포 경로로 복귀:

```bash
cd <repo>/server
./scripts/deploy.sh deploy <원래_N_또는_새_N+1>
```

또는 develop 에 hotfix 머지 → Jenkins 자동 트리거로 새 빌드 (권장 — 정상 경로).

---

## 4. 결과 검증 (체크리스트)

| 항목 | 명령 / 확인 지점 | 기대 |
|---|---|---|
| HTTPS 200 | `curl -sf https://k14c201.p.ssafy.io/api/actuator/health` | `{"status":"UP",...}` |
| HTTP/2 응답 | `curl -sI https://k14c201.p.ssafy.io/api/actuator/health \| head -1` | `HTTP/2 200` |
| HTTP→HTTPS 301 | `curl -sI http://k14c201.p.ssafy.io/api/actuator/health \| head -3` | `301 Moved Permanently` |
| app healthy | EC2 `./scripts/deploy.sh status` | `healthy` |
| 컨테이너 image | EC2 `docker inspect server-app-1 --format '{{.Image}}'` | rollback 대상 image ID 와 일치 |
| relay 살아있음 | EC2 `docker exec server-relay-1 pgrep -f RelayApplication` | PID 출력 |
| nginx upstream 연결 | EC2 `docker compose logs --tail=30 nginx \| grep -i upstream` | `connect() failed` 류 0건 |

---

## 5. 롤백 후 follow-up

1. **Mattermost 운영 채널 보고** — 결과 1줄 (다운타임 / 복귀한 build 번호 / 다음 액션).
2. **Jira 티켓 생성** — 롤백 사유로 별도 INFRA / BE 버그 티켓 생성, S14P31C201-129 의 후속 epic 으로 묶기.
3. **원인 조사** — 직전 build 의 변경 (commit range) 을 BE 담당과 함께 검토. `git log <N-1>..<N>` (각 build 가 빌드한 commit 은 Jenkins build #N 의 `COMMIT_SHA` 파라미터에 기록됨).
4. **history 회수 검토** — `./scripts/deploy.sh history` 가 너무 많이 누적되어 디스크 압박이면 옛 태그 정리 (`docker rmi server-app:<옛N>`). 단, 직전 N-3 정도까지는 보존.
5. **본 런북 업데이트** — 이번 롤백에서 만난 함정/추가 절차가 있으면 즉시 본 문서에 반영 (특히 부록 A 에 새 리허설 로그 추가).

---

## 6. 알려진 한계 / 주의 사항

- **5 ~ 15초 다운타임 발생** — `--force-recreate` 가 컨테이너를 끄고 다시 띄우는 시간. 사용자 영향 있음. 한가한 시간대 권장.
- **DB 변경이 있는 배포는 본 런북 사용 X** — `ddl-auto: validate` 라 application 이 schema mismatch 로 부팅 fail. BE 트랙 (S14P31C201-153) 으로.
- **EC2 working tree sync 가 사전조건** — 운영자 SSH 작업 전 `git fetch + rev-parse` 비교 필수.
- **`wait_healthy` 가 90초 timeout 이면 수동 개입** — 자동 재시도 / 자동 forward-rollback 같은 안전망 없음.
- **수동 SSH 롤백은 Jenkins Mattermost 알림 대상 아님** — 운영자가 직접 채널 보고.
- **server-app 과 relay 가 같은 image 공유** — app 만 따로 / relay 만 따로 롤백 불가능. `deploy.sh rollback` 은 두 service 동시 `--force-recreate`.
- **postgres / redis / nginx 는 본 런북 영향 X** — image 가 server-app 이 아니라 별도. 데이터 손실 우려 없음.
- **CRLF lineending 함정** — `.env` 가 git checkout 시 CRLF 로 변환되면 dash (Ubuntu /bin/sh) 가 source 깨뜨림. 운영 EC2 의 `.env` 는 LF 유지 (메모리 [dash + .env 함정] 참조).

---

## 부록 A — 리허설 로그

본 런북 첫 검증 결과를 누적합니다. 새 리허설마다 항목 추가.

### A.1 첫 리허설 — TBD (S14P31C201-129)

> _본 항목은 Phase B (운영 EC2 리허설) 실행 후 채워집니다._

- **일시**: TBD
- **실행자**: 송주헌
- **시간대**: TBD (한가한 심야 합의 후)
- **빌드 전이**: server-app:`<N>` → server-app:`<N-1>` → server-app:`<원래_N>`
- **측정값**:
  - 다운타임 (T1 → T3): TBD ms
  - `wait_healthy` 폴링: TBD 회 (~ TBD 초)
  - HTTP 5xx 발생: TBD
  - relay UDP 영향: TBD
- **함정 / 발견**: TBD
- **본 런북 보강 사항**: TBD

---

## 핵심 reference

- 스크립트: [server/scripts/deploy.sh](../server/scripts/deploy.sh)
- compose healthcheck 정의: [server/docker-compose.yml](../server/docker-compose.yml) (`app.healthcheck`, `relay.healthcheck`)
- 배포 파이프라인 가이드: [docs/ci-jenkins-setup.md](ci-jenkins-setup.md)
- 2차 MVP 인프라 검증: [docs/2nd-mvp-infra-verification.md](2nd-mvp-infra-verification.md)
- relay UDP 인프라: S14P31C201-462 (자체 Relay UDP 7777, app 과 image 공유)
- BE 데이터 롤백 트랙: S14P31C201-153 (별도 책임)
