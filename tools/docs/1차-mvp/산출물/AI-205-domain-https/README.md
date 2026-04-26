# AI-205 도메인 및 HTTPS 적용 산출물

## 목적

이 폴더는 `AI-205 도메인 및 HTTPS 적용` 작업의 운영 절차와 검증 기준을 둔다.

`AI-204`에서 로컬 프록시 기준 ComfyUI UI, WebSocket, 생성 smoke test가 통과했으므로, 이번 작업은 실제 도메인과 HTTPS 접속 경로를 준비하는 데 집중한다.

## 포함 문서

```text
AI-205-domain-https/
  README.md
  dns-and-port-checklist.md
  letsencrypt-nginx-runbook.md
  renewal-check-result.md
  mr-description.md
```

## 작업 코드와 문서 매핑

| 작업 코드 | 내용 | 문서 |
| --- | --- | --- |
| AI-205-01 | AI 도구용 도메인 또는 서브도메인 생성 | `dns-and-port-checklist.md` |
| AI-205-02 | 80, 443 포트 외부 접근 가능 상태 확인 | `dns-and-port-checklist.md` |
| AI-205-03 | Let's Encrypt 인증서 발급 및 Nginx SSL 설정 | `letsencrypt-nginx-runbook.md` |
| AI-205-04 | HTTP -> HTTPS 리다이렉트와 인증서 자동 갱신 설정 | `letsencrypt-nginx-runbook.md` |
| AI-205-05 | certbot dry-run 및 자동 갱신 검증 | `renewal-check-result.md` |

## 적용 파일

- `tools/infra/docker-compose.yml`
- `tools/infra/.env.example`
- `tools/infra/nginx/templates/ai-tool.conf.template`
- `tools/infra/nginx/README.md`
- `tools/infra/nginx/templates/README.md`

## 완료 기준

- `PUBLIC_DOMAIN`과 `COMFYUI_DOMAIN` DNS가 프록시 서버 public IP를 가리킨다.
- 외부에서 80, 443 포트 접속이 가능하다.
- `https://${PUBLIC_DOMAIN}/nginx-health`가 `ok`를 반환한다.
- `https://${COMFYUI_DOMAIN}/nginx-health`가 `ok`를 반환한다.
- `https://${COMFYUI_DOMAIN}`에서 ComfyUI UI가 로드된다.
- `/ws` WebSocket이 HTTPS 프록시 뒤에서 `101 Switching Protocols`로 열린다.
- `certbot renew --dry-run`이 성공한다.

## 현재 판정

2026-04-27 기준 실제 구매 도메인은 아직 없다. 따라서 AI-205의 코드와 운영 절차는 준비했지만, 실제 DNS 연결, Let's Encrypt 인증서 발급, `certbot renew --dry-run` 검증은 도메인 구매 후 수행한다.

현재 완료한 범위:

- Nginx 80 -> 443 redirect 설정 초안
- `/.well-known/acme-challenge/` webroot 경로
- HTTPS 443 server block
- certbot compose service와 인증서 volume
- DNS, 포트, 인증서 발급, 갱신 검증 runbook
- MR 설명 초안

현재 보류한 범위:

- 실제 DNS A 레코드 생성
- 실제 도메인 기준 80, 443 외부 접근 확인
- Let's Encrypt 실제 인증서 발급
- `certbot renew --dry-run` 검증

AI-204에서 내부망/로컬 프록시 기준 ComfyUI UI, WebSocket, 생성 smoke test가 통과했으므로, 도메인 구매 전에는 이 상태를 AI-206 Basic Auth 작업의 선행 조건으로 사용한다.
