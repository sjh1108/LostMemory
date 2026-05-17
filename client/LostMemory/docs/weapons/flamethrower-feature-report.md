# 화염방사기(Flamethrower) 무기 시스템 개발 보고서

> **개발 기간**: 2026-05-18
> **담당**: gimhoein
> **브랜치**: feature/flamethrower (별도 작업 브랜치)

---

## 1. 개요

### 1.1 배경
무기 진화 트리(활 → ? → 대포)의 **2번째 티어(강화)** 자리에 들어갈 무기로 화염방사기를 선택. 활(단발 정확 사격)과 대포(폭발 AoE) 사이의 자연스러운 진화 단계로 "지속 광역 데미지(DoT)" 컨셉을 구현.

### 1.2 결정 사항 (디자인 단계)
- **무기 타입**: 화염방사기 (vs 총 비교 후 선택 — 차별화된 게임플레이가 결정 이유)
- **입력 방식**: 좌클릭 hold = Primary, 우클릭 hold = Secondary (강화)
- **자원**: 마나 기반 (PlayerMana 재사용, 별도 연료 시스템 안 만듦)
- **동시 입력**: 우클릭 우선 (좌 분사 중 우 누르면 자동 전환)
- **콘 형상**: Primary/Secondary 각각 독립적인 거리·각도

---

## 2. 게임플레이 동작

### 2.1 입력 → 행동 매핑

| 입력 | 행동 | 마나 소비 | VFX |
|---|---|---|---|
| 좌클릭 hold | Primary 분사 (기본) | 8 mana/sec | Fire_VFX |
| 우클릭 hold | Secondary 분사 (강화) | 20 mana/sec | Fire_VFX_upgrade |
| 좌+우 동시 | 우클릭 우선 (좌 자동 중단) | 20 mana/sec | Secondary 표시 |
| 우 → 좌 (release) | 좌가 hold 중이면 자동 복귀 | 8 mana/sec | Primary 재개 |
| 마나 0 | 분사 자동 중단 | — | 정지 |

### 2.2 데미지 / 화상 시스템

| 항목 | Primary | Secondary |
|---|---|---|
| 콘 거리 (Range) | 3.5m | 5.5m |
| 콘 절반 각도 | 30° (총 60°) | 40° (총 80°) |
| 직접 DPS | 15 | 35 |
| 데미지 틱 간격 | 0.1초 | 0.08초 |
| 화상 DPS | 3 | 8 |
| 화상 지속 | 2초 | 3초 |

> **화상(Burn)**: 기존 `EnemyStatusEffect.ApplyBurn` 재사용. 콘 영역 이탈 후에도 적에게 지속 데미지.

---

## 3. 기술 아키텍처

### 3.1 파일 구조

```
LostMemory/Assets/_Project/Scripts/
├─ Runtime/TestKhi/
│  ├─ KhiFlamethrowerController.cs   (입력·마나·상태)
│  ├─ KhiFlameZone.cs                 (콘 데미지·화상 적용)
│  ├─ KhiFlamethrowerPresenter.cs     (시각 회전)
│  └─ WeaponModeController.cs         (수정: Flamethrower 모드 추가)
└─ Editor/Khi/
   └─ KhiFlameZoneEditor.cs           (Scene 핸들 에디터)
```

### 3.2 컴포넌트 다이어그램

```
TestKhi_MinimalCharacter2D (캐릭터 root)
├─ WeaponModeController           ← 무기 모드 전환 (Q/마우스휠)
├─ PlayerMana                     ← 기존 — 마나 자원 관리
├─ KhiFlamethrowerController      ← 입력/상태/마나 제어
├─ KhiFlameZone                   ← 콘 데미지 계산·적용
└─ FlamethrowerVisual (자식 GameObject)
   ├─ KhiFlamethrowerPresenter   ← 마우스 방향으로 회전
   ├─ Fire_VFX                    ← Primary ParticleSystem
   └─ Fire_VFX_upgrade            ← Secondary ParticleSystem
```

### 3.3 데이터 흐름

```
1. 사용자 입력 (Mouse)
       ↓
2. KhiFlamethrowerController.Update
   - 좌/우 hold 감지, 우선순위 결정
   - HasManaForStream() 체크
   - 상태 전이: _activeStreamMode 변경
       ↓
3. StartStreaming(mode) / StopStreaming()
   - flameZone.SetStreaming(true/false, mode)
   - PlayParticles(mode) / StopParticles(mode)
       ↓
4-A. KhiFlameZone.FixedUpdate (분사 중)
   - mode 에 해당하는 range/angle 사용
   - Physics2D.OverlapBoxNonAlloc → 콘 안 적 검출
   - Health.Damage + EnemyStatusEffect.ApplyBurn
       ↓
4-B. ParticleSystem (시각)
   - FlamethrowerVisual 의 transform.rotation 이 마우스 방향
   - Fire_VFX / Fire_VFX_upgrade Play/Stop
       ↓
5. DrainStreamMana (매 프레임)
   - _manaAccumulator 누적 → 정수 단위로 PlayerMana.Consume
   - 잔량 부족 시 자동 StopStreaming
```

---

## 4. 구현 상세

### 4.1 KhiFlamethrowerController

**책임**: 입력 처리, 상태 머신, 마나 소비, ParticleSystem on/off.

**핵심 상태**:
- `_activeStreamMode` (`FlameMode?`) — `null`/`Primary`/`Secondary`
- `_manaAccumulator` (float) — 소수점 마나 누적

**우선순위 정책 코드** (Update 핵심 로직):
```csharp
FlameMode? wanted = null;
if (rightPressed && HasManaForStream()) wanted = FlameMode.Secondary;
else if (leftPressed && HasManaForStream()) wanted = FlameMode.Primary;

if (wanted != _activeStreamMode) {
    if (_activeStreamMode.HasValue) StopStreaming();
    if (wanted.HasValue) StartStreaming(wanted.Value);
}
```

**마나 소비**: PlayerMana 가 int 기반이라 소수점 누적기로 처리.
```csharp
_manaAccumulator += drainPerSec * Time.deltaTime;
int whole = Mathf.FloorToInt(_manaAccumulator);
if (whole > 0) { playerMana.Consume(whole); ... }
```

### 4.2 KhiFlameZone

**책임**: 콘 형상 영역 안 적 검출, 직접 데미지 + 화상 DoT 적용.

**모드별 파라미터** 내부 struct:
```csharp
private struct ModeParams {
    public float damagePerSec;
    public float tickInterval;
    public float burnDamagePerSec;
    public float burnDuration;
}
private ModeParams _primary;
private ModeParams _secondary;
private FlameMode _activeMode;
```

**콘 검출 알고리즘**:
1. `Physics2D.OverlapBoxNonAlloc` — 콘을 감싸는 회전된 박스로 후보 콜라이더 수집
2. 각 후보에 대해 `Vector2.Angle(aim, toTarget) > coneHalfAngleDeg` 필터링 → 부채꼴 정확도 보장
3. 거리 체크 `toTarget.sqrMagnitude > range * range`

### 4.3 KhiFlamethrowerPresenter

**책임**: 마우스 방향으로 시각 회전.

**두 가지 모드 지원**:
- **자식 SpriteRenderer 모드** (Bow/Staff 와 동일): `weaponSprite` 슬롯에 자식 드래그
- **Self 모드** (FlamethrowerVisual 처럼 ParticleSystem 만 있는 GameObject): `weaponSprite` 비워둠 → 본 transform 회전 → 자식 ParticleSystem 들이 함께 회전

### 4.4 WeaponModeController 통합

`WeaponMode` enum 에 `Flamethrower = 3` 추가. `ApplyMode` 에서:
- `flamethrowerController.enabled`
- `flamethrowerPresenter.enabled`
- `flamethrowerZone.enabled`
- `flamethrowerObject.SetActive` (FlamethrowerVisual 토글)

다른 무기 모드(검/활/스태프)와 동일한 패턴.

### 4.5 KhiFlameZoneEditor (Custom Scene Handles)

**책임**: Scene 뷰에서 콘 거리·각도를 인터랙티브하게 조절.

**시각 표시**:
- Primary 콘 (오렌지 부채꼴 + 외곽선)
- Secondary 콘 (빨강 부채꼴 + 외곽선)
- 점선 중심축
- 좌상단 라벨 (Range, Half Angle, Total Angle 실시간 표시)

**핸들** (총 6개):
- Primary: range 슬라이더 (오렌지 큰 구체) + 가장자리 핸들 × 2 (노랑 작은 구체)
- Secondary: range 슬라이더 (빨강 큰 구체) + 가장자리 핸들 × 2 (분홍 작은 구체)

**Undo 지원**: 모든 핸들 드래그가 `Undo.RecordObject` 로 기록되어 Ctrl+Z 가능.

**재사용성**: `range`, `coneHalfAngleDeg`, `secondaryRange`, `secondaryConeHalfAngleDeg` 같은 필드명을 가진 다른 무기 컴포넌트에도 그대로 적용 가능 (대포 폭발 반경, 부채꼴 검 스윙 등).

---

## 5. 사용 자산

### 5.1 비주얼 (자체 제작 / 프로젝트 내)

| 자산 | 경로 | 용도 |
|---|---|---|
| Fire_VFX (씬 인스턴스) | FlamethrowerVisual 자식 | Primary 분사 ParticleSystem |
| Fire_VFX_upgrade (씬 인스턴스) | FlamethrowerVisual 자식 | Secondary 분사 (사용자 자체 제작) |
| Kenney 화염 스프라이트 | `Art/Effects/Kenney/PNG (Transparent)/` | ParticleSystem Texture Sheet 후보 |
| Fire_VFX.prefab | `Prefabs/Effect/Fire_VFX.prefab` | 적 화상 상태 VFX (기존 시스템) |

### 5.2 사운드 (미연결)

현재 미구현. 추후 외부 조달 권장 자산:
- 분사 루프: Freesound `Foley_Object_Flame_Thrower_Loop_Stereo` (CC0)
- 시작 우슈: Freesound `Basic Fire whoosh by Niedec` (CC0)

---

## 6. Unity 셋업 가이드 (재현 절차)

### 6.1 캐릭터 프리팹 구조

```
TestKhi_MinimalCharacter2D (root)
├─ ... (기존 컴포넌트들)
├─ KhiFlamethrowerController         (root 에 Add Component)
├─ KhiFlameZone                       (root 에 Add Component)
└─ FlamethrowerVisual (빈 자식 GameObject 신규 생성)
   ├─ KhiFlamethrowerPresenter      (이 자식에 Add Component, weaponSprite 비워둠)
   ├─ Fire_VFX                       (ParticleSystem, Play On Awake OFF, Looping ON)
   └─ Fire_VFX_upgrade               (ParticleSystem, Play On Awake OFF, Looping ON)
```

### 6.2 ParticleSystem 필수 설정

| 항목 | Primary (Fire_VFX) | Secondary (Fire_VFX_upgrade) |
|---|---|---|
| Looping | ✅ ON | ✅ ON |
| Play On Awake | ❌ OFF | ❌ OFF |
| Simulation Space | Local | Local |
| Shape | Cone | Cone |
| Angle | 30 (KhiFlameZone 의 half angle 매칭) | 40 |
| Transform Rotation Y | -90 (+X 분사 정렬) | -90 |

### 6.3 인스펙터 와이어링

#### WeaponModeController (Flamethrower Mode 섹션)
- Flamethrower Controller ← root 의 KhiFlamethrowerController
- Flamethrower Presenter ← FlamethrowerVisual 의 KhiFlamethrowerPresenter
- Flamethrower Zone ← root 의 KhiFlameZone
- **Flamethrower Object ← FlamethrowerVisual GameObject 자체** (모드 토글 핵심)

#### KhiFlamethrowerController
- Flame Zone (자동 resolve)
- Player Mana (자동 resolve)
- **Primary Flame Particles ← Fire_VFX (씬 인스턴스, FlamethrowerVisual 자식)**
- **Secondary Flame Particles ← Fire_VFX_upgrade (씬 인스턴스)**

#### KhiFlameZone
- Enemy Layer ← `Enemy` (또는 적 콜라이더 레이어)
- Burn VFX Prefab ← `Prefabs/Effect/Fire_VFX.prefab` (선택)
- Aim Camera ← 자동 resolve (Camera.main)

---

## 7. 검증 (Play 모드 테스트)

### 기본 동작
- [x] Q 키 / 마우스 휠로 화염방사기 모드 진입
- [x] 좌클릭 hold → Primary 분사 (작은 콘, 적은 마나)
- [x] 좌 release → 즉시 정지
- [x] 우클릭 hold → Secondary 분사 (큰 콘, 큰 마나)
- [x] 좌→우 전환 → Primary 자동 중단, Secondary 시작
- [x] 우→좌 전환 → Secondary 중단, Primary 자동 재개
- [x] 마나 0 → 자동 중단, 마나 회복 시 재시작 가능

### 시각
- [x] 마우스 방향으로 콘 회전
- [x] Primary/Secondary VFX 가 별도로 표시
- [x] 시작 시 자동 분사 없음 (Play On Awake OFF)

### 데미지·화상
- [x] 콘 안 적이 데미지 받음
- [x] 분사 중단 후 화상 DoT 적용 유지
- [x] Secondary 가 Primary 보다 강한 데미지/화상

### 에디터
- [x] Scene 뷰에 두 콘 동시 표시 (오렌지 + 빨강)
- [x] 6개 핸들 모두 드래그 가능
- [x] Undo 동작
- [x] 좌상단 라벨에 Range/Half Angle 실시간 표시

---

## 8. 알려진 제약 및 향후 개선

### 알려진 제약
1. **화상 틱 간격 1초 고정**: `EnemyStatusEffect` 가 1초 고정 틱으로 burn 처리. 더 빠른 DoT 원하면 EnemyStatusEffect 자체 수정 필요 (다른 시스템과 공유라 신중).
2. **보스 면역**: `EnemyStatusEffect.ApplyBurn` 이 보스 태그 면역. 보스에게는 직접 데미지만 들어감.
3. **콘 형상은 박스 + 각도 필터 근사**: PolygonCollider2D 같은 정확한 부채꼴 콜라이더 아님 (성능 vs 정확도 트레이드오프).

### 향후 개선 후보
1. **사운드 통합**: AudioSource + 분사 루프/시작/정지 SFX (Freesound CC0 자산 다운로드 후 와이어링)
2. **연료 게이지 UI**: PlayerMana 의 일부 UI 또는 무기 전용 게이지 표시
3. **OnHitEffectRegistry 통합**: 유물(Relic) 효과 (체인, 슬로우 등) 가 화염방사기 데미지에도 작용하도록
4. **차징/예열 메커닉**: 길게 누를수록 콘 거리/각도 확장
5. **불 잔존 효과**: 분사 끝나도 짧은 시간 지면에 불꽃이 남아 데미지 유지
6. **카메라 셰이크 / 화면 효과**: 강화 분사 시 임팩트 강조
7. **무기 본체 스프라이트**: 현재 placeholder. 자체 제작 또는 외부 조달 후 SpriteRenderer 추가

---

## 9. 재사용 가능성 (다른 무기 응용)

### 9.1 같은 패턴이 적용 가능한 경우
- **대포 폭발 반경** (3티어): 단발 광역, 콘이 아닌 원형 → range 만 사용
- **부채꼴 검 스윙**: 짧은 거리 콘, 1회성 → KhiFlameZone.FireBurst 패턴 (현재는 제거됨, 재추가 가능)
- **유탄발사기 폭발**: 발사체 + 폭발 시 콘 영역

### 9.2 재사용 가능한 컴포넌트
- **KhiFlameZoneEditor**: `range` / `coneHalfAngleDeg` 필드명 유지하면 다른 컴포넌트에도 `[CustomEditor(typeof(...))]` 만 추가하여 그대로 활용
- **PlayerMana 누적기 패턴**: 다른 지속형 자원 소비 무기에 그대로 복사 가능
- **우선순위 입력 전이 로직**: 좌/우 mutex 필요한 다른 무기에 동일 적용

---

## 10. 변경된 파일 목록

| 파일 | 변경 종류 | 라인 수 |
|---|---|---|
| `Runtime/TestKhi/KhiFlamethrowerController.cs` | 신규 | ~165 |
| `Runtime/TestKhi/KhiFlameZone.cs` | 신규 | ~175 |
| `Runtime/TestKhi/KhiFlamethrowerPresenter.cs` | 신규 | ~90 |
| `Runtime/TestKhi/WeaponModeController.cs` | 수정 (Flamethrower 모드 추가) | +15 |
| `Editor/Khi/KhiFlameZoneEditor.cs` | 신규 | ~190 |

신규 파일 4개, 수정 1개. 약 635 라인 추가.

---

## 11. 참고 자료

- 무기 모드 시스템: `WeaponModeController.cs`
- 화상 시스템: `EnemyStatusEffect.cs` (`Runtime/Enemies/`)
- 마나 시스템: `PlayerMana.cs` (`Runtime/Combat/`)
- 유사 패턴 (참고): `KhiBowController`, `KhiStaffController`, `KhiBowPresenter`, `KhiStaffPresenter`
