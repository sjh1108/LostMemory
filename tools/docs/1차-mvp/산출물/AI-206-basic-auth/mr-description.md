## 관련 이슈

Related Issue: [S14P31C201-313](https://ssafy.atlassian.net/browse/S14P31C201-313)

## 작업 목적

내부 AI 도구 도메인 앞단에 Nginx Basic Auth를 적용해 ComfyUI UI와 AI 도구 API 접근을 1차로 제한합니다.

실제 구매 도메인과 인증서는 아직 없으므로, 이번 MR에서는 설정과 운영 절차를 반영하고 실제 브라우저 검증은 도메인/인증서 준비 후 진행 대상으로 남깁니다.

## 주요 변경 사항

- `tools/infra/nginx/templates/ai-tool.conf.template`에 Basic Auth 설정 추가
  - `COMFYUI_DOMAIN` 전체 보호
  - `PUBLIC_DOMAIN`의 `/api/` 보호
  - `/nginx-health`, `/.well-known/acme-challenge/` 예외 유지
- `tools/infra/docker-compose.yml`에 `.htpasswd` read-only mount 추가
- `tools/infra/.env.example`에 `BASIC_AUTH_REALM` 추가
- `.gitignore`에 실제 `.htpasswd` 제외 규칙 추가
- `tools/infra/nginx/auth/README.md`에 계정 생성, 전달, 교체 기준 추가
- AI-206 산출물 폴더와 진행 문서 갱신

## 테스트 여부

- [x] 설정 파일 정적 검토
- [x] compose 설정 렌더링 확인
- [ ] Nginx 컨테이너 기동 확인
- [ ] 실제 도메인 브라우저 Basic Auth 확인

## 검증 결과

로컬에서 아래 명령으로 compose 설정이 정상 렌더링되는 것을 확인했다.

```bash
docker compose --env-file .env.example config
```

Docker config 경고가 로컬 사용자 Docker 설정 파일 권한 때문에 출력되었지만, compose YAML과 서비스 구성은 정상 렌더링되었다.

## 보류 사유

Basic Auth 자체는 Nginx template에 반영했지만 실제 HTTPS server block은 Let's Encrypt 인증서 파일을 필요로 합니다. 현재 구매 도메인과 인증서가 없어 아래 항목은 완료할 수 없습니다.

- 실제 도메인 기준 Basic Auth 팝업 확인
- 인증 실패 시 401 응답 확인
- 인증 성공 후 ComfyUI UI와 `/api/` proxy 동작 확인

## 리뷰 시 중점 확인 사항

- `COMFYUI_DOMAIN` 전체와 `PUBLIC_DOMAIN /api/`만 보호하는 범위가 MVP에 적절한지
- `/nginx-health`와 ACME challenge를 Basic Auth 예외로 둔 것이 운영 요구에 맞는지
- `.htpasswd`를 Git 제외하고 운영 환경에서만 생성하는 방식이 충분한지
