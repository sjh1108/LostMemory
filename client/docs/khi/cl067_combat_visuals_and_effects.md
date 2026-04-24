# CL-067 기본 전투 시각화 + 패링 이펙트 연결 (1부 무기 / 2부 이펙트)

작성일: 2026-04-24

대상 씬: `LostMemory/Assets/Scenes/test_khi.unity`

**상태**: 🔵 **계획 수립 / 구현 예정** — cl019 체크리스트 보류 항목 해소를 위해
Epic H 원위치에서 CL-019 직후로 당겨 진행. 구현 완료 후 본 문서의 검증 섹션과
cl019 체크리스트 보류 항목 동시 갱신.

## 목적

CL-019 체크리스트의 보류 항목(#B5 anticipation/recovery 박자, #B6 스윙 호 히트프레임
위치, #C6 패링 successSparkPrefab) 해소. 추가로 적/보스 패턴 설계 시 참조할 **시각
베이스라인** 확보 — Epic E·G에서 박자 설계할 때 시각화 부재로 인한 feel debt 회피.

cl019 체크리스트와 동시에 진행되어, cl067 완료 시점에 체크리스트 풀 패스 가능.

## 설계 기준

- **TDE 에셋 차용, 의존성 차단**: TDE 프리팹·컴포넌트는 안 씀. 스프라이트/텍스처
  파일만 `Assets/_Project/Art/` 로 복사 후 자체 컴포넌트로 표현. TDE 업데이트와 분리.
- **기존 패턴 재사용**: cl017/cl018에서 확립한 Presenter 패턴(이벤트 구독 → 단순
  시각화). 새로운 아키텍처 도입 안 함.
- **무기는 자식 GameObject**: Khi 플레이어 transform 자식으로 Weapon 오브젝트.
  aim 방향 따라 회전. SpriteRenderer 1개로 4상태(Idle/Slash1/2/3) 전환.
- **이펙트는 정적 스프라이트 + 트레일**: ParticleSystem 안 씀(2D 톤 유지). MMParticlesSlash
  텍스처를 SpriteRenderer로 사용 + 검 끝 TrailRenderer로 잔상.
- **회귀 0**: 기존 `KhiAttackVisualPresenter`의 procedural 슬래시 생성은 유지하되,
  외부 스프라이트 슬롯 우선 사용. 슬롯 비면 기존 procedural fallback.
- **placeholder 자각**: 정식 도트 아트는 후속. 이 티켓은 "박자 평가 가능한 수준"이 목표.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 검 에셋 | TDE `KoalaSword(Idle/Slash1/2/3).png` 4장 차용 | 이미 콤보 3타 분리 완료, 사이드뷰 톤 일치, 라이선스 OK |
| 슬래시 텍스처 | TDE `MMParticlesSlash.png` | 가벼운 흰색 호 형태, 색 틴트로 콤보별 차등 가능 |
| 무기 컴포넌트 | 신규 `KhiWeaponPresenter` | 단일 책임: 콤보 단계 ↔ SpriteRenderer 전환 + aim 회전 |
| 슬래시 강화 방식 | 기존 `KhiAttackVisualPresenter` 확장 (교체 X) | procedural 코드 fallback 보존, 외부 sprite 슬롯 우선 |
| TrailRenderer | 무기 끝 자식 오브젝트에 부착 | 검 자체 자식이라 회전·이동 자동 추적. Sprites/Default 머티리얼 |
| sortingOrder | Weapon = +1 (캐릭터 +0 위), Slash FX = +1001 (기존 유지) | 캐릭터 가림 방지 |
| 패링 spark prefab | 1회성 임시 프리팹: 빈 GO + SpriteRenderer(MMParticlesFlash) + 자식 4개 라인 | cl018 슬롯 채움. 정식 VFX는 후속 |
| 작업 구간 | 1부 무기 → 2부 이펙트 → 패링 spark → 검증 순 | 의존성 순. 무기 없으면 트레일 부착 못 함 |

## 추가/수정 파일

### 신규 코드 (1개)
```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiWeaponPresenter.cs
    SerializeField:
      - SpriteRenderer weaponSprite
      - Sprite idleSprite, slash1Sprite, slash2Sprite, slash3Sprite
      - KhiMeleeComboController comboController (자동 resolve)
      - KhiPlayerAim playerAim (자동 resolve, 회전용)
      - float idleRevertDelay = 0.15f (스윙 후 idle 복귀 지연)
    이벤트 구독:
      - AttackStarted(request, step) → step.ComboStep에 따라 slashN 스프라이트로 전환
      - AttackActiveEnded → idleRevertDelay 후 idle 복원 (Coroutine)
    Update:
      - playerAim.AimDirection 따라 weaponSprite.transform.localRotation 적용
      - aim X<0 시 SpriteRenderer.flipY로 좌우 반전
```

### 수정 코드 (1개)
```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiAttackVisualPresenter.cs
    SerializeField 추가:
      - Sprite externalThinSlashSprite, externalWideSlashSprite (slot 비면 기존 procedural fallback)
      - bool useTrailRenderer = false
      - Color trailStartColor, trailEndColor
      - float trailTime = 0.18f
    GetSlashSprite():
      - 외부 슬롯이 있으면 그것 우선 반환, 없으면 기존 _thinSlashSprite/_wideSlashSprite 생성 경로
    Awake:
      - useTrailRenderer && weaponPresenter 발견 시 weapon 자식에 TrailRenderer 부착
    수정 범위 최소: HandleAttackStarted, ShowTemporarySlash, FadeAndDestroySlash 무수정
```

### 에셋 임포트 (5개)
```text
Assets/_Project/Art/Khi/Weapon/KhiSwordIdle.png       (← KoalaSwordIdle.png 복사)
Assets/_Project/Art/Khi/Weapon/KhiSwordSlash1.png     (← KoalaSwordSlash1.png 복사)
Assets/_Project/Art/Khi/Weapon/KhiSwordSlash2.png     (← KoalaSwordSlash2.png 복사)
Assets/_Project/Art/Khi/Weapon/KhiSwordSlash3.png     (← KoalaSwordSlash3.png 복사)
Assets/_Project/Art/Effects/KhiSlashFx.png            (← MMParticlesSlash.png 복사)
```
임포트 후 Inspector에서:
- Texture Type: Sprite (2D and UI)
- Pixels Per Unit: 32 또는 64 (Khi 캐릭터 크기에 맞춰 조정)
- Filter Mode: Point (no filter) — 픽셀아트 톤
- Compression: None

### 신규 프리팹 (1개)
```text
Assets/_Project/Art/Effects/KhiParrySpark.prefab
    구성:
      - Root: 빈 GameObject
      - 자식 1: SpriteRenderer(KhiParryFlash) — MMParticlesFlash.png 복사본, 시안 틴트
      - 자식 2~5: SpriteRenderer(라인 스프라이트) — 회전 차등 (0/45/90/135도)
    별도 동작 컴포넌트 없음 (KhiParryFeedbackPresenter가 Instantiate 후 sparkLifetime 만에 Destroy)
```

### 씬 wiring (test_khi.unity)
```text
Khi 플레이어 GameObject:
  └─ 자식 신규 "Weapon"
       - SpriteRenderer (sortingOrder = +1, sortingLayer = 캐릭터와 동일)
       - KhiWeaponPresenter (4개 sprite 슬롯에 KhiSword* 4장 연결)
       └─ 자식 신규 "WeaponTip"
            - 빈 transform (TrailRenderer 부착 위치, 검 끝 좌표)

Khi 플레이어 GameObject:
  - KhiAttackVisualPresenter Inspector:
      - externalThinSlashSprite ← KhiSlashFx
      - externalWideSlashSprite ← KhiSlashFx (동일 사용, 색만 차등)
      - useTrailRenderer = true
  - KhiParryFeedbackPresenter Inspector:
      - successSparkPrefab ← KhiParrySpark.prefab
```

### 문서
```text
client/docs/khi/cl067_combat_visuals_and_effects.md (이 문서)
client/docs/khi/cl019_combat_feel_checklist.md (보류 항목 해소 마킹)
client/docs/khi/client1_tasks_master_plan.md (한 줄 메모 추가)
```

## 컴포넌트 구조

### 1부 — KhiWeaponPresenter (신규)

**책임**: 콤보 단계에 따라 검 SpriteRenderer 스프라이트 전환 + aim 방향 회전.

**상태 다이어그램**:
```text
            AttackStarted(step=1)         AttackActiveEnded
[Idle] ─────────────────────────▶ [Slash1] ─────────────▶ (idleRevertDelay) ─▶ [Idle]
   ▲                                                                              │
   └──────────────────────────────────────────────────────────────────────────────┘

step=2/3은 동일 흐름, slash2Sprite/slash3Sprite로 전환
```

**Update**:
- `playerAim.AimDirection` 읽어서 `weaponSprite.transform.localEulerAngles.z` 갱신
- aim.x < 0 시 `weaponSprite.flipY = true` (사이드뷰 좌우 대응)

### 2부 — KhiAttackVisualPresenter (확장)

**기존 동작**: AttackStarted 시 procedural 슬래시 텍스처 생성 → 페이드.

**확장**:
- `externalThinSlashSprite`/`externalWideSlashSprite` 슬롯이 있으면 procedural 대신 외부 사용
- `useTrailRenderer = true` 시 Awake에서 `KhiWeaponPresenter`의 Weapon/WeaponTip 위치에 TrailRenderer 부착
- 트레일 색/시간/너비는 SerializeField로 노출

**procedural fallback**: 외부 슬롯 없으면 기존 `CreateSlashSprite()` 경로 그대로 동작 → 회귀 0.

### 3부 — KhiParrySpark.prefab (cl018 보류 해소)

**구성**: 빈 GameObject 루트 + SpriteRenderer 5개 (중심 1 + 방사 4).
**동작**: 동작 컴포넌트 없음. `KhiParryFeedbackPresenter.SpawnSuccessSpark()`가 Instantiate
하고 `sparkLifetime`(0.4s) 후 Destroy. 페이드 애니가 필요하면 후속 작업으로 추가.

## 주요 설계 결정

### 1. 왜 TDE 프리팹 대신 스프라이트만 차용

TDE Koala 프리팹은 `MMSpriteReplace`, `Animator`, TDE WeaponHandler 의존성을 끌고 옴.
우리는 자체 KhiMeleeComboController로 공격 라이프사이클을 관리하므로, 프리팹을 그대로
쓰면 두 시스템이 충돌 가능. 스프라이트(.png)만 가져와 자체 컴포넌트로 표현하면
TDE 업데이트와 완전 분리되고, Khi 시스템과 일관됨.

### 2. 왜 Animator 안 쓰고 SpriteRenderer 직접 전환

스프라이트가 4장(Idle + 3 슬래시)뿐이고 전환이 이벤트 트리거형이라 Animator state
machine은 과설계. SpriteRenderer.sprite 직접 할당이 가장 단순. 후속에 프레임 애니
스프라이트 시트가 들어오면 그때 Animator 도입 검토.

### 3. 왜 KhiAttackVisualPresenter를 교체가 아닌 확장

기존 procedural 코드는 ~140줄로 잘 동작 중이고 `Texture2D` 동적 생성, `DrawBrushStroke`
수식 등이 들어 있어 삭제 시 다시 만들기 비쌈. 외부 슬롯이 비면 fallback으로 살려두면
이펙트 에셋이 없는 환경(테스트, 후속 분기 작업)에서도 정상 동작 유지. 회귀 0.

### 4. 왜 TrailRenderer를 Inspector 토글로

Trail은 호불호 갈리는 효과(어떤 톤에서는 거추장스러움). cl019 체크리스트 평가 시
ON/OFF 비교가 즉시 가능해야 함 → SerializeField 토글.

### 5. 왜 패링 spark prefab을 코드/스크립트 없이 빈 GO + SpriteRenderer 만으로

cl018에서 설명한 대로 패링 빈도가 낮고 sparkLifetime 자동 Destroy로 충분. 페이드가
필요하면 후속에서 spark용 미니 컴포넌트 추가 (또는 SpriteRenderer.color 알파를 spark
오브젝트 자체 코루틴으로 줄이기). 현재는 정적 표시 후 Destroy.

### 6. 왜 1부 무기를 먼저 진행

슬래시 트레일은 검 끝 좌표가 필요하므로 KhiWeaponPresenter의 WeaponTip이 먼저 있어야
함. 의존성 방향: 무기 → 트레일. 1부 → 2부 순.

## 검증 (E2E)

### 자동 검증
- 코드 컴파일: dotnet build (수동 실행 권장 — 신규 KhiWeaponPresenter.cs 추가)
- 외부 sprite 슬롯이 비어 있을 때 기존 procedural fallback 정상 동작 (회귀 검증)

### 수동 검증 (Unity Editor Play Test)

**전제** (이 티켓에서 완료):
- [ ] 5개 png 임포트 (Khi 4 + Effect 1)
- [ ] KhiParrySpark.prefab 생성
- [ ] Khi 플레이어에 Weapon/WeaponTip 자식 + KhiWeaponPresenter 부착
- [ ] KhiAttackVisualPresenter Inspector 외부 슬롯 + 트레일 토글 연결
- [ ] KhiParryFeedbackPresenter successSparkPrefab 연결

**1부 무기 시각화 검증**:
- [ ] #1 idle 상태에서 검 스프라이트 표시 (캐릭터 위)
- [ ] #2 콤보 1타 입력 → KhiSwordSlash1 스프라이트로 전환 → idleRevertDelay 후 idle 복원
- [ ] #3 콤보 2타/3타 동일 흐름, 각자 슬래시 스프라이트
- [ ] #4 aim 방향 따라 검 회전 (4방향 + 대각)
- [ ] #5 좌측 aim 시 좌우 반전 (flipY)

**2부 슬래시 이펙트 검증**:
- [ ] #6 적중 시 KhiSlashFx 스프라이트가 적 위치에 페이드 표시
- [ ] #7 트레일 ON 시 검 끝 잔상 표시. OFF로 토글 시 즉시 사라짐
- [ ] #8 콤보 1/2/3타 색상 차등 유지 (firstSlashColor, secondSlashColor, thirdSlashColor)
- [ ] #9 외부 슬롯 비웠을 때 기존 procedural slash 정상 표시 (fallback 검증)

**3부 패링 spark 검증**:
- [ ] #10 패링 성공 시 KhiParrySpark prefab Instantiate, 0.4s 후 Destroy
- [ ] #11 spark 위치가 플레이어 중심 (transform.position)
- [ ] #12 cl018의 다른 패링 연출(링 펄스, 플래시, 카메라 쉐이크) 모두 정상 (회귀)

**전체 회귀**:
- [ ] #13 cl017 히트스톱·플래시·카메라 임펄스 정상
- [ ] #14 cl018 패링 링 펄스, 강화 플래시, unscaled time 진행 정상
- [ ] #15 cl019 체크리스트 보류 항목 #B5/#B6/#C6 표시 가능 → 통과/조정 마킹 가능

### cl019 체크리스트 갱신
완료 후 [cl019_combat_feel_checklist.md](cl019_combat_feel_checklist.md) 보류 항목
🔒 마킹 해제 + 평가 메모 채움.

## 미해결 / 알려진 이슈 (예상)

### Koala 스프라이트 톤이 Khi와 안 맞을 수 있음
Koala는 노란 캐릭터 톤. 스프라이트 자체가 Khi 캐릭터 디자인과 충돌하면 색 틴트로
조정하거나 기본 흰색으로 desaturate 후 사용. 정식 도트는 후속.

### TrailRenderer Sorting Layer
TrailRenderer는 SpriteRenderer와 다른 sorting 시스템. 캐릭터 뒤에 트레일이 가려질
가능성. `MMTrailRendererSortingLayer.cs`(TDE 내장 유틸)를 코드로 참조해 정렬 강제
가능. 첫 시도에서 가려지면 추가 작업.

### KhiPlayerAim 의존
KhiWeaponPresenter Update가 매 프레임 KhiPlayerAim 참조. 컴포넌트 누락 시 검 회전
멈춤. Awake에서 자동 resolve + null 가드.

### 패링 spark 페이드 없음
정적 표시 후 즉시 사라짐 (Destroy). 시각적으로 "팝" 하고 끊김. 거슬리면 spark 자체에
간단 코루틴 추가하거나 sparkLifetime 짧게(0.2s).

### Animator 트리거 코드 잔존
KhiAttackVisualPresenter의 PlayAnimatorTrigger() 호출은 그대로 유지 (씬에 Animator 없으면
no-op). 향후 본체 캐릭터 애니메이션 도입 시 활용. 무기 시각화와는 독립.

## 후속 CL 연결

- **즉시 후속**: cl019 체크리스트 보류 항목 재평가 → 통과 마킹 또는 추가 조정
- **CL-090 무기 데이터 SO**: 검 외 다른 무기 추가 시 KhiWeaponPresenter의 sprite 4개
  슬롯을 ScriptableObject 참조로 전환 (현재는 직접 참조)
- **CL-101 사운드 1차 패스**: 무기 스윙 SFX, 슬래시 SFX, 패링 ding 일괄 연결
- **본격 VFX (정식 도트)**: 외부 슬롯/프리팹만 교체. 본 컴포넌트 코드 무수정 가능
- **KhiPlayerAnimator 도입 시**: 캐릭터 본체 애니와 KhiWeaponPresenter는 독립 동작.
  애니 추가 시 weapon은 그대로

## 관련 문서

- 현재 진행 중인 평가표: [cl019_combat_feel_checklist.md](cl019_combat_feel_checklist.md)
- 직전 CL: [cl018_parry_success_feedback_implementation.md](cl018_parry_success_feedback_implementation.md)
- 피드백 시스템: [cl017_hit_feedback_implementation.md](cl017_hit_feedback_implementation.md)
- 마스터 플랜: [client1_tasks_master_plan.md](client1_tasks_master_plan.md) (CL-067 정의, Epic H)
- 통합 작업 plan (로컬): `C:\Users\SSAFY\.claude\plans\eventual-snuggling-cloud.md`
- TDE 안전 규칙: [topdown-engine-extension-and-original-protection.md](../commonness/topdown-engine-extension-and-original-protection.md)
