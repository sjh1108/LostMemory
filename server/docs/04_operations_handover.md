# 운영 인계 문서

> EC2 호스트 (`k14c201.p.ssafy.io`) 의 모든 운영 절차, 인증 정보 위치, 자주 발생하는 트러블슈팅 패턴을 한 문서에 정리합니다. 인수인계 받는 운영자가 30분 안에 시스템 전체 운영 가능하도록 작성했습니다.
>
> 상세 운영 절차는 `server/README.md` 의 각 절 (HTTPS / Let's Encrypt, Docker 자원 정리, Jenkins 알림, 로그 운영, 배포 / 롤백 등) 을 참조하세요. 본 문서는 그 절차들로 가는 **빠른 진입점 + 트러블슈팅 모음** 입니다.

---

## 1. 빠른 시작 (5분)

EC2 SSH 접속:
```bash
ssh -i ~/.ssh/K14C201T.pem ubuntu@k14c201.p.ssafy.io
cd /home/ubuntu/lostmemory
```

워킹트리 최신화 (운영 작업 전 항상 첫 단계):
```bash
git fetch origin && git switch develop && git pull --ff-only origin develop
```

서비스 상태 확인:
```bash
cd server
docker compose --env-file .env ps                 # 모든 컨테이너 healthy 확인
curl -I https://k14c201.p.ssafy.io/api/actuator/health    # HTTP/2 200
```

세 가지 다 통과하면 시스템 정상.

---

## 2. 시스템 구성

### 호스트
- 인스턴스: 싸피 제공 EC2 (`k14c201.p.ssafy.io`, 16GB RAM, 4 vCPU, Ubuntu)
- 워킹트리 경로: `/home/ubuntu/lostmemory` (모든 운영 명령의 단일 source of truth)

### 컨테이너 (docker-compose 으로 관리)
| 서비스 | 이미지 | 역할 |
|---|---|---|
| `nginx` | `nginx:alpine` | 리버스 프록시, HTTPS 종단, UDP 7777 stream proxy |
| `app` | `server-app:latest` (자체 빌드) | Spring Boot 백엔드 API |
| `relay` | `server-app:latest` (재사용, RelayApplication main override) | Netty UDP Relay |
| `postgres` | `postgres:16-alpine` | 데이터 영속 |
| `redis` | `redis:7-alpine` | 세션 / 캐시 |
| `certbot` | `certbot/certbot:v2.11.0` | Let's Encrypt 발급 / 갱신 (profile `certbot`, 평소 미기동) |
| `jenkins` | `lostmemory-jenkins:latest` (자체 빌드) | CI/CD (별도 docker-compose.jenkins.yml) |

### 네트워크
- `frontend` — 외부 노출 (nginx, app, relay, certbot)
- `backend` (internal) — 외부 차단 (postgres, redis, app)

### 외부 진입 포트
- TCP 80 (`HTTP_PORT`) — Let's Encrypt ACME challenge + HTTPS redirect
- TCP 443 (`HTTPS_PORT`) — 게임 서버 API / Jenkins UI
- UDP 7777 — 자체 Relay (nginx stream proxy → relay 컨테이너)

### 데이터 볼륨 (절대 prune 금지)
- `server_postgres_data` — 게임 DB
- `server_redis_data` — 세션 / 캐시
- `server_certbot_etc` — Let's Encrypt 인증서
- `server_certbot_webroot` — ACME challenge 응답

---

## 3. 인증 정보 위치 (값은 별도 채널 인계)

| 자산 | 위치 | 누가 갱신 |
|---|---|---|
| EC2 SSH key | `~/.ssh/K14C201T.pem` (운영자 로컬) | 싸피 콘솔 |
| GitLab Personal Access Token | 운영자 GitLab 계정 (https://lab.ssafy.com/-/user_settings/personal_access_tokens) | 운영자 본인 |
| Jenkins admin 계정 | Jenkins 컨테이너 첫 부팅 시 console log | Jenkins UI |
| Jenkins credential `lostmemory-env` | Jenkins UI → Credentials → System → Global (Secret file) | 운영자 |
| `.env` (host) | `/home/ubuntu/lostmemory/server/.env` | 운영자 SSH |
| Jenkins credential `lostmemory-env` 와 host `.env` 는 **동기화 유지** | — | 운영자 |
| Mattermost incoming webhook URL | `lostmemory-env` 의 `MATTERMOST_WEBHOOK_URL` 라인 | 운영자 |
| AWS console | 사무국 또는 인계 책임자 | 사무국 |

> `.env`, deploy key, JWT secret 등은 git 에 커밋하지 않습니다 (`server/.gitignore` 에서 차단됨).

---

## 4. 자주 쓰는 운영 명령

### 배포 / 롤백 (`server/scripts/deploy.sh`)
```bash
cd /home/ubuntu/lostmemory/server
./scripts/deploy.sh status                # 현재 image / state / health
./scripts/deploy.sh history               # 보존된 server-app:N 태그 목록
./scripts/deploy.sh deploy <N>            # 빌드된 latest 를 N 으로 태깅 + up -d
./scripts/deploy.sh rollback <N>          # N 태그를 latest 로 재태깅 + force-recreate (~10초)
```

### 컨테이너 직접 조작
```bash
docker compose --env-file .env ps                              # 모든 컨테이너 상태
docker compose --env-file .env logs --tail=100 <service>       # 로그
docker compose --env-file .env restart <service>               # 재시작
docker compose --env-file .env up -d --force-recreate <svc>    # 재생성 (옛 컨테이너 stop/rm 후 새로)
docker compose --env-file .env exec nginx nginx -t             # nginx config 검증
docker compose --env-file .env exec nginx nginx -s reload      # nginx 무중단 reload
```

### HTTPS 갱신 (`server/scripts/certbot-renew.sh`, root crontab 자동)
```bash
sudo /home/ubuntu/lostmemory/server/scripts/certbot-renew.sh  # 수동 트리거
sudo tail -20 /var/log/certbot-renew.log                       # 결과 확인
```
- 자동 cron: 월요일 03:17 (Let's Encrypt 정책상 만료 30일 이내일 때만 실제 갱신)

### Docker 자원 정리 (`server/scripts/docker-prune.sh`, root crontab 자동)
```bash
sudo /home/ubuntu/lostmemory/server/scripts/docker-prune.sh    # 수동 트리거
sudo tail -20 /var/log/docker-prune.log                         # 결과 확인
docker system df                                                # 현재 디스크 점유
```
- 자동 cron: 일요일 03:30 (dangling image + 30일 이상된 build cache 만)
- **volume / 컨테이너 / network prune 안 함** — 데이터 볼륨 보호

### Mattermost 알림 (Jenkins 빌드 결과)
- Jenkinsfile 의 `notifyMattermost()` 함수가 자동 호출
- webhook URL 은 `lostmemory-env` credential 의 `MATTERMOST_WEBHOOK_URL` 라인
- 채널 / URL 변경 시 운영자가 credential 갱신 + Replace

---

## 5. 트러블슈팅 (이번 세션 마주친 실제 증상 위주)

### 5.1 워킹트리 sync 누락 → 운영 명령이 옛 파일로 동작

**증상**:
- `nginx -t` 통과는 했는데 reload 후 새 conf 가 적용 안 됨
- logrotate 정책 갱신 후 `Handling 1 logs` 만 표시 (새 정책 미인식)
- 새 스크립트 (`docker-prune.sh` 등) 실행 시 "No such file"

**진단**:
```bash
cd /home/ubuntu/lostmemory
git branch --show-current      # 어느 브랜치 위에 있는지
git log --oneline -3           # HEAD 가 origin/develop 와 일치하는지
git ls-remote origin develop   # origin 의 진짜 develop HEAD
```

**해결**:
```bash
git fetch origin --prune
git pull --ff-only origin develop
```

→ 위 단계 후 nginx reload / logrotate cp / 스크립트 실행 다시.

### 5.2 `git pull --ff-only` fail — "untracked working tree files would be overwritten"

**증상**:
```
error: The following untracked working tree files would be overwritten by merge:
        server/scripts/<some-file>
```

**원인**: 운영자가 EC2 위에서 직접 만든 untracked 파일이 있는데 develop 의 새 commit 이 같은 파일을 추가하려고 시도.

**해결**:
```bash
# 1. 안전 백업
cp server/scripts/<some-file> /tmp/<some-file>.local-backup

# 2. 백업한 파일 제거
rm server/scripts/<some-file>

# 3. pull
git pull --ff-only origin develop

# 4. 백업 vs 새 파일 비교 — 차이 있으면 운영자 수정 검토
diff /tmp/<some-file>.local-backup server/scripts/<some-file>
```

### 5.3 GitLab `git pull/push` HTTP Basic Access denied

**증상**:
```
remote: HTTP Basic: Access denied. ... required to use a token instead of a password
fatal: Authentication failed for 'https://lab.ssafy.com/...'
```

**원인**: GitLab 이 비밀번호 인증을 거부하고 Personal Access Token 만 허용.

**해결**:
1. PAT 발급 — `https://lab.ssafy.com/-/user_settings/personal_access_tokens` (scope: `read_repository`, `write_repository`)
2. `git pull` 시 Username 자리에 GitLab 아이디, **Password 자리에 PAT 입력** (비밀번호 아님)
3. (선택) 매번 입력 회피: `git config --global credential.helper store` (보안 trade-off — 평문 저장)

### 5.4 sudo redirect Permission denied

**증상**:
```bash
sudo /path/to/script.sh >> /var/log/some.log 2>&1
# → -bash: /var/log/some.log: Permission denied
```

**원인**: `sudo` 는 스크립트 실행에만 적용되고 `>>` redirect 는 현재 shell (ubuntu user) 권한으로 처리. ubuntu 는 `/var/log/` write 권한 없음.

**해결**: 전체를 root shell 안에서:
```bash
sudo bash -c '/path/to/script.sh >> /var/log/some.log 2>&1'
```

> cron 의 root crontab 항목은 root 가 직접 실행하므로 redirect 도 root 권한 — 영향 없음. **수동 검증 시에만** 위 패턴 사용.

### 5.5 nginx config 회귀 — reload 한 conf 가 다음 재시작 시 사라짐

**증상**: SSL cipher / stream block 등 새 설정이 nginx 메모리에는 적용됐는데, 컨테이너 재시작 시 옛 설정으로 돌아감.

**원인**: nginx 컨테이너가 마운트하는 conf 파일이 워킹트리에 있는데, 그 워킹트리가 옛 commit 으로 돌아가 있음 (예: 운영자가 git switch 로 다른 브랜치 잠시 갔다가 안 돌아옴).

**진단**:
```bash
git log --oneline -1           # 워킹트리 HEAD
grep <expected-config-line> server/nginx/conf.d/default.conf   # 기대 설정 라인 매치
```

**해결**: 워킹트리 sync (5.1 절차) → `nginx -t` → `nginx -s reload`. stream block 같은 master 변경 시 `up -d --force-recreate nginx`.

### 5.6 server-app:N 태그 보존 검증 (deploy.sh history rollback 자산)

**위험**: `docker image prune -a` 또는 `docker system prune -a` 는 태그 붙은 이미지도 제거 → rollback 이력 손실.

**진단**:
```bash
docker images | grep server-app | wc -l    # 보존된 N 개수
./scripts/deploy.sh history                # 같은 정보를 보기 좋게
```

**원칙**:
- **자동화는 dangling 만**: `docker image prune -f` (`-a` 아님)
- **태그 정리는 수동 + 백업 후**: `docker image rm server-app:<N>` 한 번에 한 개씩

### 5.7 데이터 볼륨 보호 검증

**위험**: `docker volume prune` 또는 `docker system prune --volumes` 는 사용 중이지 않은 볼륨까지 삭제.

**진단**:
```bash
docker volume ls | grep -E 'postgres_data|redis_data|certbot_'
# → server_postgres_data / server_redis_data / server_certbot_etc / server_certbot_webroot 4개 모두 보여야 함
```

**원칙**: **자동화에서 volume prune 절대 금지**. volume 정리가 필요한 경우는 운영자가 백업 (pg_dump / redis BGSAVE) 후 SSH 직접.

---

## 6. 외부 의존 (운영 책임자 권한 필요)

### AWS (싸피 콘솔)
- EC2 인스턴스 자체 (rebooting / resize)
- Security Group 인바운드 룰 (TCP 80 / 443 / UDP 7777 / SSH 22 등)
- Elastic IP (DNS A 레코드 대상)

### DNS (싸피 또는 운영 도메인 책임자)
- `k14c201.p.ssafy.io` A 레코드 → EC2 IP
- 변경 시 Let's Encrypt 갱신 가능 여부 사전 검증 필요

### GitLab (`lab.ssafy.com/s14-final/S14P31C201`)
- MR 머지 권한
- Personal Access Token 발급
- Project Deploy Keys (현재 미사용, 필요 시 SSH 인증)

### Mattermost (`meeting.ssafy.com`)
- Jenkins 알림 incoming webhook URL
- 채널 변경 시 webhook URL 재발급 + `lostmemory-env` 갱신

### Let's Encrypt
- 만료 알림 메일 (`LETSENCRYPT_EMAIL` 의 운영자 메일)
- prod 발급 rate limit: 도메인당 주 5회

---

## 7. 인계 책임 매트릭스

| 자산 / 절차 | 현재 책임자 | 인계 시 인계 받는 사람 |
|---|---|---|
| EC2 SSH key | (운영자 본인) | 새 운영자 |
| Jenkins admin 계정 | (운영자 본인) | 새 운영자 |
| GitLab admin / token | (운영자 본인) | 새 운영자 |
| Mattermost webhook | (운영자 본인) | 새 운영자 |
| AWS 콘솔 접근 | 사무국 | 사무국 + 새 운영자 |
| DNS 레코드 변경 | 사무국 | 사무국 |
| 도메인 만료 시 갱신 | 사무국 | 사무국 |

---

## 8. 인계 직전 체크리스트

운영자가 인계할 때 새 운영자에게 다음을 직접 시연 / 전달:

- [ ] EC2 SSH 접속 (개인 PC → EC2) 1회 시연
- [ ] `git pull --ff-only origin develop` 1회 시연
- [ ] `./scripts/deploy.sh status` 1회 실행 (현재 정상 상태 보여주기)
- [ ] `./scripts/deploy.sh history` 1회 실행 (보존된 rollback 태그 설명)
- [ ] Jenkins UI 로그인 1회 시연 + admin 계정 인계
- [ ] `lostmemory-env` credential 다운로드 1회 시연 (Secret file 위치 알려주기)
- [ ] Mattermost 알림 채널 1회 (직전 빌드 알림이 도착했는지 확인)
- [ ] HTTPS 만료일 확인 (`openssl s_client -connect ... | openssl x509 -noout -dates`)
- [ ] AWS 콘솔 접근권한 사무국에 문의 (`<새 운영자 ID>` 추가 요청)
- [ ] 본 문서 (`server/docs/04_operations_handover.md`) 위치 알려주기 + 트러블슈팅 절 한 번 같이 읽기

---

## 9. 본 문서 갱신 정책

- **본 문서는 운영 절차의 진입점** 입니다. 새로 발견된 트러블슈팅 패턴은 `5. 트러블슈팅` 절에 추가 commit 으로 누적합니다.
- 절차 자체 (배포 / cron / HTTPS) 의 상세는 `server/README.md` 가 source of truth — 본 문서는 링크만 유지하고 절차를 중복 기술하지 않습니다 (drift 방지).
- 메모리 룰 / git 컨벤션 변경 시 본 문서의 트러블슈팅 절도 갱신 (예: SSH key 인증 도입 시 5.3 절 갱신).
