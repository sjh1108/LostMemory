# Tools

개발 보조 도구, 스크립트, AI 파이프라인 관련 폴더.

- `ComfyUI/`: 로컬 이미지 생성 및 `img2img` 실험용 도구 루트
- `ai_server/`: AI 도구 전용 Spring Boot 백엔드 초안
- `infra/`: AI 도구 전용 Nginx, Postgres, Docker Compose 인프라 초안
- `docs/`: ComfyUI 및 AI 툴링 관련 운영 문서

## 운영 기준

`tools` 하위 AI 도구는 기존 프로젝트 루트의 `server/`, `client/`와 분리해서 관리한다.

- 기존 게임 서버 설정은 수정하지 않는다.
- AI 도구 백엔드, 프록시, DB, ComfyUI 연동 문서는 `tools` 아래에 둔다.
- 구매 예정 도메인과 운영 데스크탑 기준 인프라 초안은 `infra/`에서 관리한다.
- 실제 AI 도구 백엔드 구현 위치는 `tools/ai_server`를 기준으로 잡는다.
