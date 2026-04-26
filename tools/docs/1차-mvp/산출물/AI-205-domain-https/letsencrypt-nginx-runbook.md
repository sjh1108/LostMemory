# AI-205 Let's Encrypt와 Nginx 실행 절차

## 1. 실제 `.env` 작성

`tools/infra/.env.example`을 참고해 `tools/infra/.env`를 만든다. `.env`는 비밀값이 들어갈 수 있으므로 Git에 올리지 않는다.

```env
PUBLIC_DOMAIN=<구매한 도메인>
COMFYUI_DOMAIN=comfy.<구매한 도메인>
LETSENCRYPT_EMAIL=<운영자 이메일>
AI_BACKEND_UPSTREAM=http://host.docker.internal:8080
COMFYUI_UPSTREAM=http://<EC2에서 접근 가능한 ComfyUI 주소>:8188
```

## 2. 최초 기동용 임시 인증서 생성

Nginx HTTPS server block은 인증서 파일이 없으면 시작되지 않는다. 최초 발급 전에는 같은 경로에 임시 self-signed 인증서를 만든 뒤, certbot 발급으로 교체한다.

EC2에서 `tools/infra`로 이동한 뒤 실행한다.

```bash
docker compose run --rm --entrypoint sh certbot -c '\
mkdir -p /etc/letsencrypt/live/${PUBLIC_DOMAIN} /etc/letsencrypt/live/${COMFYUI_DOMAIN} && \
openssl req -x509 -nodes -newkey rsa:2048 -days 1 \
  -keyout /etc/letsencrypt/live/${PUBLIC_DOMAIN}/privkey.pem \
  -out /etc/letsencrypt/live/${PUBLIC_DOMAIN}/fullchain.pem \
  -subj /CN=${PUBLIC_DOMAIN} && \
openssl req -x509 -nodes -newkey rsa:2048 -days 1 \
  -keyout /etc/letsencrypt/live/${COMFYUI_DOMAIN}/privkey.pem \
  -out /etc/letsencrypt/live/${COMFYUI_DOMAIN}/fullchain.pem \
  -subj /CN=${COMFYUI_DOMAIN}'
```

## 3. Nginx 시작

```bash
docker compose up -d nginx
docker compose exec nginx nginx -t
```

HTTP challenge path가 외부에서 열리는지 확인한다.

```bash
docker compose run --rm --entrypoint sh certbot -c 'mkdir -p /var/www/certbot/.well-known/acme-challenge && echo ok > /var/www/certbot/.well-known/acme-challenge/ping'
curl -i http://${PUBLIC_DOMAIN}/.well-known/acme-challenge/ping
curl -i http://${COMFYUI_DOMAIN}/.well-known/acme-challenge/ping
```

## 4. Let's Encrypt 인증서 발급

각 도메인별로 발급한다.

```bash
docker compose run --rm certbot certonly --webroot \
  --webroot-path /var/www/certbot \
  --email "${LETSENCRYPT_EMAIL}" \
  --agree-tos \
  --no-eff-email \
  -d "${PUBLIC_DOMAIN}"

docker compose run --rm certbot certonly --webroot \
  --webroot-path /var/www/certbot \
  --email "${LETSENCRYPT_EMAIL}" \
  --agree-tos \
  --no-eff-email \
  -d "${COMFYUI_DOMAIN}"
```

발급 후 Nginx를 reload한다.

```bash
docker compose exec nginx nginx -s reload
```

## 5. HTTPS 확인

```bash
curl -i https://${PUBLIC_DOMAIN}/nginx-health
curl -i https://${COMFYUI_DOMAIN}/nginx-health
curl -I http://${PUBLIC_DOMAIN}/
curl -I http://${COMFYUI_DOMAIN}/
```

완료 기준:

- HTTPS health check가 `200`과 `ok`를 반환한다.
- HTTP 요청은 `301`로 HTTPS 주소를 가리킨다.
- `https://${COMFYUI_DOMAIN}`에서 ComfyUI 화면이 열린다.

## 6. 갱신 명령

수동 갱신:

```bash
docker compose run --rm certbot renew --webroot --webroot-path /var/www/certbot
docker compose exec nginx nginx -s reload
```

운영에서는 cron 또는 systemd timer로 위 갱신 명령을 하루 1~2회 실행한다.
