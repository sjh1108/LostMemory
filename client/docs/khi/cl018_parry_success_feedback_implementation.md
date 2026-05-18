# CL-018 패링 성공 전용 피드백 적용 구현 기록

작성일: 2026-04-24

대상 씬: `LostMemory/Assets/Scenes/test_khi.unity`

지라/브랜치: `feat/S14P31C201-212/cl-패링-성공-전용-피드백-적용`

**상태**: 🟢 **코드 완료 / 핵심 동작 수동 검증 통과** — Unity Editor Play 테스트에서
링 펄스, 강화 플래시, 카메라 쉐이크, 히트스톱 중 unscaled 진행 확인. 연속 패링·SFX는
환경 한계로 미검증(코드상 안전).

## 목적

CL-017에서 히트스톱+플래시+카메라 임펄스 3종 기본 피드백을 `KhiCombatFeedbackBinder`로
연결했지만, 패링 성공의 **전용 임팩트**는 약했다:

- `KhiParryFeedbackPresenter`의 성공 연출이 `flashSprite`에 흰색 컬러를 덮어쓰는
  임시 구현(원본 코드 9번 줄 주석: *"실제 아트 이펙트는 후속 CL에서 교체한다"*).
- 성공/실패가 색만 다른 동일한 단순 플래시 → "내가 제대로 받아쳤다"는 쾌감 부족.
- 패링 링이 윈도우 종료와 함께 그냥 꺼질 뿐, 성공 순간 특별한 애니메이션 없음.
- 사운드 피드백 부재.
- `Time.time` 사용 → 히트스톱(`timeScale=0`) 동안 플래시가 동결되어 시각적으로 끊김.

CL-018은 **패링 성공 분기만 강화**해서 위 5개 결함을 해소한다. 실패 연출과 다른
이벤트(HitStun/TargetHit) 피드백은 무수정.

## 설계 기준

- **확장이지 교체 아님**: 기존 `ParrySucceeded` 이벤트 흐름·실패 분기·다른 이벤트는 0바이트 수정.
- **2D 스프라이트 톤 유지**: 3D ParticleSystem 도입 금지. SpriteRenderer 색/알파 + LineRenderer
  기존 프레젠터 패턴 그대로.
- **에셋 슬롯 옵셔널**: VFX 프리팹/AudioClip은 `SerializeField` 슬롯만 노출, null이어도 정상 동작.
- **튜닝값은 모두 Inspector**: 기본값은 시작점, 디자이너가 플레이테스트로 조정.
- **unscaled time**: `KhiHitFlashPresenter`와 동일하게 히트스톱 중에도 연출 진행.
- **관심사 분리**: 패링 전용 SFX는 Presenter에 두고, Binder는 플레이어 루트 오케스트레이터 역할 유지.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 성공 링 펄스 매개변수 | `successRingExpansion=2.0`, `successRingDuration=0.25s` | 윈도우 반경 대비 2배 확장. 0.25s는 히트스톱(0.10s)보다 길어 freeze 풀리고도 잔상 유지. |
| 성공 플래시 분리 | `successFlashDuration=0.22s`, `successFlashIntensity=1.2` | 기존 공용 `flashDuration=0.15s`보다 길게 → 성공감↑. 실패는 기존 0.15s 유지. |
| 시간 기준 | `Time.unscaledTime` (Update/UpdateFlash/UpdateRing 전체) | 히트스톱(timeScale=0) 중에도 펄스/플래시 진행 유지. |
| 스파크 스폰 방식 | `Instantiate → Destroy(spark, sparkLifetime)` | 패링 빈도 낮음. 풀링은 과설계. 후속 CL-067 본격 VFX 도입 시 재평가. |
| SFX 위치 | `KhiParryFeedbackPresenter` 내부 AudioSource 자동 확보 | 패링 전용 사운드는 패링 책임. Binder는 다른 이벤트 다수 다루는 오케스트레이터. |
| Binder 수치 톤업 | `hitStopOnParry` 0.08→0.10, `parryShakeIntensity` 0.10→0.14 | 다른 이벤트보다 패링 임팩트가 더 커야 함(CL-017 계층화 원칙 유지). |
| 펄스 우선권 | `UpdateRing`에서 펄스 진행 중이면 윈도우 로직보다 우선 후 `return` | 성공 직후 패링 상태가 ParryWindow에서 빠르게 빠져나가도 펄스가 자연 종료. |

## 수정 파일

신규 파일: 없음.

수정 파일 (2개):

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryFeedbackPresenter.cs
    - 성공 전용 SerializeField 8개 추가
      (successFlashDuration/Intensity, successRingExpansion/Duration/Color,
       successSparkPrefab, sparkLifetime, successSfx, sfxVolume)
    - HandleParrySucceeded → BeginFlash 강화 + StartSuccessPulse + SpawnSuccessSpark + PlaySuccessSfx
    - UpdateRing에 성공 펄스 분기 추가 (윈도우 로직보다 우선, 종료 시 기본 반경 복원)
    - BeginFlash 시그니처 변경: (color, duration, intensity)
    - Time.time → Time.unscaledTime 전환 (UpdateFlash, BeginFlash, StartSuccessPulse)
    - EnsureAudioSource() 신설 — 2D, playOnAwake=false 자동 확보
    - BuildRingGeometryAtRadius(lr, radius) 분리 — 펄스 시 가변 반경 적용

LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiCombatFeedbackBinder.cs
    - hitStopOnParry 기본값 0.08 → 0.10
    - parryShakeIntensity 기본값 0.10 → 0.14
    - 필드 추가/제거 없음
```

씬 wiring (수동, Unity Editor 작업):

```text
LostMemory/Assets/Scenes/test_khi.unity
    Khi 플레이어 GameObject에 다음 3개 컴포넌트 부착:
      - KhiParryFeedbackPresenter (이번 티켓 메인)
      - KhiHitFlashPresenter (CL-017 누락분 보완)
      - KhiCombatFeedbackBinder (CL-017 누락분 보완)
    각 컴포넌트의 참조 필드는 자동 resolve(?? fallback)로 비워둠.
```

문서:

```text
client/docs/khi/cl018_parry_success_feedback_implementation.md
```

## 컴포넌트 구조

### KhiParryFeedbackPresenter (수정)

이벤트 구독 4개는 그대로 (`ParryStarted/Succeeded/Failed/Ended`). 분기별 동작:

| 이벤트 | 변경 전 | 변경 후 |
|---|---|---|
| ParryStarted | 링 enable + windowColor 적용 | 동일 (무수정) |
| ParrySucceeded | `BeginFlash(successColor)` 단일 호출 | **강화 플래시 + 링 펄스 + 스파크 스폰 + SFX** 4단계 |
| ParryFailed | `BeginFlash(failureColor)` | `BeginFlash(failureColor, flashDuration, 1f)` 시그니처 변경만, 동작 동일 |
| ParryEnded | 무조건 `ring.enabled = false` | **펄스 진행 중이면 ring 유지** (`_successPulseStartedAt < 0f` 가드) |

**성공 펄스 동작 (UpdateRing)**:
```text
if (_successPulseStartedAt >= 0f):
    elapsed = unscaledTime - _successPulseStartedAt
    if (elapsed < successRingDuration):
        radius = Lerp(ringRadius, ringRadius * successRingExpansion, t)
        BuildRingGeometryAtRadius(ring, radius)
        ring.color = successRingColor with alpha *= (1 - t)
        return  // 윈도우 로직보다 우선
    else:
        _successPulseStartedAt = -1
        BuildRingGeometry(ring)  // 기본 반경 복원
// 이후 윈도우 로직 진행
```

**Inspector 신규 파라미터 기본값**:

| 그룹 | 필드 | 기본값 |
|---|---|---|
| Success - Flash | `successFlashDuration` | 0.22s |
| Success - Flash | `successFlashIntensity` | 1.2 |
| Success - Ring Pulse | `successRingExpansion` | 2.0 |
| Success - Ring Pulse | `successRingDuration` | 0.25s |
| Success - Ring Pulse | `successRingColor` | (0.8, 0.95, 1.0, 1.0) |
| Success - Spark VFX | `successSparkPrefab` | (비어있음 — 후속 작업) |
| Success - Spark VFX | `sparkLifetime` | 0.4s |
| Success - SFX | `successSfx` | (비어있음 — 후속 작업) |
| Success - SFX | `sfxVolume` | 1.0 |

### KhiCombatFeedbackBinder (수정)

ParrySucceeded 분기 기본값만 톤업, 코드 흐름은 동일:

| 파라미터 | CL-017 | CL-018 | 비고 |
|---|---|---|---|
| `hitStopOnParry` | 0.08s | **0.10s** | 다른 이벤트(0.06/0.04)보다 더 강조. 너무 길면 반응성 해침. |
| `parryFlashColor` | cyan(0.5, 0.85, 1) | (동일) | 플레이어 스프라이트 전체 플래시 용도. |
| `parryFlashDuration` | 0.15s | (동일) | |
| `parryShakeIntensity` | 0.10 | **0.14** | |
| `parryShakeDuration` | 0.10s | (동일) | |

## 주요 설계 결정

### 1. 왜 SFX를 Binder가 아닌 Presenter에 두는가

Binder는 HitStun/Parry/TargetHit 3개 이벤트를 다루는 오케스트레이터. SFX 필드를
Binder에 추가하면 다른 이벤트도 일관되게 SFX 슬롯이 필요해지고, 결국 6~9개 AudioClip
필드가 늘어남. 패링 SFX는 패링 전용 컴포넌트(`KhiParryFeedbackPresenter`)에 두는 것이
관심사 분리상 자연스럽다. 다른 이벤트도 SFX가 필요해지면 각자 전용 Presenter 또는
별도 `KhiCombatSfxPresenter`로 묶는다.

### 2. 왜 Instantiate/Destroy (풀링 안 함)

패링 성공은 빈도가 낮음(타이밍 윈도우 0.16s, 쿨다운 존재). 풀 자료구조를 도입하면
초기화/반환 경로가 늘어 코드 표면이 커진다. CL-067 본격 VFX 시 다른 이펙트(피격/타격)와
함께 통합 풀링 검토. 현재는 `Destroy(spark, sparkLifetime)` 한 줄로 충분.

### 3. 왜 펄스 우선권 (윈도우 로직보다 먼저)

성공 직후 `KhiParryController` 상태는 빠르게 ParryWindow → Cooldown으로 빠지지만,
펄스 시각 효과(0.25s)는 그보다 길게 살아남아야 한다. UpdateRing에 펄스 분기를 먼저
두고 진행 중이면 `return` → 윈도우 로직이 ring을 강제로 끄지 못하게 함. 펄스 종료
프레임에 `BuildRingGeometry(ring)`로 반경 원복 → 다음 윈도우 사용 시 오염 없음.

### 4. 왜 unscaled time 전환

CL-017에서 `KhiHitFlashPresenter`는 unscaled, `KhiParryFeedbackPresenter`는 scaled로
일관성이 깨져 있었다. 패링 성공은 히트스톱(0.10s)을 동반하므로 scaled time 기준에선
플래시·펄스가 freeze 동안 정지 → 시각적 단절. CL-017과 동일하게 unscaled로 통일.

### 5. 왜 BeginFlash 시그니처 변경 (오버로드 아님)

기존 호출자는 HandleParrySucceeded와 HandleParryFailed 두 개뿐. 둘 다 새 시그니처로
업데이트 비용 미미. 오버로드를 두면 "어느 버전이 호출되는가" 추적 비용 발생. 단순
시그니처 통일.

### 6. 왜 successFlashIntensity가 알파 클램프됨에도 필드로 남기는가

현 구현은 `applied.a = Clamp01(color.a * intensity)`로 알파 1을 넘지 못함. successColor
기본 알파가 1이면 intensity>1은 시각 효과 없음. 그러나:

- 디자이너가 successColor 알파를 0.7~0.9로 낮추면 intensity가 의미를 가짐
- 향후 HDR overbright 셰이더 도입 시 RGB 곱셈으로 확장 가능 (필드 보존)
- Inspector에서 노출되어 있어 튜닝 의도 가시화

필드 제거는 시그니처 후퇴이므로 유지.

### 7. 왜 KhiHitFlashPresenter / KhiCombatFeedbackBinder 부착도 함께 했는가

CL-017 머지 후 씬 wiring이 누락된 상태였음 (코드만 들어가고 컴포넌트 미부착). CL-018
패링 피드백은 Binder의 `parry.ParrySucceeded` 분기와 `flashPresenter`에 의존하므로
세 컴포넌트가 모두 부착되어야 동작. 별도 핫픽스 티켓으로 분리하면 cl018 검증 자체가
불가능 → 같은 wiring 작업에 포함시킴 (블록킹 의존성).

## 검증

### 자동 검증

코드 구조 변경만 있고 컴파일 의존성/시그니처 외부 노출 변경 없음. 기존 호출자(없음)
대비 회귀 위험 0. dotnet build는 별도 실행 필요(미수행).

### 수동 검증 (Unity Editor Play Test) — 핵심 항목 통과

**전제 작업** (이 티켓에서 완료):

- [x] `Khi 플레이어 GameObject`에 `KhiParryFeedbackPresenter` 부착
- [x] 동일 오브젝트에 `KhiHitFlashPresenter` 부착 (CL-017 누락 보완)
- [x] 동일 오브젝트에 `KhiCombatFeedbackBinder` 부착 (CL-017 누락 보완)

**검증 결과**:

- [x] #1 패링 성공 → 링이 한 번 빠르게 확장(2배)되며 페이드 (기존엔 그냥 꺼졌음)
- [x] #2 패링 성공 → 플레이어 스프라이트에 시안 플래시(Binder 분기) + Presenter 강화 플래시
- [x] #3 패링 성공 → 카메라 쉐이크 0.10 → 0.14로 체감 증가
- [x] #4 히트스톱(0.10s) 중에도 링 펄스/플래시가 시각적으로 진행 (unscaled 검증)
- [x] #5 패링 실패 → 빨간 플래시 정상 (회귀 없음)
- [ ] #6 연속 패링 시 펄스 잔상/이중 트리거 — **환경 미검증** (코드상 안전: 새 펄스가 timer 덮어쓰기, HandleParryEnded는 펄스 중 ring 유지)
- [ ] #7 SFX 재생 — **AudioClip 미연결로 미검증** (`successSfx` 슬롯 비어 있으면 PlayOneShot 스킵)

### 파라미터 튜닝

Inspector에서 `successRingDuration`, `successFlashDuration`, `successRingExpansion` 등
즉시 조정 가능. 기본값은 시작점이며 CL-019 체크리스트 시 본격 튜닝.

## 미해결 / 알려진 이슈

### successSparkPrefab 미연결 — 스파크 VFX 미동작

스파크 프리팹 자체 생성은 별도 작업으로 분리(에디터 작업, 스프라이트 임포트 필요).
슬롯이 비어 있으면 `SpawnSuccessSpark()`는 즉시 return → 다른 연출(링/플래시/SFX)은
정상 진행. 후속 작업으로 임시 프리팹 구성 필요:

권장 구조: 빈 GameObject + SpriteRenderer(`MMParticlesFlash.png`) + 자식 4~8개에
SpriteRenderer(작은 라인 스프라이트) + 위치/회전 차등.

### successSfx 미연결 — 사운드 피드백 미동작

후보: `Assets/TopDownEngine/ThirdParty/MoreMountains/MMInterface/Common/Sounds/Ding.wav`,
`Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftGlassyTap.wav`. Inspector에서 시범 연결 후
체감 평가 필요. 비어 있으면 `PlaySuccessSfx()`는 즉시 return → 안전.

### successFlashIntensity가 알파 1 초과 시 무효

상기 "주요 설계 결정 #6" 참조. 현재 알파 클램프로 intensity>1은 효과 없음. successColor
알파를 1 미만으로 설정하거나, 향후 커스텀 셰이더(`_FlashColor`, `_FlashAmount`) 도입 시
의미 부활.

### 펄스 종료 직후 ParryStarted 발화 시 1프레임 색상 깜빡임

연속 패링 시나리오에서, 펄스 진행 중에 사용자가 다시 패링 입력 → `HandleParryStarted`가
ring 색상을 windowColor로 설정. 다음 프레임 UpdateRing의 펄스 분기가 다시
successRingColor로 덮어씀 → 1프레임 깜빡임. 빈도/지각 가능성 낮음, 후속 튜닝 시
필요하면 `HandleParryStarted`에 펄스 가드 추가.

### 다른 SpriteRenderer flash 시스템과의 충돌

CL-017 `KhiHitFlashPresenter`는 MaterialPropertyBlock 사용, CL-018 `flashSprite`는
SpriteRenderer.color 직접 변경. 서로 다른 SpriteRenderer를 대상으로 하면 무관하지만
같은 SpriteRenderer에 둘 다 적용 시 마지막 호출이 승리. 현재 wiring에서는 분리되어
있어 안전.

## 후속 CL 연결

- **(이 티켓 보류 분) successSparkPrefab 생성**: 스프라이트 기반 임시 프리팹 작성 후
  Inspector 연결. 별도 작은 작업으로 처리.
- **(이 티켓 보류 분) successSfx 연결**: 위 후보 클립 시범 연결 후 체감 평가.
- **CL-019 전투 감각 체크리스트**: 본 티켓의 8개 신규 파라미터가 튜닝 대상.
  체크리스트 기준값 통과 시 그 값으로 고정.
- **CL-067 본격 VFX/사운드**: `successSparkPrefab`을 정식 VFX로 교체.
  `KhiParryFeedbackPresenter`의 `SpawnSuccessSpark()`/`PlaySuccessSfx()` 인터페이스는
  유지, 에셋만 교체.
- **DownEntered 피드백** / **카메라 줌인** / **슬로우모션** / **포스트 프로세싱**: 이번 범위 외.
  필요 시 별도 티켓.

## 관련 문서

- 직전 CL: [cl017_hit_feedback_implementation.md](cl017_hit_feedback_implementation.md)
- 패링 원천 구현: [cl012_timing_parry_implementation.md](cl012_timing_parry_implementation.md)
- 패링 보류 검증: [cl012_parry_pending_verification.md](cl012_parry_pending_verification.md)
- 마스터 플랜: [client1_tasks_master_plan.md:38](client1_tasks_master_plan.md) (CL-018 정의)
- 구현 계획 (로컬): `C:\Users\SSAFY\.claude\plans\eventual-snuggling-cloud.md`
- 씬/프리팹 안전 규칙: [agent-unity-safety-rules.md](../commonness/agent-unity-safety-rules.md)
