## 관련 이슈

Related Issue: [S14P31C201-304](https://ssafy.atlassian.net/browse/S14P31C201-304)

## 작업 목적

내부 공유용 ComfyUI 접근 경로에 추후 실제 도메인과 HTTPS를 적용할 수 있도록 Nginx, certbot, 운영 절차를 준비합니다.

현재 실제 구매 도메인은 아직 없으므로, 이번 MR에서는 HTTPS 적용 준비와 검증 runbook을 반영하고 실제 DNS/Let's Encrypt 검증은 도메인 구매 후 진행 대상으로 남깁니다.

## 주요 변경 사항

- `tools/infra/docker-compose.yml`에 certbot service와 인증서/webroot volume 추가
- `tools/infra/nginx/templates/ai-tool.conf.template`에 HTTP -> HTTPS redirect, ACME challenge, HTTPS 443 server block 추가
- `tools/infra/.env.example`에 `LETSENCRYPT_EMAIL` 예시 추가
- AI-205 산출물 폴더 추가
  - DNS와 80/443 포트 체크리스트
  - Let's Encrypt 발급 및 Nginx 실행 runbook
  - certbot dry-run 검증 기록 양식
- 1차 MVP README에 AI-204, AI-205 산출물 링크 추가

## 테스트 여부

- [x] compose 설정 렌더링 확인
- [ ] 유닛 테스트 작성
- [ ] 통합 테스트 확인

## 검증 결과

로컬에서 아래 명령으로 compose 설정이 정상 렌더링되는 것을 확인했다.

```bash
docker compose --profile certbot config
```

Docker config 경고가 로컬 사용자 Docker 설정 파일 권한 때문에 출력되었지만, compose YAML과 서비스 구성은 정상 렌더링되었다.

## 보류 사유

Let's Encrypt HTTP-01 인증은 실제 공개 도메인이 프록시 서버 public IP를 가리켜야 한다. 현재는 구매 도메인이 없어 다음 항목은 완료할 수 없다.

- AI-205-01 실제 DNS 레코드 생성
- AI-205-02 실제 도메인 기준 80, 443 외부 접근 확인
- AI-205-03 실제 Let's Encrypt 인증서 발급
- AI-205-05 certbot dry-run 검증

AI-204에서 내부망/로컬 프록시 기준 ComfyUI UI, WebSocket, 생성 smoke test가 통과했으므로, 도메인 구매 전에는 이 상태를 기준으로 AI-206 Basic Auth 작업을 진행한다.

## 리뷰 시 중점 확인 사항

- 실제 도메인 구매 후 사용할 runbook 순서가 충분한지
- Nginx HTTPS server block과 WebSocket proxy header가 ComfyUI UI에 적합한지
- 도메인 부재로 실검증 대기 상태를 AI-206 선행 조건으로 인정해도 되는지
