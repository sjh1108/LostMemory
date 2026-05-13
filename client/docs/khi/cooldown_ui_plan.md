# 패링 / 대시 쿨타임 + 인벤토리 키 힌트 UI 구현 계획

## Context
Khi 캐릭터는 패링(우클릭)과 대시(스페이스 등) 양쪽 다 쿨타임이 있어 실패하면 일정 시간 사용 불가. 현재는 사용자가 입력해보기 전엔 사용 가능 여부를 알 수 없어 타이밍 게임플레이의 핵심 정보가 빠져 있음. 화면에 두 능력의 쿨타임 잔량을 시각적으로 표시해서 의사 결정에 사용할 수 있게 한다.

추가로 인벤토리(I 키) 단축키를 플레이어가 모를 수 있어 가방 아이콘 + "I" 텍스트로 키 힌트를 같은 HUD에 표시.

기존 HUD 시스템(`PlayerHUD.prefab`, `HealthBarView`, `PlayerHUDPresenter`)이 이미 잡혀 있으므로 동일 패턴으로 확장.

## 결정 사항 (사용자 선택)
- **UI 형태**: 라디얼(원형) 아이콘 + dim overlay + 카운트다운 숫자
- **위치**: `PlayerHUD.prefab` 내부, HP 바 오른쪽 옆에 2개 슬롯
- **패링 표시**: FailureRecovery + Cooldown 합쳐서 한 게이지 (단순)
- **아이콘 sprite**: 임시 단색 sprite로 먼저 구현 → 추후 디자이너 sprite로 교체
- **단계별 색 구분** 같은 고급 디테일은 v2로 미룸
- **인벤토리 힌트**: 가방 아이콘 + "I" 키 라벨, 정적 표시 (이벤트 구독 없음, v1 단순화)

## 아키텍처

### 책임 분리
- **View** (`CooldownIndicatorView`): 표시만. 데이터 받아서 fill/text 업데이트.
- **Presenter** (`ParryCooldownPresenter`, `DashCooldownPresenter`): 컨트롤러 이벤트 구독 + Update 폴링 → View 호출.
- **Controller**(KhiParryController/KhiDashController): UI 모름. read-only getter만 노출.

### 다이어그램
```
KhiParryController ──(ParryStarted/Ended + getters)─→ ParryCooldownPresenter ─→ CooldownIndicatorView A
KhiDashController.Cooldown (MMCooldown.OnStateChange + Progress) ─→ DashCooldownPresenter ─→ CooldownIndicatorView B
```

## 작업 단계

### 1. 코드 작성

#### 1-1. `KhiParryController.cs` — getter 추가 (비파괴, 1줄)
```csharp
/// <summary>FailureRecovery + Cooldown 합산 총 길이. UI 진행률 계산용.</summary>
public float ParryCooldownTotalDuration => parryFailureRecovery + parryCooldown;
```
(또는 별도로 `ParryCooldownDuration => parryCooldown` 노출)

#### 1-2. `CooldownIndicatorView.cs` (신규, ~50줄)
경로: `Assets/_Project/Scripts/Runtime/UI/CooldownIndicatorView.cs`

`HealthBarView` 패턴 그대로:
- `[SerializeField] Image fillImage` (Type=Filled, Method=Radial360)
- `[SerializeField] Image iconImage` (어둡게 처리될 메인 아이콘)
- `[SerializeField] TextMeshProUGUI remainingText`
- `[SerializeField] CanvasGroup rootGroup` (전체 표시/숨김)
- `[SerializeField] float readyAlpha = 1f, cooldownAlpha = 0.4f`
- Public API:
  - `void SetReady()` — fill=1, text="", icon alpha=1
  - `void SetCooldown(float progress01, float remainingSeconds)` — fill=progress, text=문자열, icon alpha=0.4
  - `void SetVisible(bool v)` — CanvasGroup alpha 토글

#### 1-3. `ParryCooldownPresenter.cs` (신규, ~70줄)
경로: `Assets/_Project/Scripts/Runtime/UI/ParryCooldownPresenter.cs`

- `[SerializeField] CooldownIndicatorView view`
- `[SerializeField] KhiParryController parry` (자동 resolve: `PlayerHUDPresenter`의 LocalPlayerResolver 헬퍼 재사용 또는 같은 패턴 복제)
- OnEnable: `parry.ParryStarted += HandleStart; parry.ParryEnded += HandleEnd;`
- OnDisable: 해제
- Update: `parry.IsParryInCooldown || state==FailureRecovery` 이면 `view.SetCooldown(progress, remaining)`, 아니면 `view.SetReady()`
- 진행률 계산: `1f - parry.CurrentStateRemaining / parry.ParryCooldownTotalDuration`

#### 1-4. `DashCooldownPresenter.cs` (신규, ~70줄)
경로: `Assets/_Project/Scripts/Runtime/UI/DashCooldownPresenter.cs`

- `[SerializeField] CooldownIndicatorView view`
- `[SerializeField] KhiDashController dash`
- OnEnable: `dash.Cooldown.OnStateChange += HandleStateChange;`
- Update: `Cooldown.CooldownState` 에 따라:
  - `Idle` → `view.SetReady()`
  - 그 외(Consuming/Stopped/Refilling) → progress 계산 후 `view.SetCooldown`
  - progress: `1f - Cooldown.CurrentDurationLeft / Cooldown.RefillDuration` (Refilling 시), Consuming 시 0
- 주석으로 MMCooldown.Progress의 함정(Refilling 외엔 0 반환) 명시

### 2. Unity 작업

#### 2-1. 임시 sprite 준비
- `Assets/_Project/Sprites/UI/Cooldown/` 폴더 생성
- 단색 원/사각형 sprite 2개 (parry_icon_temp.png, dash_icon_temp.png) 또는 Unity 기본 UI/Circle 사용
- 가방 아이콘 임시 (bag_icon_temp.png) — 단색 사각형 또는 Unicode 🎒 텍스트로 대체 가능

#### 2-2. PlayerHUD.prefab 편집
- HP 바 RectTransform 옆에 GameObject 3개 추가:
  - `ParryCooldownIndicator` (CooldownIndicatorView + ParryCooldownPresenter)
  - `DashCooldownIndicator` (CooldownIndicatorView + DashCooldownPresenter)
  - `InventoryKeyHint` (정적 — 자식: Icon(Image, 가방 sprite), Label(TMP, "I"))
- 쿨타임 인디케이터 각각에 자식: Icon(Image), Fill(Image, Filled/Radial360), Text(TMP)
- `CooldownIndicatorView` 컴포넌트 부착 + 직렬화 필드 와이어링
- 해당 Presenter 컴포넌트도 같은 GameObject에 부착 + view 필드 self-reference, parry/dash 컨트롤러는 자동 resolve 또는 LocalPlayerReady 이벤트로 채움
- `InventoryKeyHint`는 정적 UI — 컴포넌트/스크립트 부착 불필요 (그냥 Image + TMP)
- **diff 최소화**: HP/MP 자식들의 RectTransform 위치는 건드리지 말 것

#### 2-3. anchor/position 설정
- HP 바 우측 끝을 기준으로 30~50px 오른쪽 시작 → [패링][대시] 두 라디얼
- 인벤토리 힌트는 별도 위치 (예: 우상단 또는 쿨타임 아이콘 옆 빈 자리)
- 쿨타임 아이콘 사이즈 ~48x48 권장
- 인벤토리 힌트는 아이콘 32x32 + 옆에 "I" 라벨 (TMP, 14~16pt)
- 사이 간격 8~12px

### 3. 검증
- PlayMode 진입
- 패링 시도(우클릭) → 성공/실패 둘 다 시도 → 라디얼 진행률 + 카운트다운이 잘 보이는지
- 대시(스페이스) → 동일하게 확인
- 두 능력 거의 동시 사용 시 둘 다 독립적으로 동작하는지
- 쿨타임 끝나면 즉시 Ready 상태 복귀하는지

## 핵심 파일

### 수정
- `Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs` (read-only getter 1~2줄 추가)
- `Assets/_Project/Prefabs/UI/PlayerHUD.prefab` (자식 GameObject 2개 추가, 기존 컴포넌트 미변경)

### 신규
- `Assets/_Project/Scripts/Runtime/UI/CooldownIndicatorView.cs`
- `Assets/_Project/Scripts/Runtime/UI/ParryCooldownPresenter.cs`
- `Assets/_Project/Scripts/Runtime/UI/DashCooldownPresenter.cs`

### 참고용 (재사용 패턴)
- `Assets/_Project/Scripts/Runtime/UI/HealthBarView.cs` — View 패턴
- `Assets/_Project/Scripts/Runtime/UI/PlayerHUDPresenter.cs` — Presenter + LocalPlayerResolver 패턴

## 위험/주의

| 위험 | 대응 |
|---|---|
| `MMCooldown.Progress` 가 Refilling 상태에서만 0~1 반환 (다른 상태에선 0) | Presenter에서 `1f - CurrentDurationLeft / RefillDuration` 직접 계산. 주석으로 함정 명시 |
| PlayerHUD.prefab 공용 수정 (안전 규칙) | HP/MP 직렬화 필드 절대 건드리지 않음. 자식 GameObject **추가만** → diff는 YAML 말미에 append만 발생. 회귀 테스트로 HP 바 동작 확인 |
| TopDownEngine 원본 보호 | `MMCooldown` 읽기만, 수정 X. `CharacterDash2D` 미터치 (KhiDashController 통해서만 접근) |
| KhiParryController API 추가가 기존 동작에 영향 | read-only getter만 추가 → 호출 사이트 없음, 영향 0 |
| Netcode (원격 플레이어 UI) | `PlayerHUDPresenter`처럼 LocalPlayer만 추적. 기존 동작과 일치 |

## 작업량 추정
- 코딩: **~90분** (View 20 + ParryPresenter 35 + DashPresenter 25 + Controller getter 5 + 정리 5)
- Unity: **~75분** (sprite 임시 준비 15 + prefab 편집 40 + anchor/와이어링 20)
  - 인벤토리 힌트는 Image+TMP 추가만이라 +15분 정도
- **총 ~2.5~3시간**

## 검증 시나리오
1. 시작 직후: 패링/대시 아이콘 둘 다 Ready 상태 (밝음, fill=1), 인벤토리 힌트 보임
2. 대시(스페이스) → 대시 아이콘 라디얼 채워짐 + 숫자 카운트다운 → 끝나면 Ready 복귀
3. 패링(우클릭) → 성공 시 짧은 쿨다운, 실패 시 긴 FailureRecovery+Cooldown 통합 표시 → 끝나면 Ready
4. 패링+대시 거의 동시 사용 → 두 아이콘 독립적 진행
5. 인벤토리 힌트는 항상 정적으로 보임 (v2에서 인벤토리 열림 시 숨기는 기능 추가 검토)
6. HP 바 / MP 바 동작 회귀 없음 확인
