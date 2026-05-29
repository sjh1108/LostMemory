# 보스 등장 트리거 비교 — Preview_2F_Boss_test vs Town_Preview

## Context
두 Preview 씬에서 보스가 한쪽은 등장하고 한쪽은 등장하지 않는 이유를 추적. 결론적으로 *트리거 조건* 차이가 아니라 *트리거 메커니즘 자체의 존재 여부* 차이임.

---

## 1. Preview_2F_Boss_test.unity — 보스 등장 경로

### 트리거 체인
씬 로드 → `BossSceneAutoStarter.OnEnable()` → 0.5s 대기 → `BossRoomLocalTransitionDriver.TryStartRouteEntry()` → 씬의 모든 Player Character 를 `BossRoomEntryPoint` 위치로 텔레포트.

### 핵심 컴포넌트와 책임
| 컴포넌트 | GameObject / 프리팹 | 파일 |
|---|---|---|
| `BossSceneAutoStarter` | `BossScenAutoStarter` (씬 직접 배치) | [BossSceneAutoStarter.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/BossSceneAutoStarter.cs) |
| `BossRoomLocalTransitionDriver` | `BossDoor Variant` 프리팹 | [BossRoomLocalTransitionDriver.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/BossRoomLocalTransitionDriver.cs) |
| `BossRoomEntryPoint` | `BossDoor Variant` 프리팹 내부 | [BossRoomEntryPoint.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/BossRoomEntryPoint.cs) |

### 실제 발화 조건 (코드 라인 기준)
- [BossSceneAutoStarter.cs:28-34](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/BossSceneAutoStarter.cs#L28) — `autoStartOnEnable=true` 이므로 컴포넌트 활성화 즉시 시작
- [BossSceneAutoStarter.cs:55-84](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/BossSceneAutoStarter.cs#L55) — `startDelaySeconds=0.5`, `maxStartAttempts=5`, `retryIntervalSeconds=0.25` 로 retry
- [BossSceneAutoStarter.cs:86-102](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/BossSceneAutoStarter.cs#L86) — `bossRoomId="Boss"`, `bossEntryPointId=""` 로 `TryStartRouteEntry` 호출
- [BossRoomLocalTransitionDriver.cs:31-62](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/BossRoomLocalTransitionDriver.cs#L31) — entryPoint 찾고 → `CollectScenePlayers()` 로 활성 Player 모음 → 텔레포트
- [BossRoomLocalTransitionDriver.cs:164-184](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/BossRoomLocalTransitionDriver.cs#L164) — `Character.CharacterTypes.Player` + active + 생존 상태인 캐릭터만 수집

즉, **씬에 활성 플레이어 1명만 있으면 0.5초 후 자동으로 보스룸 입장**. 별도의 트리거 콜라이더나 NPC 대화 같은 게이트는 없음.

---

## 2. Town_Preview.unity — 보스 미등장 사유

**보스 트리거 메커니즘 자체가 씬에 부재.** 조건 미충족이 아니라 부품이 없어서 안 작동하는 케이스.

| 컴포넌트 | Preview_2F_Boss_test | Town_Preview |
|---|---|---|
| `BossSceneAutoStarter` | O | ✗ |
| `BossRoomLocalTransitionDriver` | O (BossDoor 프리팹) | ✗ |
| `BossRoomEntryPoint` | O | ✗ |
| `BossRoomDoorController` | O | ✗ |

Town_Preview 는 마을/로비 컨셉의 씬이라, 설계상 보스 조우는 별도의 던전 씬(`Dungeon_*F_Boss.unity`)으로 씬 전환해야 일어나는 구조. `Preview_2F_Boss_test` 는 그 던전 씬 진입 후 흐름을 빠르게 테스트하려고 *AutoStarter 를 씬에 임베드한 테스트 변형*이다.

---

## 만약 Town_Preview 에서도 보스를 띄우고 싶다면 (참고)
- `BossDoor Variant` 프리팹을 씬에 배치 (driver + entryPoint + doorController 한 묶음)
- 빈 GameObject 에 `BossSceneAutoStarter` 추가, `bossRoomId="Boss"` 로 설정
- 단, 마을 컨셉을 유지하면서 자동 트리거를 넣는 건 게임 디자인상 맞지 않음. 보스를 *마을에서 직접* 띄우려는 의도라면 `BossDoor` 같은 별도 인터랙션 트리거(콜라이더 진입 시 발화)가 더 자연스러움.

---

## 검증 방법
1. Preview_2F_Boss_test 씬을 에디터에서 Play → 0.5초 뒤 플레이어가 BossRoomEntryPoint 위치로 텔레포트하는지 확인
2. `BossSceneAutoStarter.debugLogging` 을 켜면 콘솔에서 retry/시작 로그 확인 가능
3. Town_Preview 에서 `FindObjectsByType<BossSceneAutoStarter>` 가 0건임을 콘솔에서 확인하면 부재 사유 확정
