# 2026-05-26 — Rena 보스 cast telegraph (빨간 장판) host-only 표시 문제

## 증상
- Rena 보스가 마법 시전 시 발 밑에 표시되는 빨간 원/박스 형태 telegraph ("장판") 가
  - **Guest 화면**: 보임
  - **Host 화면**: 안 보임
- 데미지 로직은 정상 (host-authoritative)
- 시각 비대칭으로 인해 협동 플레이 시 host 가 회피 타이밍을 시각적으로 못 잡음

## Root cause (왜 안 됐는가)

### 아키텍처 가정
프로젝트의 monster telegraph sync 패턴 (`MonsterAttackBroadcast.cs`) 은 다음 가정으로 설계됨:

```
[Host 측 일반 enemy]
1. Enemy controller 가 자체 AttackTelegraph2DView.Show() 호출 → host 화면에 직접 visual 표시
2. 그 후 BroadcastTelegraph() 호출 → ClientRpc 발화
3. ClientRpc 내부: IsHost return; (중복 방지) → guest 만 SpawnVisualClone()
```

이 가정이 성립하면 host = local visual 1개, guest = ClientRpc visual 1개 → 양쪽 일관.

### Rena 의 경우 가정이 깨짐
`RenaBossSpellCombatController.BroadcastCastTelegraph()` 는 broadcast 만 호출하고
**host-side `telegraphView.Show()` 직접 호출이 없음**.

| 측 | Visual 갯수 |
|---|---|
| Host | 0 (local 호출 없음 + ClientRpc `IsHost return` 으로 skip) |
| Guest | 1 (ClientRpc → SpawnVisualClone) |

### 코드 추적
- `MonsterAttackBroadcast.cs:89~92`
  ```csharp
  // Bug #37: host 측은 enemy controller 의 *원본 telegraphView.Show* 가 visual 이미 표시 중.
  //   ClientRpc 의 SpawnVisualClone 까지 또 작동하면 host 화면에 중복 sprite → skip.
  if (IsHost) return;
  ```
- `RenaBossSpellCombatController.cs:461` — `BroadcastCastTelegraph(kind, duration, releaseDelay);` 만 호출, 직접 `Show()` 없음.

## 사용자 의도 (최종 확정)
"빨간 장판이 안 보이길 원해 — host/guest 둘 다 안 보이게."

→ **추가가 아닌 제거** 가 정답이었음. (초기에 host 측에도 표시되도록 fix 하려 했으나 사용자 의도와 반대.)

## Fix
**파일**: `Assets/_Project/Scripts/Runtime/Enemies/Boss/Rena/RenaBossSpellCombatController.cs`

`StartCast()` 내부 `BroadcastCastTelegraph(...)` 호출 1줄 + 위 주석 2줄 삭제 (line 459~461 영역):

```diff
            SetWanderEnabled(false);
            FaceTarget();
            PlayAnimation(ResolveCastStateName(kind));
-           // 게스트 측 attack-cue ClientRpc broadcast — cast 시작 즉시 telegraph 표시.
-           // RTT 만큼만 늦게 게스트도 "보스 공격 임박" 인지. release 까지 시각 wind-up 으로 흡수.
-           BroadcastCastTelegraph(kind, duration, releaseDelay);
+           // Cast telegraph (빨간 장판) 제거 — host/guest 양쪽에서 안 보이게.
+           // BroadcastCastTelegraph 메서드 본체는 dead code 로 남김 (시연 후 정리).
            Log(kind + " cast started.");
```

`BroadcastCastTelegraph` 메서드 본체 (line 481~532) 와 `ResolveAttackBroadcast` 헬퍼는 dead code 로 남김 → 시연 후 cleanup.

## 영향 범위
- Rena 의 모든 마법 (Fireball / Inferno / IceSweep / ThunderStrike / Thunderbolt) cast telegraph 제거
- 실제 공격 effect (projectile, ice pillar, thunder area, thunderbolt 빔) 정상 작동
- 데미지 로직 영향 0
- 다른 enemy (skeleton archer, dagger 등) 영향 0 — Rena 만 이 호출 경로 사용

## NGO 안정성
- ClientRpc 시그니처 변경 0
- prefab 변경 0
- NetworkObject GUID 변경 0
- Join risk 0

## 교훈
1. **Sync 버그는 host/guest 양쪽 시각을 비교하기 전엔 방향을 단정하지 말 것** — 처음엔 host-only 시각을 guest 에도 추가하려 했지만 실제로는 양쪽 다 제거가 정답.
2. **사용자에게 의도를 명시적으로 확인** — "안 보이게" 인지 "양쪽 다 보이게" 인지 한 번에 확인하지 못해 첫 fix 방향이 반대로 갔음.
3. **`if (IsHost) return;` 패턴은 host 측에 local visual 이 *항상 별도로 호출되는 가정*에서만 안전** — 새 enemy 추가 시 이 가정을 깨면 host-only-invisible 버그 발생.
