# SSAFY Co-op Roguelite

## 프로젝트 한 줄 소개

유물/보상 3개 중 1개를 선택하며 방을 돌파하고 보스에 도전하는 1~4인 온라인 협동 탑다운 로그라이트 액션 게임.

## 핵심 재미

1. 손맛 있는 근접/보조거리 전투
2. 빌드 선택에 따라 전투 스타일이 달라지는 성장감
3. 실력이 부족해도 협동으로 극복하는 재미

## 현재 MVP 방향

- 온라인 협동 지원
- 호스트 기반 진행
- 1개 챕터 우선 완성
- 전투의 정성적 완성도 우선
- 보스전/타격감/빌드 선택 재미 집중

## 문서 목록

- [게임 개요](docs/01_game_overview.md)
- [코어 루프](docs/02_core_loop.md)
- [전투 시스템](docs/03_combat_system.md)
- [멀티플레이 규칙](docs/04_multiplayer.md)
- [콘텐츠 범위](docs/05_content_scope.md)
- [메타 성장](docs/06_meta_progression.md)
- [백엔드 범위](docs/07_backend_scope.md)
- [MVP 계획](docs/08_mvp_plan.md)
- [아트 방향](docs/09_art_direction.md)
- [무기 설계](docs/10_weapon_design.md)
- [AI 제작 파이프라인](docs/11_ai_pipeline.md)
- [전체 개발 계획](docs/12_development_plan.md)
- [클라이언트 세부 계획](docs/13_client_detailed_plan.md)
- [클라이언트 지라 스토리 목록](docs/14_client_jira_story_backlog.md)
- [ComfyUI 작업 문서](tools/docs/README.md)

## 폴더 구조

- `client/`: 게임 클라이언트 프로젝트 폴더
- `server/`: 백엔드 및 서비스 API 프로젝트 폴더
- `tools/`: 개발 보조 도구, 스크립트, AI 파이프라인 관련 폴더
- `docs/`: 기획 및 설계 문서 폴더

## 팀 구성 기준

- 클라이언트 3명
- 서버 2명
- AI 1명

## 현재 우선순위

1. 재미있는 전투 1사이클 완성
2. 온라인 협동 최소 버전 연결
3. 보상 3택과 무기/유물 성장 루프 완성
4. 보스전 데모 완성

## Git LFS 사용 안내

이 프로젝트는 대용량 에셋 관리를 위해 **Git LFS**를 사용합니다.  
이미지, 사운드, 원본 작업 파일 등 바이너리 에셋은 Git LFS로 관리합니다.

### 최초 1회 세팅

각 팀원은 로컬 환경에서 아래 명령어를 **최초 1회** 실행해야 합니다.

```bash
git lfs install
