# AI-205 갱신 검증 기록

## certbot dry-run

아직 실제 도메인과 EC2 공개 포트가 확정되지 않았으므로, 저장소 작업 시점에는 dry-run을 실행하지 않는다.

실제 배포 서버에서 아래 명령으로 검증한다.

```bash
docker compose run --rm certbot renew --dry-run --webroot --webroot-path /var/www/certbot
```

## 2026-04-27 현재 상태

| 항목 | 값 |
| --- | --- |
| 실행 일시 | 2026-04-27 |
| 실행 서버 | 로컬 개발 PC |
| PUBLIC_DOMAIN | 미정 |
| COMFYUI_DOMAIN | 미정 |
| 결과 | 실제 dry-run 미실행 |
| 사유 | 구매 도메인이 없어 Let's Encrypt HTTP-01 challenge를 수행할 수 없음 |
| 후속 처리 | 도메인 구매 후 DNS A 레코드와 80 포트 접근 확인을 먼저 수행한 뒤 dry-run 실행 |

## 기록 양식

| 항목 | 값 |
| --- | --- |
| 실행 일시 | |
| 실행 서버 | |
| PUBLIC_DOMAIN | |
| COMFYUI_DOMAIN | |
| 결과 | |
| 특이사항 | |

## 완료 기준

- dry-run이 실패 없이 종료된다.
- 실패 시 DNS, 80 포트, challenge webroot 접근성 순서로 재점검한다.
- dry-run 성공 후 실제 갱신 명령과 Nginx reload 절차를 cron 또는 systemd timer에 등록한다.
