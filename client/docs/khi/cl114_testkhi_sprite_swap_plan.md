# cl114 — TestKhi_MinimalCharacter2D Sprite/Animation 교체 절차서

## 목적

`Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` 의 placeholder sprite 를 `Assets/_Project/Art/Characters/Sprite/hero.png` 기반의 캐릭터 외형 + 4방향 이동/특수 상태 애니메이션 풀셋으로 교체한다.

본 문서는 Unity Editor 에서 사용자가 직접 따라하는 절차이다. 자동화 스크립트는 사용하지 않는다.

상호 참조: [`character-replacement-guide.md`](./character-replacement-guide.md) — "권장 방식: 현재 테스트 프리팹을 복제해서 외형만 바꾸는 방식" 의 실제 1차 적용에 해당.

## 스코프

- 변경 대상: `TestKhi_MinimalCharacter2D.prefab` 만
- 제외: `TestKhi_Net_AD.prefab` (동일 placeholder 사용 중이지만 별도 티켓에서)
- 변경 범위: `MinimalCharacterModel` 자식 GameObject 의 SpriteRenderer.Sprite + 신규 Animator + `_Project/Animations/Characters/TestKhi/` 신규 자산
- 변경하지 않음: TDE Character 루트 컴포넌트 구조, `PlayerID`, `CharacterType`, Layer/Tag, Rigidbody2D, BoxCollider2D, 무기 부착(`WeaponAttachment`), 패리/대시/슬래시 관련 컴포넌트, SortingLayer/SortingOrder

## 사전 사실 확인

| 항목 | 값 |
|---|---|
| Prefab 경로 | `LostMemory/Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` |
| 변경 대상 SpriteRenderer | `MinimalCharacterModel` 자식 (prefab line 1145~1203) |
| 현재 sprite (placeholder) GUID | `eeb68d44a260e964584c4310ad5add3a` (hero.png 아님) |
| 현재 Animator 컴포넌트 | 없음 — Transform + SpriteRenderer 만 존재 |
| 신규 Sprite 시트 | `LostMemory/Assets/_Project/Art/Characters/Sprite/hero.png` |
| Sprite 시트 GUID | `13786762124fab74880fe357f2993310` |
| 시트 픽셀 크기 | 502×16 |
| 검증 그리드 | 16×16 cell, spacing 2×0, offset 0×0, 정확히 28셀 (`28*16 + 27*2 = 502`) |
| 현 슬라이스 상태 | hero_0~hero_30 잘못 잘림 (인덱스 7/14/22 결번, 셀 23/24 1px jog) — 재슬라이스 필요 |
| Pixels Per Unit | 64 (유지) |
| 기존 .controller / .anim | 없음 — 모두 신규 생성 |
| 시트 GUID 외부 참조 | 0건 (재슬라이스 안전) |

## 셀 ↔ 애니메이션 매핑 (확정 — 1차 적용 6 클립)

번호는 1-indexed 시각적 위치(좌→우). 재슬라이스 후 sprite 이름은 `hero_0`~`hero_27` (Unity 자동 명명, 0-indexed).

디자이너 GIF (`stay_front`, `run_front`, `stay_back`, `run_back`, `hurt_front`) 와 단일 프레임 head-down 포즈에 맞춰 **6개 클립** 을 만든다. 셀 14~22, 25~27 은 본 1차 적용 범위에서 사용하지 않음 (후속 작업으로 보존).

| 셀(1-idx) | 0-idx | 카테고리 설명 | 사용 클립 |
|---:|---:|---|---|
| 1~3   | 0~2   | 정면 idle (stay_front) | `Front_Idle` |
| 4~7   | 3~6   | 정면 walk (run_front) | `Front_Walk` |
| 8~10  | 7~9   | 후면 idle (stay_back) | `Back_Idle` |
| 11~14 | 10~13 | 후면 walk (run_back) | `Back_Walk` |
| 24    | 23    | 피격 (붉은 톤, hurt_front) | `Front_Hurt` |
| 25    | 24    | 눈 감음 / 기절 / head-down | `Front_Head` |
| 15~22, 26~28 | 14~22, 25~27 | 보조/플래시/대체 idle | (1차 적용 미사용 — F 후속) |

> **사용자 확인 필요**: 위 표에서 `Front_Head` 를 셀 24(눈 감음/기절) 로 가정. 다른 셀(예: 셀 25 의 가장 깔끔한 정면 idle 단일 프레임) 이라면 알려주세요.

### 자연 facing 가정

- **Front** sprite 는 캐릭터가 **왼쪽 아래(↙)** 를 보고 있는 자세
- **Back**  sprite 는 캐릭터가 **오른쪽 위(↗)** 를 보고 있는 자세

Front 와 Back 의 기본 facing 이 **정반대 대각선** 이라서, 좌/우 반전(flipX) 룰이 두 상태에서 **반대로** 적용된다 (D 섹션 5번 참조). 4 cardinal 매핑:

| 입력 | 사용 상태 | flipX |
|---|---|:---:|
| ↓ Down  | Front | false (자연 좌하향 그대로) |
| ↑ Up    | Back  | false (자연 우상향 그대로) |
| ← Left  | Front | false (Front 가 좌측 향함) |
| → Right | Front | true  (Front 를 좌→우로 미러) |
| ↑→ UpRight | Back  | false |
| ↑← UpLeft  | Back  | true  (Back 을 우→좌로 미러) |

본 시트만으로는 정면 idle/walk + 후면 idle/walk + flipX 로 4방향(+ 대각선) 을 커버. 측면 전용 시트는 후속 작업.

## A. hero.png 재슬라이스

1. Project 창에서 `Assets/_Project/Art/Characters/Sprite/hero.png` 선택
2. Inspector 우측 상단 **Sprite Editor** 클릭
3. 좌상단 **Slice** 패널 열기
   - Type: **Grid By Cell Size**
   - Pixel Size: **16 × 16**
   - Offset: **0 × 0**
   - Padding: **2 × 0**
   - Pivot: **Center** (0.5, 0.5)
   - Method: **Delete Existing**
4. **Slice** 클릭 → 우상단 **Apply**
5. 시트 안에 **28개** 정렬된 sprite (`hero_0`~`hero_27`) 가 보이는지 확인. 결번/jog 가 모두 사라져야 함
6. Inspector 의 **Texture Importer** 옵션 점검·유지 (이미 맞으면 변경 없음):
   - Texture Type: Sprite (2D and UI)
   - Sprite Mode: **Multiple**
   - Pixels Per Unit: **64**
   - Filter Mode: **Point (no filter)**
   - Compression: **None**
7. 만약 다른 자산이 hero 시트의 개별 sprite 를 참조하던 경우 끊어진다. 본 작업 시작 전 기준 외부 참조 0건이므로 안전. (확인 명령: 프로젝트 루트에서 `grep -rl "13786762124fab74880fe357f2993310" client/LostMemory/Assets/`)

## B. 폴더 준비 및 .anim 6개 생성

폴더가 없으면 생성:

```text
LostMemory/Assets/_Project/Animations/
LostMemory/Assets/_Project/Animations/Characters/
LostMemory/Assets/_Project/Animations/Characters/TestKhi/  (또는 Hero/)
```

> 클립 파일명 prefix 는 사용자가 만든 실제 이름(`Hero_*`) 기준으로 표기. plan 의 `TestKhi_*` 표기는 모두 `Hero_*` 와 호환되므로 둘 중 일관된 prefix 를 쓰면 됨.

각 클립은 Animation 창에서 Sample Rate=10 으로 먼저 맞추고 sprite 들을 트랙에 드래그하는 방식으로 작성. 단일 프레임 정지 클립은 Sample=1.

| 파일명 (예시) | 사용 sprite (0-idx) | 프레임 수 | Sample | Loop |
|---|---|---:|---:|:---:|
| `Hero_Front_Idle.anim` | hero_0, hero_1, hero_2             | 3 | 10 | ✓ |
| `Hero_Front_Walk.anim` | hero_3, hero_4, hero_5, hero_6     | 4 | 10 | ✓ |
| `Hero_Back_Idle.anim`  | hero_7, hero_8, hero_9             | 3 | 10 | ✓ |
| `Hero_Back_Walk.anim`  | hero_10, hero_11, hero_12, hero_13 | 4 | 10 | ✓ |
| `Hero_Front_Hurt.anim` | hero_23                            | 1 | 1  | ✗ |
| `Hero_Front_Head.anim` | hero_24                            | 1 | 1  | ✗ |

각 클립 작성 절차 (공통):
1. Animation 창에서 클립 생성 (Project 우클릭 Create → Animation, 또는 sprite 들을 임시 GO 에 드래그-앤-드롭)
2. Animation 창 ⋮ 메뉴 → **Show Sample Rate** 활성화 → 좌상단 Samples 필드 **10** (단일 프레임은 1)
3. 키프레임이 sample 위치 0, 1, 2, 3 (= 0/0.1/0.2/0.3 초) 에 정렬되어 있는지 확인. 안 맞으면 마우스로 드래그해 정렬 또는 삭제 후 재드래그
4. Project 창에서 .anim 클릭 → Inspector → **Loop Time** 체크박스를 표대로 설정

> **셀 경계 ±1 조정 가능**: `Front_Idle` (0~2) ↔ `Front_Walk` (3~6) 경계는 추정값. 검증 단계에서 stay/run 의 시각적 분기가 어색하면 셀 1개를 옆 클립으로 옮긴다. 후면(7~13) 도 동일.

## C. AnimatorController 생성

1. `LostMemory/Assets/_Project/Animations/Characters/TestKhi/` 에 우클릭 → `Create` → `Animator Controller` → `TestKhi_Animator.controller`
2. 더블클릭으로 Animator 창 열기
3. 좌측 **Parameters** 탭에 추가 (TDE 표준 자동 set + 커스텀 트리거 혼합):

   | 파라미터 | 타입 | 기본값 | Set 주체 |
   |---|---|---|---|
   | `Speed`         | Float   | 0      | TDE `CharacterMovement` 자동, KhiAnimatorMovementBinder 가 LateUpdate 에서 평활값으로 덮어씀 |
   | `Walking`       | Bool    | false  | TDE 자동 (보조 — 미사용 가능, 그래도 선언만 해두면 sanity 경고 회피) |
   | `Idle`          | Bool    | true   | TDE 자동 (보조) |
   | `MovementY`     | Float   | 0      | KhiAnimatorMovementBinder (KhiPlayerAim.aim.y 매핑) |
   | `Hit`           | Trigger | -      | `KhiHitStunController.TrySetAnimatorTrigger` (Health.OnHit 시) |
   | `Down`          | **Trigger** | -  | `KhiDownController.TrySetAnimatorTrigger` (HP 0 시) — **Bool 아님** |
   | `Revive`        | Trigger | -      | `KhiDownController.TrySetAnimatorTrigger` (CompleteRevive 시) |
   | `WalkSpeedMul`  | Float   | 1.0    | 코드 또는 ScriptableObject 인스펙터 (Walk state Speed multiplier 묶음) |

   > **주의 (cl114 도중 발견)**: `KhiDownController` 의 `TrySetAnimatorTrigger` 가 `Down` 도 `SetTrigger` 로 호출함 ([KhiDownController.cs:487-500](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs)). 따라서 `Down` 은 Bool 이 아니라 **Trigger** 로 만들어야 함. Trigger 는 한 번 발사 후 자동 소비되며, Dead state 진입 후엔 Dead → Front_Idle (Revive 트리거) 만 fire 가능하므로 Down 상태 유지.

4. **States** 추가 (드래그앤드롭으로 .anim 끌어다 놓기) — 총 6개:
   - `Front_Idle` — `Hero_Front_Idle.anim` (우클릭 → **Set as Layer Default State**)
   - `Front_Walk` — `Hero_Front_Walk.anim`  (Inspector 의 **Speed** 우측 Multiplier 체크 → `WalkSpeedMul`)
   - `Back_Idle` — `Hero_Back_Idle.anim`
   - `Back_Walk` — `Hero_Back_Walk.anim`   (Inspector 의 **Speed** Multiplier → `WalkSpeedMul`)
   - `Hurt` — `Hero_Front_Hurt.anim`
   - `Head` — `Hero_Front_Head.anim`
5. (Reserved Sub-State Machine 미사용 — 본 단계 클립 없음. 추후 보조 프레임 사용 시 F 후속 작업에서 추가)
6. **Transitions** (Has Exit Time 은 명시된 경우만 ✓, Transition Duration 은 별도 명시 외 0.05s):

   | From | To | 조건 | Has Exit Time |
   |---|---|---|:---:|
   | Front_Idle | Front_Walk | `Speed > 0.1` AND `MovementY < 0.1`   | ✗ |
   | Front_Idle | Back_Walk  | `Speed > 0.1` AND `MovementY > 0.1`   | ✗ |
   | Back_Idle  | Back_Walk  | `Speed > 0.1` AND `MovementY > -0.1`  | ✗ |
   | Back_Idle  | Front_Walk | `Speed > 0.1` AND `MovementY < -0.1`  | ✗ |
   | Front_Walk | Front_Idle | `Speed < 0.1` | ✗ |
   | Back_Walk  | Back_Idle  | `Speed < 0.1` | ✗ |
   | Front_Walk | Back_Walk  | `MovementY > 0.1`  | ✗ |
   | Back_Walk  | Front_Walk | `MovementY < -0.1` | ✗ |
   | Any State  | Hurt       | `Hit` (트리거) | ✗ |
   | Hurt       | Front_Idle | (조건 없음) | ✓ (Exit Time 0.95) |
   | Any State  | Head       | `Down` (트리거) | ✗ |
   | Head       | Front_Idle | `Revive` (트리거) | ✗ |

   > 측면(Left/Right) 전이는 본 단계에서 추가하지 않는다 — `SpriteRenderer.flipX` 토글로만 처리 (D 섹션 5번 참조).
   > AnyState transition (`Hurt`, `Head`) 의 Inspector 에서 **`Can Transition To Self` 체크 해제** — 진입 후 같은 트리거가 재발사될 때 자기 자신으로 루프 도는 것을 방지.

## D. Prefab 적용

1. Project 창에서 `TestKhi_MinimalCharacter2D.prefab` 더블클릭 → Prefab 편집 모드 진입
2. Hierarchy 에서 `TestKhi_MinimalCharacter2D / MinimalCharacterModel` 선택
3. Inspector → **SpriteRenderer**:
   - **Sprite** 필드를 hero.png 의 **`hero_0`** (= `Front_Idle` 의 첫 프레임) 로 교체
   - Draw Mode: **Simple** 인지 확인 (Tiled/Sliced 가 아니어야 함)
   - SortingLayer / Order in Layer / Color / Flip X / Flip Y / Material — **건드리지 않는다** (flipX 토글은 런타임 코드에서만 — 5번 항목 참조)
4. 같은 GameObject 에 **Animator** 컴포넌트 추가:
   - Add Component → `Animator`
   - Controller: `TestKhi_Animator.controller` 드래그
   - Avatar: None
   - Apply Root Motion: ✗
   - Update Mode: Normal
   - Culling Mode: **Always Animate** (네트워크 동기화 안전)
5. **`KhiSpriteFlipBinder` 컴포넌트 부착** — 마우스 기반 좌/우 facing 매핑:
   - `MinimalCharacterModel` 자식 GameObject 선택 → Add Component → "Khi Sprite Flip Binder" 검색 → 추가
   - `Aim` 필드: 비워두면 Awake 에서 부모의 `KhiPlayerAim` 자동 탐색. 명시 바인딩하려면 루트 `TestKhi_MinimalCharacter2D` 를 드래그
   - `Aim Dead Zone X`: 기본값 `0.05` — `|aim.x|` 가 이 값 이하면 facing 갱신 건너뜀 (마우스가 캐릭터 X 라인 근처일 때 떨림 방지)
   - `Initial Facing Right`: 시작 시 캐릭터 facing (기본 true = 오른쪽)
   - `Back Idle State Name` / `Back Walk State Name`: 기본값 `Back_Idle` / `Back_Walk` (Animator state 이름과 일치해야 함)
   - 동작 룰 (코드 내장):
     - Front 계열 (`Front_Idle`, `Front_Walk`, `Hurt`, `Head`): 자연 ↙ → 우측 facing 시 미러 → `flipX = facingRight`
     - Back  계열 (`Back_Idle`, `Back_Walk`):                자연 ↗ → 좌측 facing 시 미러 → `flipX = !facingRight`
   - facing 결정은 `KhiPlayerAim.GetAimDirection()` 의 X 부호로 — 마우스 위치 기반 (이동 키와 무관)
   - 옆구리 부착 회피 정책 덕분에 무기·슬래시·패리 FX 위치 보정 코드는 **불필요**

6. **`KhiAnimatorMovementBinder` 컴포넌트 부착** — 마우스 기반 Front/Back 분기:
   - 같은 `MinimalCharacterModel` 에 Add Component → "Khi Animator Movement Binder" → 추가
   - `Aim` 필드: 비워두면 Awake 에서 부모의 `KhiPlayerAim` 자동 탐색
   - `MovementY Param`: 기본값 `MovementY` 그대로 (Animator 파라미터 이름과 일치)
   - 동작: `Animator.SetFloat("MovementY", aim.y)` 매 프레임 호출. 마우스가 캐릭터 위쪽 → MovementY > 0.1 → `Back_Walk` (이동 시) 트리거. 아래쪽 → MovementY < 0.1 → `Front_Walk`
   - 파라미터 이름 `MovementY` 유지하지만 실제 의미는 "마우스 Y 방향" (Animator 호환을 위해 이름 유지)
   - Speed 는 TDE `CharacterMovement` (UseDefaultMecanim=true) 가 자동 set 하므로 본 바인더는 MovementY 만 담당
   - `WalkSpeedMul` 의 기본 1.0 은 controller 기본값으로 충분 — 디자이너 조정 시점에만 코드/SO 로 노출
7. Hierarchy 루트 `TestKhi_MinimalCharacter2D` 선택 → Inspector 에서 TDE **Character** 컴포넌트의 **Character Animator** 필드에 위에서 추가한 Animator 를 드래그 (현재 None)

8. **Hit / Down 자동 트리거 — Animator 참조만 명시**:
   - **`Khi Hit Stun Controller`** → `Animator` 필드에 `MinimalCharacterModel` 드래그, `Hit Animator Trigger Name` = `Hit`
   - **`Khi Down Controller`** → `Animator` 필드에 `MinimalCharacterModel` 드래그, `Down/Revive Animator Trigger Name` 기본값 (`Down`/`Revive`) 유지
   - **`Health`** (TDE) → `Target Animator` 필드에 `MinimalCharacterModel` 드래그 (두 컨트롤러가 1순위로 읽음)
   - cl114 진행 중 `setAnimatorTrigger(s)` 토글 필드는 **제거됨** — 이제 Animator 가 바인딩되어 있고 파라미터 이름이 일치하면 자동으로 SetTrigger 호출. 토글 ON/OFF 신경 쓸 필요 없음

9. 좌상단 **<** 화살표로 Prefab Stage 빠져나오며 **Save** 확인

> 5/6 의 binder 스크립트는 본 절차서의 **Unity Editor 작업 영역 밖** (코드 작성). 작은 별도 commit 으로 처리 권장. 스크립트가 아직 없으면 prefab 만 먼저 적용하고 Animator 창에서 `MovementY` 를 수동 토글해 검증 가능 (E 섹션).

## E. 검증

### E-1. 단일 씬 Play 검증
1. `Assets/Scenes/test_khi.unity` (또는 `Assets/Scenes/MVP1/MVP1_testkhi.unity`) 열기
2. Play 진입
3. 캐릭터가 placeholder 가 아닌 hero 도트로 보이는가?
4. 정지 시 `Front_Idle` (hero_0~2 루프) 가 부드럽게 재생되는가? (단일 프레임 정지가 아니어야 함)
5. WASD/방향 입력 시:
   - 아래(S) → `Front_Walk` (hero_3~6) 재생
   - 위(W)   → `Back_Walk` (hero_10~13) 재생
   - 정지 후 위쪽으로 움직였다 멈추면 → `Back_Idle` (hero_7~9) 로 복귀
   - 좌(A) / 우(D) → 직전 facing 의 walk + flipX 토글로 좌우 전환 (binder 스크립트 적용 후)
   - binder 스크립트 적용 전이면 좌/우 시 Front 또는 Back walk 가 재생되어도 OK
6. **stay/run 경계 검증** — `Front_Idle` 와 `Front_Walk` 의 시각적 분기가 자연스러운가? 어색하면:
   - hero_3 이 idle 처럼 보이면 → `Front_Idle` 에 hero_3 추가, `Front_Walk` 에서 제거
   - hero_2 가 walk 처럼 보이면 → `Front_Walk` 에 hero_2 추가, `Front_Idle` 에서 제거
   - 후면(hero_7~13) 도 동일 규칙
7. **WalkSpeedMul 검증** — Animator 창에서 `WalkSpeedMul` 을 0.5/2.0 으로 수동 토글 → walk 클립 속도가 즉시 변하는지 확인. 기본 1.0 으로 복귀
8. 데미지 트리거 (KhiHealth 호출 또는 Animator 창에서 수동으로 `Hit` 트리거 토글) → `Hurt` (Front_Hurt 클립) 재생 후 자동 복귀
9. `Down` Bool 토글 → `Head` (Front_Head 클립) 진입 / `Revive` 트리거 → 복귀
10. **flipX 룰 검증** — 캐릭터를 오른쪽으로 이동시키면 Front 가 미러됨(좌하 → 우하 방향). 위쪽으로 이동시키면 Back 이 자연 그대로(우상). 위쪽 + 왼쪽 이동 시 Back 이 미러됨(우상 → 좌상)

### E-2. 회귀 체크
- **오른쪽 보기 / 왼쪽 보기 둘 다** 무기·슬래시·패리·대시가 시각적으로 정합 (옆구리 부착 회피 정책 덕분에 자동 정합 예상)
- 무기 부착(`WeaponAttachment`) / `KhiSlashAnimator` 슬래시 이펙트 / 패리(`KhiParrySpark`) / 대시 잔상(`KhiDashAfterimage`) — 정상
- SortingGroup / SortingLayer 깨짐 없음
- 충돌 BoxCollider2D 크기/오프셋 변동 없음 (도트 16×16 ≈ 0.25 unit, 기존 collider Size 0.6×0.5 와 시각적으로 어울리는지만 확인 — 어색하면 별도 협의)
- `TestKhi_Net_AD.prefab` 외형은 변동 없음 확인 (본 PR 범위 외)

### E-3. 다른 사용 씬 가벼운 시각 확인
prefab 자체가 변경되었으므로 다음 씬은 Play 까지 가지 않고 Hierarchy 의 prefab 인스턴스에서 도트만 보이면 OK:
- `Assets/_Project/Scenes/Dungeon/Dungeon.unity`
- `Assets/Scenes/Test/BossClear.unity`
- `Assets/Scenes/Test/BossTest.unity`
- `Assets/Scenes/Test/test_map.unity`
- `Assets/Scenes/Test/BossDoorTest.unity`
- `Assets/Scenes/MVP1/MVP1.unity`
- `Assets/Scenes/test_khi 1.unity`
- `Editor/Stage/TownSceneBuilder.cs` 가 참조하는 town 빌더 — 빌더 트리거 시 이상 없는지 별도 확인

## F. 후속 작업 (별도 티켓 후보)

- **`KhiSpriteFlipBinder` / `KhiAnimatorMovementBinder` 스크립트 추가** — `MinimalCharacterModel` 의 SpriteRenderer.flipX 를 **상태별 반대 룰** 로 매핑(D-5 의사코드 참조), Animator.SetFloat("MovementY", rb.linearVelocity.y) 매핑. 본 plan 의 D-5/D-6 가 의존
- **WalkSpeedMul 디자이너 노출** — ScriptableObject `KhiAnimSpeedConfig.asset` 노출 후 Awake 에서 1회 SetFloat
- 측면(Left/Right) 전용 sprite 시트 확보 시 `TestKhi_Side_Walk` 클립 + `FacingX` 파라미터 추가 (현재 단계는 flipX 재활용으로 충분)
- `TestKhi_Net_AD.prefab` 동일 적용
- 셀 경계 ±1 조정이 검증에서 발견되면 .anim 키프레임 갱신
- **보조 클립(Reserved) 추가** — 셀 14~22, 25~27 미사용분을 필요 시 클립화: 정면/후면 보조 프레임, `Hit_Flash` (hero_26) 1프레임 stinger 합성, 단일 프레임 idle 대체(`hero_25`), Down 보조(`hero_27`)
- TDE `KhiPlayerHit` / `KhiPlayerDown` 등 게임 로직에서 Animator 트리거를 자동 호출하도록 코드 연결 (현재는 수동 토글로만 검증)
- **영구 정책**: 칼집/장비 등 캐릭터 옆구리에 X 오프셋이 있는 자식 GameObject **추가 금지** — flipX 룰 단순화 유지를 위해. 무기는 WeaponAttachment(X=0) 정중앙 위 부착만 사용

## 핵심 파일

| 파일 | 역할 |
|---|---|
| `LostMemory/Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` | 변경 대상 prefab |
| `LostMemory/Assets/_Project/Art/Characters/Sprite/hero.png` (+ `.meta`) | 재슬라이스 대상 |
| `LostMemory/Assets/_Project/Animations/Characters/TestKhi/*.anim` (6개 신규) | 신규 클립: `Hero_Front_Idle`, `Hero_Front_Walk`, `Hero_Back_Idle`, `Hero_Back_Walk`, `Hero_Front_Hurt`, `Hero_Front_Head` |
| `LostMemory/Assets/_Project/Animations/Characters/TestKhi/TestKhi_Animator.controller` (신규) | Animator 컨트롤러 (6 state: Front_Idle/Walk, Back_Idle/Walk, Hurt, Head) |
| `client/docs/khi/character-replacement-guide.md` | 상위 가이드 — 본 작업은 그 권장 흐름의 1차 적용 |

## 메모리 / 컨벤션 주의

- Unity Editor 작업은 사람만 수행 (.prefab/.meta/.anim/.controller 직접 편집 금지)
- 브랜치 스코프: 본 절차는 `TestKhi_MinimalCharacter2D` 만. `TestKhi_Net_AD` 작업은 별도 티켓
- 커밋·MR 생성·Jira 상태 변경은 사용자가 직접 수행
