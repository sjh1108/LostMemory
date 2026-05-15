# 2차 MVP 인프라 검증 결과 (INFRA-19 / S14P31C201-125)

- **검증 일자**: 2026-05-06
- **검증자**: 송주헌 (인프라 주담당)
- **범위**: INFRA 단독 — SSL + CI/CD + 운영 자산
- **대상 도메인**: `k14c201.p.ssafy.io`
- **검증 시점 빌드**: `server-app:20` (commit `645fd3c76`, develop)
- **선행 작업**: INFRA-15 (HTTPS) / INFRA-16 (Mattermost) / INFRA-17 (logging) / INFRA-18 (deploy.sh) / INFRA-406 (자동 트리거 https) — 모두 develop 머지 + 운영 적용 완료

이 문서는 위 5개 인프라 작업이 통합된 상태로 2차 MVP 시연 수준에 도달했는지 항목별 검증한 결과다. BE API 시연 흐름과 Unity 클라 end-to-end 는 본 검증 범위 밖 — 시연 리허설에서 별도 검증.

---

## 검증 결과 요약

| 카테고리 | 항목 | 결과 | 비고 |
|---|---|---|---|
| SSL | cert chain (Let's Encrypt) | ✅ | issuer = E8, 만료 2026-08-02 (88일 후) |
| SSL | HTTPS 200 응답 | ✅ | `{"status":"UP","groups":["liveness","readiness"]}` |
| SSL | HTTP → HTTPS 301 redirect | ✅ | `Location` 헤더 정상 |
| SSL | ACME challenge 경로 80 살아있음 | ✅ | 404 응답 (정상 — 챌린지 없을 때 기대 동작) |
| SSL | 자동 갱신 dry-run | ✅ | "all simulated renewals succeeded" |
| SSL | SSL Labs 등급 | ⏸ | 운영자 브라우저 확인 필요 (별도 캡처 → Jira 코멘트) |
| CI/CD | develop push → 자동 트리거 | ✅ | 빌드 #18/19/20 모두 `Started by remote host 54.180.247.19` |
| CI/CD | Jenkinsfile → deploy.sh deploy → healthy | ✅ | 빌드 #20 `[deploy] app healthy` (INFRA-18 첫 production 사용) |
| CI/CD | Mattermost 빌드 결과 알림 | ✅ | 사용자 채널 직접 확인 (이번 세션 머지 4회 모두 도착) |
| CI/CD | rollback dry-run | ✅ | 2026-05-15 1회 검증 완료 — 실측 다운타임 ~10.5s, 런북 [docs/runbook-rollback.md](runbook-rollback.md) (S14P31C201-129) |
| 운영 | 6 컨테이너 모두 healthy | ✅ | server-app/postgres/nginx/redis/jenkins/gitlab-runner 모두 Up |
| 운영 | Docker logging rotation | ⚠️ | 5/6 적용. gitlab-runner 1건 보류 (다음 자연 재배포 시 자동 적용) |
| 운영 | logrotate `/etc/logrotate.d/lostmemory` | ✅ | dry-run 정상. 대상 파일 미존재는 cron 첫 실행 전이라 정상 |
| 운영 | 디스크 사용량 < 30% | ✅ | 309G 중 20G 사용 (7%) |
| 주의 | EC2 working tree develop sync | ⚠️ | `./scripts/deploy.sh` 미존재 — develop pull 누락. 운영자 SSH 작업 전 sync 필요 |

**결론**: SSL / CI/CD / 운영 자산 영역에서 시연 가능 수준 도달. 단 두 가지 추가 작업 (SSL Labs 등급 캡처 / EC2 working tree develop sync) 은 시연 직전 D-1 에 마무리. rollback dry-run (⏸) 은 S14P31C201-129 에서 후속 검증 완료 (2026-05-15) — 런북 [docs/runbook-rollback.md](runbook-rollback.md).

---

## 1. SSL / HTTPS

### 1.1 cert chain

```bash
$ openssl s_client -connect k14c201.p.ssafy.io:443 -servername k14c201.p.ssafy.io < /dev/null 2>/dev/null \
    | openssl x509 -noout -issuer -subject -dates

issuer=C = US, O = Let's Encrypt, CN = E8
subject=CN = k14c201.p.ssafy.io
notBefore=May  4 06:10:12 2026 GMT
notAfter=Aug  2 06:10:11 2026 GMT
```

**결과**: ✅ Let's Encrypt E8 intermediate. 만료 88일 후. 자동 갱신 cron (root, 월요일 03:17) 이 만료 30일 이내일 때 갱신.

### 1.2 HTTP → HTTPS 301

```bash
$ curl -sI http://k14c201.p.ssafy.io/api/actuator/health | head -3

HTTP/1.1 301 Moved Permanently
Server: nginx
Date: Wed, 06 May 2026 08:07:48 GMT
```

**결과**: ✅ INFRA-15 의 redirect 동작.

### 1.3 HTTPS 200

```bash
$ curl -sI https://k14c201.p.ssafy.io/api/actuator/health | head -3
HTTP/2 200
server: nginx
date: Wed, 06 May 2026 08:07:48 GMT

$ curl -sf https://k14c201.p.ssafy.io/api/actuator/health
{"status":"UP","groups":["liveness","readiness"]}
```

**결과**: ✅ HTTP/2 200 + Spring actuator UP.

### 1.4 ACME challenge 경로 80 살아있음

```bash
$ curl -sI http://k14c201.p.ssafy.io/.well-known/acme-challenge/probe | head -3

HTTP/1.1 404 Not Found
Server: nginx
Date: Wed, 06 May 2026 08:07:48 GMT
```

**결과**: ✅ 404 정상 (실제 챌린지 토큰 없을 때 기대 동작). 다음 갱신 시 `/var/www/certbot` 에 토큰 배치 → 200 응답.

### 1.5 자동 갱신 dry-run

```bash
$ docker compose --env-file .env --profile certbot run --rm certbot renew --dry-run

- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
Processing /etc/letsencrypt/renewal/k14c201.p.ssafy.io.conf
- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
Simulating renewal of an existing certificate for k14c201.p.ssafy.io

- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
Congratulations, all simulated renewals succeeded:
  /etc/letsencrypt/live/k14c201.p.ssafy.io/fullchain.pem (success)
- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
```

**결과**: ✅ ACME 통신 + cert 갱신 절차 모두 정상.

### 1.6 SSL Labs 외부 등급

⏸ **운영자 브라우저 확인 필요**:
- URL: `https://www.ssllabs.com/ssltest/analyze.html?d=k14c201.p.ssafy.io`
- 확인 후 등급 + 발견된 약점 (cipher suite / HSTS / TLS 1.3 등) 을 Jira S14P31C201-125 코멘트에 캡처 첨부.
- 등급 B 이하면 nginx `ssl_protocols` / `ssl_ciphers` 튜닝 별도 티켓 필요.

---

## 2. CI/CD 파이프라인

### 2.1 자동 트리거 검증

```
build #18 | Started by remote host 54.180.247.19 | Build #18 deployed
build #19 | Started by remote host 54.180.247.19 | Build #19 deployed
build #20 | Started by remote host 54.180.247.19 | Build #20 deployed
```

**결과**: ✅ INFRA-406 (GitLab CI variable 의 https 갱신) 적용 후 모든 빌드가 `Started by remote host` (자동 트리거). 직전 INFRA-15 머지 직후의 빌드 #17 이전은 모두 `Started by user wkrekdahdml` (수동) 였음 — 정확히 INFRA-406 시점부터 자동화 작동.

### 2.2 Jenkinsfile → deploy.sh deploy 흐름 (INFRA-18 검증)

빌드 #20 의 핵심 라인:
```
Commit message: "[docs] 배포/롤백 운영 (deploy.sh) 섹션 추가"
+ chmod +x scripts/deploy.sh
+ ./scripts/deploy.sh deploy 20
[deploy] app healthy
+ ./scripts/deploy.sh status
✅ Build #20 deployed. Image: server-app:20, branch: develop, commit: 645fd3c76...
```

**결과**: ✅ INFRA-18 의 새 Deploy stage (`./scripts/deploy.sh deploy "$BUILD_NUMBER"`) 가 develop 머지 첫 빌드부터 정상 작동. `[deploy] app healthy` 메시지가 deploy.sh 내부의 `wait_healthy()` 함수가 호출됐다는 증거.

### 2.3 Mattermost 빌드 결과 알림 (INFRA-16 검증)

⏸ **사용자가 채널 캡처 → Jira 코멘트 첨부**.

이번 세션의 빌드 #18/19/20 + INFRA-122/123/124 머지 4회 모두 ✅ 메시지 도착 확인됨 (사용자 직접 채널 확인). 추가 캡처는 발표 자료/평가 산출물 차원.

### 2.4 deploy.sh history (보존된 image 태그)

```
$ docker images server-app
REPOSITORY   TAG       SIZE      CREATED
server-app   latest    519MB     16 hours ago
server-app   14        519MB     16 hours ago
server-app   15        519MB     16 hours ago
server-app   16        519MB     16 hours ago
server-app   17        519MB     16 hours ago
server-app   18        519MB     16 hours ago
server-app   19        519MB     16 hours ago
server-app   20        519MB     16 hours ago
```

**결과**: ✅ 7개 빌드 보존 (#14-20). buildDiscarder 의 numToKeepStr=20 이 메타데이터를 정리하지만 docker image 자체는 별도 누적. 한 image 519MB × 7 ≈ 3.6GB 차지.

### 2.5 rollback dry-run

✅ **2026-05-15 1회 검증 완료 (S14P31C201-129)**.

**핵심 측정값** (`:35` → `:34` rollback):

| 항목 | 값 |
|---|---|
| 명령 총 소요 (T1→T2) | 17.274 s |
| 사용자 체감 다운타임 (외부 502 연속 구간) | **~10.49 s** |
| healthy 폴링 횟수 | 5/30 (~15 s) |
| 외부 502 갯수 (0.5s polling) | 17 개 |
| relay 부팅 시간 | 3.787 s (UDP 7777 listener 정상) |
| image swap 검증 | ✅ (`82d7c2191bce` → `7059a89b52cc`) |

→ 예측 다운타임 "5-10초" 와 거의 일치 (polling 간격 0.5s 라 +1초 정도 over-measure).

**발견된 함정 / 본 런북에 반영된 보강 사항** (상세는 [docs/runbook-rollback.md](runbook-rollback.md) 부록 A.1):

1. `<N-1>` placeholder 직접 paste 가 bash syntax error 유발 → `PREV_N=NN` 변수 패턴으로 정정
2. Windows PowerShell `curl` 은 `Invoke-WebRequest` alias 라 `-sf` 안 먹힘 → `curl.exe` 명시
3. EC2 host 의 `localhost:8080` 직접 호출 불가 (publish 안 됨) → `docker exec server-app-1 curl ...` 로
4. **🔥 `./scripts/deploy.sh deploy <N>` 으로 forward 복구 불가능** (silent inconsistency 유발 — 모든 history tag 가 같은 image 가리키게 됨, 원래 image untagged 손실). forward 정상 경로 = Jenkins 트리거 / develop merge / EC2 재빌드 — 본 런북 §3.5 4 갈래로 재설계.

> 운영 장애 시 운영자가 잘못 따라했으면 silent inconsistency 로 "복귀했다" 착각 + 실제 image 안 바뀐 채 진행할 위험 있던 함정. 본 리허설의 가장 큰 수확.

전체 운영 런북: [docs/runbook-rollback.md](runbook-rollback.md). 차후 리허설마다 부록 A 누적.

---

## 3. 운영 자산

### 3.1 컨테이너 healthy

```
server-app-1        Up 3 hours (healthy)
server-postgres-1   Up 29 minutes (healthy)
server-nginx-1      Up 3 hours
server-redis-1      Up 3 hours (healthy)
jenkins             Up 3 hours
gitlab-runner       Up 3 hours
```

**결과**: ✅ 6개 모두 Up. healthcheck 가 정의된 4개 (app/postgres/redis 외) 모두 healthy. nginx/jenkins/gitlab-runner 는 healthcheck 미정의지만 외부 접근 + 빌드 트리거로 살아있음 검증.

### 3.2 Docker logging rotation 적용 (INFRA-17 검증)

```
server-app-1: json-file map[compress:true max-file:3 max-size:10m]
server-postgres-1: json-file map[compress:true max-file:3 max-size:10m]
server-nginx-1: json-file map[compress:true max-file:3 max-size:10m]
server-redis-1: json-file map[compress:true max-file:3 max-size:10m]
jenkins: json-file map[compress:true max-file:3 max-size:10m]
gitlab-runner: json-file map[]
```

**결과**: ⚠️ 5/6 적용. **gitlab-runner 보류 1건** — Docker daemon 의 LogConfig 는 컨테이너 생성 시점에 박혀 단순 `docker restart` 로 갱신 안 됨. `/etc/docker/daemon.json` 의 글로벌 default 가 다음 `docker rm + run` 시점부터 자동 적용. 1MB 수준이라 당장 디스크 위협 없음.

### 3.3 logrotate 설정

```
$ sudo logrotate -d /etc/logrotate.d/lostmemory

Handling 1 logs
rotating pattern: /var/log/certbot-renew.log  weekly (8 rotations)
empty log files are not rotated, old logs are removed
considering log /var/log/certbot-renew.log
  log /var/log/certbot-renew.log does not exist -- skipping
Creating new state
```

**결과**: ✅ logrotate conf 정상 등록 + dry-run 통과. 대상 파일 미존재는 cron 첫 실행 전이라 예상된 동작 — 다음 갱신 cron (월요일 03:17) 후 자동으로 회전 대상에 잡힘.

### 3.4 디스크 / docker overhead

```
$ df -h /
/dev/root       309G   20G  290G   7% /

$ docker system df
TYPE            TOTAL     ACTIVE    SIZE      RECLAIMABLE
Images          22        6         6.822GB   4.878GB (71%)
Containers      6         6         5.226MB   0B (0%)
Local Volumes   10        6         2.689GB   2.64GB (98%)
Build Cache     40        0         1.942GB   35.93MB
```

**결과**: ✅ 디스크 사용 7% (20G/309G). 한참 여유. docker overhead 11.5GB 중 Reclaimable 비율 높음 (image 71% / volume 98%) — 별도 1회 `docker system prune -a --volumes` 로 약 7GB 회수 가능 (이번 검증 범위 밖).

### 3.5 컨테이너 json-log 큰 파일 top 3

```
589.94 KB  /var/lib/docker/containers/b28b48995627.../-json.log
 27.77 KB  /var/lib/docker/containers/324ee48e0954.../-json.log
 15.00 KB  /var/lib/docker/containers/e050dabbff43.../-json.log
```

**결과**: ✅ 가장 큰 파일도 590KB 수준. INFRA-17 의 max-size 10MB 한도까지 한참 여유.

---

## 발견된 이슈 / 후속

| # | 카테고리 | 내용 | 후속 |
|---|---|---|---|
| 1 | 운영 | gitlab-runner 의 LogConfig 가 빈 옵션 (`map[]`) — 정책 적용 안 됨 | 보류. 다음 자연 재배포 시 자동 적용. 즉시 조치 원하면 `docker stop + rm + run` (단 처음 띄울 때 옵션 정확히 알아야 안전) |
| 2 | 운영 | EC2 working tree (`/home/ubuntu/lostmemory`) 가 develop sync 안 됨 — `./scripts/deploy.sh` 미존재 | 운영자가 머지 후 매번 git pull. 자동화는 별도 후속 (cron 또는 Jenkins post-deploy hook) |
| 3 | SSL | SSL Labs 등급 미확인 | 운영자 브라우저로 1회 확인 + 등급 캡처 → Jira 코멘트. B 이하면 nginx ssl_protocols/ssl_ciphers 튜닝 별도 티켓 |
| 4 | CI/CD | rollback sub-command 실 검증 미완 | 한가한 시간대 1회 dry-run 후 결과를 본 문서 또는 D-1 검증 문서에 추가 |
| 5 | 운영 | docker overhead 11.5GB 중 Reclaimable 7GB | 1회성 `docker system prune` 또는 별도 cron 정책 (이번 PR 범위 밖) |

---

## 결론

**SSL / CI/CD / 운영 자산 영역에서 2차 MVP 시연 가능 수준 도달.** 발견된 5개 이슈는 모두 운영 안정성 영향 작은 위생 항목 (즉시 차단 사유 없음).

시연 직전 D-1 에 짧게 보강할 항목:
- SSL Labs 등급 캡처 (이슈 #3)
- gitlab-runner 재배포 시 LogConfig 적용 확인 (이슈 #1)
- rollback sub-command 1회 dry-run (이슈 #4)
- EC2 working tree develop sync 자동화 검토 (이슈 #2)

**INFRA 통과 ≠ 시연 통과** — BE API 시연 흐름 (게스트 로그인 → 진행 저장 → 런 결과) 과 Unity 클라 end-to-end (두 명 PC + Relay) 는 별도 시연 리허설 단계에서 검증.
