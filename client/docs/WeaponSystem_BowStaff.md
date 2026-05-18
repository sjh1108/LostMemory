# 활(Bow) + 스태프(Staff) + 마나 시스템 개발 완료 정리

## 개요

기존 검(Khi) 시스템에 더해 **활·스태프 두 무기** + **공용 마나 시스템** + **마우스 휠 무기 모드 전환**을 추가했다. 모든 무기는 검 자체 구현(`KhiMeleeComboController` 패턴)을 따라 자체 시스템으로 작성됐으며, TopDown Engine 무기 시스템에 의존하지 않는다.

세 무기 모두 한 번에 하나만 활성되고 마우스 휠로 순환 전환된다. 우클릭 입력은 모드별로 다르게 매핑된다 (검=패링, 활=연사, 스태프=차징 분기).

---

## 신규 스크립트

### Combat
| 파일 | 경로 | 역할 |
|------|------|------|
| `PlayerMana.cs` | `_Project/Scripts/Runtime/Combat/` | 마나 데이터 (`CurrentMana`, `MaxMana`, `Consume`, `Recover`, `ManaChanged` 이벤트) + 자동 회복 |

### UI
| 파일 | 경로 | 역할 |
|------|------|------|
| `PlayerManaPresenter.cs` | `_Project/Scripts/Runtime/UI/` | 로컬 플레이어 `PlayerMana` 자동 resolve → `HealthBarView.UpdateMP` 호출 |

### TestKhi (무기 시스템)
| 파일 | 역할 |
|------|------|
| `KhiArrowProjectile.cs` | 모든 발사체 공통 (활 화살, 파이어볼, 마법탄, 얼음탄). 발사·비행·데미지 + impact Animator + homing |
| `KhiBowController.cs` | 활 — 좌클릭 단발 / 우클릭 hold 연사 |
| `KhiBowPresenter.cs` | 활 sprite 회전 (`transform.position` 기준 마우스 방향) |
| `KhiBowAnimator.cs` | 활 발사 시퀀스 (Bow_4 Draw 1~3 → Release) |
| `KhiStaffController.cs` | 스태프 — 좌클릭 마법탄 연사 / 우클릭 짧게 파이어볼 / 우클릭 길게 메테오 |
| `KhiStaffPresenter.cs` | 스태프 sprite 회전 + `localScale.y` 부호 반전으로 자식 Tip 미러 |
| `KhiStaffChargeBarView.cs` | 차징바 UI (procedural sprite + Fill scale + Ready 색 변경) |
| `KhiMeteor.cs` | 메테오 — 예고(펄스 + magicCircle 회전) + 낙하 + 폭발 + OverlapCircle 데미지 |
| `WeaponModeController.cs` | 검↔활↔스태프 모드 전환 (마우스 휠 + Q 키 백업) |

---

## 신규 Prefab

| Prefab | 경로 | 설명 |
|--------|------|------|
| `KhiArrow.prefab` | `_Project/Prefabs/Weapons/Bow/` | 활 화살 — Arrow_1 sprite, destroyOnHit=true |
| `KhiFireball.prefab` | `_Project/Prefabs/Weapons/Staff/` | 파이어볼 — RafaelMatos Fireball Animator (fireball + fireball-destroy state), destroyOnHit=true |
| `KhiIceBolt.prefab` | `_Project/Prefabs/Weapons/Staff/` | 얼음탄 — IceBall.controller (iceball + iceball destroy state) |
| `KhiMeteor.prefab` | `_Project/Prefabs/Weapons/Staff/` | 메테오 root + 자식 (MeteorWarning + magiccircle + MeteorExplosion) |
| `KhiStaffChargeBar.prefab` | `_Project/Prefabs/Weapons/Staff/` | 차징바 UI (Background + Fill 자식) |
| `KhiMeteorBody.prefab` | `_Project/Prefabs/Weapons/Staff/` | (현재 미사용 — KhiFireball 복제본) |

---

## 동작 흐름

### 무기 모드 전환
- **마우스 휠 위/아래** → `Sword → Bow → Staff → Sword` (순환)
- 백업: Q 키 (`switchKey`)
- `WeaponModeController.ApplyMode`가 각 모드의 컴포넌트 `enabled` 토글 + sprite GameObject `SetActive` 토글
- 입력 충돌 방지 (활/스태프 모드에서 검 우클릭 패링 안 발동 등)

### 검 (기존)
- 좌클릭: `KhiMeleeComboController` 콤보
- 우클릭: `KhiParryController` 패링

### 활
- 좌클릭: 단발 화살 (`singleShotInterval = 0.5s`)
- 우클릭 hold: 빠른 연사 (`rapidShotInterval = 0.12s`, 데미지 70%)
- 마나: 자동 회복만 (활은 무료)

### 스태프
- 좌클릭 hold: 마법탄 자동 연사 (`boltInterval = 0.15s`, 무료, 약한 유도 `boltHomingTurnRate = 90` deg/sec)
- 우클릭 release `< 0.5초`: 파이어볼 (관통, 마나 50)
- 우클릭 release `≥ 0.5초`: 메테오 (광역, 마나 100)
- 차징바: 캐릭터 머리 위 자동 표시 + 0.5초 도달 시 황금색

### 메테오 시퀀스
1. `Detonate()` → `ShowWarning(true)` + `StartCoroutine(FallMeteor)`
2. **0~1초**: 예고(MeteorWarning 펄스 + magiccircle 회전) + MeteorExplosion이 `(0, 8, 0)`에서 `(0, 0, 0)`으로 Lerp + Animator default state `fireball` loop
3. **1초 도착**: `ShowWarning(false)` + `Animator.Play("fireball-destroy")` (폭발 시퀀스) + `OverlapCircleAll` 데미지
4. **0.3초 후**: `Destroy(gameObject)`

### 마나
- `maxMana = 100`, 자동 회복 `5/sec`
- 스킬 사용 시 소비 (파이어볼 50, 메테오 100, 마법탄 0)
- HUD MP 바 자동 표시 (`PlayerHUD.prefab`에 `PlayerManaPresenter` 부착)

---

## KhiArrowProjectile 기능 (모든 발사체 공용)

| 기능 | 필드 | 비고 |
|------|------|------|
| 기본 발사 | `Launch(dir, speed, damage, attacker)` | - |
| 수명 | `maxLifetime` (3초) | 자동 destroy |
| 관통/단발 | `destroyOnHit` | true=단발, false=관통 |
| Impact 애니 | `impactAnimator`, `impactStateName`, `impactHoldDuration` | hit 시 Animator.Play + 지연 destroy |
| 유도 | `homingTurnRateDegPerSec`, `homingDetectionRadius`, `homingDelay` | 0 = 직선 |
| 외부 override | `SetHoming(turnRate, radius)` | KhiStaffController가 마법탄 발사 시 호출 |
| 디버그 로그 | `logProjectileEvents` | 검증용 |

---

## Sprite Import 변경 (Texture → Sprite mode)

- **활/화살**: Bow_1, Bow_4, Arrow_1, Bow_4 애니메이션 5장 (Bow4, Bow4_Draw_1~3, Bow4_Release_1)
- **스태프**: Staff_1
- **메테오**: magic_05, fire_01 (현재 MeteorWarning sprite + 폐기 sprite)

Sprite mode 설정: `textureType: 8`, `spriteMode: 1`, `spritePixelsToUnits: 16`, `filterMode: Point`, `wrapMode: Clamp`, `alphaIsTransparency: 1`

---

## 재사용 자산 (RafaelMatos)

| Asset | 사용처 |
|-------|--------|
| `Fireball.controller` (`bd06ff85be7397c46984e0e433283fed`) | KhiFireball visual 자식 + KhiMeteor MeteorExplosion (state: fireball, fireball-destroy) |
| `IceBall.controller` (`f1d6b17c065de9340a0cd712352203b7`) | KhiIceBolt visual 자식 (state: iceball, iceball destroy) |
| `FlameEnergyProjectile.controller` (`4f4955ab91390c946a214687031f45ba`) | (이전에 KhiMeteor에 사용 → 현재 Fireball.controller로 교체됨) |
| Fireball sprite (`a13fdadb50dbf6c4497d60101174505e`) | KhiFireball, KhiMeteor MeteorExplosion |
| Ice projectile sprites (`3a21515680322e04caaf73e054fe47a0`) | KhiIceBolt visual |

---

## 캐릭터 prefab 부착 컴포넌트

`TestKhi_MinimalCharacter2D.prefab`에 사용자가 inspector로 부착:

### 캐릭터 root
- `PlayerMana` (마나 데이터)
- `KhiBowController`, `KhiBowPresenter` (활)
- `KhiStaffController` (스태프 — `chargeBarPrefab`에 `KhiStaffChargeBar.prefab` 연결)
- `WeaponModeController` (모드 전환 — sword/bow/staff 컴포넌트들 reference 연결)
- `WeaponSlotSwitcher` (TDE 기반, 사실상 미사용)

### 자식 GameObject
- `BowVisual` → SpriteRenderer + 활 sprite
- `StaffVisual` → SpriteRenderer + 스태프 sprite + 자식 **`Tip`** (스태프 끝 발사 위치)
- `KhiStaffChargeBar` (Awake에서 KhiStaffController가 자동 Instantiate)

---

## HUD

`PlayerHUD.prefab`에 `PlayerManaPresenter` 컴포넌트 자동 부착됨. `HealthBarView`의 MP 바를 로컬 플레이어 `PlayerMana`와 자동 연결.

---

## 결정 사항 (사용자 확정)

| 항목 | 결정 |
|------|------|
| MVP 스태프 강화 | 마법사 1단계 base (강화 path 추후) |
| 모드 전환 | 마우스 휠 |
| 스태프 입력 | 좌=마법탄, 우 짧=파이어볼, 우 길=메테오 |
| 마나 | 자동 회복만 + 스킬 소비 |
| 파이어볼 | 단발 관통 → 추후 destroyOnHit=true로 변경, impact 애니 추가 |
| 메테오 | 1초 예고 + 광역 폭발 + 낙하 시각 (위에서 직선) |
| 차징 임계값 | 0.5초 (inspector 노출) |
| 마법탄 유도 | 90 deg/sec (약한 유도) |
| 활/스태프 sprite | 16x16 RPG Item Icons (Bow_4, Staff_1) + RafaelMatos (불꽃·얼음) |

---

## 미해결 / 추후 작업

- 강화 path (트페/마법사/비숍) — 사용자 결정 추후
- 속성 시스템 (Fire/Water/Electric/Wind) — MVP 이후
- 유물 시스템 — MVP 이후
- 봉 무기
- 디버그 로그 일괄 끄기 (`logProjectileEvents`, `logMeteorEvents`, `logSkillsToConsole`, `logModeChanges`)
- KhiMeteorBody.prefab 폐기 정리
- 마법탄 sprite 진짜 마법구로 교체 (현재 Arrow_1 placeholder)
- 활 KhiBowAnimator visual 검증
- TDE 기반 잔재 정리 (`BowDualMode.cs`, `WeaponSlotSwitcher.cs`, `KhiBow.prefab` — Khi 자체 시스템 마이그레이션 후 미사용)

---

## 폐기/마이그레이션 이력

| 폐기 | 사유 |
|------|------|
| `BowDualMode.cs` | TDE ProjectileWeapon 의존 — Khi 자체 시스템으로 재구현 |
| `WeaponSlotSwitcher.cs` | TDE CharacterHandleWeapon.ChangeWeapon 의존 — `WeaponModeController`로 대체 |
| `KhiBow.prefab` | TDE 기반 거대 prefab — `KhiArrow.prefab` + `KhiBowController`로 대체 |
| `Koala.prefab` 가정 | 실제 캐릭터는 `TestKhi_MinimalCharacter2D.prefab` — 검 자체 시스템이 별도 구축됨 |
| `KhiMeteorBody.prefab` | 별도 Instantiate 방식 → MeteorExplosion 자식 직접 활용으로 단순화 |

---

## 검증 시나리오

1. Play 모드 진입 → HUD에 HP/MP 바 둘 다 표시 (MP 0/100에서 자동 회복 시작)
2. 마우스 휠로 검 → 활 → 스태프 순환
3. **검**: 좌클릭 콤보, 우클릭 패링
4. **활**: 좌클릭 단발, 우클릭 hold 연사 (마나 무관)
5. **스태프**:
   - 좌클릭 hold → 마법탄 뿅뿅 (약한 유도, 무료)
   - 우클릭 짧게 → 파이어볼 발사 (mana -50, 관통, impact 애니)
   - 우클릭 길게 (0.5초+) → 머리 위 차징바 황금색 → release 시 메테오 시전
     - 마우스 위 8 유닛에서 fireball Animator loop 떨어짐 → 도착 후 fireball-destroy 폭발 + 광역 데미지

---

## 핵심 파일 경로

### Scripts
```
LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerMana.cs
LostMemory/Assets/_Project/Scripts/Runtime/UI/PlayerManaPresenter.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiArrowProjectile.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiBowController.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiBowPresenter.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiBowAnimator.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiStaffController.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiStaffPresenter.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiStaffChargeBarView.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeteor.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/WeaponModeController.cs
```

### Prefabs
```
LostMemory/Assets/_Project/Prefabs/Weapons/Bow/KhiArrow.prefab
LostMemory/Assets/_Project/Prefabs/Weapons/Staff/KhiFireball.prefab
LostMemory/Assets/_Project/Prefabs/Weapons/Staff/KhiIceBolt.prefab
LostMemory/Assets/_Project/Prefabs/Weapons/Staff/KhiMeteor.prefab
LostMemory/Assets/_Project/Prefabs/Weapons/Staff/KhiStaffChargeBar.prefab
LostMemory/Assets/_Project/Prefabs/Weapons/Staff/KhiMeteorBody.prefab  # 미사용
```

### Modified
```
LostMemory/Assets/_Project/Scripts/Runtime/UI/PlayerHUDPresenter.cs  # MP 책임 분리 (PlayerManaPresenter로 이관)
LostMemory/Assets/_Project/Prefabs/UI/PlayerHUD.prefab  # PlayerManaPresenter 컴포넌트 추가
LostMemory/Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab  # 캐릭터 root에 무기 시스템 컴포넌트 부착 (사용자 작업)
```
