# 3차 MVP 배포 리허설 (S14P31C201-134)

- **대상**: 운영 EC2 `k14c201.p.ssafy.io` 의 `server-app` (Spring Boot) + `relay` (Netty UDP) 컨테이너
- **목적**: 2026-05-18 발표 당일 자동 배포 흐름의 다운타임 / 회복 / 알림이 발표에 영향을 주지 않는 수준인지 **팀 관찰 하에 2회** 검증
- **회당 소요**: 약 5~10분 (사전조건 포함)
- **선행 도구**: [`server/scripts/deploy.sh`](../server/scripts/deploy.sh) (deploy/status), [`server/Jenkinsfile`](../server/Jenkinsfile), GitLab develop push, Mattermost 채널
- **상태**: 초안 — 1차/2차 raw 측정은 부록 A/B 에 운영자가 직접 채움

> 본 리허설은 **app/relay 컨테이너 자동 배포 경로** 만 다룹니다. DB 백업 ([S14P31C201-132](https://ssafy.atlassian.net/browse/S14P31C201-132)) 은 별도, 발표 당일 모니터링 셋업 ([S14P31C201-135](https://ssafy.atlassian.net/browse/S14P31C201-135)) 은 후속 티켓.

---

## 0. 환경 사실 (sticky)

| 항목 | 값 |
|---|---|
| 운영 EC2 호스트 | `k14c201.p.ssafy.io` |
| SSH 진입 | `ssh ubuntu@k14c201.p.ssafy.io` |
| 운영 working tree 경로 | `~/lostmemory` |
| compose 디렉토리 | `~/lostmemory/server` |
| app / relay 컨테이너 이름 | `server-app-1`, `server-relay-1` |
| 외부 health endpoint | `https://k14c201.p.ssafy.io/api/actuator/health` |
| compose env file | `~/lostmemory/server/.env` (LF 유지) |
| Jenkins URL | (운영자 Mattermost 또는 운영 노트 참고) |
| Jenkins credential | `lostmemory-env` (Secret file, 평문 검증 시 EC2 SSH 안 Script Console) |
| Mattermost 빌드 알림 채널 | Jenkins → 공용 webhook |
| 발표 일자 | 2026-05-18 (sprint 종료일) |
| EC2 호스트 TZ | UTC (1차 리허설 시각은 KST 환산 함께 기록) |

### 0.1 리허설 인원

| 역할 | 인원 | 책임 |
|---|---|---|
| 인프라 운영자 | 1 (송주헌) | deploy/trigger 실행, T0/T1/T2 측정, 부록 raw 채움 |
| 외부 health watcher | 1 | 본인 PC 에서 `curl.exe -sf https://...` 무한 loop, 회복 시각 캡처 |
| Mattermost watcher | 1 | 빌드 알림 도달 시각 캡처 (스크린샷 권장) |
| 게임 클라 / BE | 1+ | 배포 직전/직후 동시 세션 유지, 단절/재접속 체감 보고 |

> 단독 진행 가능하지만, 팀 관찰 부분 (외부 watcher / Mattermost / 클라 체감) 은 발표 당일과 같은 사람 배치로 미리 연습하는 게 본 티켓 의도.

---

## 1. 측정 항목 / 통과 기준

| 항목 | 목표 | 측정 방법 |
|---|---|---|
| 컨테이너 다운타임 (T1 → T2) | **< 30초** | `date +"%H:%M:%S.%3N"` 으로 T1 시작 / T2 healthy 도달 시각 기록 |
| 외부 5xx / connection refused 시간 창 | **< 30초** | 본인 PC `while; do curl ...; sleep 1; done` 의 fail 구간 |
| Mattermost 빌드 완료 알림 도달 | **빌드 완료 + 60초 이내** | 운영자 스크린샷 시각 vs Jenkins console 종료 시각 |
| postgres / redis / nginx 컨테이너 | **영향 없음** | recreate 대상 아님 — `docker compose ps` 상태가 변하지 않아야 함 |
| 클라 세션 단절 → 재접속 | 단절은 있어도 재접속 1회 시도로 복귀 | 게임 클라 / BE 측 보고 |
| 게임 진행 데이터 손실 | **없음** | postgres 컨테이너 그대로라 자명하지만 클라 보고로 cross-check |

### 통과 기준 통합

세 항목 모두 ✅ 면 발표 당일 자동 배포 진행 OK:
- 다운타임 < 30s
- 알림 도달 < 60s
- 부수 컨테이너 영향 없음

---

## 2. 사전조건 체크리스트

본 실행 전에 모두 ✅ 확인. 하나라도 실패면 본 리허설 중단하고 운영자 단톡에 보고.

### 2.1 운영 EC2 working tree develop sync

```bash
ssh ubuntu@k14c201.p.ssafy.io
cd ~/lostmemory
git fetch origin develop
LOCAL=$(git rev-parse HEAD)
REMOTE=$(git rev-parse origin/develop)
[ "$LOCAL" = "$REMOTE" ] && echo "OK: synced" || echo "WARN: pull 검토"
```

> ⚠️ EC2 가 develop 보다 뒤처져 있으면 deploy.sh / Jenkinsfile 옛 버전. 리허설 전 `git pull --ff-only` 필요. ([docs/2nd-mvp-infra-verification.md](2nd-mvp-infra-verification.md) 결론 참조)

### 2.2 6 컨테이너 모두 healthy

```bash
cd ~/lostmemory/server
docker compose --env-file .env ps
```

기대: `server-app-1` / `server-postgres-1` / `server-redis-1` / `server-nginx-1` / `server-relay-1` / `jenkins` (또는 동등) 모두 `Up ... (healthy)`.

### 2.3 deploy.sh 존재 + 실행권한 + history 확인

```bash
cd ~/lostmemory/server
ls -l ./scripts/deploy.sh
./scripts/deploy.sh status
./scripts/deploy.sh history
```

`history` 에 최소 2개 이상 보존된 `server-app:N` 태그가 있어야 함 (리허설 도중 회귀 시 rollback 가능 — [docs/runbook-rollback.md](runbook-rollback.md)).

### 2.4 외부 health endpoint 정상

본인 PC 에서:

```bash
curl.exe -sf https://k14c201.p.ssafy.io/api/actuator/health
# {"status":"UP","groups":["liveness","readiness"]}
```

> ⚠️ **Windows PowerShell**: `curl` 이 `Invoke-WebRequest` alias 라 `-sf` 인자 안 먹힘. 진짜 curl 은 `curl.exe` 명시 호출.

### 2.5 Mattermost 채널 살아있음

운영 채널에서 본인 핸들로 1줄 "리허설 시작 예정" 공지 + Jenkins 빌드 알림 채널에 직전 빌드 메시지가 보이는지 확인.

### 2.6 디스크 여유

```bash
df -h ~
```

`Avail` 1GB 이상.

### 2.7 백업 안전망 (선택, 권장)

리허설 전 DB 백업 1회 수동 실행해 두면 회귀 시 복원 가능:

```bash
sudo /home/ubuntu/lostmemory/server/scripts/db-backup.sh
```

리허설 자체는 app/relay 만 recreate 라 DB 영향 없지만, 발표 직전이라 안전망 권장.

---

## 3. 1차 리허설 — Jenkins UI manual trigger 경로

**시나리오**: develop 의 현재 tip 으로 코드 변경 없이 빌드/배포만 다시 실행.
**의도**: trigger 경로(GitLab webhook) 의존성을 배제한 **순수 Jenkins → deploy.sh → healthy** 흐름의 다운타임 기준치 확보.

### 3.1 직전 상태 캡처 (T0)

EC2 안에서:

```bash
cd ~/lostmemory/server
date +"%H:%M:%S.%3N"            # T0
./scripts/deploy.sh status
./scripts/deploy.sh history | head -5
echo "현재 latest image ID: $(docker inspect server-app-1 --format '{{.Image}}')"
docker compose --env-file .env ps
```

→ 출력을 부록 A 의 "T0 캡처" 칸에 paste.

**외부 health watcher** 본인 PC 에서 별도 터미널에 watch loop 띄움:

```bash
# bash / git bash
while :; do
  printf '%(%H:%M:%S)T '
  curl.exe -sf -o /dev/null -w '%{http_code}\n' https://k14c201.p.ssafy.io/api/actuator/health \
    || echo "FAIL"
  sleep 1
done
```

→ 이 watch 출력 전체를 발표 / 회고 시점에 보관.

### 3.2 ⚠️ Jenkins manual trigger (T1 → T2)

**별도 paste — 의도적으로 분리합니다.** §3.1 결과 캡처를 부록 A 에 paste 한 다음 본 단계 진행.

운영자가 직접 Jenkins UI 에서 trigger:

1. Jenkins → 해당 job → **Build with Parameters**
2. 파라미터 그대로 둠 (현재 develop tip 빌드)
3. Build 버튼 누름 → 빌드 시작 시각을 **T1** 으로 기록

빌드 진행 중 EC2 안에서:

```bash
# T2 측정 — app healthy 도달 직후
date +"%H:%M:%S.%3N"
./scripts/deploy.sh status
```

`status` 가 `running (health: healthy)` 보이는 그 시점이 T2.

### 3.3 회복 확인

```bash
# 외부에서
curl.exe -sf https://k14c201.p.ssafy.io/api/actuator/health

# EC2 안에서
docker compose --env-file .env ps
docker compose --env-file .env logs --tail=30 app
docker compose --env-file .env logs --tail=30 relay
```

부수 컨테이너 (postgres/redis/nginx) `Up` 상태 유지 확인.

### 3.4 Mattermost 알림 확인

운영자 Mattermost 빌드 채널에 Jenkins 빌드 완료 메시지 도달 시각 캡처 (스크린샷).

### 3.5 클라 / BE 측 보고

리허설 직전부터 진행 중이던 세션에 단절/재접속 발생했는지 1줄 보고.

---

## 4. 1차 회고

부록 A 측정값 정리 후 본 섹션 채움.

- **다운타임 (T2 - T1)**: _(예: 10.5s)_
- **외부 5xx 시간 창**: _(예: 8s)_
- **Mattermost 알림 도달 지연**: _(빌드 완료 + N s)_
- **부수 컨테이너 영향**: 없음 / 있음 (있으면 상세)
- **클라 세션 영향**: 없음 / 있음 (있으면 상세)
- **발견된 이슈**: (있다면 별도 fix 후 2차 리허설로 검증)

---

## 5. 2차 리허설 — develop push 자동 trigger 경로

**시나리오**: 발표 당일 실제 흐름. **GitLab develop push → gitlab-runner → Jenkins webhook → deploy.sh**.
**의도**: trigger 경로까지 포함한 end-to-end 다운타임 + 알림 지연 측정.

### 5.1 trigger 변경 준비

발표 당일과 동일한 의미 있는 변경이 가장 정확하지만, 리허설용으로는 비-운영 영향 변경이 안전:
- **선택지 A**: docs/ 또는 주석 변경 (영향 0, build cache hit 빠름)
- **선택지 B**: empty commit (`git commit --allow-empty`)
- **선택지 C**: 발표 당일 머지 예정인 실제 feature MR (의미 있는 변경 + dependency 검증) — 1차 다운타임이 충분히 짧으면 권장

본 리허설 default 는 **선택지 A** (가장 안전).

### 5.2 사전조건 재확인

§2 체크리스트의 2.2 (컨테이너 healthy), 2.4 (외부 health), 2.5 (Mattermost), 2.7 (백업) 만 다시 확인.

### 5.3 T0 캡처

§3.1 과 동일.

### 5.4 ⚠️ develop push (T1)

**별도 paste — 분리합니다.**

운영자 본인 PC 또는 SSAFY GitLab UI 에서:

```bash
# 본인 작업 worktree 에서
git switch develop
git pull --ff-only

# 선택지 A: docs 변경 (예시)
echo "리허설 2차 trigger $(date -Iseconds)" >> docs/.rehearsal-trigger
git add docs/.rehearsal-trigger
git commit -m "[chore] S14P31C201-134 리허설 2차 trigger"

# T1 측정 — push 직전
date +"%H:%M:%S.%3N"
git push origin develop

# 또는 선택지 B: empty commit
# git commit --allow-empty -m "[chore] S14P31C201-134 리허설 2차 trigger"
# git push origin develop
```

push 종료 시각이 아닌, **Jenkins console 의 빌드 시작 시각** 을 T1 으로 기록 (trigger 경로 지연까지 포함).

### 5.5 T2 / 회복 확인

§3.2 후반부 ~ §3.3 와 동일.

### 5.6 Mattermost / 클라 / BE 보고

§3.4 ~ §3.5 와 동일.

### 5.7 trigger commit 정리

선택지 A 의 `docs/.rehearsal-trigger` 는 발표 전 별도 commit 으로 삭제 (또는 .gitignore 에 추가).

---

## 6. 2차 회고

부록 B 측정값 정리 후 본 섹션 채움. §4 양식과 동일.

추가로 1차 대비 차이 분석:
- **trigger 경로 지연** (push → Jenkins 빌드 시작): _( N s)_
- **다운타임 차이**: 1차 (X s) vs 2차 (Y s)
- **알림 지연 차이**

---

## 7. 결론 + 발표 당일 적용 사항

부록 A/B 와 §4 / §6 종합 후 본 섹션 채움.

| 통과 기준 | 1차 | 2차 | 발표 당일 대응 |
|---|---|---|---|
| 다운타임 < 30s | _(s)_ | _(s)_ | _(OK / 임계치 조정)_ |
| 외부 5xx 창 < 30s | _(s)_ | _(s)_ | _()_ |
| 알림 < 60s | _(s)_ | _(s)_ | _()_ |
| 부수 컨테이너 영향 | _()_ | _()_ | _()_ |
| 클라 세션 영향 | _()_ | _()_ | _()_ |

**발표 당일 대응 사항** (예시 — 실측 후 확정):
- 발표 직전 10분 동안 배포 freeze
- 알림 채널 watcher 배치 시각
- 회귀 시 rollback 진입 기준 (다운타임 > X / 외부 5xx > Y)

---

## 부록 A — 1차 리허설 raw 측정 (운영자 채움)

| 항목 | 값 |
|---|---|
| 실행 일시 | _(YYYY-MM-DD HH:mm KST / UTC)_ |
| 실행자 | _(이름)_ |
| 외부 watcher | _(이름)_ |
| Mattermost watcher | _(이름)_ |
| 클라 / BE 옵저버 | _(이름)_ |
| T0 (직전 캡처) | _(HH:MM:SS.mmm)_ |
| T1 (Jenkins 빌드 시작) | _(HH:MM:SS.mmm)_ |
| T2 (app healthy 도달) | _(HH:MM:SS.mmm)_ |
| **다운타임 = T2 - T1** | _(s)_ |
| 외부 5xx / refused 시간 창 | _(s, watch loop 출력 기준)_ |
| Mattermost 알림 도달 시각 | _(HH:MM:SS)_ |
| 알림 지연 = 알림 - T2 | _(s)_ |
| 부수 컨테이너 상태 변동 | _( 없음 / 상세)_ |
| 클라 / BE 보고 | _( 없음 / 상세)_ |
| 결과 | _( OK / 이슈 / 재시도)_ |

### A.1 T0 캡처 (paste)

```
(여기에 §3.1 출력 paste)
```

### A.2 외부 watcher 출력 (요약)

```
(연속 200 → FAIL N건 → 연속 200 패턴 paste)
```

### A.3 발견된 이슈

- _( 항목별 bullet)_

---

## 부록 B — 2차 리허설 raw 측정 (운영자 채움)

부록 A 양식과 동일.

| 항목 | 값 |
|---|---|
| 실행 일시 | _()_ |
| trigger 방식 | _( 선택지 A docs / B empty / C feature MR)_ |
| trigger commit | _( hash)_ |
| T0 | _()_ |
| T1 (Jenkins console 빌드 시작) | _()_ |
| push 종료 시각 | _( T1-push 경로 지연 계산)_ |
| T2 | _()_ |
| **다운타임 = T2 - T1** | _()_ |
| 외부 watcher 5xx 창 | _()_ |
| Mattermost 알림 지연 | _()_ |
| 부수 컨테이너 영향 | _()_ |
| 클라 / BE 보고 | _()_ |
| 결과 | _()_ |

### B.1 T0 캡처 / 외부 watcher 출력 / 발견된 이슈

부록 A.1 ~ A.3 와 동일 양식.
