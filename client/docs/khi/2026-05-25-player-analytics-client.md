# Player Analytics — 클라이언트 구현 계획

날짜: 2026-05-25
관련: [2026-05-24-player-analytics-tracking-plan.md](./2026-05-24-player-analytics-tracking-plan.md), [2026-05-24-player-analytics-backend-handoff.md](./2026-05-24-player-analytics-backend-handoff.md)

## Context

클라이언트 측 인게임 이벤트(세션 시작/종료, 스테이지 진입/클리어, 플레이어 사망 등) 를 batch 로 자체 백엔드(`/api/analytics/events`) 에 POST 하는 시스템 구축. 백엔드/스키마/배포 정책은 `client/docs/khi/2026-05-24-player-analytics-backend-handoff.md` 에서 합의 완료. 본 plan 은 그 핸드오프 문서 §9 진행 순서 중 **클라 책임 부분(2단계, 4단계)** 의 구체적 실행 계획.

목표: 게임 hitch 없이 (POST 응답 안 기다림) Tier 1 이벤트 5종을 안정적으로 수집해 백엔드로 흘림. 보스 패턴 단위 사망 분석까지 MVP 에 포함.

## 범위 (Tier 1 — MVP)

5개 이벤트, payload 의 모든 필드 포함:
- `session_start` (party_size, device)
- `session_end` (last_stage_id, reason, duration_sec)
- `stage_entered` (party_size, prev_stage_id)
- `stage_cleared` (duration_sec)
- `player_died` (cause_enemy_id, **cause_pattern_id**, run_id, run_elapsed_sec)

## 핵심 컴포넌트

### A. `AnalyticsApiClient` (신규, static class)

위치: `Assets/_Project/Scripts/Runtime/Networking/Analytics/AnalyticsApiClient.cs`

기존 [SessionApiClient.cs](LostMemory/Assets/_Project/Scripts/Runtime/Networking/Session/SessionApiClient.cs) 패턴 그대로 복제:
- `HttpClient` 정적 싱글톤, 10초 타임아웃
- `BaseUrl` Editor/Build 분기 (SessionApiClient 에서 그대로 가져옴)
- `AddAuth()` 로 `SessionApiClient.AccessToken` 재사용 (`Authorization: Bearer`)
- Newtonsoft.Json 직렬화, `NetLog` 로깅

메서드: `Task PostEventsAsync(IReadOnlyList<AnalyticsEvent> events)` 만.

### B. `AnalyticsClient` (신규, MonoBehaviour 싱글톤)

위치: `Assets/_Project/Scripts/Runtime/Networking/Analytics/AnalyticsClient.cs`

- `Guid SessionId` (게임 실행 1회당 1개, Awake 에서 발급)
- `Queue<AnalyticsEvent> _pending` (in-memory)
- `Track(string eventType, string stageId, object payload)` 공개 메서드 — payload 는 익명 객체 OK
- `InvokeRepeating` 으로 5초마다 flush, 또는 50개 누적 시 즉시 flush
- 재시도: 5xx/타임아웃은 큐에 되돌리고 exponential backoff 최대 3회
- 4xx 는 batch 폐기
- `OnApplicationQuit` 에서 동기 flush 시도 (`.GetAwaiter().GetResult()` 짧은 타임아웃)

생명주기: TitleScene 로그인 성공 직후 `DontDestroyOnLoad` 로 생성.

### C. `AnalyticsEvent` DTO

```csharp
public class AnalyticsEvent {
    public string event_type;
    public string event_time;  // ISO-8601 UTC
    public string stage_id;
    public Dictionary<string, object> payload;
}
```

POST body wrapper: `client_version`, `session_id`, `events`.

## 이벤트 발화 hook

| 이벤트 | 발화 위치 | 비고 |
|---|---|---|
| `session_start` | [TitleSceneController.cs](LostMemory/Assets/_Project/Scripts/Runtime/UI/Title/TitleSceneController.cs) 로그인 성공 직후 | AnalyticsClient 생성 직후 첫 호출 |
| `session_end` | `OnApplicationQuit` + 디스커넥트 콜백 | duration_sec = `Time.realtimeSinceStartup` |
| `stage_entered` | [RoomEntryRuntimeController.cs](LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs) `RoomEntered` 이벤트 구독 | stage_id = SceneName |
| `stage_cleared` | 동 파일 `RoomCleared` 이벤트 구독 | duration_sec = stage 진입 시각 보관 후 차이 |
| `player_died` | [KhiDownController.cs](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs) `DefeatedByTimeout`/`DefeatedSolo` 구독 | payload 는 아래 §사망 메타데이터 참조 |

## 신규 코드 (이벤트 hook 외 추가 작업)

### 1. `RunManager.StartRun()` 에 run_id 발급

파일: [RunManager.cs](LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs) (행 ~120, runStartedAt 옆)

```csharp
public Guid CurrentRunId { get; private set; }
// StartRun() 안에:
CurrentRunId = Guid.NewGuid();
```

### 2. Damage 시스템에 lastDamage 메타 추가

파일: [PlayerDamageReceiver.cs](LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerDamageReceiver.cs) (피격 처리 메서드)

- `Health` 또는 `PlayerDamageReceiver` 에 `string LastDamageSourceId`, `string LastDamagePatternId` 두 필드 추가
- 피격 시 attacker GameObject 에서 ID 조회해 채움
- attacker 식별: `MonsterAttackBroadcast` (이미 존재) 의 broadcast 메시지에 `enemyId`/`patternId` 두 필드 추가하거나, attacker 컴포넌트에서 직접 끌어오기

### 3. 보스 공격 클래스에 PatternId 상수 박기 (~10개)

명명 규칙: `"Bertha.LightAttack1"`, `"Rena.IcePillar"` 같은 `{Boss}.{Pattern}` 형식.

대상 파일들:
- Bertha: `BerthaLightAttack1Controller`, `BerthaLightAttack2Controller`, `BerthaHeavyAttackController`, `BerthaDashAttackController`, `BerthaAreaAttackController`, `BerthaProjectilePatternDriver`
- Rena: `RenaBossThunderStrikeArea`, `RenaBossIcePillarArea`, `RenaBossThunderboltBeam`, `RenaBossProjectile`

각 클래스에:
```csharp
public const string PatternId = "Bertha.LightAttack1";
```

이 상수가 hit 발생 시 `MonsterAttackBroadcast` 로 흘러서 PlayerDamageReceiver 의 `LastDamagePatternId` 에 저장되도록 wiring.

### 4. EnemyId 매핑

- 보스: `"Bertha"`, `"Rena"` 등 prefab 명 기반 상수
- 일반 적: prefab 명 그대로 (`"Skeleton1"`, `"Orc_CL037"` 등)

각 적 prefab 의 루트 컴포넌트에 `EnemyId` 필드 추가하거나, `gameObject.name` 에서 `(Clone)` 떼서 사용.

## 구현 순서

각 단계 후 백엔드와 sync.

1. **AnalyticsApiClient + AnalyticsClient 골격** — `session_start`/`session_end` 만 발화. 백엔드 더미 endpoint 와 첫 통합 테스트. ✅ 완료
2. **run_id 발급** — `RunManager.ResetRunResultTracking` 에서 `Guid.NewGuid()`. ✅ 완료
3. **stage_entered/stage_cleared 발화** — `RunManager.SubscribeAllRoomControllers` 에 RoomEntered 구독 추가, `AnalyticsClient.FireStageEntered/Cleared` 호출. ✅ 완료
4. **damage 원인 추적** — `AnalyticsDamageTracker` (singleton, MMDamageTakenEvent listener) + `AnalyticsPatternTag` (prefab 부착용 MonoBehaviour). instigator 부모 chain 에서 tag 검색, enemy_id 는 tag 없으면 instigator root GameObject 이름 fallback. ✅ 완료
5. **player_died 발화** — `RunManager.HandlePlayerDefeatedDirect` (이미 DefeatedByTimeout/DefeatedSolo 구독) 에서 `AnalyticsClient.FirePlayerDied` 호출. ✅ 완료
6. **보스 공격 prefab 에 AnalyticsPatternTag 부착** — 코드 경로는 5번에서 완성. **보스 prefab inspector 작업 미수행** — designer 가 BerthaRoot/RenaRoot 의 각 attack controller GameObject 에 컴포넌트 추가 + patternId 문자열 입력 (예: `"Bertha.LightAttack1"`) 시 자동 수집. 부착 안 되어 있으면 `cause_pattern_id = null` 로 흐름 (백엔드 JSONB 호환).

5번까지 끝나면 시연 가능한 상태. 6번은 prefab inspector 후속 작업.

## Verification

- **수동**: Editor 에서 한 판 플레이 — 로그인 → 던전 입장 → 스테이지 클리어 → 보스에 사망 → 종료. NetLog 콘솔에 5개 이벤트 모두 POST 성공 로그가 찍히는지 확인.
- **백엔드 dummy 단계**: 백엔드가 더미 endpoint (1단계 결과물) 띄우면 클라쪽에서 `curl` 로 직접 호출해 schema 일치 먼저 검증 후 게임 통합.
- **백엔드 DB 단계**: 백엔드 PostgreSQL 에서 `SELECT * FROM player_events WHERE session_id = '...'` 로 5개 이벤트 모두 적재 확인.
- **재시도 동작**: 백엔드 죽인 상태에서 게임 플레이 → 큐에 쌓이는지 + 백엔드 복구 후 재시도로 들어가는지 확인.

## 의도적 비범위

- 영구 저장 (재시작 시 큐 복구 X — 메모리만)
- 토큰 refresh (401 시 batch 폐기, 핸드오프 문서 §7 결정대로)
- Tier 2/3 이벤트 (boss_attempt, network_disconnect, shop_visited 등 — 나중에)
- 일반 적의 attack pattern_id (보스만)
