# Mattermost 머지 알림 CI 설정 가이드

## 개요
develop 브랜치에 커밋이 머지될 때마다 팀 Mattermost 채널로 알림이 발송됩니다.
이 동작은 `.gitlab-ci.yml`의 `notify_develop_merge` job이 담당합니다.

## 최초 셋업 (레포 관리자 1회만 수행)

### 1. Mattermost Incoming Webhook 발급
대상 채널 → 채널 설정 → Integrations → Incoming Webhooks → Add
발급된 URL을 복사.

### 2. GitLab CI/CD 변수 등록
GitLab 프로젝트 → Settings → CI/CD → Variables → Add variable

| 항목 | 값 |
| --- | --- |
| Key | `MATTERMOST_WEBHOOK_URL` |
| Value | (위에서 복사한 웹훅 URL) |
| Type | Variable |
| Protected | develop이 Protected branch면 체크, 아니면 해제 |
| Masked | 체크 |
| Expand | 해제 |

### 3. 동작 확인
develop에 테스트용 MR을 머지하여 Mattermost 채널에 메시지가 도착하는지 확인.

## 트리거 조건
- `$CI_COMMIT_BRANCH == "develop"` : develop 브랜치 변경에만 반응
- `$CI_PIPELINE_SOURCE == "push"` : MR 이벤트 파이프라인 제외, 실제 push/merge 시에만 실행

## 트러블슈팅
- **알림이 안 옴** → CI/CD 변수 `MATTERMOST_WEBHOOK_URL`이 등록됐는지 확인. Protected 설정과 브랜치의 Protected 상태가 일치하는지 확인.
- **Job 자체가 안 돌아감** → 프로젝트에 연결된 GitLab Runner가 있는지(Settings → CI/CD → Runners), 그리고 Runner가 `meeting.ssafy.com`으로 아웃바운드 가능한지 확인.
- **메시지 포맷이 깨짐** → `.gitlab-ci.yml`의 jq payload 조립부를 확인. 특수문자 포함 커밋 메시지도 jq가 처리함.