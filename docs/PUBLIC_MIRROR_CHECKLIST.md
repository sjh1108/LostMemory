# 공개 mirror 발행 전 필수 체크리스트

SSAFY 내부 GitLab(`lab.ssafy.com/s14-final/S14P31C201`)을 외부 공개 mirror(GitHub, 포트폴리오 등)로 발행하기 전에 **반드시** 통과해야 하는 보안 위생 절차입니다. 어떤 항목이라도 빠지면 mirror 진행을 보류하세요.

> ⚠️ 이 문서는 mirror 작업자(또는 자동화 도구)가 시작 직전 한 번 더 확인하는 용도입니다. 정기 push와 무관합니다.

---

## 체크리스트

### 1. 인프라 IP / 도메인 / 호스트명 history 마스킹 (최우선)

`tasks/`와 `tools/docs/1차-mvp/산출물/` 등에 운영 IP와 도메인이 commit history에 남아 있습니다. mirror 직전 `git filter-repo --replace-text`로 일괄 마스킹합니다.

마스킹 대상 카테고리:

| 카테고리 | placeholder 예시 |
|---|---|
| EC2 public IP (서울 리전) | `***EC2_PUBLIC_IP***` |
| Tailscale tailnet IP (운영 PC) | `***TAILNET_HOST***` |
| Tailscale tailnet IP (EC2) | `***TAILNET_EC2***` |
| 내부 GPU 데스크탑 사설 IP | `***INTERNAL_GPU***` |
| 공개 DDNS 도메인 | `***DOMAIN***` |
| EC2 hostname (`ip-172-31-…`) | `***EC2_HOSTNAME***` |
| Tailscale 사용자 식별자 | `***TAILSCALE_USER***` |
| 운영 PC hostname (`desktop-…`) | `***WORKSTATION_HOSTNAME***` |

> 실제 값은 본 문서에 적지 않습니다 (적는 순간 마스킹 대상이 됩니다). 운영자가 자체 보관 중인 운영 노트 또는 `.env`를 참조하세요.

### 2. git author 메타 처리 결정

`git log`에 commit author 이메일/실명이 영구 기록되어 있습니다. mirror 전에 다음 중 선택:

- **유지** — 모든 author 본인에게 외부 공개 동의를 받습니다
- **마스킹** — `git filter-repo --mailmap mailmap.txt`로 일괄 치환

### 3. `tasks/`, `.claude/` 폴더 확인

- `tasks/` 폴더는 더 이상 추적 대상 아닙니다 (세션 메모는 `.claude/tasks/`로 이전됨, .gitignore 적용)
- `.claude/`는 이미 ignore 되어 mirror에 들어가지 않습니다
- 신규 작업으로 `tasks/`가 다시 생겼다면 동일 정책 적용

### 4. 실제 시크릿 파일 부재 재확인

다음 명령으로 mirror 직전 final check:

```bash
# 추적되는 secret 의심 파일 검색
git ls-files | grep -E '\.htpasswd$|\.pem$|\.key$|id_rsa|\.env$' | grep -v '\.example$'

# 본문에서 token-like 패턴
git grep -E 'AKIA[0-9A-Z]{16}|password\s*[:=]|secret\s*[:=]|eyJ[A-Za-z0-9_-]{20,}'
```

기대 결과: **0건**. 잡히는 게 있으면 마스킹 또는 untrack 결정.

### 5. mirror 절차 (실행 순서 고정)

```bash
# (1) 백업 — 실수 복구용
git clone --mirror https://lab.ssafy.com/s14-final/S14P31C201.git backup-pre-filter.git

# (2) 작업용 격리 mirror clone
git clone --mirror https://lab.ssafy.com/s14-final/S14P31C201.git filter-work.git
cd filter-work.git

# (3) 인프라 패턴 마스킹
git filter-repo --replace-text /path/to/replacements.txt --force

# (선택) author 마스킹 동시 적용
# git filter-repo --mailmap /path/to/mailmap.txt --force

# (4) 검증 — 마스킹 대상이 0건인지
git log -p --all | grep -E '<운영자가 보관 중인 정규식 패턴>' | head

# (5) 공개 remote로 push
git remote set-url origin <PUBLIC_MIRROR_URL>
git push --force --mirror
```

---

## 절대 하지 말 것

- ❌ 원본 `lab.ssafy.com` develop/master에 직접 force-push (팀원 전원 영향)
- ❌ 마스킹 검증(체크리스트 4번) 안 한 채 mirror push
- ❌ 사전 백업 없이 진행
- ❌ 본 문서 또는 commit message에 실제 IP/도메인 평문 기재

---

## 관련 자료

- 본 체크리스트는 SSAFY 내부 운영 노트의 mirror용 발췌입니다
- 실행 명령 세부와 마스킹 대상 실제 값은 운영자(`docs/AGENTS.md` 또는 인계 문서) 보관 자료 참조
