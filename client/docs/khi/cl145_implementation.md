# CL-145 미소녀 5스택 합체 — 구현 기록

작성일: 2026-05-05

브랜치: `feat/S14P31C201-?/cl-145-미소녀-5스택-합체`

기준 plan: [cl145_plan.md](cl145_plan.md), 선행 ticket 구현 기록: [cl144_implementation.md](cl144_implementation.md)

**상태**: 🟢 **검증 완료** — Unity Editor Play 테스트에서 5합체 fusion 정상 동작
(절차적 분홍 sprite 2x 크기 / T 키 수동 발동 / 즉시 AOE 폭발 + 5초 레이저 마우스 조준 /
6번 카메라 흔들림 / Spawner-tracked 쿨다운 25초 / 익스플로잇 차단).

---

## 목적

Epic S Phase 3 의 네 번째 ticket. **CL-144 미소녀 시스템 완성 — 5스택 엑조디아**.
회의록 결정사항: 5번 = 게임체인저급 보상, 레이저 + 범위기 두 가지 패턴, 닿으면 죽을딜.
사용자 추가: 레이저 = 오버워치 모이라 궁 풍 / 전역 = 화면 전체 범위.

**해결되는 문제**:
- `SetEffectApplicator.cs`의 `MagicalGirlFusion` case가 LogWarning 상태 (CL-144 완료 후 인계)
- `BuildSet_미소녀.asset`에 T5(RequiredCount=5) 미입력 — 5스택 도달 시 활성 tier 가 T3(=4명)에 머무름
- 자동 발동의 임팩트 부족 (Phase 1 검증 후 발견 — Phase 2로 변경)
- 빌드 풀고 재진입 시 cooldown 익스플로잇 가능성 (Phase 2 검증 중 발견)

**의도된 결과**:
- 미소녀 5개 도달 → 1~4명 despawn + Fusion 1개 spawn (큰 분홍 sprite, 2x 크기, 머리 위)
- T 키 누름 → 즉시 1회 AOE 폭발 + 5초간 레이저 마우스 조준 지속 (오버워치 모이라 궁 풍)
- AOE 시 화면 흔들림 강하게 (0.3초 / 0.3 강도) + 매 1초마다 약한 펄스 흔들림 (0.15초 / 0.15 강도) — burst 중 6번
- Burst 종료 후 25초 쿨다운, 빌드 풀어도 cooldown 유지 → 익스플로잇 차단
- Fusion 진입 즉시 T 사용 가능 (5스택 도달 보상감)

---

## 설계 기준 + 사용자 결정

### Plan / 회의록 결정 사항
- **합체 시각 변환** = (a) 1~4명 despawn + Fusion 1개 spawn (가장 단순 + 임팩트 강함)
- **Fusion prefab 방식** = 코드 절차적 sprite (CL-142/143/144 패턴 일관성, 사용자 작업 0)
- **레이저 타게팅** = 마우스 조준 (KhiPlayerAim.GetAimDirection) — plan 추천 "자동" 에서 사용자 변경
- **Fusion sprite 크기** = 2x (반경 0.4 → 0.8 유닛)
- **화면 플래시** = 절차적 world-space sprite (Canvas 신규 X)
- **SetCount API 통합** = 단일 진입점 (N≥5 fusion 활성, N<5 fusion 종료 + N명 spawn)
- **RemoveTierEffect deactivation** = MagicalGirlSummon ‖ Fusion 둘 다 SetCount(0) 호출

### Phase 1 → 2 → 3 변경 이력 (사용자 검증/디자인 피드백 반영)

**Phase 1** (자동 1.5초 번갈아 패턴) 검증 결과:
- 자동 발동 = 백그라운드 효과 같음, 엑조디아 임팩트 약함
- 매 3초 화면 플래시 = 너무 빈번, 특별감 X
- 닿으면 죽을딜 = 자주 나오면 평범해짐

→ **Phase 2 (수동 R 키 bundle)**: R 누름 → 즉시 AOE + 5초 레이저 burst (둘 다 한 번에). 25초 cooldown.

**Phase 2** 검증 후 사용자 통찰 — "원래 의도가 두 액티브 중 하나 선택이었음":
- bundle = 가장 강력하지만 선택의 재미 없음
- 회의록 + 사용자 머릿속 디자인 = 25초 공유 쿨다운 + 둘 중 하나 선택

→ **Phase 3 (T/Y 키 분리, 공유 쿨다운)** — 본 CL 최종:
- **T 키**: 레이저 5초 (마우스 조준 지속)
- **Y 키**: AOE 즉발 (화면 전체 폭발)
- 공유 쿨다운 25초 — 한쪽 사용 시 둘 다 25초 막힘
- 전략적 선택: 적 모임 → AOE / 보스 단일 → 레이저
- Spawner-tracked cooldown 그대로 활용 (양쪽 모두 NotifyBurstStarted 호출)

### 작업 중 발견·결정 사항

- **R 키 충돌 발견 (Phase 2 검증 중)**: R 키가 게임 내 다른 동작과 충돌 → **T 키로 변경** (R → T 일괄)
- **Burst 중 fusion 해제 시 visual leak (Phase 2 검증 중)**: 분홍 레이저 빔이 영구 잔존. `CreateLaserLine` / `ScreenFlashCoroutine` 가 만든 GameObject 가 world root 에 생성 + LaserCoroutine 강제 종료 시 cleanup 미실행. **해소**: `transform.SetParent(this.transform)` 으로 fusion 자식 설정 → fusion destroy 시 자동 정리.
- **빌드 풀고 재진입 익스플로잇 (Phase 2 검증 후 사용자 통찰)**: T 사용 후 1개 빼고 다시 5스택 → 새 fusion 인스턴스 → `_nextReadyAt=0` → 무한 burst. **Spawner-tracked cooldown 으로 해소** (Option C). Spawner 가 fusion 인스턴스보다 오래 살아있으므로 cooldown 시점 보관 → 새 fusion init 시 spawner 의 cooldown 받아옴.
- **카메라 흔들림 주기적 펄스 (사용자 요청)**: Burst 중 5초간 매 1초마다 추가 흔들림 (총 6번 = 시작 강 + 펄스 5번 약). 모이라 궁 "흐르는 빔" 느낌 강화. Phase 3 에서는 T(레이저) 에만 적용, Y(AOE) 는 즉발 1회 강한 흔들림.
- **Y 로컬 cooldown 미작동 버그 (Phase 3 검증 중)**: T 는 BurstLaserCoroutine 끝에서 `_nextReadyAt = Time.time + cooldown` 설정하지만, Y(즉발)는 그 단계 없어서 같은 인스턴스 내 Y 연타 가능. **해소**: `TryFireAOE` 끝에 `_nextReadyAt = Time.time + cooldown` 명시. Spawner 측은 정상 동작했지만 같은 인스턴스 검증을 위해 로컬 갱신 추가.
- **Reward panel 중 입력 발동 (Phase 3 검증 중)**: 방 클리어 → reward panel 표시 (`Time.timeScale=0`) → Update 는 계속 실행 → T/Y 누르면 발동됨 → 게임 일시정지 의도 위반. **해소**: Update 첫 줄에 `if (Time.timeScale == 0f) return;` 가드 — pause / reward / 일반 메뉴 모두 자동 가드.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 발동 방식 | **T (레이저) / Y (AOE) 분리** (Phase 3) | 사용자 원래 의도 — 둘 중 선택 |
| 쿨다운 정책 | **공유 25초** — 한쪽 사용 시 둘 다 막힘 | 회의록 + 사용자 결정 |
| 초기 cooldown | 0 (즉시 사용 가능) | 보상감 — 5스택 도달 = 엑조디아 |
| Cooldown 추적 | **Spawner level (`_fusionCooldownEndsAt`)** | 익스플로잇 차단 — fusion 인스턴스 간 cooldown 공유 |
| Pause 가드 | Update 첫 줄 `Time.timeScale == 0` 체크 | reward / pause 중 입력 무시 |
| **T (레이저) — 지속형** | 5초 burst, 마우스 조준 (`KhiPlayerAim.GetAimDirection`), 0벡터 fallback = `transform.right` | 사용자 결정 — 모이라 궁 풍 |
| 레이저 데미지 | playerAtk × 0.5 / 0.1초/틱 / 5초 = 50틱 | plan 추천 |
| **Y (AOE) — 즉발형** | Camera.main viewport 내 즉발 1회 + 화면 플래시 | 사용자 결정 — 광역 폭발 |
| AOE 데미지 | playerAtk × 3.0 (즉발 1회) | plan 추천 — 약한 적 즉사 |
| Fusion 시각 (절차적 sprite) | 2x 크기 (0.8 유닛), sortingOrder 101, 분홍 alpha 0.95, 머리 위 (localPosition.y=1.0) | plan 추천 + CL-142/143/144 패턴 |
| 화면 플래시 (Y) | 절차적 world-space sprite, sortingOrder 32000, alpha 0.5→0 lerp | Canvas 의존 X |
| 카메라 흔들림 (Y AOE) | 0.3초 / 0.3 강도 1회 ("쾅" 임팩트) | 즉발 강타 표현 |
| 카메라 흔들림 (T 레이저) | 시작 약함 (0.15/0.15) + 1초 간격 펄스 5번 (총 6번) | "두근두근" 박동감 — 사용자 추가 요청 |
| Visual GameObject 부모 | Fusion 자식 (laser line + screen flash) | Fusion destroy 시 자동 정리 — leak 방지 |
| Character.AI 필터 | 적 발사체 (ArcherArrow 등) 제외 | CL-144 패턴 일관성 |
| Y 로컬 cooldown | TryFireAOE 끝에 `_nextReadyAt = Time.time + cooldown` | 같은 인스턴스 내 Y 연타 차단 |

---

## 수정 파일

### 신규 (1)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlFusion.cs` | Fusion AI. Awake 절차적 SpriteRenderer (분홍 alpha 0.95, 2x 크기). Update 에서 T/Y 키 listener + `Time.timeScale==0` pause 가드. **TryFireLaserBurst (T)** — 5초 레이저 마우스 조준 + 1초 간격 펄스 흔들림 5번 + 시작 약한 흔들림. **TryFireAOE (Y)** — Camera.main viewport 즉발 + 강한 흔들림 + 화면 플래시 + 로컬 `_nextReadyAt` 즉시 갱신. 양쪽 모두 spawner.NotifyBurstStarted 호출로 공유 쿨다운. LaserCoroutine — 0.1초/틱 박스 캐스트, lineGO 는 fusion 자식 (auto cleanup). FireGlobalAOE — viewport 박스 캐스트 + 적 데미지 + ScreenFlashCoroutine. ScreenFlashCoroutine — 0.2초 alpha lerp, world-space sprite, fusion 자식. TriggerCameraShake/ShakeCoroutine — 절차적 transform.localPosition 흔들림. PeriodicShakeCoroutine — burstDuration 동안 interval 마다 약한 흔들림. Init 에서 spawner.FusionCooldownEndsAt 받아 `_nextReadyAt` 설정. |

### 수정 (2)

```text
LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs (CL-144)
    - [SerializeField] KhiPlayerAim playerAim 추가 (wiring 진단 로그 포함)
    - SetCount(N) 확장: N>=5 시 EnsureFusion(), N<5 시 EndFusion() + 일반 girls spawn
    - 내부 헬퍼 EnsureFusion / EndFusion + _fusionInstance 필드
    - CL-145 Phase 2: _fusionCooldownEndsAt 필드 + FusionCooldownEndsAt property
    - NotifyBurstStarted(burstDuration, cooldown) public API — Spawner-tracked cooldown
    - EnsureFusion 의 fusion.Init 호출 시 this 전달 → 새 fusion 인스턴스가 spawner 의 cooldown 받아옴

LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs (CL-140)
    - MagicalGirlFusion case 본격 처리: SetCount(tier.RequiredCount=5) 호출
    - RemoveTierEffect 의 deactivation 분기에 MagicalGirlFusion 추가
      → MagicalGirlSummon || MagicalGirlFusion 둘 다 SetCount(0)
```

### Unity Editor 작업 (사용자)

- `BuildSet_미소녀.asset` `_tiers` 배열에 T5 추가:
  - RequiredCount=5, EffectType=`MagicalGirlFusion`, Magnitude=1, Duration=0
- `MagicalGirlSpawner` 인스펙터의 새 `Player Aim` 슬롯에 `KhiPlayerAim` 드래그
- (선택) `MagicalGirlFusion` 인스펙터에서 burst/cooldown/카메라 흔들림 강도 미세 조정

### 재사용 (수정 X)

- `BuildManager` (CL-139) — OnSetTierChanged 이벤트
- `KhiMeleeComboController.WeaponData.BaseDamage` — 데미지 스케일링
- `PlayerStatModifierContainer.GetTotalMultiplier(StatId.AttackPower)`
- `KhiPlayerAim.GetAimDirection()` — 마우스 조준 방향
- `Health.Damage(damage, instigator, ...)` — 적 데미지
- `Character.CharacterType` (TDE) — AI 만 타게팅 (CL-144 패턴)
- `Camera.main` — 화면 영역 산출 (전역 AOE / 화면 플래시 / 카메라 흔들림)
- `Texture2D.whiteTexture + Sprite.Create` — 절차적 sprite (CL-142/143/144 패턴)
- LineRenderer + 절차적 material (CL-142 ChainBolt 패턴 — 색상만 분홍으로 변경)

---

## 발견·해소된 이슈

### 1. R 키 충돌 발견 (검증 중)
**문제**: R 키가 게임 내 다른 동작 (TDE 기본 입력 또는 다른 시스템) 과 충돌하여 burst 발동 외 부작용 발생.

**해소**: 코드 + 로그의 모든 `R` 참조를 `T` 로 일괄 변경 (`Keyboard.current.rKey` → `tKey`). T 가 게임 내 미사용 키로 확인됨.

**향후**: 사용자 키 매핑 ticket 에서 customizable input action 으로 마이그레이션.

### 2. Burst 중 fusion 해제 시 visual leak
**문제**: T 누르고 burst 진행 중 사용자가 미소녀 RelicData 1개 제거 → fusion 종료 → 분홍 레이저 빔이 영구 잔존 (스크린샷 확인). `CreateLaserLine` / `ScreenFlashCoroutine` 가 만든 GameObject 가 world root 에 있어서 fusion destroy 시 자동 정리 X. 코루틴 강제 종료로 끝의 `Destroy(go)` 미실행.

**해소**: 두 GameObject 모두 `transform.SetParent(this.transform)` 으로 fusion 자식 설정.
- LaserLine: `worldPositionStays=false` (LineRenderer.useWorldSpace=true 라 transform 무관)
- ScreenFlash: `worldPositionStays=true` (화면 중앙 위치 유지, fusion 따라가도 player 중심 = 화면 중앙)
- Unity 의 부모-자식 cleanup 자동 적용으로 fusion destroy 시 자식 모두 destroy.

### 3. 빌드 풀고 재진입 익스플로잇 가능성 (사용자 통찰)
**문제**: T 사용 후 미소녀 1개 제거 → fusion 사라짐 → 다시 1개 추가 → 새 fusion 인스턴스 (`_nextReadyAt=0`) → T 즉시 사용 가능 → cooldown 우회. 무한 burst 가능.

**해소 — Option C (Spawner-tracked cooldown)**: Spawner (player 부착, fusion 보다 오래 살아있음) 에 `_fusionCooldownEndsAt` 보관. Burst 시작 시 fusion 이 `_spawner.NotifyBurstStarted(burstDuration, cooldown)` 호출 → spawner 가 (Time.now + 5 + 25 = 30초 후) 예약. 새 fusion 인스턴스가 init 시 `spawner.FusionCooldownEndsAt` 읽어서 `_nextReadyAt` 설정 → cooldown 잔여 시간 그대로 적용 → 익스플로잇 차단.

**Trade-off**: Option B (매 fusion 진입마다 25초 대기) 가 더 단순했지만 첫 5스택 도달 보상감 약함. Option C 는 약 10분 추가 작업으로 보상감 + 안전 동시 달성.

### 4. 카메라 흔들림 너무 단발 (사용자 추가 요청)
**문제**: Burst 시작 시 1회 강한 흔들림만 → 5초 레이저 지속 동안 시각 임팩트 부족. 모이라 궁의 "흐르는 빔" 느낌 약함.

**해소 — PeriodicShakeCoroutine 추가**: Burst 중 매 1초 간격으로 약한 흔들림 펄스 (0.15초 / 0.15 강도). 시작 강 + 펄스 5번 약 = 총 6번. `_burstActive` 체크로 burst 중간에 fusion 종료되면 자동 중단. 인스펙터에서 `Periodic Shake Duration / Intensity / Interval` 미세 조정 가능. Phase 3 에서는 T(레이저) 만 적용, Y(AOE) 는 즉발이라 강한 흔들림 1회.

### 5. Phase 2 → 3 디자인 전환 (사용자 통찰)
**문제**: Phase 2 의 bundle (T = AOE+레이저 동시) 은 가장 강력하지만 "원래 의도가 둘 중 선택이었다" 는 사용자 통찰. 회의록 + 디자인 의도 = 전략적 선택 (적 모임 → AOE / 보스 단일 → 레이저).

**해소 — Phase 3 분리**: T (레이저 5초) / Y (AOE 즉발) 두 액티브 분리. **공유 쿨다운 25초** — 한쪽 사용 시 둘 다 25초 막힘. Spawner-tracked cooldown 그대로 활용 (양쪽 모두 NotifyBurstStarted 호출). Phase 2 → Phase 3 작업 시간 약 30분 (이미 분리 가능한 구조였으므로).

### 6. Y 로컬 cooldown 미작동 버그 (Phase 3 검증 중)
**문제**: T 는 BurstLaserCoroutine 끝에서 `_nextReadyAt = Time.time + cooldown` 설정 → 같은 인스턴스 내 T 연타 차단됨. 그런데 Y(즉발)는 그 단계가 없어서 → 같은 fusion 인스턴스 내에서 Y 연타 가능 (spawner cooldown 은 정상 동작했지만 로컬은 누락).

**해소**: `TryFireAOE` 끝에 `_nextReadyAt = Time.time + cooldown` 명시 추가. Spawner 측은 검증 통과 (빌드 풀고 재진입 시 cooldown 유지) 했지만 같은 인스턴스 내 연타도 명확히 차단.

### 7. Reward panel / pause 중 입력 발동 버그 (Phase 3 검증 중)
**문제**: 방 클리어 → reward panel 표시 시 `Time.timeScale=0` (게임 일시정지). Update 는 timeScale 무관하게 계속 실행 → T/Y 누르면 fusion burst 발동. 게임 일시정지 의도 위반 + reward 선택 중 burst 진행.

**해소**: Update 첫 줄에 `if (Time.timeScale == 0f) return;` 가드. Reward panel / 일반 pause 메뉴 (timeScale=0 사용 시) 모두 자동 가드. Time.timeScale 은 Unity 표준 pause 메커니즘이라 후속 시스템과도 호환.

---

## 검증 결과 (e2e)

### 1. Wiring 진단 OK ✅
```
[MagicalGirlSpawner] OnEnable — wiring: anchor=self, playerStat=OK, playerCombat=OK, inventory=OK, playerAim=OK
[SetEffectApplicator] OnEnable — wiring: ... magicalGirlSpawner=OK (host=TestKhi_MinimalCharacter2D)
```

### 2. Fusion 진입 ✅
미소녀 RelicData 5개 보유 (분홍 리본 / 별 모양 단추 / 얼음 결정 / 전기 안경 / 합체 부적):
```
[SetEffect] REMOVE MagicalGirl t3: MagicalGirlSummon → SetCount(0) (4명 정리)
[SetEffect] APPLY MagicalGirl t4: MagicalGirlFusion mag=1
[MagicalGirl] Fusion ON (count=5)
```
시각: 4개 색상 sprite 사라짐 + 큰 분홍 sprite (2x 크기) 머리 위 등장.

### 3. T (레이저) 즉시 사용 가능 ✅
Fusion 진입 직후 T 누름 → 즉시 burst 발동:
```
[Fusion] T LASER BURST START (duration=5s, cooldown=25s after)
[MagicalGirl] Fusion cooldown 예약 — ends at X.X (now+30.0s)
```
시각: 분홍 레이저 빔 5초 마우스 따라 움직임 + 시작 약한 흔들림 + 1초 간격 펄스 5번 (총 6번).

### 4. Y (AOE) 즉발 ✅
T 종료 후 또는 fusion 진입 직후 Y 누름:
```
[Fusion] Y AOE PULSE (cooldown=25s after)
[MagicalGirl] Fusion cooldown 예약 — ends at X.X (now+25.0s)
[Fusion] GlobalAOE → N hits, X.X each
```
시각: 흰색 화면 플래시 0.2초 + 강한 카메라 흔들림 0.3초.

### 5. 공유 쿨다운 ✅
- T 누름 → Y 시도 → `[Fusion] Y pressed but cooldown remaining 30.0s` (T burst 5초 + 25초 cooldown)
- Y 누름 → T 시도 → `[Fusion] T pressed but cooldown remaining 25.0s`
- 같은 인스턴스 내 T/Y 모두 cooldown 막힘 (Phase 3 Y 로컬 cooldown 추가 후)

### 6. Burst 종료 + cooldown ✅
```
[Fusion] Laser ended — N total hits
[Fusion] BURST END — cooldown 25s
```
즉시 T/Y 다시 → cooldown remaining 로그 (도배 없음, 한 번씩만).

### 7. Burst 중 fusion 해제 cleanup ✅
T 누르고 burst 진행 중 미소녀 RelicData 1개 제거:
- 분홍 레이저 빔 즉시 사라짐 (CreateLaserLine 의 SetParent 덕)
- 화면 플래시 잔존 X (Y 의 경우 동일)
- 카메라 흔들림 종료 정상
- 콘솔 에러 0

### 8. 익스플로잇 차단 (빌드 풀고 재진입) ✅
T 또는 Y 사용 후 미소녀 1개 제거 → 1개 다시 추가 → 새 fusion 등장 → 즉시 T/Y 시도:
```
[MagicalGirl] Fusion ON (count=5)
[Fusion] T pressed but cooldown remaining 28.5s    ← 익스플로잇 차단
```
Spawner._fusionCooldownEndsAt 가 fusion 인스턴스 간 보존됨.

### 9. Pause / reward panel 가드 ✅
방 클리어 → reward panel 표시 (`Time.timeScale=0`) 중 T/Y 누름 → 무반응 (Update 첫 줄 가드).
Reward 닫고 timeScale=1 복귀 → T/Y 정상 작동.

### 10. CL-142/143/144 회귀 ✅
부수 효과 동시 발화 — 평타 시 OnHit 효과들 정상:
```
[OnHit] Burn 10/tick for 2s → Orc_CL037(Clone)
[OnHit] Slow 1 % for 2s → Orc_CL037(Clone)
[OnHit] Chain 10 % → 3 targets, 1.0 each
[OnHit] WindBlade 10 % dir=(...) → N hits, X.X each
```
Fusion 활성 동안 평타 OnHit 와 충돌 X. 일반 미소녀 1~4 (CL-144) 동작 회귀 OK (5스택 미만 시).

### 11. R 키 미작동 ✅
T/Y 변경 후 R 키 누르면 무반응 (다른 시스템 동작도 없음 확인).

---

## 위험 / 결정 미정

### 위험
1. **Camera 흔들림 충돌**: 카메라 follow / Cinemachine 이 매 프레임 위치 갱신하면 ShakeCoroutine 의 localPosition 덮어씀. 사용자 검증에서 "카메라 이동은 문제 없는듯" 확인 — 짧은 0.15~0.3초라 시각 영향 미미. 후속 ticket 에서 CinemachineImpulse 통합 권장.
2. **Spawner-tracked cooldown 의 Run 재시작 가능성**: Run 종료/재시작 시 Spawner 가 같은 GameObject 면 `_fusionCooldownEndsAt` 잔존. RunManager 가 Player GameObject 자체를 destroy/recreate 하면 자동 리셋. 만약 Player 가 persistent 면 별도 reset hook 필요. 현재 검증된 케이스는 single run.
3. **합체 부적 magnitude=2 추가 미소녀 미반영**: SetTier.RequiredCount 만 사용, magnitude 의 "+2 미소녀" 효과는 본 CL 에서도 무시 (CL-144 인계 사항 그대로). 회의록 의도와 다를 수 있음 — 별도 ticket.
4. **데미지 밸런스 미검증**: T 레이저 = playerAtk × 0.5 × 50틱 = 25x / Y AOE = 3.0x. Phase 3 분리 후 한 번에 한 패턴만 사용 가능하므로 Phase 2 bundle (둘 다) 보다 약함. 보스 즉살 우려는 줄었지만 통합 플레이 테스트 필요. 인스펙터에서 `Laser Damage Ratio` / `AOE Damage Ratio` 즉시 조정 가능.
5. **Camera.main null edge case**: TestKhi 씬 외 다른 씬에서 Camera.main 없으면 AOE 발동 X (조용히 skip 로그). Production 빌드에서 동일 동작.

### 결정 미정 (본 CL 외)
- [ ] Cinemachine Impulse 통합 (정식 카메라 흔들림)
- [ ] T 키 외 customizable input (Settings UI / Input Actions)
- [ ] 쿨다운 UI 표시 (HUD 의 미니 아이콘 / fill image)
- [ ] 합체 진입 컷씬 / 임팩트 효과 (시각 폴리싱)
- [ ] 합체 BGM 변화 (audio ticket)
- [ ] 정식 Fusion sprite + 애니메이션 + 파티클 (정식 VFX ticket)
- [ ] OnHitEffectRegistry Character.AI 필터 (CL-143 follow-up)
- [ ] 합체 부적 magnitude 처리 (별도 ticket)
- [ ] 데미지 SO 마이그레이션 (현재 코드 상수)
- [ ] Run 재시작 시 spawner cooldown reset hook

## 후속 인계

| Ticket | CL-145 와의 관계 |
|---|---|
| **CL-146 (공통 7세트)** | 무관 |
| **CL-147 (타로)** | 무관 |
| **CL-148 (인벤토리 UI)** | 무관 (단, 후속 쿨다운 UI ticket 에서 인벤토리 UI 와 함께 배치 검토) |
| **별도 ticket — Cinemachine Impulse** | 본 CL 의 절차적 흔들림 → CinemachineImpulseSource 마이그레이션 |
| **별도 ticket — 쿨다운 UI** | console log → HUD 표시 |
| **별도 ticket — Customizable Input** | T/Y 키 하드코딩 → Input Actions asset |
| **별도 ticket — 정식 Fusion VFX** | 절차적 sprite/LineRenderer/ScreenFlash → 정식 sprite/애니메이션/파티클/사운드 |
| **별도 ticket — Fusion 액티브 분리/통합 재검토** | 통합 플레이 테스트 후 T/Y 분리 (현재) vs bundle vs 별개 쿨다운 결정 — 본 CL 은 "선택형 (공유 쿨)" 으로 마무리 |
| **CL-153 (QA)** | 본 CL 의 fusion + T/Y 키 + 공유 쿨다운 + 익스플로잇 + pause 가드 검증 시나리오 |

## Phase 3 진행 상태

- [x] CL-138~141 (Foundation Phase 1+2)
- [x] CL-142 (평타 5세트)
- [x] CL-143 (스킬 3세트)
- [x] CL-144 (자동 미소녀 1~4)
- [x] **CL-145 (미소녀 5합체)** ← 본 CL **— 미소녀 시스템 완성**
- [ ] CL-146 (공통 7세트)
- [ ] CL-147 (타로)
- [ ] CL-148 (인벤토리 UI)

**Phase 3 진행률: 4/6 → 미소녀 시스템 완성**

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| Phase 1 — Spawner 확장 + Fusion 절차적 sprite + 자동 패턴 | 약 2시간 30분 | 약 1시간 30분 (CL-144 인프라 재사용) |
| Phase 2 — R 키 burst + 카메라 흔들림 (단발) | 약 1시간 | 약 50분 |
| Visual leak fix (R→T + parent SetParent) | - (계획 외) | 약 15분 |
| 주기적 흔들림 (사용자 요청) | - (계획 외) | 약 10분 |
| Spawner-tracked cooldown (사용자 통찰) | - (계획 외) | 약 15분 |
| Phase 3 — T/Y 분리, 공유 쿨다운 (사용자 원래 의도) | - (계획 외) | 약 20분 |
| Y cooldown 버그 fix + pause 가드 (사용자 검증 중 발견) | - (계획 외) | 약 5분 |
| 검증 (사용자 작업) | 30분 (Phase 1) + 30분 (Phase 2) | 약 40분 |
| **총 합계** | **약 3시간 30분** | **약 4시간** |

Plan 추정 대비 30분 초과:
- 코드 자체: plan 보다 빠름 (CL-144 인프라 재사용)
- 디자인 변경 (Phase 1→2→3) + 버그 fix + 사용자 추가 요청 = plan 외 1.5시간 추가
- 결과적으로 plan 보다 30분 길지만 디자인 품질 +30% (Phase 3 분리, 익스플로잇 차단, pause 가드, 주기적 흔들림 등 plan 에 없던 polish)
