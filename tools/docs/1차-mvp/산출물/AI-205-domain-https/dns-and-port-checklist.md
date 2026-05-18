# AI-205 DNS와 포트 체크리스트

## 사전 조건

- EC2 또는 공개 프록시 서버 public IP가 확정되어 있다.
- 가능하면 Elastic IP를 사용한다. EC2 public IP가 바뀌면 DNS를 다시 수정해야 한다.
- `tools/infra/.env`에 실제 값을 넣는다.

```env
PUBLIC_DOMAIN=<구매한 도메인>
COMFYUI_DOMAIN=comfy.<구매한 도메인>
LETSENCRYPT_EMAIL=<운영자 이메일>
```

## AI-205-01 DNS 레코드

- [ ] `PUBLIC_DOMAIN` A 레코드가 프록시 서버 public IP를 가리킨다.
- [ ] `COMFYUI_DOMAIN` A 레코드가 프록시 서버 public IP를 가리킨다.
- [ ] TTL은 초기 검증 중 300초 정도로 낮게 둔다.
- [ ] DNS 전파 후 아래 명령으로 실제 응답 IP를 확인한다.

```powershell
Resolve-DnsName <구매한 도메인> -Type A
Resolve-DnsName comfy.<구매한 도메인> -Type A
```

Linux 또는 WSL:

```bash
dig +short <구매한 도메인>
dig +short comfy.<구매한 도메인>
```

## AI-205-02 80, 443 포트

프록시 서버 보안 그룹 또는 방화벽에서 아래 인바운드를 허용한다.

| 포트 | 용도 | 허용 소스 |
| --- | --- | --- |
| TCP 80 | HTTP-01 challenge, HTTPS redirect | `0.0.0.0/0`, `::/0` |
| TCP 443 | 실제 HTTPS 접속 | `0.0.0.0/0`, `::/0` |

외부 PC에서 확인한다.

```powershell
Test-NetConnection <구매한 도메인> -Port 80
Test-NetConnection <구매한 도메인> -Port 443
Test-NetConnection comfy.<구매한 도메인> -Port 80
Test-NetConnection comfy.<구매한 도메인> -Port 443
```

완료 기준:

- DNS 응답 IP가 프록시 서버 public IP와 일치한다.
- `TcpTestSucceeded`가 80, 443 모두 `True`다.
