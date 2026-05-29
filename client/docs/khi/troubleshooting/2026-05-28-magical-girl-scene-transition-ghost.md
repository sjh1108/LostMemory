# 씬 전환 시 미소녀 ghost / stuck 중복 (NGO 멀티)

## 증상

NGO 멀티 환경에서 던전/상점 씬 전환마다:

| Player | 관찰 |
|---|---|
| Host 자신 화면 | 자기 미소녀 2배 — 정상 follow 1쌍 + 시작 위치에 stuck 1쌍 |
| Host 화면 | guest 미소녀 보임 |
| Guest 자신 | 자기 미소녀만 보임, host 의 visual clone 부재 |

1F_Shop 에서 가장 두드러지게 보였으나 실제로는 **모든 NGO multi 씬 전환의 공통 회귀**. 솔로/Editor 단일 씬에선 정상.

## Root cause — 2 Spawner race (scene-placed + persistent)

각 던전/상점 씬엔 scene-placed `TestKhi_MinimalCharacter2D` 가 배치돼 있고, `EditorTestCharacterMarker` 가 NGO 활성 시 `Destroy(gameObject)` 호출. **`Destroy` 는 deferred — frame 끝에 발효**. 그 사이 같은 frame 의 `MonoBehaviour.OnEnable` 단계가 먼저 실행되고:

1. scene-placed 캐릭터 위 `MagicalGirlSpawner.OnEnable` 이 `ReplayOwnedRelics()` 실행 → `HandleRelicAcquired` → `AddGirlByVisual` 통과 (dict 비어있음, 가드 false) → 2명 spawn. anchor = scene-placed Player.transform.
2. `EditorTestCharacterMarker.Awake` 의 예약된 `Destroy(gameObject)` 가 frame 끝에 발효 → scene-placed Player + 그 위 미소녀들의 anchor transform 사라짐 → **미소녀 stuck (null anchor 로 SmoothDamp 가 원점 부근에 고정)**.
3. DDoL 위 persistent NGO PlayerObject 의 `MagicalGirlSpawner.HandleActiveSceneChangedForReplay` 가 같은/다음 frame 에 발화 → 정상 2명 spawn → 총 4명.

기존 가드 `[MagicalGirlSpawner.cs:388-395]` 는 `ownNetObj.IsSpawned == true` 를 요구. scene-placed 캐릭터의 NetworkObject 는 **NGO 가 spawn 한 게 아니므로 IsSpawned=false** → 가드 우회.

게스트 측 host clone 부재도 같은 race 의 부산물 — scene-placed Spawner 가 broadcast 가 NGO-aware 가 아닌 채로 `NotifyLocalGirlSpawned` 호출했거나, 진짜 NGO PlayerObject Spawner 의 broadcast 도착 시점에 게스트의 클라가 아직 씬 sync 안 끝남.

## Fix

[`MagicalGirlSpawner.cs`](../../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs) `OnEnable` 진입 직후 가드 추가 + helper:

```csharp
private void OnEnable()
{
    if (IsDoomedSceneCharacter())
    {
        if (_logSpawn || _logSceneTransitionAudit)
            Debug.Log($"[MagicalGirl] OnEnable — scene-placed 캐릭터 위 Spawner. 구독/replay skip.", this);
        return;
    }
    // ... 기존 wiring / inventory 구독 / activeSceneChanged 구독
}

private bool IsDoomedSceneCharacter()
{
    var nm = Unity.Netcode.NetworkManager.Singleton;
    if (nm == null || !nm.IsListening) return false;
    var marker = GetComponent<EditorTestCharacterMarker>()
              ?? GetComponentInParent<EditorTestCharacterMarker>();
    return marker != null;
}
```

핵심: **`EditorTestCharacterMarker` 부착 + NGO 활성 = 같은 frame 안에 Destroy 예약** 이라는 단일 시그널로 판단. inventory 구독 / activeSceneChanged 구독 / ReplayOwnedRelics 전부 skip → 본 Spawner 는 inert.

## 영향 범위

- 솔로 / Editor 단일 씬: `nm == null || !nm.IsListening` → 가드 통과, 회귀 없음. 기존 Option B (DDoL 제거 + activeSceneChanged replay) 동작 유지.
- NGO 멀티 + scene-placed Player: scene-placed Spawner 가 깨끗하게 inert → ghost 미소녀 spawn 안 함 → stuck 사라짐. 정상 spawn 은 persistent NGO PlayerObject 의 Spawner 한 곳에서만 일어남.
- 게스트 visual clone: scene-placed Spawner 가 broadcast 안 보냄 → 진짜 PlayerObject Spawner 의 broadcast 만 채널에 흐름 → 시점 race 자연 해소 기대.

NGO 시그니처 / ClientRpc / prefab 변경 무. Join risk 0.

## 교훈

1. **Unity `Destroy` 는 frame 끝에 발효** — 같은 frame 의 OnEnable / Start 는 정상 실행됨. "곧 destroy 될 객체" 를 무시하려면 단순 null check 가 아니라 외부 시그널 (이 경우 EditorTestCharacterMarker + NGO 상태) 로 명시 판단 필요.
2. **scene-placed + DDoL persistent 두 라이프타임 동시 존재** 패턴은 race 위험이 큼. 한쪽이 다른 쪽 활동을 봉인할 명확한 가드 필요. `NetworkObject.IsSpawned` 만으론 부족 — scene-placed 객체는 IsSpawned=false 인 채로 OnEnable 한 cycle 을 산다.
3. 동일 패턴 의심 컴포넌트: scene-placed 캐릭터 위에 부착된 다른 controller (HealthSync, RelicInventory 등) 도 같은 검토 필요. 이미 PlayerHealthSync 는 `HandleActiveSceneChangedForReset` 으로 reset 만 하므로 ghost spawn 위험 없음. 새로운 OnEnable side-effect 추가 시 `IsDoomedSceneCharacter` 패턴 재사용 권장.

## 검증

1. 솔로 → 던전 전환 → 미소녀 정확히 N 명 (이전 fix 그대로)
2. 호스트+게스트 멀티 → 던전 전환 → 호스트 자기 미소녀 N 명 (stuck 0), guest 자기 미소녀 N 명
3. 1F_Shop 진입 / 퇴장 → 위와 동일
4. `_logSceneTransitionAudit` ON 으로 audit log 확인:
   - scene-placed Spawner 의 `OnEnable — 구독/replay skip` 로그 1회
   - persistent Spawner 의 `activeSceneChanged ... replay 트리거` 로그 1회
   - `AddGirlByVisual ENTER` / `>>> SPAWN NEW` 가 persistent Spawner ID 에서만 발화

---

## 후속 fix (같은 날) — guest 측 host visual clone 누락

위 가드 적용 후 ghost/stuck 은 해결됐으나, **씬 전환 직후 guest 화면에 host 의 기존 미소녀가 안 보임** (새 씬에서 host 가 새로 획득한 미소녀는 정상). 즉 *기존 보유분 replay broadcast 만* 누락.

### Root cause — `activeSceneChanged` 와 게스트 씬 sync 의 timing race

`HandleActiveSceneChangedForReplay` 가 `SceneManager.activeSceneChanged` 에 hook 돼있는데, 이건 **host 의 자기 씬 active 됐을 때 즉시 발화**. 게스트는 NGO 의 별도 sync 파이프라인으로 새 씬 로드 중. host 가 `ReplayOwnedRelics → AddGirlByVisual → NotifyLocalGirlSpawned → ServerRpc → BroadcastGirlSpawnClientRpc` 까지 한 frame 안에 끝내고 게스트로 ClientRpc 보내지만, 게스트의 receiver (= 게스트 측 host PlayerObject 위 broadcast 인스턴스) 가:

- 게스트가 아직 이전 씬에 있어 ClientRpc 가 queue 됐다가 새 씬에서 dispatch 되는데, 그 사이 destroy 되는 stale visual clone 과 race
- 또는 ClientRpc 가 receiving NetworkBehaviour 의 unstable 상태 (씬 전환 중) 에서 drop

새로 획득은 OK — 양쪽 씬 안정화 후 broadcast 라서.

### Fix — NGO 활성 시 `OnLoadEventCompleted` 로 트리거 전환

[`MagicalGirlSpawner.cs`](../../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs):

```csharp
private void SubscribeSceneTransitionTrigger()
{
    var nm = Unity.Netcode.NetworkManager.Singleton;
    if (nm != null && nm.IsListening && nm.SceneManager != null)
    {
        // NGO 멀티 — 모든 client sync 완료 후 발화. broadcast receiver 모두 준비됨 보장.
        nm.SceneManager.OnLoadEventCompleted += HandleNgoLoadEventCompleted;
        _ngoLoadEventHooked = true;
    }
    else
    {
        // 솔로 — broadcast 무관, activeSceneChanged 로 즉시 트리거.
        SceneManager.activeSceneChanged += HandleActiveSceneChangedForReplay;
    }
}
```

`OnLoadEventCompleted` 는 NGO `NetworkSceneManager` 가 모든 클라이언트의 LoadComplete 응답 수신 후 발화 — 이 시점엔 게스트의 receiving broadcast 인스턴스도 새 씬에 안정 상태. 같은 이벤트를 [`PlayerHealthSync`](../../../LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/PlayerHealthSync.cs) 가 PlayerObject respawn 에 이미 사용 — 검증된 패턴.

### 영향

- 솔로: activeSceneChanged 경로 유지, 회귀 없음.
- NGO 멀티: 씬 전환 후 N00ms~수백 ms 지연 (모든 client sync 대기) 후 replay → broadcast 도착 안정 → 게스트가 host 의 모든 기존 미소녀 visual clone 정상 표시.
- 게스트의 자기 미소녀: 본인 owner 측 spawn 은 같은 client 안의 trivial path → 항상 정상 (영향 무).

### 추가 교훈

4. **NGO ClientRpc 는 transient — 송신 시점 receiver 상태에 영향 받음.** 씬 전환 같은 unstable window 에서 transient broadcast 보내면 drop 또는 stale receiver 도착 위험. 영속 state 가 필요하면 NetworkList/NetworkVariable, 또는 송신 시점을 sync 완료 이후로 미루기.
5. **`SceneManager.activeSceneChanged` vs `NetworkSceneManager.OnLoadEventCompleted` 구분.** 전자는 local-only (Unity SceneManager), 후자는 NGO 전체 sync 완료. 멀티 sync 가 필요한 작업은 반드시 후자.
