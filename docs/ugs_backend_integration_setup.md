# LostMemory — 통합 정보 (Integration Reference)

> 갱신: 2026-04-27
> 변경 시 MM 공지 필수
> 미입력된 정보는 Notion -> env 확인할 것

---

## UGS

- Project ID:           `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`
- Organization ID:      `xxxxxxxxxxxxxx`
- Env "development":    `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`
- Env "production":     `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`
> 실제 Project ID / Environment ID는 Notion env 페이지에서 관리. 본 문서엔 미기재.

- 사용 SDK 조합:        Sessions API (`com.unity.services.multiplayer`)
- 인증 연동 방식:       옵션 1 (분리) — 아래 "인증 연동 방식" 섹션 참고
- 활성화 서비스:        Player Authentication (Anonymous) / Lobby / Relay
                        (Sessions API는 내부적으로 Lobby + Relay를 사용하므로 둘 다 활성화)
- 비활성화:             Matchmaker, Multiplay Hosting, Custom ID Provider
                        (운영 단계에서 옵션 2 검토 시 Custom ID Provider 활성화 예정)

---

## 인증 연동 방식

이 게임에는 **두 종류의 신원**이 있다.

- **백엔드 신원** — 우리 서버 DB의 `user_id` (loginId 로그인 후 백엔드가 발급한 JWT의 `sub`)
- **UGS 신원** — UGS의 `playerId` (Relay/Sessions 호출 시 인증에 사용)

이 둘을 어떻게 묶을지에 두 가지 방식이 있다.

### 옵션 1 — 분리 (현재 채택)

- 두 신원을 별개로 관리. 클라가 토큰 두 개(우리 JWT + UGS Player Token)를 따로 들고 다님
- 백엔드 호출은 우리 JWT, UGS 호출은 UGS Player Token
- UGS는 Anonymous Authentication으로 토큰 발급
- 백엔드는 UGS와 직접 통신하지 않음 (UGS Server API 호출 코드 없음)

**장점:** 가장 단순. 백엔드 추가 작업 거의 없음.
**단점:** UGS playerId와 우리 user_id가 무관해서, 서버가 "이 UGS playerId가 우리 시스템의 어떤 유저인지" 자동으로 알 수 없음.

### 옵션 2 — Custom ID Provider (운영 단계 검토)

- 백엔드가 UGS의 Custom ID Provider 역할을 함
- 클라가 우리 백엔드에 "UGS 토큰 달라" 요청 → 백엔드가 우리 `user_id` 기반으로 UGS 토큰 발급
- 결과적으로 UGS playerId == 우리 user_id (1:1 매핑)

**장점:** 신원 일관성 보장. 부정행위 방지·서버 권한 행사 가능.
**단점:** 백엔드에 UGS Server API 호출 코드 필요. UGS Service Account Key 관리 필요.

### 채택 사유

- MVP 일정 최우선
- 협동 게임이라 PvP 부정행위 압력이 낮아 옵션 1의 신원 매칭 약점이 MVP 단계에선 허용 가능
- 옵션 1로 만든 코드는 옵션 2로 마이그레이션 시 거의 그대로 남고, `/api/auth/ugs-token` 엔드포인트 + 클라 측 토큰 획득 라인만 추가하면 됨

### 옵션 1 전제하 백엔드 영향

- 백엔드 `.env`에 UGS 관련 값(Project ID, Secret 등) 추가하지 않음
- 클라는 백엔드에서 UGS 토큰을 받을 필요 없음 (UGS SDK가 직접 발급)

---

## 백엔드

| 항목 | 값 |
|---|---|
| dev Base URL | `http://<dev PC IP>:8080` (LAN 테스트 시 백엔드 PC IP, 본인 PC면 `localhost`) |
| prod Base URL | **TBD** (배포 시점에 갱신) |
| Swagger UI | `http://<dev PC IP>:8080/swagger-ui/index.html` |
| 인증 헤더 | `Authorization: Bearer <accessToken>` |
| Content-Type | `application/json; charset=UTF-8` |
| JSON 케이스 | camelCase |
| 시간 포맷 | UTC ISO-8601 (예: `2026-04-27T13:00:00Z`) |

### 응답 스키마

```json
{
  "success": true,
  "data": { },
  "error": null
}
```

또는 에러 시:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "AUTH_INVALID_CREDENTIALS",
    "message": "아이디 또는 비밀번호가 올바르지 않습니다"
  }
}
```

---

## ErrorCode 표

### Common

| 코드 | HTTP | 설명 |
|---|---|---|
| `COMMON_INVALID_INPUT` | 400 | 요청 값 오류 |
| `COMMON_UNAUTHORIZED` | 401 | 인증 필요 |
| `COMMON_FORBIDDEN` | 403 | 권한 없음 |
| `COMMON_RESOURCE_NOT_FOUND` | 404 | 리소스 없음 |
| `COMMON_INTERNAL_ERROR` | 500 | 서버 오류 |

### 도메인 (구현 진행에 따라 추가 예정)

- `AUTH_*` — 회원가입 / 로그인 / 토큰 갱신 / 로그아웃 API 구현 시 추가
- `SESSION_*` — 세션 생성 / 참여 / 삭제 API 구현 시 추가
- `RUN_*` — 런 시작 / 종료 API 구현 시 추가

---

## 권한 구조 (참고)

| User Type | 멤버     |
|---------|--------|
| Owner   | `손홍민`  |
| Manager | 나머지 팀원 전원 |

---

## 변경 이력

| 날짜 | 변경 내용 | 작성자 |
|---|---|---|
| 2026-04-27 | 최초 작성 (옵션 1 + Sessions API 결정 반영) | (본인) |
| 2026-04-27 | UGS DoD 통과 확인 (계획 문서 16-A 참조) | 손홍민 |

---

## 링크

- UGS Dashboard: https://cloud.unity.com/home/products
- 계획 문서: `/server/docs/03_ugs_and_backend_integration_plan.md`
