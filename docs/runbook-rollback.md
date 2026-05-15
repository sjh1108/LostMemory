# 운영 런북 — 배포 롤백 (server-app / relay)

- **대상**: 운영 EC2 `k14c201.p.ssafy.io` 의 `server-app` (Spring Boot) + `relay` (Netty UDP) 컨테이너
- **소요**: 약 **5 ~ 15초 다운타임** (BUILD_NUMBER 별 image 가 호스트에 보존된 경우 한정)
- **선행 도구**: [`server/scripts/deploy.sh`](../server/scripts/deploy.sh) `rollback <N>`
- **상태**: 초안 (S14P31C201-129). 첫 리허설 결과는 본 문서 부록 A 참조.

> 본 런북은 **app/relay 컨테이너 image 만 되돌리는 경로** 를 다룹니다. DB 스키마 변경 / 데이터 정합 롤백은 본 런북 밖이며 BE 트랙 (S14P31C201-153) 으로 위임합니다.

---

## 0. 환경 사실 (sticky)

운영자가 본 런북의 명령을 그대로 paste 할 수 있도록 환경 정보를 한 곳에 모읍니다. 운영 EC2 가 바뀌면 본 표만 수정하면 본문 명령들이 그대로 살아납니다.

| 항목 | 값 |
|---|---|
| 운영 EC2 호스트 | `k14c201.p.ssafy.io` |
| SSH 진입 | `ssh ubuntu@k14c201.p.ssafy.io` |
| 운영 working tree 경로 | `~/lostmemory` |
| compose 디렉토리 | `~/lostmemory/server` |
| app 컨테이너 이름 | `server-app-1` |
| relay 컨테이너 이름 | `server-relay-1` |
| 외부 health endpoint | `https://k14c201.p.ssafy.io/api/actuator/health` |
| compose env file | `~/lostmemory/server/.env` (LF 유지) |

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
cd ~/lostmemory
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
cd ~/lostmemory/server
ls -l ./scripts/deploy.sh
# -rwxr-xr-x ... 가 보여야 함. 'x' 빠지면 chmod +x ./scripts/deploy.sh
```

### 2.3 보존된 image tag 가 충분한지

```bash
cd ~/lostmemory/server
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
cd ~/lostmemory/server
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

본인 PC 에서 (git bash / WSL / Linux / macOS 기준):

```bash
curl -sf https://k14c201.p.ssafy.io/api/actuator/health
# {"status":"UP","groups":["liveness","readiness"]} 가 정상
```

> ⚠️ **Windows PowerShell 함정**: `curl` 이 `Invoke-WebRequest` alias 라 `-sf` 가 안 먹힙니다 (`매개 변수 이름 'sf'과(와) 일치하는 매개 변수를 찾을 수 없습니다`). 진짜 curl 은 **`curl.exe`** 로 명시 호출:
> ```powershell
> curl.exe -sf https://k14c201.p.ssafy.io/api/actuator/health
> ```

### 2.6 Mattermost 사전 공지

운영 채널에 1줄 공지:
```
[운영] 롤백 진행 — server-app:N → server-app:N-1, 다운타임 ~10초 예상.
```

수동 SSH 롤백은 Jenkins 자동 알림 대상이 아닙니다 (자동 알림은 Jenkins 빌드 완료 시에만). 본인이 직접 채널 보고합니다.

---

## 3. 단계별 롤백 절차

> ⚠️ **§2 사전조건 6개 모두 ✅ 통과한 뒤에만 §3 진입.** 특히 §2.3 history 결과에서 **롤백 대상 build 번호 (직전 N-1)** 를 먼저 확인하지 않으면 §3.2 에서 명령에 채울 값 자체가 없습니다.
> ⚠️ **3.1 (정상 상태 캡처) 와 3.2 (rollback 명령) 은 같은 paste 블록에 묶지 말 것.** 3.1 결과 확인 후 별도 paste 로 3.2 실행. (자동 paste 시 destructive 명령이 검증 없이 흘러 들어가는 사고 방지 — 메모리 패턴.)

### 3.1 직전 상태 캡처 (T0)

EC2 안에서:

```bash
cd ~/lostmemory/server
date +"%H:%M:%S.%3N"            # T0 시작 시각
./scripts/deploy.sh status
./scripts/deploy.sh history
echo "현재 latest image ID: $(docker inspect server-app-1 --format '{{.Image}}')"
# 컨테이너 안에서 actuator 직접 호출 — host 의 localhost:8080 은 publish 안 되어 있어 의미 X
docker exec server-app-1 curl -sf http://localhost:8080/api/actuator/health
```

→ 출력 전체를 따로 저장 (Mattermost 메모 또는 터미널 스크롤 캡처). 롤백 후 비교에 씁니다. 외부 https smoke 는 본인 PC 에서 §2.5 와 동일하게 별도 진행.

### 3.2 ⚠️ Rollback 실행 (T1 → T2)

**별도 paste — 의도적으로 분리합니다.** 아래 블록의 `PREV_N=NN` 의 `NN` 자리에 **2.3 history 결과에서 직접 본 직전 build 번호** (예: `14`) 를 채운 뒤 한 블록으로 paste. 추측 금지.

> ⚠️ **bash syntax 함정**: `./scripts/deploy.sh rollback <14>` 처럼 꺾쇠 자체를 paste 하면 bash 가 `<` 를 input redirection 으로 해석해 `syntax error near unexpected token 'newline'` 발생. 변수로 한 단계 분리하는 패턴 (`PREV_N=14; ./scripts/deploy.sh rollback "$PREV_N"`) 을 사용해 함정 차단.

```bash
cd ~/lostmemory/server          # 또는 운영 working tree 위치
PREV_N=NN                       # ← NN 자리에 2.3 history 의 직전 build 번호 (숫자만)
date +"%H:%M:%S.%3N"            # T1: rollback 시작
./scripts/deploy.sh rollback "$PREV_N"
date +"%H:%M:%S.%3N"            # T2: 명령 종료 (= 컨테이너 healthy 도달)
```

기대 출력:
```
[rollback] server-app:<N-1> -> server-app:latest 재태깅 + recreate
WARN[0000] Found orphan containers ([jenkins server-grafana-1 ...]) for this project. ...
[+] up 4/4
 ✔ Container server-redis-1    Healthy
 ✔ Container server-postgres-1 Healthy
 ✔ Container server-app-1      Started
 ✔ Container server-relay-1    Started
[deploy] healthy 대기... (1/30)
...
[deploy] app healthy
[rollback] build #<N-1> 으로 복귀 완료
```

> ℹ️ **`WARN: Found orphan containers` 는 무시.** monitoring 스택 (`docker-compose.monitoring.yml`) 과 jenkins (`docker-compose.jenkins.yml`) 이 같은 compose project name 을 공유해서 발생하는 정상 경고. `--remove-orphans` 붙이면 안 됨 (monitoring/jenkins 까지 내려버림).

**timeout 발생 시** (`[deploy][ERROR] app healthy 안 됨` + 마지막 100줄 로그 출력):
- 90초 안에 healthy 안 된 상황. **자동 재시도 X**.
- 곧바로 `docker compose --env-file .env logs --tail=200 app relay` 로 원인 진단.
- DB / Redis / .env 변수 누락 / image 자체 손상 등이 전형. 메모리의 [application.yaml envvar 기본값 미사용 정책] 참조 — `BindException` 으로 부팅 실패한다면 envvar 누락.
- **상황을 운영자 판단 영역으로 즉시 escalate** (단톡 보고 + BE 담당 호출).

### 3.3 외부 smoke test (T3)

본인 PC 에서 즉시. **Windows PowerShell 사용 시 `curl` → `curl.exe`, `/dev/null` → `NUL` 로 치환** (§2.5 함정 노트 참조).

git bash / WSL / Linux / macOS:
```bash
curl -sf https://k14c201.p.ssafy.io/api/actuator/health
echo "---"
curl -sf -o /dev/null -w "%{http_code} %{time_total}s\n" https://k14c201.p.ssafy.io/api/actuator/health
```

Windows PowerShell:
```powershell
curl.exe -sf https://k14c201.p.ssafy.io/api/actuator/health
"---"
curl.exe -sf -o NUL -w "%{http_code} %{time_total}s`n" https://k14c201.p.ssafy.io/api/actuator/health
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

> ⚠️ **본 단계의 가장 중요한 함정**: `./scripts/deploy.sh deploy <N>` 으로 이미 보존된 :N image 로 복귀하려고 시도하면 **noop** 또는 **silent inconsistency** 가 발생합니다. 본 리허설 (S14P31C201-129) 에서 실측 확인됨 (부록 A.1 참조).
>
> 이유: `deploy <N>` 의 동작은 `docker tag server-app:latest server-app:<N>` 입니다. 즉 **현재 :latest 가 가리키는 image 를 :N 이름으로 다시 태깅** — 이미 직전 rollback 으로 :latest 가 옛 :N-1 image 를 가리키는 상태였다면, :N tag 가 :N-1 image 를 덮어쓰게 됩니다. 원래 :N image 는 untagged 가 되어 곧 docker prune 으로 사라집니다. 컨테이너는 image swap 없이 그대로 (`Running` 출력, healthy 폴링 0회, 다운타임 0초 = 사실상 noop).

**정상 forward 경로 — 다음 중 하나**:

#### 3.5-a (권장) — Jenkins 수동 빌드 트리거

운영자 브라우저에서 Jenkins → `lostmemory-server-deploy` → **Build with Parameters** → 기본값 그대로 → **Build**.

- develop HEAD 의 새 빌드가 :latest + 새 :BUILD_NUMBER 로 보존 → 정상 history 회복
- Mattermost 자동 알림 도착 (Jenkins post-build webhook)
- 5~10분 소요 (Gradle build + Docker build), 그 사이 직전 rollback 상태 (`:N-1`) 그대로 운영 — 영향 X
- 운영자 SSH 명령 0개 — 가장 안전한 정상 경로

#### 3.5-b — develop 에 hotfix merge

- 실제 코드 수정이 동반된 경우. develop 머지 → GitLab CI 가 Jenkins 자동 트리거 → 위 3.5-a 와 동일 흐름
- 운영자 SSH 명령 0개

#### 3.5-c — EC2 에서 강제 재빌드 (수동 SSH, Jenkins 우회)

Jenkins 가 죽었거나 즉시 forward 가 필요한 비상 경로:

```bash
cd ~/lostmemory
git fetch origin develop
git pull --ff-only origin develop       # 이미 sync 면 noop
cd server
docker compose --env-file .env build app    # 5~10분
NEXT_N=NN                                    # 새 BUILD_NUMBER (예: 직전 N+1)
./scripts/deploy.sh deploy "$NEXT_N"         # 약 10~17초 다운타임 또 한 번
```

> ⚠️ `deploy <N>` 가 안전한 이유: 본 경로는 `docker compose build app` 으로 **새 image 를 :latest 로 빌드** 한 직후라, `deploy <N>` 의 `docker tag :latest :N` 이 **새 image** 를 :N 으로 보존하는 의도된 사용법.

#### 3.5-d — :N-1 그대로 운영 종료 (rollback 사유가 해결될 때까지)

직전 rollback 으로 안정 image 에 있고 hotfix 가 아직 준비 안 됐다면, 정상 develop merge / Jenkins 빌드 때까지 :N-1 그대로 운영. **추가 명령 0개**.

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

- **5 ~ 15초 다운타임 발생** — `--force-recreate` 가 컨테이너를 끄고 다시 띄우는 시간. 사용자 영향 있음. 한가한 시간대 권장. (실측: 본 리허설 1회차 명령 17.274s / 외부 502 연속 약 10.49s — 부록 A.1)
- **`deploy <N>` 은 forward 복구 명령 아님** — `docker tag :latest :N` 만 수행하므로 직전 rollback 상태에서 호출 시 noop + 보존 image 손실 가능. forward 복구는 §3.5-a (Jenkins) / §3.5-b (develop merge) / §3.5-c (수동 재빌드) 경로 사용.
- **보존된 history image 들이 같은 hash 일 수 있음 (Reproducible 빌드)** — Docker BuildKit + Gradle 이 deterministic 하게 동작 → develop source 가 동일하면 결과 image hash 도 동일. 본 리허설에서 `:30~:34` 가 모두 `7059a89b52cc`, `:35/:36` 가 모두 `82d7c2191bce` 로 확정 (실측). 즉 history 가 7개 보여도 실질적으로는 2개 image. **결과**: (a) history 길이만 보고 "다양한 시점 image 가 보존됐다" 가정 X — 진짜 다양성은 source 분기 시점 기준. (b) Jenkins 재빌드가 image hash 일치를 보장하지는 않으므로 운영자 직관과 다를 수 있음.
- **untagged image 는 빠르게 prune 될 수 있음** — `docker tag :latest :N` 으로 :N 이 다른 image 를 가리키게 되면 원래 :N image 는 reference 0 → 다음 prune 또는 build cache 압박 시 사라짐. dangling image 백업 가정 X.
- **DB 변경이 있는 배포는 본 런북 사용 X** — `ddl-auto: validate` 라 application 이 schema mismatch 로 부팅 fail. BE 트랙 (S14P31C201-153) 으로.
- **EC2 working tree sync 가 사전조건** — 운영자 SSH 작업 전 `git fetch + rev-parse` 비교 필수.
- **`wait_healthy` 가 90초 timeout 이면 수동 개입** — 자동 재시도 / 자동 forward-rollback 같은 안전망 없음.
- **수동 SSH 롤백은 Jenkins Mattermost 알림 대상 아님** — 운영자가 직접 채널 보고.
- **server-app 과 relay 가 같은 image 공유** — app 만 따로 / relay 만 따로 롤백 불가능. `deploy.sh rollback` 은 두 service 동시 `--force-recreate`.
- **postgres / redis / nginx 는 본 런북 영향 X** — image 가 server-app 이 아니라 별도. 데이터 손실 우려 없음.
- **`WARN: Found orphan containers` 는 정상** — monitoring (`docker-compose.monitoring.yml`) + jenkins (`docker-compose.jenkins.yml`) 이 같은 compose project name 공유. `--remove-orphans` 절대 추가 X.
- **CRLF lineending 함정** — `.env` 가 git checkout 시 CRLF 로 변환되면 dash (Ubuntu /bin/sh) 가 source 깨뜨림. 운영 EC2 의 `.env` 는 LF 유지 (메모리 [dash + .env 함정] 참조).

---

## 부록 A — 리허설 로그

본 런북 첫 검증 결과를 누적합니다. 새 리허설마다 항목 추가.

### A.1 첫 리허설 — 2026-05-15 (S14P31C201-129)

- **일시**: 2026-05-15 KST ~01:35 ~ 01:55 (EC2 host UTC 표기 기준 / app 컨테이너 안 KST)
- **실행자**: 송주헌
- **시간대**: 심야 (한가한 시간대, Mattermost 사전 공지 후)
- **빌드 전이**: `:35` (`82d7c2191bce`) → `:34` (`7059a89b52cc`) → (잘못된 deploy 시도) → Jenkins 수동 트리거 → **`:36` (`82d7c2191bce`, 원래 :35 와 동일 hash — reproducible 빌드)**
- **PowerShell polling**: 0.5s 간격, `curl.exe -sf --max-time 2`

#### 측정값 (1차 rollback: :35 → :34)

| 항목 | 값 |
|---|---|
| T1 rollback 시작 (EC2 UTC) | `01:47:06.930` |
| T2 rollback 종료 (EC2 UTC) | `01:47:24.204` |
| **명령 총 소요** | **17.274 s** |
| healthy 폴링 횟수 | 5/30 (~15 s) |
| 첫 외부 502 (PC KST) | `10:47:07.506` (T1 + ~0.6 s) |
| 마지막 외부 502 | `10:47:17.992` |
| 첫 외부 200 복구 | `10:47:18.568` (T1 + ~11.66 s) |
| **외부 502 연속 구간 (사용자 체감 다운타임)** | **약 10.49 s** |
| 외부 502 갯수 (0.5s polling) | 17 개 |
| relay 부팅 시간 (relay log) | `Started RelayApplication in 3.787 s` |
| relay UDP 7777 listener | 정상 시작 |
| Mattermost 자동 알림 | 안 옴 (수동 SSH 라 정상) |

#### 측정값 (2차 — 잘못된 forward `deploy 35` 시도)

| 항목 | 값 |
|---|---|
| 명령 소요 | 0.729 s (사실상 noop) |
| healthy 폴링 | 0 회 |
| 외부 502 갯수 | 0 |
| 결과 | **silent inconsistency** — 컨테이너 image 그대로 `:34`, 모든 `:30~:35` tag 가 `:34` image 가리킴, 원래 `:35` image 사라짐 |

#### 측정값 (3차 — Jenkins 수동 트리거로 정상 forward)

| 항목 | 값 |
|---|---|
| 트리거 | Jenkins UI → `lostmemory-server-deploy` → Build with Parameters → 기본값 |
| 운영자 SSH 명령 | **0 개** (정상 경로) |
| 새 build 번호 | `:36` |
| 새 image ID | `sha256:82d7c2191bce...` |
| **🎯 발견** | 새 :36 image ID 가 **원래 :35 image ID 와 완전 동일** — Docker BuildKit + Gradle 의 reproducible 빌드가 deterministic 하게 동작. develop source 변경 없으면 image hash 도 동일. |
| 외부 smoke | `{"status":"UP",...}` ✅ |
| Mattermost 알림 | 정상 경로라 자동 도착 기대 (운영자 확인 사항) |

#### 발견된 함정 (본 런북 cold-read 검증의 핵심 수확)

1. **`<N-1>` placeholder paste 함정** — bash 가 `<` 를 input redirection 으로 해석해 `syntax error near unexpected token 'newline'`. → §3.2 / §3.5 의 명령을 `PREV_N=NN` / `NEXT_N=NN` 변수 패턴 + 함정 경고로 정정.
2. **Windows PowerShell `curl` 함정** — `Invoke-WebRequest` alias 라 `-sf` 플래그 모름. → §2.5 / §3.3 에 `curl.exe` 명시 + `/dev/null` → `NUL` 가드 추가.
3. **EC2 host 의 `localhost:8080` 함정** — server-app 컨테이너 8080 은 publish 안 되어 host 에서 직접 호출 불가. → §3.1 명령에서 `docker exec server-app-1 curl ...` 로 교체.
4. **🔥 `deploy <N>` 의 forward 복구 함정 (가장 큰 수확)** — 직전 rollback 상태에서 `deploy <N>` 호출 시 `docker tag :latest :N` 이 silent inconsistency 유발. 모든 history tag 가 같은 image 가리킴 + 원래 :N image 손실. **운영 장애 시 운영자가 "복귀했다" 착각하면서 실제 image 는 안 바뀐 채 진행할 위험.** → §3.5 를 3.5-a (Jenkins), 3.5-b (develop merge), 3.5-c (수동 재빌드), 3.5-d (그대로 종료) 4 갈래로 재설계. §6 알려진 한계에 deploy/rollback 의미 차이 추가.
5. **`WARN: Found orphan containers`** — monitoring/jenkins 스택이 같은 compose project name 공유. 정상 메시지지만 첫 운영자에게는 헷갈림. → §3.2 기대 출력 + §6 한계에 명시.
6. **history 보존 image 들이 같은 hash** — `:30 ~ :34` 가 전부 `7059a89b52cc` 동일. reproducible 빌드 결과거나 변경 없는 commit 들. → §6 한계에 명시 (history 길이가 실질 rollback 다양성과 무관할 수 있음).

#### 본 런북 보강 사항 (본 리허설로 추가된 변경)

- §0 환경 사실 sticky 표 (EC2 호스트 / 경로 / 컨테이너 이름)
- §2.5 / §3.3 PowerShell 분기 가드
- §3 진입 조건 강화 (§2 사전조건 통과 후에만)
- §3.1 EC2 localhost:8080 명령 → docker exec 로
- §3.2 PREV_N 변수 패턴 + bash syntax 함정 경고 + orphan 경고 기대 출력
- §3.5 4 갈래 forward 경로 재설계 (deploy 함정 큰 가중치)
- §6 deploy/rollback 의미 차이, reproducible 빌드 함정, dangling prune 위험, orphan 경고 추가

#### 결론

- ✅ **rollback 명령은 정상 동작** — 약 17초 명령 시간 / 약 10.5초 사용자 체감 다운타임 (예상치 5-10초 와 거의 일치).
- ✅ relay 자동 재시작 / image swap 검증 / smoke 모두 통과.
- 🔥 **forward 복구는 `deploy <N>` 가 아니라 Jenkins 트리거 또는 develop merge** — 본 발견이 본 리허설의 가장 큰 수확. 본 런북 §3.5 가 이 함정을 명시적으로 막도록 재설계됨.
- 본 리허설로 본 런북이 cold-read 가능 수준에 도달. 다음 리허설은 (가능하면) 다른 팀원이 본 런북만 보고 따라 진행 → 추가 함정 발견 시 §A.2 에 누적.

---

## 핵심 reference

- 스크립트: [server/scripts/deploy.sh](../server/scripts/deploy.sh)
- compose healthcheck 정의: [server/docker-compose.yml](../server/docker-compose.yml) (`app.healthcheck`, `relay.healthcheck`)
- 배포 파이프라인 가이드: [docs/ci-jenkins-setup.md](ci-jenkins-setup.md)
- 2차 MVP 인프라 검증: [docs/2nd-mvp-infra-verification.md](2nd-mvp-infra-verification.md)
- relay UDP 인프라: S14P31C201-462 (자체 Relay UDP 7777, app 과 image 공유)
- BE 데이터 롤백 트랙: S14P31C201-153 (별도 책임)
