# CL-202 구현 요약 — OnHit VFX 정식 교체 (Freeze / Burn / Slow)

**관련 plan**: [cl202_onhit_status_plan.md](cl202_onhit_status_plan.md)

## 목적

상태이상 3종(Freeze / Burn / Slow)의 시각화 정비. 기존 placeholder는 `Texture2D.whiteTexture` SpriteRenderer 동적 생성 + 색 틴트라 단조롭고, Slow는 시각이 0이라 적이 둔화된지 인지 불가능했음. 정식 VFX 프리팹 + sprite tint 조합으로 교체하고 정책 D(가장 우선 1개 visual만 표시) 도입.

## Plan과 달라진 점 (코드베이스 현황 반영)

| 항목 | Plan 원안 | 실제 구현 | 사유 |
|---|---|---|---|
| VFX prefab 소유 | `EnemyStatusEffect`의 `[SerializeField]` | `OnHitEffectRegistry`에서 SerializeField, `EnsureVFXPrefabs(...)`로 EnemyStatusEffect에 주입 | EnemyStatusEffect는 `GetOrAddStatus`에서 런타임 `AddComponent`로 부착되어 디자인 타임 와이어링 불가 |
| 프리팹 경로 | `_Project/Prefabs/VFX/` | `_Project/Prefabs/Effect/` | CL-201 일치, 기존 컨벤션 |
| Slow VFX | `SlowStatusVFX.prefab` (발 밑 안개 + 적 위 입자 + 발 밑 원판 통합) | **prefab 생략, 적 sprite tint로 단순화** | 사용자 결정 — particle 3개보다 sprite 변색이 직관적이고 카메라 줌/각도 무관 항상 보임 |
| Burn 시각 | prefab(Fire + Smoke + Light)만 | prefab + **적 sprite tint 추가** | 사용자 추가 요청 — "적이 안에서 타는" 인상 |
| Freeze 시각 | IceBlock 정적 sprite | IceBlock + **덜덜거림 효과** | 사용자 추가 — placeholder 같은 정적 인상 깨기 |

## 결정 사항 (사용자 확정)

### Plan 기준
- **표현 방식**: Sprite 베이스 + ParticleSystem 보조 (적이 정적 → Sprite 적합)
- **종료 정리**: 0.25초 페이드아웃
- **동시 적용 정책**: **D — 효과 모두 적용, VFX 1개만**
- **우선순위**: Freeze > Burn > Slow
- **SFX 3종 모두 추가** (Freeze/Burn/Slow)
- **히트스톱 제외** (Chain/Wind에서 이미 처리, 중복 회피)
- **아키텍처**: Option A — OnHitEffectRegistry 보유 + 주입

### 작업 중 추가 결정
- **Slow tint 색**: `(0.7, 0.9, 1, 1)` 기본 (옅은 푸른빛). 인스펙터에서 자유 조정
- **Burn tint 색**: `(1, 0.6, 0.4, 1)` 기본 (따뜻한 주황)
- **LateUpdate에서 매 프레임 강제 적용** — TopDownEngine 데미지 플리커 등 다른 시스템이 Update에서 색을 덮어써도 LateUpdate가 마지막에 우리 tint로 끝남
- **Awake에서 sprite 미리 캐시** — originalColor 정확성 보장 (적이 깨끗한 상태일 때)
- **Sprite 검색 휴리스틱** — `Shadow / VFX / Effect / Status / IceBlock` 이름 자식 제외, 가장 큰 bounds 선택 (AdventurerShadow가 잡히는 문제 해결)
- **IceShivering 컴포넌트** — IceBlockGroup 자식만 떨림, CrystalParticles는 영향 X

## 변경 파일

### 신규 스크립트

| 파일 | 역할 |
|---|---|
| [IceShivering.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/VFX/IceShivering.cs) | 매 프레임 random offset으로 떨림 효과. Amplitude / Frequency / UseUnscaledTime SerializeField |

### 수정 스크립트

[OnHitEffectRegistry.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs) — Status VFX prefab 3 + SFX 3 + tint 색 2 + 디버그 1
- `_freezeStatusVFXPrefab`, `_burnStatusVFXPrefab`, `_slowStatusVFXPrefab`
- `_freezeApplySfx`, `_burnApplySfx`, `_slowApplySfx`, `_statusSfxVolume`
- `_slowTintColor`, `_burnTintColor`
- `_debugFreezeOverrideSeconds` (디버깅용)
- `ApplySlow / ApplyFreeze / ApplyBurn` — `EnsureVFXPrefabs` + `SetSlowTintColor` / `SetBurnTintColor` push + SFX 재생

[EnemyStatusEffect.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs) — 전면 리팩토링
- `using System.Collections; using LostMemory.VFX;` 추가
- 삭제: 정적 색 상수 (`FreezeVisualColor` 등), placeholder 필드 (`_freezeVisual`, `_burnVisual` 등), placeholder 메서드 ~80줄 (`SetFreezeVisualActive`, `EnsureFreezeVisual`, `SetBurnVisualActive`, `EnsureBurnVisual`)
- 신규 SerializeField: `_vfxFadeOutSeconds = 0.25f`
- 신규 private 필드: VFX prefab 3개, `_vfxPrefabsBound`, `_slowTintColor`, `_burnTintColor`, `StatusVisualType` enum, `_activeVisualType`, `_activeVisualInstance`, `_enemyMainSprite`, `_enemyOriginalColor`, `_enemyColorCached`
- 신규 public 메서드: `EnsureVFXPrefabs(freeze, burn, slow)`, `SetSlowTintColor(color)`, `SetBurnTintColor(color)`
- 신규 private 메서드: `SetActiveVisual`, `RefreshActiveVisual`, `OnEnterVisualType`, `OnLeaveVisualType`, `FadeOutAndDestroy` (코루틴), `TryCacheEnemySprite`, `ApplySlowSpriteTint` / `RemoveSlowSpriteTint`, `ApplyBurnSpriteTint` / `RemoveBurnSpriteTint`
- `ApplySlow / ApplyFreeze / ApplyBurn` 끝에 `RefreshActiveVisual()` 호출 추가
- `Update` — 만료 시 `visualChanged` 추적 → `RefreshActiveVisual()` 호출
- `LateUpdate` — Slow/Burn type 별로 매 프레임 sprite tint 강제
- `Awake` — `TryCacheEnemySprite()` 미리 호출
- `OnDestroy` — `_activeVisualInstance` 정리 + `RemoveSlowSpriteTint()` 안전 호출

### 신규 프리팹

| 프리팹 | 구성 |
|---|---|
| `_Project/Prefabs/Effect/FreezeStatusVFX.prefab` | IceBlockGroup(Shivering) → IceBlockBack/Front + CrystalParticles |
| `_Project/Prefabs/Effect/BurnStatusVFX.prefab` | FireSprite(사용자 자체 처리) + SmokeParticles + LightFlicker |

> SlowStatusVFX는 만들지 않음. `_slowStatusVFXPrefab` 슬롯은 비어있어도 OK — sprite tint만으로 시각 표현

## 핵심 동작

### 정책 D 흐름

```
ApplyXxx 호출 (OnHitEffectRegistry → EnemyStatusEffect)
  ↓
EnsureVFXPrefabs (1회 바인딩) + SetXxxTintColor (매번 push)
  ↓
ApplyXxx — magnitude/duration 갱신
  ↓
RefreshActiveVisual()
  ↓
freezeActive ? Freeze : burnActive ? Burn : slowActive ? Slow : None
  ↓
SetActiveVisual(type, prefab)
  ↓
- 기존 instance 있으면 FadeOutAndDestroy 백그라운드
- OnLeaveVisualType(prevType) — 떠나는 type 의 tint 제거
- OnEnterVisualType(type) — 들어오는 type 의 tint 적용
- 새 prefab 있으면 SpawnAttached 로 적에 부착
```

### Sprite Tint 매 프레임 강제 (LateUpdate)

```csharp
switch (_activeVisualType)
{
    case StatusVisualType.Slow: _enemyMainSprite.color = _slowTintColor; break;
    case StatusVisualType.Burn: _enemyMainSprite.color = _burnTintColor; break;
}
```

다른 시스템(Health 데미지 플리커 등)이 Update에서 색을 덮어써도 LateUpdate가 마지막 → 우리 tint가 최종 결과.

### Sprite 캐시 휴리스틱

```csharp
GetComponentsInChildren<SpriteRenderer>()
  → name 에 'shadow' / 'vfx' / 'effect' / 'status' / 'iceblock' 포함된 자식 제외
  → 남은 후보 중 bounds.size.x * bounds.size.y 가 가장 큰 sprite 선택
```

대부분의 적이 `AdventurerShadow` 같은 보조 sprite를 갖고 있어 단순 `GetComponentInChildren`이 그림자를 잡는 문제 해결.

## 검증 결과

- 얼음 세트 (`정전기 띠` 외 얼음 듀얼 1개) → SlowOnHit만 발동 → 적이 푸르스름하게 변색 ✅
- 빙결 권갑 → 평타마다 0.5초 Freeze 발동 → IceBlock + 결정 입자 + 덜덜거림 ✅
- 불 빌드 → Burn DoT + Fire/Smoke/Light + 주황 tint ✅
- 정책 D — 동시 발동 시 Freeze 1개만, 효과는 모두 들어감 ✅
- 페이드아웃 0.25초 후 다음 우선순위 효과로 자연스러운 전환 ✅
- 적 사망 시 VFX 잔상 없이 OnDestroy에서 정리 ✅
- 데미지 플리커 후에도 sprite tint 유지 (LateUpdate 강제 적용 효과) ✅
- 인스펙터에서 tint 색 변경 시 다음 ApplyXxx 부터 즉시 반영 ✅

## 알려진 이슈 / 보류

- **SFX 자산 미할당** — 슬롯 노출됨, 자산 결정 후 인스펙터에서 채우기 (코드 변경 X)
- **`SlowStatusVFX.prefab` 슬롯 비어있음** — sprite tint로 대체했으니 의도적. 미래에 보조 particle 추가하고 싶으면 prefab 만들어서 슬롯에 채우면 됨 (코드 변경 0)
- **TopDownEngine 데미지 플리커 가려짐** — Slow/Burn 활성 중에는 LateUpdate가 매 프레임 tint 강제라 빨간 플리커가 거의 안 보임. 사용자 의도 부합 (계속 푸르스름/주황)
- **Sprite 검색 휴리스틱 한계** — name 기반이라 적 프리팹 네이밍 컨벤션 변경 시 영향. 이름에 "shadow" 포함된 본체 sprite가 있으면 제외됨 (드물 듯)
- **A→B 마이그레이션** — 미래에 적별 VFX 커스터마이징 필요 시 EnemyStatusEffect를 적 프리팹에 미리 부착 + SerializeField로 마이그레이션 (별도 티켓)
- **`_debugFreezeOverrideSeconds`** — 디버깅 잔존 SerializeField. 0이 default라 실제 동작 영향 X. 추후 유사 디버깅에 재활용 가능

## 인스펙터 와이어링 (플레이어 프리팹 OnHitEffectRegistry)

| 슬롯 | 값 |
|---|---|
| Freeze Status VFX Prefab | `Assets/_Project/Prefabs/Effect/FreezeStatusVFX.prefab` |
| Burn Status VFX Prefab | `Assets/_Project/Prefabs/Effect/BurnStatusVFX.prefab` |
| Slow Status VFX Prefab | (비워둠) |
| Slow Tint Color | `(0.7, 0.9, 1, 1)` |
| Burn Tint Color | `(1, 0.6, 0.4, 1)` |
| Freeze/Burn/Slow Apply Sfx | (미할당 OK) |
| Status Sfx Volume | 0.6 |
| Debug Freeze Override Seconds | 0 (디버깅 시 5 등으로 임시 강제 가능) |

## 미래 개선

- **A→B 마이그레이션** — 적별 VFX 커스터마이징 필요 시
- **음성 자산 결정** — Freeze/Burn/Slow 적용 음 + Chain/Wind 적중 음 (CL-201 슬롯도 함께)
- **`SlowStatusVFX.prefab` 추가 (선택)** — sprite tint만으론 부족하다 판단되면 발 밑 안개 등 보조 particle 추가
- **AudioSource.PlayClipAtPoint 풀링** — Chain/Wind/Status 모든 SFX 호출 GC 부담 통합 처리

---

## 추가 작업 — 평타 hitbox 튜닝 도구 + 디버그 viz default OFF

CL-202 본 작업 직후 발견된 평타 hitbox/VFX 관련 이슈 정리. 별도 티켓이 아니라 같은 흐름의 후속 정비라 본 md 에 기록.

### 배경

`Sword_Default.asset` 의 step 별 `hitboxOffset` 이 비대칭 y 값(1타: y=-0.2, 2타: y=+0.2, 3타: y=0)을 가져, 좌우 공격 시 Quaternion 180° 회전이 y 부호도 뒤집어 hitbox/VFX 가 좌우로 어긋나 보이는 현상 발생. 또한 `showRuntimePreview` / `drawDebugGizmos` default 가 `true` 라 인게임에 빨간 박스가 매 평타 노출.

### 변경 1: `WeaponData.GlobalHitboxPostRotationOffset` 신규

회전 *후* 더해지는 글로벌 오프셋 → 좌/우/상/하 어느 방향이든 같은 양만큼 시프트 (Quaternion 영향 X, 좌우 대칭 보장).

수정 파일:

| 파일 | 변경 |
|---|---|
| [WeaponData.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Data/WeaponData.cs) | `_globalHitboxPostRotationOffset` SerializeField + `GlobalHitboxPostRotationOffset` accessor 추가 |
| [KhiMeleeHitbox.cs:39](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeHitbox.cs:39) | `Sample` 시그니처에 `Vector2 globalPostRotationOffset` 파라미터 추가, 회전 후 더함 |
| [KhiMeleeComboController.cs:235](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs:235) | `Sample` 호출 시 `weaponData.GlobalHitboxPostRotationOffset` 전달 |
| [KhiAttackVisualPresenter.cs:118](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiAttackVisualPresenter.cs:118) | VFX center 계산에 동일 offset 적용 (`_comboController.WeaponData.GlobalHitboxPostRotationOffset`) |

### 변경 2: 디버그 viz default OFF

`KhiMeleeHitbox.cs` 상단 SerializeField default 변경:

| 필드 | 기존 default | 변경 후 |
|---|---|---|
| `drawDebugGizmos` | `true` | `false` |
| `showRuntimePreview` | `true` | `false` |

Tooltip 도 추가 — 디버깅 시만 ON.

### 동작

```
오른쪽 공격 (aim 0°):
  rotated = Q(0°) * (1.05, 0) = (1.05, 0)
  center = origin + (1.05, 0) + (0, 0.5) = origin + (1.05, 0.5)

왼쪽 공격 (aim 180°):
  rotated = Q(180°) * (1.05, 0) = (-1.05, 0)
  center = origin + (-1.05, 0) + (0, 0.5) = origin + (-1.05, 0.5)
                                            ↑ y는 항상 +0.5 (대칭)
```

VFX 계산도 동일 — 어떤 방향에서도 hitbox 와 VFX 가 정확히 일치.

### 사용법 권장

`Sword_Default.asset` 인스펙터:

1. 모든 step 의 `hitboxOffset.y` 를 **0** 으로 통일 (좌우 회전 시 y 흔들림 0)
   - 1타: `(1.05, -0.2)` → `(1.05, 0)`
   - 2타: `(1.05, +0.2)` → `(1.05, 0)`
   - 3타: `(1.25, 0)` → 그대로
2. **Global Hitbox Post Rotation Offset** = `(0, 0.5)` 입력 (캐릭터 발 → 몸통 중앙으로 시프트)

이러면 모든 swing 이 캐릭터 발 기준 1.05~1.25 앞 + 0.5 위에 일관 발생, 좌우 완벽 대칭.

### 인스펙터 라이브 튜닝

Play 중에도 `Global Hitbox Post Rotation Offset` 값 조정 시 다음 평타부터 즉시 반영. 영구 저장은 SO 우클릭 → "Save Current Values".

### 주의

- 기존 플레이어 프리팹의 `KhiMeleeHitbox` 컴포넌트는 이전 default 값(`true`) 으로 직렬화되어 있을 수 있음 → 인스펙터에서 직접 OFF 처리 필요 (1회). 신규 프리팹부터는 자동 OFF
- `Sample` 시그니처 변경되었으니 외부 호출자가 있다면 인자 추가 필요. 현재 호출자는 KhiMeleeComboController 1곳뿐 (확인됨)
