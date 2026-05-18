# Basic Auth 계정 파일

이 폴더는 Nginx Basic Auth용 `htpasswd` 파일을 두는 자리다.

실제 계정 파일은 아래 경로에 만든다.

```text
tools/infra/nginx/auth/.htpasswd
```

`.htpasswd`에는 비밀번호 해시가 들어가므로 Git에 올리지 않는다.

## 생성 예시

Docker가 설치된 환경에서는 아래처럼 생성할 수 있다.

```bash
docker run --rm httpd:2.4-alpine htpasswd -nbB ai-admin 'change-this-password' > tools/infra/nginx/auth/.htpasswd
```

Windows PowerShell에서는 리다이렉션 인코딩을 피하려면 아래처럼 저장한다.

```powershell
docker run --rm httpd:2.4-alpine htpasswd -nbB ai-admin 'change-this-password' | Set-Content -NoNewline -Encoding ascii tools/infra/nginx/auth/.htpasswd
```

## 운영 기준

- MVP 초기 계정은 팀 내부 공유용 1개 계정으로 시작한다.
- 비밀번호는 메신저 공개 채널이나 Git에 남기지 않는다.
- 담당자가 바뀌거나 외부 공유 가능성이 생기면 `.htpasswd`를 다시 생성하고 Nginx를 재시작한다.
- `/nginx-health`와 `/.well-known/acme-challenge/`는 Basic Auth 예외다.
- `COMFYUI_DOMAIN` 전체와 `PUBLIC_DOMAIN`의 `/api/`는 Basic Auth 보호 대상이다.
