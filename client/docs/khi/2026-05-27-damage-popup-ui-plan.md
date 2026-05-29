# 데미지 / CRITICAL Popup UI 계획

## Context

플레이어가 몬스터에게 입히는 데미지가 화면에 표시되지 않아서 — 평타 한 방이 얼마나 들어가는지, 크리티컬이 터졌는지 — 플레이어가 체감하기 어렵다. 스탯 시스템 (`StatId.AttackPower`, `StatId.Critical`, `StatId.CriticalDamage`)이 이미 구현돼 있고 [KhiMeleeComboController.cs:336-349](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs:336)에서 finalDamage 계산과 크리티컬 판정이 끝나지만, **결과를 시각화하는 경로가 없다.**

목표: 몬스터에 데미지가 적용되는 시점에 월드 공간 floating text(숫자)를 띄우고, 크리티컬일 땐 별도 라벨("CRITICAL!") + 색·크기 차별화. **본인 데미지만 표시**(멀티 동기화 없음, 호스트 권위 그대로).

재사용 가능한 기존 자산:
- TDE `MMFloatingTextSpawner` / `MMFloatingTextSpawnEvent` — `Assets/TopDownEngine/ThirdParty/MoreMountains/MMFeedbacks/MMFeedbacks/MMFloatingText/`
- `KhiMeleeComboController.TargetHit` 이벤트 — [line 57](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs:57) (단, 시그니처 확장 필요)
- `OnHitEffectRegistry` — 이미 `TargetHit` 구독 중, 보조효과(체인/풍속/화상) 데미지 적용 지점을 모두 가지고 있음

## 설계

### 데이터 흐름

```
KhiMeleeComboController (finalDamage + isCritical 계산)
  → TargetHit(request, step, hit, finalDamage, isCritical) 이벤트
  → DamagePopupSpawner (구독자, 새 컴포넌트)
       → 카테고리/스타일 결정 (Normal / Critical / SubEffect)
       → MMFloatingTextSpawnEvent.Trigger(value, position, ...)

KhiArrowProjectile (화살 hit) → 같은 DamagePopupSpawner 진입점

OnHitEffectRegistry.ApplyChain / ApplyWindBlade / ApplyBurn (보조효과 데미지)
  → DamagePopupSpawner 진입점 (카테고리=SubEffect)
```

### 적용 범위 (인스펙터 토글)

`DamagePopupSpawner`에 카테고리별 `bool` SerializeField — 기본값:

| 카테고리 | 기본 ON/OFF | 비고 |
|---------|------------|------|
| Melee (평타) | ON | 가장 중요. 크리티컬 가시화 핵심 |
| Arrow (화살) | ON | 주력 원거리 |
| SubEffect (체인/풍속/화상/도트) | **OFF (기본)** | 시끄러우면 끔. 디버그/튜닝 시 켬 |

→ "너무 시끄러우면 줄일지" 요구를 인스펙터 한 줄로 해결.

### CRITICAL 연출 (옵션 2 채택)

- 일반: 흰색 / 기본 크기 / 텍스트 = `"123"`
- 크리: 노랑(또는 주황) / 1.5~2배 크기 / 텍스트 = `"CRITICAL!\n123"` (2줄) 또는 `"123 CRITICAL!"`

스타일은 `DamagePopupStyle` ScriptableObject로 분리:
- `Color normalColor, criticalColor, subEffectColor`
- `float normalScale, criticalScale, subEffectScale`
- `string criticalPrefix` (= `"CRITICAL!"`, 인스펙터에서 다국어/이미지화 시점에 교체)
- `MMFloatingTextSpawnerSettings` 참조 (어느 spawner에 trigger 쏠지)

이 SO 구조 덕에 나중에 (1) "CRITICAL!" 문자열 → 이미지 prefab으로 교체, (2) 데미지 카테고리 추가 시 스타일만 늘리면 됨 — 코드 안 건드림.

## 핵심 파일

**수정**:
- `client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs`
  - `TargetHit` 이벤트 시그니처 확장: `Action<KhiAttackRequest, AttackStepData, Health, float, bool>` (finalDamage, isCritical 추가)
  - line 343-349 크리티컬 분기에서 `bool wasCritical` 로컬 변수 잡고 line 358 invoke에 전달
- `client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/OnHitEffectRegistry.cs`
  - 위 시그니처 변경 따라 `HandleHit` 메서드 파라미터 업데이트 (signature break — 외부 구독자 없으니 안전)
  - `ApplyChain` / `ApplyWindBlade` / 도트 / 화상 등 추가 데미지 적용 직후 `DamagePopupSpawner.NotifySubEffectDamage(target, amount)` 호출 (포지션 = target.transform.position)
- `client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiArrowProjectile.cs`
  - hit 처리 부분에서 (호스트 측 데미지 적용 직후) `DamagePopupSpawner.NotifyArrowDamage(target, finalDamage, wasCritical)` 호출
  - 화살 측 크리티컬 계산이 이미 있는지 확인 → 없으면 평타와 동일 패턴으로 `StatId.Critical` 적용

**새로 만듦**:
- `client/LostMemory/Assets/_Project/Scripts/Runtime/UI/DamagePopupSpawner.cs` — 싱글톤 MonoBehaviour, 씬에 1개. 카테고리별 토글 + MMFloatingTextSpawner 참조 보유. API: `NotifyMeleeDamage(Health target, float amount, bool isCritical)`, `NotifyArrowDamage(...)`, `NotifySubEffectDamage(...)`.
- `client/LostMemory/Assets/_Project/ScriptableObjects/UI/DamagePopupStyle.cs` — ScriptableObject 정의
- `client/LostMemory/Assets/_Project/ScriptableObjects/UI/DamagePopupStyle.asset` — 인스턴스 1개 (인스펙터 튜닝용)
- **Prefab**: `MMFloatingText` 기반 데미지 텍스트 프리팹 1개. TMP_Text + MMFloatingText 컴포넌트. 위치: `_Project/Prefabs/UI/DamageFloatingText.prefab`
- **Scene 배치**: 플레이어 씬(TestKhi 씬 + 던전 보스 씬 등) bootstrap에 `MMFloatingTextSpawner` GameObject + `DamagePopupSpawner` GameObject 추가. 둘 다 `DontDestroyOnLoad` 옵션 — 씬 전환마다 재설정 안 하도록.

### 멀티플레이 처리

- `KhiMeleeComboController.TargetHit`은 **로컬 플레이어 코드 흐름**에서만 발화 (자기 캐릭터의 평타 결과). 따라서 본인 데미지만 자동으로 표시됨 — 별도 owner 체크 불필요.
- 단, **게스트의 경우 호스트가 최종 데미지를 결정**하므로 게스트 클라이언트에 표시되는 숫자가 호스트에서 본 숫자와 1tick 안에서 일치하지 않을 수 있음. 게스트는 자신이 보낸 RPC 결과를 기다리지 않고 **로컬에서 계산한 finalDamage·isCritical을 그대로 popup 표시**. 정확도보다 즉시성 우선. (호스트 권위 데미지 계산 차이는 보통 무시 가능 — 동일 stat 사용 시 결과 동일)
- 보조효과(`OnHitEffectRegistry`)는 [line 194](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/OnHitEffectRegistry.cs:194)에서 **호스트만 실행** 가드가 있음. 게스트에서는 보조효과 popup이 안 뜨는 게 자연스러움 (게스트는 어차피 보조효과 발화 주체가 아님) — 의도된 동작이므로 추가 RPC 불필요.

## 구현 순서

1. `DamagePopupStyle` SO + `DamageFloatingText.prefab` 만들기 (에디터 작업) — 시각 확인 먼저
2. `DamagePopupSpawner` 작성 + 씬 1곳에 배치 → 디버그 키로 `NotifyMeleeDamage` 호출해서 popup 떠오르는지 확인
3. `KhiMeleeComboController.TargetHit` 시그니처 확장 + `OnHitEffectRegistry.HandleHit` 시그니처 따라잡기
4. `DamagePopupSpawner`를 `TargetHit`에 구독 (씬 bootstrap에서 wiring) → 평타 크리티컬 정상 표시 확인
5. `KhiArrowProjectile` hook 추가
6. `OnHitEffectRegistry` 보조효과 데미지 hook 추가 (기본 OFF 상태로 토글)

각 단계마다 에디터에서 한번씩 돌려보고 다음 단계로.

## 검증

Unity 에디터에서:
1. **평타 일반**: 몬스터 옆에서 좌클릭 평타 → 흰색 숫자가 몬스터 머리 위로 떠올랐다 사라지는지
2. **평타 크리티컬**: `KhiMeleeComboController`의 `StatId.Critical` 인스펙터에서 critChance를 1.0+1.0(=100%)로 임시 override → 모든 타격이 크리티컬, 노란색 + 큰 크기 + `"CRITICAL!"` 라벨 확인
3. **화살**: 활 쏴서 명중 시 popup 표시. 크리티컬 toggle 동작 확인
4. **보조효과 OFF (기본)**: 체인/풍속 발동해도 popup 안 뜨는지
5. **보조효과 ON (디버그)**: `DamagePopupSpawner.showSubEffect = true`로 켜고 → 작은 회색 숫자가 추가로 뜨는지
6. **멀티 (호스트 + 게스트)**: 두 플레이어가 같은 몬스터를 동시에 때릴 때 — 호스트는 자기 데미지만, 게스트도 자기 데미지만 보이는지
7. **씬 전환**: 던전 진입/포탈 이동 후에도 `DamagePopupSpawner`가 살아있어 popup이 계속 뜨는지 (DontDestroyOnLoad 검증)

## 의도적으로 안 하는 것

- **데미지 누적 합산 표시** (예: "152 (총 1234)") — 범위 밖
- **DPS 미터 / 데미지 로그 패널** — 별도 요구사항
- **적이 입은 데미지 vs 적이 준 데미지 구분** — 본 작업은 "내가 준 데미지"만
- **`MMFloatingTextSpawner`를 직접 prefab/scene에 두지 않고 코드에서 spawn** — TDE 의도된 사용법 따름. 한번 spawn하면 이벤트 기반으로 동작
- **MonsterHealthSync 측 데미지 변화 미러링** — "내 데미지만 표시" 결정에 따라 불필요
