# UGS 셋업 및 백엔드-Unity 연동 작업 계획

- 문서 목적: Unity Services(UGS) 프로젝트 생성부터 백엔드-Unity 연동 API 구현까지의 작업 순서를 단계별로 정리한다.
- 적용 범위: MVP 기준
- 작성 시점: 2026-04-27
- 주체 표기: **[합의]** 클라팀과 결정 / **[BE]** 백엔드 직접 / **[클라]** 클라팀이 처리 / **[공유]** 산출물 공유

---

# 결정 사항 요약 (2026-04-27)

| 항목 | 결정 |
|---|---|
| 인증 연동 방식 | **옵션 1 — 분리** (MVP. 옵션 2는 운영 안정화 시 마이그레이션) |
| UGS SDK 조합 | **Sessions API** (`com.unity.services.multiplayer`) |
| 통신 규약 | **dev 세팅만 사용**. prod 세팅은 배포 시점에 확정 |

---

# 0단계 — 사전 합의

## 0-1. 인증 연동 방식 (옵션 1 vs 2)

이 게임에는 **두 종류의 신원**이 있다.

- **백엔드 신원** — 우리 DB의 `user_id` (loginId 로그인 후 발급한 JWT의 `sub`)
- **UGS 신원** — UGS의 `playerId` (Relay/Sessions 호출 시 인증용)

이 둘을 어떻게 묶을지가 옵션 1 vs 2의 핵심.

### 옵션 1 — 분리 (MVP 권장)
**구조:** 두 신원을 별개로 둠. 클라가 토큰 두 개를 따로 들고 다님.

**클라 흐름:**
1. 우리 백엔드에 로그인 → 우리 JWT (access/refresh) 수령
2. UGS SDK의 `AuthenticationService.SignInAnonymouslyAsync()` 호출 → UGS Player Token 수령
3. 백엔드 호출할 땐 우리 JWT, UGS 호출할 땐 UGS Player Token

**백엔드 추가 작업:** 없음. 우리는 UGS를 모름.

**`sessions` 테이블 동기화 방식:**
- 클라 호스트가 UGS Session 생성 (UGS Session ID 받음) → `POST /api/sessions`로 백엔드에도 row 생성 (필요 시 UGS Session ID를 컬럼에 저장)
- 백엔드는 그 row를 가지고만 있고, 실제 P2P 통제는 UGS에 위임

**장점:** 가장 단순. 백엔드 일거리 0. MVP 일정 안에 끝남.
**단점:** UGS playerId가 우리 user_id와 무관해서, 클라가 거짓말하면 백엔드가 잡기 어려움. (단, 우리 JWT로 호출하면 JWT의 `sub`로 userId가 강제되니, 클라가 자기 user_id를 임의로 바꿀 수는 없음. 다만 다른 UGS playerId를 가진 친구가 우리 시스템에서 누구인지 매칭이 어려울 수 있음)
**적합:** 협동 게임이라 PvP 부정행위 압력 낮음 → MVP에 충분.

### 옵션 2 — Custom ID Provider (정합성)
**구조:** 백엔드가 UGS의 "Custom ID Provider" 역할을 해서, 우리 user_id로 UGS 토큰을 직접 발급. UGS playerId와 user_id가 1:1.

**클라 흐름:**
1. 우리 백엔드에 로그인 → 우리 JWT 수령
2. 클라가 우리 백엔드에 `POST /api/auth/ugs-token` 같은 엔드포인트 호출
3. 백엔드가 UGS Server API(`POST /v1/token/exchange` 등)로 우리 user_id 기반 UGS 토큰 발급해서 클라에 전달
4. 클라는 그 토큰으로 UGS 호출 — UGS playerId가 우리 user_id 기반으로 일관됨

**백엔드 추가 작업:**
- UGS Service Account Key 발급/보관 (시크릿 관리)
- UGS Server API 호출 클라이언트 (HTTP 클라 + 캐싱)
- `/api/auth/ugs-token` 엔드포인트
- (선택) UGS 측 세션 강제 종료/멤버 강퇴 API 연동

**장점:** 신원 일관성. 부정 사용자 차단 등 서버 권한 행사 가능.
**단점:** 백엔드 작업량 증가, Service Account Key 관리 정책 필요.
**적합:** 운영 단계, 부정행위가 실제 문제가 되기 시작할 때.

### 결정 (2026-04-27)
- **MVP는 옵션 1 채택.** 운영 안정화 시 옵션 2로 마이그레이션 검토.
- 옵션 1 코드는 옵션 2로 갈 때 거의 그대로 남고, `/api/auth/ugs-token` 엔드포인트 + 클라 측 토큰 획득 라인만 추가됨.
- 따라서 본 문서의 4~8단계는 **모두 옵션 1 전제**로 작성됨.

## 0-2. UGS SDK 조합

### Sessions API (신, 권장)
- Unity가 2024년 발표한 통합 멀티플레이어 API.
- `com.unity.services.multiplayer` 패키지. `MultiplayerService.Instance.CreateSessionAsync(...)` 한 번 호출로 매칭룸 생성+P2P 연결 동시 처리.
- 내부적으로 Lobby + Relay를 한꺼번에 다룸.

### Lobby + Relay (구, 분리)
- `com.unity.services.lobby`로 룸 정보 관리, `com.unity.services.relay`로 P2P 연결 따로.
- 자료/예제가 더 많음. 기존 Unity 멀티 튜토리얼은 거의 이 조합.

### 결정 (2026-04-27)
- **Sessions API 채택.** (`com.unity.services.multiplayer`)
- Lobby+Relay 분리형은 채택하지 않음. 본 문서의 1단계(대시보드 셋업)와 3단계(클라 SDK 설치) 모두 Sessions API 기준으로 진행.

## 0-3. 통신 규약

| 항목 | 값 |
|---|---|
| dev Base URL | `http://localhost:8080` (개인 PC) 또는 `http://<백엔드 PC IP>:8080` (LAN 테스트) |
| prod Base URL | **현 단계 미정.** 배포 시점에 본 표를 갱신 |
| 인증 헤더 | `Authorization: Bearer <accessToken>` |
| Content-Type | `application/json; charset=UTF-8` |
| JSON 케이스 | camelCase (Spring 기본) |
| 시간 포맷 | UTC ISO-8601 (예: `2026-04-27T13:00:00Z`) |
| 응답 스키마 | `{ "success": bool, "data": {...} \| null, "error": { "code": "AUTH_xxx", "message": "..." } \| null }` |

---

# 1단계 — UGS 대시보드 셋업

## 4. cloud.unity.com 가입 + Organization 확인
- https://cloud.unity.com 접속, Unity ID 로그인 (없으면 가입)
- 우측 상단에서 Organization 확인. 개인 계정이면 본인 이름의 org가 자동 생성돼 있음
- 팀 공용 org가 따로 있으면 거기로 전환 (없으면 개인 org로 시작 후 나중에 transfer 가능)

## 5. 프로젝트 생성
- 좌측 **Projects** → **Create Project**
- Project Name: `LostMemory` (또는 합의된 이름)
- 생성 후 **Project ID**(UUID) 화면에 표시됨 → 메모

## 6. Environment 분리
- 좌측 메뉴 **Environments**
- 기본 `production` 1개 존재
- **Add Environment** → 이름 `development`
- 두 환경의 **Environment ID**(UUID) 각각 메모

## 7. Relay 활성화
- 좌측 **Multiplayer > Relay**
- **Get Started** 또는 **Enable** 버튼
- 무료 티어 한도 안내 페이지 확인 (월 50 CCU, 트래픽 한도 등)

## 8. Sessions(또는 Lobby) 활성화
- **Multiplayer > Multiplayer Services > Sessions** (신택했다면)
- 또는 **Multiplayer > Lobby** (구택했다면)
- 둘 다 켜둬도 무방. 단 코드 라인이 갈라지므로 합의된 쪽 위주로

## 9. Authentication 활성화
- **Player Authentication > Authentication**
- **Anonymous** provider Enable (기본)
- 옵션 2 갈 거면 **Custom ID** provider도 Enable (지금 안 켜도 나중에 켜도 됨)

## 10. 팀원 invite
- 좌측 **Members** (또는 Organization Settings > Members)
- 이메일로 invite
- 권한: 클라팀 핵심은 **Manager**, 나머지 **Member**, 본인 **Owner** 유지

---

# 2단계 — 정보 공유

## 11. 산출 정보 정리 (`/LostMemory/docs/ugs_backend_integration_setup.md`)

> 실제 단일 소스 문서: `C:\project\LostMemory\docs\ugs_backend_integration_setup.md` (클라팀과 공유, 변경 시 슬랙 `#api-changes` 공지)



```
[UGS]
- Project ID:           xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
- Env "development":    xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
- Env "production":     xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
- 사용 SDK 조합:        Sessions API  (또는 Lobby + Relay)
- 인증 연동 방식:       옵션 1 (분리) / 옵션 2 (Custom ID)

[백엔드]
- dev Base URL:         http://<dev PC IP>:8080
- prod Base URL:        TBD
- Swagger UI:           http://<dev PC IP>:8080/swagger-ui/index.html
- 응답 포맷:            ApiResponse<T> { success, data, error }

[ErrorCode 표]
COMMON_INVALID_INPUT       400  요청 값 오류
COMMON_UNAUTHORIZED        401  인증 필요
COMMON_FORBIDDEN           403  권한 없음
COMMON_RESOURCE_NOT_FOUND  404  리소스 없음
COMMON_INTERNAL_ERROR      500  서버 오류
(도메인 코드는 5단계 진행 중 추가 예정)
```

## 12. 통신 규약 박제
- 0-3 표를 같은 페이지에 박아두기
- 변경 알림 채널: 슬랙 `#api-changes` 같은 채널 하나 정해두면 마찰 적음

---

# 3단계 — DoD 확인

## 14. 클라팀 Project Link
- Unity Editor > **Edit > Project Settings > Services**
- "Use an existing Unity Project ID" → 위에서 공유한 Project ID 입력

## 15. SDK 패키지 설치
- Package Manager에서:
  - 공통: `com.unity.services.core`, `com.unity.services.authentication`
    (둘 다 `com.unity.services.multiplayer` 설치 시 transitive로 자동 포함)
  - Sessions 택: `com.unity.services.multiplayer`, `com.unity.netcode.gameobjects`
    - NGO는 Sessions API의 `WithRelayNetwork()`가 운영하는 P2P 데이터 채널 운반자. 미설치 시 `[Error: MissingAssembly]` 발생
    - Multiplayer SDK 2.x는 Lobby/Relay 기능을 패키지 내부에 흡수하므로 `com.unity.services.lobby`, `com.unity.services.relay` 별도 설치 불필요
  - ~~Lobby+Relay 택~~: 본 프로젝트 미채택 (1단계 결정 참조)

## 16. 동작 확인 스크립트
- 클라가 다음을 한 번 성공시키고 스크린샷
- 위치: `Assets/Scripts/Tests/UgsConnectionTest.cs` (씬에 빈 GameObject 만들어 컴포넌트로 부착)
- `NetworkManager`는 Sessions API 호출 전 반드시 `Singleton`이 존재해야 하므로 `Awake()`에서 코드로 부트스트랩하여 씬 셋업 의존성 제거

```csharp
using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class UgsConnectionTest : MonoBehaviour
{
    private void Awake()
    {
        if (NetworkManager.Singleton == null)
        {
            var go = new GameObject("NetworkManager");
            var nm = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport
            };
        }
    }

    private async void Start()
    {
        try
        {
            var options = new InitializationOptions();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            options.SetEnvironmentName("development");
#else
            options.SetEnvironmentName("production");
#endif
            await UnityServices.InitializeAsync(options);
            Debug.Log("[UGS] InitializeAsync OK");

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"[UGS] SignedIn. playerId={AuthenticationService.Instance.PlayerId}");

            var sessionOptions = new SessionOptions
            {
                MaxPlayers = 3,
                IsPrivate = false
            }.WithRelayNetwork();

            var session = await MultiplayerService.Instance.CreateSessionAsync(sessionOptions);
            Debug.Log($"[UGS] Session created. id={session.Id}, code={session.Code}, host={session.IsHost}");

            await session.LeaveAsync();
            Debug.Log("[UGS] ✅ All checks passed.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UGS] ❌ FAILED: {e}");
        }
    }
}
```

### 통과 시 Console 로그
```
[UGS] InitializeAsync OK
[UGS] SignedIn. playerId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
[UGS] Session created. id=..., code=AB12CD, host=True
[UGS] ✅ All checks passed.
```

## 16-A. DoD 충족 확인 (2026-04-28, 손홍민)

| 항목 | 결과 | 증거 |
|---|---|---|
| Project Link | ✅ `InitializeAsync()` 통과 | (스크린샷 경로 TBD) |
| Authentication 활성화 | ✅ `SignInAnonymouslyAsync()` 성공, playerId 발급 | playerId 앞 4자리: `TBD` |
| 환경 분리 | ✅ `development` 환경 명시 작동 | `ProjectSettings.asset` cloudProjectId 갱신 확인 |
| Relay 활성화 | ✅ Sessions API 경유 Relay 채널 할당 + JoinCode 발급 | JoinCode: `TBD` |
| Lobby (Sessions 경유) | ✅ `CreateSessionAsync()` 내부에서 Lobby 룸 생성 성공 | Session ID 발급 확인 |

### 진행 중 발견 사항 (의존성 보강 반영)
- 15번 패키지 목록에 `com.unity.netcode.gameobjects` 추가 (Sessions+Relay 채널 운영 필수)
- 16번 스크립트에 `NetworkManager` 코드 부트스트랩 추가 (씬 셋업 없이 자기완결)
- `com.unity.services.lobby` / `com.unity.services.relay` 별도 패키지는 미채택 결정 명시화

**다음 단계:** 4단계 — 백엔드 인증 인프라 (JwtAuthenticationFilter)

---

# 4단계 — 백엔드 인증 인프라

## 17. JwtAuthenticationFilter 작성
**위치:** `com.lostmemory.server.global.security.JwtAuthenticationFilter`

**동작:**
1. `OncePerRequestFilter` 상속
2. `Authorization` 헤더에서 `Bearer ` 제거
3. `JwtProvider.isValid(token)` → false면 SecurityContext 손대지 않고 통과 (`EntryPoint`가 401 처리)
4. true면 `JwtProvider.getUserId(token)`로 userId 추출
5. `UsernamePasswordAuthenticationToken(userId, null, List.of())` 생성해서 `SecurityContextHolder.getContext().setAuthentication(...)`
6. `chain.doFilter(request, response)`

**주의:** access 토큰만 통과시켜야 함. JwtProvider에 `getTokenType(token)` 메서드 추가해서 `"access"`만 허용하도록 가드.

## 18. SecurityConfig 수정

```
http
    .csrf(...).httpBasic(...).formLogin(...)  // 그대로
    .sessionManagement(...)                    // 그대로
    .exceptionHandling(...)                    // 그대로
    .authorizeHttpRequests(auth -> auth
        .requestMatchers(
            "/api/auth/signup",
            "/api/auth/login",
            "/api/auth/refresh",
            "/swagger-ui/**",
            "/v3/api-docs/**",
            "/actuator/health"
        ).permitAll()
        .anyRequest().authenticated()
    )
    .addFilterBefore(jwtAuthenticationFilter, UsernamePasswordAuthenticationFilter.class);
```

## 19. CORS 설정
**왜 필요:** Unity Editor에서 플레이 모드로 백엔드 호출 시 origin이 `null` 또는 Unity 내부 origin. WebGL 빌드는 브라우저 origin.

**방법:** `CorsConfigurationSource` Bean + `SecurityConfig.cors(Customizer.withDefaults())` 추가

```java
@Bean
public CorsConfigurationSource corsConfigurationSource() {
    CorsConfiguration config = new CorsConfiguration();
    config.setAllowedOriginPatterns(List.of("*"));  // dev는 와일드카드, prod는 도메인 화이트리스트로
    config.setAllowedMethods(List.of("GET","POST","PUT","DELETE","OPTIONS"));
    config.setAllowedHeaders(List.of("*"));
    config.setAllowCredentials(true);
    UrlBasedCorsConfigurationSource source = new UrlBasedCorsConfigurationSource();
    source.registerCorsConfiguration("/**", config);
    return source;
}
```

## 20. 도메인 ErrorCode 추가

```
// AUTH
AUTH_INVALID_CREDENTIALS        (401, "아이디 또는 비밀번호가 올바르지 않습니다")
AUTH_LOGIN_ID_DUPLICATED        (409, "이미 사용 중인 로그인 ID입니다")
AUTH_NICKNAME_DUPLICATED        (409, "이미 사용 중인 닉네임입니다")
AUTH_REFRESH_TOKEN_INVALID      (401, "유효하지 않은 리프레시 토큰입니다")
AUTH_REFRESH_TOKEN_EXPIRED      (401, "만료된 리프레시 토큰입니다")
// SESSION
SESSION_NOT_FOUND               (404, "세션을 찾을 수 없습니다")
SESSION_FULL                    (409, "정원이 가득 찼습니다")
SESSION_ALREADY_JOINED          (409, "이미 참가한 세션입니다")
SESSION_NOT_HOST                (403, "호스트만 수행할 수 있습니다")
SESSION_INVALID_PRIVATE_CODE    (400, "비공개 코드가 일치하지 않습니다")
// RUN
RUN_NOT_FOUND                   (404, "런을 찾을 수 없습니다")
RUN_ALREADY_ENDED               (409, "이미 종료된 런입니다")
RUN_NOT_PARTICIPANT             (403, "해당 런 참가자가 아닙니다")
```

`ErrorCode.status()`가 enum 메서드라 status 인자도 받게 시그니처 확장 필요 (현재는 status가 enum 필드).

---

# 5단계 — Auth 도메인 API

## 21. POST /api/auth/signup

**Request:**

```json
{ "loginId": "user01", "password": "P@ssw0rd!", "nickname": "용사" }
```

**처리:**
1. 검증: loginId 패턴, password 길이/복잡도, nickname 길이
2. `users`에서 loginId/nickname 중복 체크 → `AUTH_LOGIN_ID_DUPLICATED` / `AUTH_NICKNAME_DUPLICATED`
3. `BCryptPasswordEncoder.encode(password)`
4. `users` insert (status=`active`)
5. `user_currencies` insert (memory_shards=0)
6. `user_talent_allocations` insert (전부 0)
7. `user_record` insert (cleared_chapter=0, cleared_stage=0)
8. `JwtProvider.createAccessToken(userId)` + `createRefreshToken(userId)`
9. `auth_refresh_tokens` insert (token_hash=hashForStorage(refresh), expires_at)

**Response:**

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJ...",
    "refreshToken": "eyJ...",
    "user": { "userId": 1, "nickname": "용사" }
  },
  "error": null
}
```

## 22. POST /api/auth/login
**Request:** `{ "loginId", "password" }`

**처리:**
1. `users` 조회 (없거나 status≠active → `AUTH_INVALID_CREDENTIALS`)
2. `BCryptPasswordEncoder.matches(password, hash)` 실패 → `AUTH_INVALID_CREDENTIALS`
3. `last_login_at` 갱신
4. access/refresh 발급, refresh row insert

**Response:** signup과 동일 구조

## 23. POST /api/auth/refresh (rotation 포함)
**Request:** `{ "refreshToken" }`

**처리:**
1. `JwtProvider.isValid(refresh)` → false면 `AUTH_REFRESH_TOKEN_INVALID`
2. (선택) tokenType이 "refresh"인지 확인
3. `auth_refresh_tokens`에서 `token_hash = hashForStorage(refresh)` AND `expires_at > now` AND `revoked_at IS NULL` 조회 (없으면 `AUTH_REFRESH_TOKEN_EXPIRED`)
4. **rotation:** 기존 row의 `revoked_at = now`로 update
5. 새 access/refresh 발급, 새 row insert

**Response:** `{ accessToken, refreshToken }`

**rotation 보안 팁:** 만약 이미 revoked된 refresh가 또 들어오면 그 user의 모든 refresh 토큰을 revoke (token reuse 탐지). 후순위지만 머리에 둘 것.

## 24. POST /api/auth/logout
**Request:** `{ "refreshToken" }` 또는 헤더의 access만

**처리:** 해당 user의 refresh 토큰 row를 revoked 처리 (단일 또는 전체 정책 결정)

**Response:** `{ "success": true, "data": null, "error": null }`

---

# 6단계 — Session 도메인 API

호출 시점은 **클라가 UGS Session 작업을 마친 직후**.

## 25. POST /api/sessions
**클라 흐름:** 호스트가 UGS Session 생성 → UGS Session ID 받음 → 백엔드 호출

**Request:**

```json
{ "maxPlayers": 2, "ugsSessionId": "...", "privateCode": "ABC123" }
```

**처리:**
1. 인증된 userId가 host
2. `sessions` insert (host_id=userId, max_players, private_code, ugs_session_id?)
   - 스키마에 `ugs_session_id` 컬럼이 없음. 추가 필요 시 ALTER 필요. (또는 옵션 1에선 일단 안 넣어도 됨)
3. `session_joins` insert (session_id, user_id=호스트, role='host')

## 26. POST /api/sessions/{sessionId}/join
**Request:** `{ "privateCode": "ABC123" }`

**처리:**
1. `sessions` 조회 (없으면 `SESSION_NOT_FOUND`)
2. private_code 검증 (있는데 다르면 `SESSION_INVALID_PRIVATE_CODE`)
3. `session_joins.count(session_id=...)` ≥ max_players → `SESSION_FULL`
4. 동일 (session_id, userId) 존재 → `SESSION_ALREADY_JOINED`
5. `session_joins` insert (role='guest')

## 27. DELETE /api/sessions/{sessionId}
**처리:**
1. `sessions.host_id == userId`인지 확인 (아니면 `SESSION_NOT_HOST`)
2. `sessions` delete (CASCADE로 session_joins/runs/run_member 정리됨)

---

# 7단계 — Run 도메인 API

## 28. POST /api/runs

**Request:**

```json
{
  "sessionId": 1,
  "members": [
    { "userId": 1, "selectedWeaponId": 10 },
    { "userId": 2, "selectedWeaponId": 11 }
  ]
}
```

**처리:**
1. session 존재 + 호출자가 호스트 검증
2. 멤버 전원이 `session_joins`에 있는지 검증
3. `runs` insert (session_id, status='progress')
4. `run_member` insert (멤버별)

## 29. POST /api/runs/{runId}/end

**Request:**

```json
{
  "result": "clear",
  "durationSeconds": 842,
  "chapterReached": 1,
  "stageReached": 1,
  "memoryShardsEarned": 10,
  "bossesDefeated": 1,
  "roomsCleared": 20,
  "enemiesKilled": 87,
  "members": [
    {
      "userId": 1,
      "finalHp": 50,
      "finalGold": 120,
      "selectedArtifactsJson": [],
      "shopPurchasesJson": []
    }
  ]
}
```

**처리 (트랜잭션):**
1. `runs` 조회. status='end'면 `RUN_ALREADY_ENDED`
2. `runs` update: status='end', ended_at=now
3. `run_results` insert
4. `run_member` update (각 멤버 final_*, *_json)
5. 각 멤버 `user_currencies.memory_shards += memoryShardsEarned`
6. result='clear'면 `user_record` 갱신: `cleared_chapter`/`cleared_stage`가 더 높으면 update

---

# 8단계 — 유저/메타 API

## 30. GET /api/player/me

**Response:**

```json
{
  "userId": 1,
  "loginId": "user01",
  "nickname": "용사",
  "status": "active",
  "lastLoginAt": "2026-04-27T13:00:00Z",
  "currency": { "memoryShards": 120 },
  "talent": { "totalPoint": 5, "critRatePoints": 2 },
  "record": { "clearedChapter": 1, "clearedStage": 3 }
}
```

## 31. 후순위
- `GET /api/weapons` (마스터 + 본인 해금 상태 합쳐서)
- `POST /api/weapons/unlock/{weaponId}` (memory_shards 차감 + user_weapon_unlocks insert)
- `PUT /api/talent/allocation` (포인트 재분배)
- `GET /api/memory/frames`, `POST /api/memory/frames/{frameId}/fill` (액자 채우기)

---

# 9단계 — 운영 / 장기

## 32. prod 배포 시크릿
- `.env.example` 파일의 빈 값들 (POSTGRES_PASSWORD, REDIS_PASSWORD, JWT_SECRET, JWT_*_EXPIRATION) 채워서 prod 서버에 배포
- 시크릿 보관 장소 합의 (1Password / 인프라 레포 vault 등)

## 33. UGS Server API 연동 (옵션 2 또는 운영 단계)
- UGS 대시보드에서 **Service Account Key** 발급
- 백엔드에 키 안전 보관 (env var)
- `/api/auth/ugs-token` 엔드포인트 또는 세션 강제 종료 등

---

# 의존 관계 핵심

- **0 → 1**: 합의 없이 대시보드 켜면 클라팀이 다른 SDK 쓰겠다고 할 때 다시 켜야 함
- **4 → 5 → 6 → 7**: JWT 필터 없이 Auth API만 만들면 보호되는 엔드포인트 테스트 불가
- **1·2 → 3**: 클라팀이 정보 받아야 DoD 확인 가능
- **4~5는 1~3과 병렬 가능**: UGS 대시보드 작업 중 클라팀 진행 동안 백엔드는 인증 필터/Auth API 먼저 만들어둘 수 있음

---

# 미결정 항목 (결정 후 본 문서 업데이트 필요)

- ~~0-1: 옵션 1 vs 2~~ → **2026-04-27 옵션 1 채택**
- ~~0-2: Sessions API vs Lobby+Relay~~ → **2026-04-27 Sessions API 채택**
- 0-3: prod Base URL → 배포 시점에 결정
- 6단계: `sessions` 테이블에 `ugs_session_id` 컬럼 추가 여부
