# AI-203-01 공개 경로 전략 선택

## 결정

ComfyUI 공개 경로는 path 방식이 아니라 subdomain 방식으로 시작한다.

선택한 후보:

```text
comfy.<구매한-도메인>
```

예시는 아래와 같다.

```text
comfy.example.com
```

실제 도메인 이름은 아직 확정하지 않는다. 도메인을 구매한 뒤 `tools/infra/.env`의 `PUBLIC_DOMAIN`, `COMFYUI_DOMAIN` 값으로 반영한다.

## 비교

### 후보 A. subdomain 방식

예시:

```text
https://comfy.example.com
```

장점:

- ComfyUI를 루트 경로(`/`)로 그대로 프록시할 수 있다.
- 정적 파일, API, WebSocket `/ws` 경로 rewrite가 단순하다.
- 나중에 Basic Auth를 ComfyUI subdomain에만 붙이기 쉽다.
- AI 도구 API와 ComfyUI 원본 UI의 책임이 분리된다.

단점:

- DNS에서 subdomain 레코드를 하나 더 만들어야 한다.
- HTTPS 인증서 발급 시 subdomain도 포함해야 한다.

### 후보 B. path 방식

예시:

```text
https://example.com/comfy
```

장점:

- 도메인을 하나만 써도 된다.
- 브라우저에서 주소 구조가 한 도메인 아래로 모인다.

단점:

- ComfyUI가 참조하는 정적 파일 경로와 WebSocket 경로를 rewrite해야 할 수 있다.
- `/ws`, `/api`, asset 경로가 기존 서비스 경로와 충돌할 수 있다.
- 프록시 설정이 복잡해지고 디버깅 난도가 올라간다.

## 최종 선택 이유

`AI-203` 기준으로는 빠르게 안정적인 프록시 구조를 만드는 것이 우선이다.

ComfyUI는 원본 UI를 크게 수정하지 않고 사용해야 하므로, path rewrite가 필요한 방식보다 subdomain 방식이 안전하다.

## 관련 후속 작업

- `AI-205`: 실제 도메인 구매 후 DNS 레코드 생성
- `AI-205`: HTTPS 인증서 발급
- `AI-206`: ComfyUI subdomain 또는 전체 도구 도메인에 Basic Auth 적용
