# AI-206 도메인 앞단 Basic Auth 적용 산출물

## 목적

이 폴더는 `AI-206 도메인 앞단 Basic Auth 적용` 작업의 설정 기준과 운영 절차를 둔다.

`AI-205`에서 실제 도메인 구매 전 HTTPS 설정 초안까지 준비했으므로, 이번 작업은 Nginx 앞단에서 내부 AI 도구 접근을 1차로 제한하는 Basic Auth 기준을 고정한다.

## 포함 문서

```text
AI-206-basic-auth/
  README.md
  mr-description.md
```

## 작업 코드와 문서 매핑

| 작업 코드 | 내용 | 반영 위치 |
| --- | --- | --- |
| AI-206-01 | Basic Auth 계정 생성 | `tools/infra/nginx/auth/README.md` |
| AI-206-02 | Basic Auth 적용 대상 경로 확정 | 이 문서, `tools/infra/nginx/README.md` |
| AI-206-03 | Nginx Basic Auth 설정 적용 | `tools/infra/nginx/templates/ai-tool.conf.template` |
| AI-206-04 | 팀 공유용 계정 전달 및 교체 규칙 문서화 | `tools/infra/nginx/auth/README.md` |
| AI-206-05 | Basic Auth 예외 경로 기준 정리 | 이 문서, `tools/infra/nginx/README.md` |

## 적용 파일

- `tools/infra/docker-compose.yml`
- `tools/infra/.env.example`
- `tools/infra/README.md`
- `tools/infra/nginx/README.md`
- `tools/infra/nginx/auth/README.md`
- `tools/infra/nginx/templates/ai-tool.conf.template`
- `.gitignore`

## 보호 대상

- `https://${COMFYUI_DOMAIN}/` 전체
- `https://${PUBLIC_DOMAIN}/api/`

## 예외 대상

- `/.well-known/acme-challenge/`
- `/nginx-health`

ACME challenge는 Let's Encrypt HTTP-01 인증서 발급에 필요하므로 인증을 걸지 않는다. `/nginx-health`는 로드밸런서, 컨테이너, 운영자가 인증 없이 프록시 생존 여부를 확인할 수 있도록 예외로 둔다.

## 계정 파일 기준

실제 계정 파일은 아래 경로에 만든다.

```text
tools/infra/nginx/auth/.htpasswd
```

`.htpasswd`는 Git에 올리지 않는다. compose는 이 파일을 `/etc/nginx/auth/.htpasswd`로 읽기 전용 마운트한다.

## 현재 판정

2026-04-27 기준 실제 구매 도메인과 인증서가 아직 없으므로 브라우저에서 실제 도메인 기준 Basic Auth 팝업 검증은 보류한다.

이번 작업에서는 아래 범위를 완료했다.

- Basic Auth 보호 대상과 예외 경로 확정
- Nginx template에 `auth_basic`, `auth_basic_user_file` 반영
- compose에 `.htpasswd` read-only mount 반영
- `.htpasswd` Git 제외 규칙 반영
- 계정 생성, 전달, 교체 기준 문서화

실제 도메인과 인증서가 준비되면 `.htpasswd`를 생성한 뒤 Nginx를 재시작하고 `COMFYUI_DOMAIN`과 `PUBLIC_DOMAIN/api/`에서 인증 팝업과 인증 실패/성공 응답을 확인한다.
