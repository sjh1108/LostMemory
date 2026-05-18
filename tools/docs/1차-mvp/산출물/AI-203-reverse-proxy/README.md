# AI-203 Reverse Proxy 산출물

## 목적

이 폴더는 `AI-203 Reverse Proxy 기본 경로 설계`의 결정 문서와 체크리스트를 둔다.

이번 작업은 기존 프로젝트 루트의 `server/` 설정을 수정하지 않는다. AI 도구는 `S14P31C201/tools` 하위에서 별도 인프라 초안을 만든다.

## 포함 문서

```text
AI-203-reverse-proxy/
  README.md
  proxy-path-decision.md
  deployment-role-memo.md
  proxy-checklist.md
```

## 작업 코드와 문서 매핑

| 작업 코드 | 내용 | 문서 |
| --- | --- | --- |
| AI-203-01 | 공개 경로 전략 선택 | `proxy-path-decision.md` |
| AI-203-02 | 프록시 서버 위치와 역할 확정 | `deployment-role-memo.md` |
| AI-203-03 | Nginx 프록시 설정 초안 작성 | `tools/infra/nginx/`, `proxy-checklist.md` |
| AI-203-04 | WebSocket 업그레이드 처리 포함 여부 검증 | `tools/infra/nginx/templates/ai-tool.conf.template`, `proxy-checklist.md` |
| AI-203-05 | docker-compose와 실제 디렉터리 구조 맞춤 | `tools/infra/docker-compose.yml`, `tools/infra/README.md` |
| AI-203-06 | nginx 디렉터리와 conf.d 스켈레톤 생성 | `tools/infra/nginx/README.md` |
| AI-203-07 | Nginx 단독 smoke test 수행 | `proxy-checklist.md` |

## 완료 기준

`AI-203`의 완료 기준은 실제 이미지 생성 성공이 아니다.

완료 기준은 아래다.

- 구매 도메인 기준 공개 경로 전략이 정리됨
- 운영 데스크탑, GPU 데스크탑, S3, Postgres 역할이 정리됨
- `tools/infra` 아래 Nginx/compose 초안이 있음
- ComfyUI WebSocket 프록시 헤더가 초안에 포함됨
- Nginx 단독 smoke test 절차가 문서화됨

실제 ComfyUI HTTP/WebSocket 연결과 브라우저 UI 확인은 `AI-204`에서 한다.
