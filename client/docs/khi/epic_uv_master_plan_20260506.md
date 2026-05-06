# Epic U / Epic V 마스터 플랜 — Balance Editor 확장 + 인벤토리 테스트 도구

> **이 문서를 처음 보는 Claude 에게**: 본 plan 은 Epic U (Balance Editor 카테고리 확장) + Epic V (Inventory Test Window 신설) 의 마스터. cold start 로 받아도 작업 시작 가능하게 자기완결적으로 작성됨.
>
> 다른 컴퓨터에서 작업 이어가는 경우, **이 문서 → cl172_plan.md → 첫 ticket 코드 작성** 흐름으로 진입.
>
> 작성일: 2026-05-06. 작성자: 김회인 (gimhoein@gmail.com)

---

## 0. 현재 진행 상태 (2026-05-06 기준)

| 항목 | 상태 |
|---|---|
| 현재 브랜치 | `S14P31C201-401-cl-162-editorwindow-ui-toolkit` (CL-162~166 작업 중) |
| CL-162 ~ CL-165 | ✅ 머지 완료 |
| CL-166 (Weapon/Skill/Shop Provider) | 🟡 진행 중 — 마무리 필요 |
| 본 plan 의 첫 진입점 | **CL-172** (cl172_plan.md 작성됨) |
| 브랜치 정책 결정 | 🟡 사용자 결정 대기 — Q4 의견 참조 |

---

## 1. Epic U / Epic V 정의

### Epic U — 밸런스 에디터 (Balance Editor)

기존 [Tools > LostMemory > Balance Editor] 창의 **카테고리 확장**.

```
Tools/LostMemory/Balance Editor (CL-162~166 인프라 위)
├─ Relic, BuildSet, Weapon, Skill, ShopConfig 카테고리 (CL-163, CL-166)
├─ + Enemy / Boss 카테고리 (CL-172, CL-173) ← 본 plan
└─ + Player Stats 카테고리 (CL-188~190 보류)
```

→ Epic U 의 새 ticket = **Provider 추가 = 코드 클래스 1개 (~15분)**. 인프라 자동 적용.

### Epic V — 인벤토리 테스트 도구 (Inventory Test Window)

**Balance Editor 와 완전 분리된 별도 EditorWindow**. Play 모드 전용.

```
Tools/LostMemory/Inventory Test Window (신설, CL-182 ~ CL-187)
├─ 좌측: 모든 RelicData 트리 + 검색
├─ 우측: 현재 Player 의 Permanent / Consumable 슬롯 시각화
└─ 동작: 더블클릭/드래그로 추가, 우클릭 제거, Quick Add 프리셋
```

→ 디자이너/테스터가 **던전 클리어 → 보상 풀 추첨 흐름 우회**해서 즉시 아이템 추가/제거. 효과/세트/스택 검증 즉시.

---

## 2. ticket 시트 라인 (Jira / Google Sheet 복사용)

탭 구분 — 시트 셀에 자동 분배:

```
Epic U. 밸런스 에디터	Editor Tool	CL-172	EnemyData/BossData SO 정의 + 인스턴스	클라1	P1	데이터 정의	3	일반 몬스터(EnemyData) + 보스(BossData) SO 클래스 + 인스턴스 작성. Bertha 코드 영향 0	CL-166	
Epic U. 밸런스 에디터	Editor Tool	CL-173	EnemyData/BossData CategoryProvider 등록	클라1	P1	카테고리 노출	1	Balance Editor 좌측 트리에 Enemy/Boss 카테고리 추가	CL-172	
Epic U. 밸런스 에디터	Editor Tool	CL-180	EnemyData/BossData 적용 어댑터 (게임 내 사용)	클라1	P3	데이터 연동	3	SO 값을 Bertha/일반 몹 컴포넌트에 주입. 일반 몹 등장 시점에 매칭	CL-172	보류
Epic V. 인벤토리 테스트 도구	Editor Tool	CL-182	InventoryTestWindow 셸 + Play 모드 가드 + Player 자동 검색	클라1	P1	에디터 셸	2	UXML/USS + asmdef + Tools 메뉴. Play 모드 전제 별도 EditorWindow + FindAnyObjectByType<PlayerRelicInventory>	—	
Epic V. 인벤토리 테스트 도구	Editor Tool	CL-183	좌측 RelicData 트리 + 우측 슬롯 표시 + 검색(이름/태그/효과)	클라1	P1	메인 UI	3	좌측 트리 (Permanent/Consumable 그룹핑) + 우측 슬롯 시각화 + 검색바 (이름·태그·효과 설명)	CL-182	
Epic V. 인벤토리 테스트 도구	Editor Tool	CL-184	더블클릭 추가 / 우클릭 제거 / Clear 버튼	클라1	P1	동작	2	TryAdd/Remove/Clear 호출. OnRelicAcquired/OnRelicRemoved 이벤트 구독으로 자동 갱신	CL-183	
Epic V. 인벤토리 테스트 도구	Editor Tool	CL-185	Consumable 4슬롯 별도 영역 + 더블클릭 시 빈 슬롯 자동 배치	클라1	P1	UI 확장	1	소모품 영역 분리. 더블클릭 시 첫 빈 슬롯 자동 (수동 슬롯 선택 불필요)	CL-184	
Epic V. 인벤토리 테스트 도구	Editor Tool	CL-186	좌측→우측 드래그 앤 드롭 추가	클라1	P2	UX 강화	2	UI Toolkit Manipulator 로 좌측 트리 → 우측 슬롯 드래그. 더블클릭과 동등 효과	CL-184	
Epic V. 인벤토리 테스트 도구	Editor Tool	CL-187	Quick Add 프리셋 (테스터 저장/로드)	클라1	P2	편의 기능	3	현재 인벤토리 상태를 프리셋으로 저장(EditorPrefs 또는 PresetSO) → 한 번 클릭으로 로드. 추천 빌드 즉석 세팅	CL-184	
Epic U. 밸런스 에디터	Editor Tool	CL-188	PlayerStatsData SO 정의 + base 값 마이그레이션	클라1	P3	데이터 정의	3	캐릭터 base 능력치(이속/체력/대시쿨/점프) 통합 SO	CL-166	보류
Epic U. 밸런스 에디터	Editor Tool	CL-189	PlayerStatsApplier 정리 + base × multiplier 통합	클라1	P3	데이터 연동	2	TDE 컴포넌트에 SO base 값 주입 어댑터	CL-188	보류
Epic U. 밸런스 에디터	Editor Tool	CL-190	PlayerStatsCategoryProvider	클라1	P3	카테고리 노출	1	Balance Editor 에 Player Stats 카테고리 추가	CL-189	보류
```

**총 12 ticket / 28점**. P1=12점, P2=5점, P3=11점.

---

## 3. 현황 요약 (탐색 결과 — 다른 Claude 가 cold 진입 시 먼저 읽기)

### 3.1 Balance Editor 인프라 (CL-162 ~ CL-166)

- 인터페이스: `IBalanceCategoryProvider` — `CategoryName` / `AssetTypeFilter`(string, "t:" 우회) / `LoadAll()`
- 위치: `Assets/_Project/Scripts/Editor/BalanceEditor/`
- asmdef references = `[]` — Editor 가 Assembly-CSharp 직접 참조 안 함 (string filter 우회 패턴)
- 기존 Provider: Relic, BuildSet (CL-163) → CL-166 에서 Weapon/Skill/ShopConfig 추가
- Provider 추가 표준 분량: **~15분/개** (cl166_plan.md 기준)

### 3.2 Player Stats — SO 없음 (인라인)

- `StatId` enum 12종 (`Combat/StatId.cs:9-24`): AttackPower, AttackSpeed, MoveSpeed, MaxHealth, FinisherDamage, DashCooldown, HealReceived, Critical, Cooldown, Range, Dodge, Defense
- `PlayerStatModifierContainer` 가 multiplier 합산 (Permanent/Temp/Conditional, 정책: `total = 1 + Σ(percents)`)
- 적용 어댑터: `PlayerHealthStatApplier`, `PlayerMovementStatApplier`, `KhiDashController:75-78`
- **base 값은 TDE 컴포넌트(CharacterMovement/Health/Dash) 의 Inspector 인라인 필드 + 코드** 흩어짐
- → Balance Editor 노출하려면 PlayerStatsData SO 신설 필요. **회귀 위험으로 본 plan 은 CL-188~190 보류**

### 3.3 Weapon — SO 완성

- `Runtime/Data/WeaponData.cs` (`LostMemory.Data` ns)
- `[CreateAssetMenu(menuName = "LostMemory/Combat/WeaponData")]`
- 콤보 step 배열 (`AttackStepData[]`) + ContextMenu 라이브 튠 (`Save Current Values`)
- Balance Editor 노출 = **CL-166 범위** (Weapon Provider 추가)

### 3.4 Enemy — SO 없음, Bertha 보스만 구현

- `Runtime/Enemies/Boss/Bertha/BerthaBossPhaseController.cs:12-14` — Health/phase threshold 직렬화 필드
- TDE Health/Character/CharacterMovement 가 stat 보유
- `EnemyStatusEffect.cs:77-105` — Slow/Freeze/Burn 적용 API
- 일반 몬스터 `EnemyData` SO 미존재 — `Runtime/Combat/EnemyCatalog.cs` 의 CL-034 주석에 "후속 도입 예정" 명시
- → CL-172 에서 SO 정의 + Bertha 인스턴스 작성. **Bertha 코드 영향 0** (시나리오 B)

### 3.5 AI 패턴 — TDE AIBrain + State Machine

- `Telegraph/AIBrainDashTelegraphDriver.cs` 등 — 모든 파라미터 = MonoBehaviour Inspector 필드 (코드 하드코딩 X, SO X)
- → SO 분리 비용 큼 + 패턴별 다양성 커서 generic SO 어색. **본 plan 패스**

### 3.6 Item / Inventory — 에디터 도구 진입점 명확

- `Runtime/Relics/RelicData.cs` SO 가 유물/소모품 통합 (`_isConsumable` 플래그)
- 인벤토리 컴포넌트:
  - `PlayerRelicInventory.TryAdd(RelicData):33` / `Remove(RelicData):75` / `Clear():101`
  - `PlayerConsumableInventory.TryAdd:24` / `Remove(int slot):47` / `Get(int slot):58`
- 이벤트: `OnRelicAcquired`, `OnRelicRemoved`, `OnCleared` (창 자동 갱신용)
- 호스트권위 NGO 환경이지만 인벤토리 add/remove 는 로컬 MB → 에디터에서 직접 호출 가능
- 보상 흐름: `RewardController.cs:78-120` → `RewardPanelView.cs:49` → `_inventory.TryAdd(selected)` → `BuildManager.cs:114` 가 효과 적용
- → Inventory Test Window 도 같은 path (`TryAdd`) 호출 → BuildManager 효과 자동 적용

---

## 4. 도메인별 SO 상태 매트릭스

| 도메인 | 현재 상태 | Balance Editor 노출 비용 | 본 plan 처리 |
|---|---|---|---|
| Relic | SO ✓ | 등록 완료 (CL-163) | — |
| BuildSet | SO ✓ | 등록 완료 (CL-163) | — |
| Weapon | SO ✓ | Provider 추가만 (CL-166) | CL-166 범위 |
| Skill | SO ✓ | Provider 추가만 (CL-166) | CL-166 범위 |
| ShopConfig | SO ✓ | Provider 추가만 (CL-166) | CL-166 범위 |
| **Enemy (일반)** | 미존재 | SO 신설 + 인스턴스 + Provider | **CL-172, CL-173** ★ |
| **Boss (Bertha)** | MB 필드 | BossData SO 신설 (EnemyData 상속) + 인스턴스 + Provider | **CL-172, CL-173** ★ |
| **Player Stats** | 인라인 | SO 신설 + 마이그레이션 + Provider (大) | CL-188~190 **보류** |
| AI 패턴 | MB 필드 | SO 분리 큼, 다양성 커서 어색 | 패스 |

---

## 5. ticket 분할 표

### A. Epic U — Balance Editor 카테고리 확장 (Enemy 트랙만 우선 진행)

| Ticket | 제목 | 점수 | 의존 | 진행 |
|---|---|---|---|---|
| CL-172 | EnemyData / BossData SO 정의 + Bertha 인스턴스 (코드 전환 X) | 3 | CL-166 | **진행 권장** ([cl172_plan.md](cl172_plan.md)) |
| CL-173 | EnemyData / BossData CategoryProvider 등록 | 1 | CL-172 | **진행 권장** |
| CL-180 | (별도 후속) EnemyData / BossData 적용 어댑터 — 게임 내 실제 사용 | 3+ | CL-172 | **보류** (일반 몹 등장 / Bertha 리팩 ticket 시점에 매칭) |

### B. Epic V — 인벤토리 테스트 도구 (별도 EditorWindow)

| Ticket | 제목 | 점수 | 의존 |
|---|---|---|---|
| CL-182 | InventoryTestWindow 셸 + Play 모드 가드 + Player 자동 검색 | 2 | — |
| CL-183 | 좌측 RelicData 트리 + 우측 슬롯 표시 + 검색 (이름/태그/효과) | 3 | CL-182 |
| CL-184 | 더블클릭 추가 / 우클릭 제거 / Clear 버튼 (이벤트 구독 자동 갱신) | 2 | CL-183 |
| CL-185 | Consumable 4슬롯 별도 영역 + 더블클릭 시 빈 슬롯 자동 배치 | 1 | CL-184 |
| CL-186 | 좌측 → 우측 드래그 앤 드롭 (UI Toolkit Manipulator) | 2 | CL-184 |
| CL-187 | Quick Add 프리셋 (테스터 저장/로드, EditorPrefs or PresetSO) | 3 | CL-184 |

### C. Epic U — Player 트랙 (보류)

| Ticket | 제목 | 점수 | 의존 | 진행 |
|---|---|---|---|---|
| CL-188 | PlayerStatsData SO 정의 + base 값 마이그레이션 | 3 | CL-166 | **보류** |
| CL-189 | PlayerStatsApplier 정리 + base × multiplier 통합 | 2 | CL-188 | **보류** |
| CL-190 | PlayerStatsCategoryProvider | 1 | CL-189 | **보류** |

### 별도 시각 도구 ticket 후보 (본 plan 외)

- 무기 콤보 timeline 에디터
- 몬스터 AI behavior tree 그래프 에디터
- Playmode 라이브 튜닝 디버그 창 (StatModifierContainer 시각화)

---

## 6. 의존성 그래프

```
CL-166 (현재 진행) ─── Weapon/Skill/Shop Provider 등록 끝

[Epic U] Enemy 트랙 — 코드 영향 0
CL-172 (EnemyData + BossData SO 통합) → CL-173 (Provider 등록)
                                          [Balance Editor Enemy/Boss 카테고리 노출]
                                          [기존 Bertha 코드 무수정]
CL-180 (적용 어댑터) — 보류, 별도 시점에 매칭

[Epic V] 인벤토리 테스트 도구 — Balance Editor 와 별도 창
CL-182 (셸) → CL-183 (트리+슬롯+검색) → CL-184 (add/remove/Clear)
                                            ├→ CL-185 (Consumable 4슬롯)
                                            ├→ CL-186 (드래그앤드롭)
                                            └→ CL-187 (Quick Add 프리셋)

[Epic U] Player 트랙 — 보류
CL-188 → CL-189 → CL-190  base 값 SO 마이그레이션. 현재 미진행

→ Enemy / Inventory 트랙 상호 독립. 병렬 진행 가능.
```

---

## 7. 우선순위 권장

1. **CL-166 마무리** (이미 진행 중) — Weapon/Skill/Shop Provider 등록
2. **CL-182 ~ CL-185** (Epic V 핵심) — 셸 / 트리+검색 / 동작 / Consumable. 밸런싱 작업의 testing 인프라
3. **CL-172 + CL-173** (Epic U Enemy SO + Provider) — 코드 영향 0, Balance Editor 노출만
4. **CL-186, CL-187** (Epic V 폴리시) — 드래그앤드롭, Quick Add 프리셋. P2
5. **보류**: CL-180 (Enemy 적용 어댑터), CL-188~190 (Player base SO)

---

## 8. 인벤토리 테스트 도구 — 별도 창 권장 근거 (Epic V)

### Balance Editor 와 분리해야 하는 이유

1. **활성 모드 다름**: Balance Editor = Edit 모드 (에셋 편집), Inventory Test = **Play 모드 전제** (씬 Player 인스턴스 필요)
2. **편집 대상 다름**: Balance Editor = `ScriptableObject` (디스크 영속), Inventory Test = **MonoBehaviour 런타임 상태** (Play 종료 시 휘발)
3. **UI 패턴 다름**: Balance Editor = TreeView + InspectorElement (필드 자동 생성), Inventory Test = 좌측 후보 트리 + **우측 슬롯 그리드** (드래그/더블클릭, 직접 그림)
4. **Dirty/Undo 모델 다름**: Balance Editor = `EditorUtility.IsDirty` + AssetDatabase, Inventory Test = **휘발성** (Undo 불필요)

### 제안 구조

- 메뉴: `Tools/LostMemory/Inventory Test Window`
- asmdef: `LostMemory.InventoryTest.Editor` (Editor 전용)
- 폴더: `Assets/_Project/Scripts/Editor/InventoryTest/`
- 좌측 트리: 모든 RelicData (Consumable / Permanent 그룹핑)
- 우측 상단: PlayerRelicInventory 슬롯 시각화
- 우측 하단: PlayerConsumableInventory 4슬롯
- 동작: 더블클릭 = 추가, 우클릭 메뉴 "Remove" = 제거, "Clear All" 버튼, 드래그앤드롭, Quick Add 프리셋
- Play 모드 아닐 때: 안내 문구 + 비활성

### 중요한 부수효과

`TryAdd` 호출 → `OnRelicAcquired` 이벤트 발화 → `BuildManager.cs:114` 가 듣고 Relic 효과(Stat multiplier 등) 자동 적용. **테스트 창의 추가가 진짜 게임 흐름과 같은 path** 를 타니 효과 검증이 정확.

### Player 인스턴스 찾기 — MVP

```csharp
_relicInv = Object.FindAnyObjectByType<PlayerRelicInventory>();
_consumeInv = Object.FindAnyObjectByType<PlayerConsumableInventory>();
```

→ 단일 Player 가정. 멀티플레이 / 2명 동시 디버깅이 필요해지면 Player 드롭다운 후속 ticket 으로.

---

## 9. 결정 히스토리 (사용자 답변 기록 — 후속 Claude 컨텍스트)

### Q1. Player Stats SO 마이그레이션 — 보류 결정

#### "마이그레이션이란"

base 값 (이속/체력/대시쿨/점프) 이 Player prefab Inspector + KhiDashController 코드 등에 흩어져 있음. 이를 `PlayerStatsData.asset` 1개 SO 로 모으는 작업.

```csharp
[CreateAssetMenu(menuName = "LostMemory/Player Stats Data")]
public class PlayerStatsData : ScriptableObject {
    public float baseMoveSpeed = 7f;
    public float baseMaxHealth = 100f;
    public float baseDashCooldown = 1.5f;
    public float baseJumpHeight = 4f;
}
```

`PlayerStatsAdapter` 같은 어댑터가 Awake 시점에 SO 값을 TDE 컴포넌트에 주입.

#### 보류 결정 근거

- **회귀 위험 큼**: 적용 어댑터 실행 타이밍이 TDE 초기화와 꼬이면 default 값 적용 → 게임플레이 깨짐
- TDE 컴포넌트 Inspector 값과 SO 값 우선순위 정해야 함 (덮어쓰기 vs 폴백)
- 멀티플레이 동기화: 호스트/클라가 동일 SO 값 보장 필요 (NGO 환경)
- 변경 범위 커서 회귀 검증 시간이 작업량보다 큼
- 현재 `PlayerStatModifierContainer` multiplier 시스템만으로도 디자이너가 곱셈 튜닝 가능
- Player prefab 1개라 base 값 중복 적은 편 → 이득 작음

→ **CL-188~190 보류**. 일반 몹 등장 / Player prefab 변경 ticket 시점에 재검토.

### Q2. Enemy 시나리오 B (코드 영향 0) 채택

| 시나리오 | 작업 범위 | Bertha 코드 영향 | 결과 |
|---|---|---|---|
| A. SO 정의만 | 신규 클래스 2개 | **없음** | 카테고리 노드 비어있음 |
| **B. SO 정의 + 인스턴스 작성** | A + asset | **없음** | 카테고리에 SO 표시되지만 게임 내 미사용 |
| C. SO 정의 + 인스턴스 + 적용 어댑터 | B + Bertha 컨트롤러 SO 참조 전환 | **있음** | 데이터-코드 연결, 회귀 위험 |

→ **시나리오 B 채택**. CL-172 + CL-173 만 진행, CL-180 적용 어댑터는 보류. 디자이너에게 "데이터-코드 비동기" 기간 안내 필요 (SO 값 게임 미반영).

### Q3. 인벤토리 테스트 도구 — 별도 EditorWindow 채택

근거: 활성 모드 / 편집 대상 / UI 패턴 / Dirty 모델 모두 Balance Editor 와 다름. ContextMenu 만 확장은 반복 테스트 비효율. 별도 창이 가장 강력 + 작업량 합리적 (6 ticket / ~13점).

추가 결정 사항 (사용자 답변):
1. 검색 범위 = 이름 + 태그 + 효과 → CL-183 에 통합
2. 더블클릭 + 드래그앤드롭 모두 지원 → CL-184 (더블클릭) + CL-186 (드래그)
3. Consumable 더블클릭 시 빈 슬롯 자동 배치 → CL-185 단순화
4. Quick Add 프리셋 + 테스터 저장 가능 → CL-187 신설

### Q4. 브랜치 / Epic — 사용자 결정

| 옵션 | 장점 | 단점 |
|---|---|---|
| A. 현재 401 브랜치 연장 | CL-166 흐름 자연 | 브랜치명 어긋남, 메모리 가이드 위배 |
| **B. ticket 별 새 브랜치, Epic 연장** | 1:1 명확, 가이드 준수 | 의존 ticket 순차 머지 |
| C. 새 Epic V (Player/Enemy 통합) | Epic U 깔끔 종결 | Epic 너무 쪼개짐 |
| **D. 도구별 Epic 분리** (현재 채택) | 도구별 추적 명확 | Epic 2개 동시 관리 |

채택 결과: **Epic U (Balance Editor 카테고리 확장) + Epic V (Inventory Test Window) 분리**. 브랜치 정책 (B vs A) 은 사용자 결정 대기.

---

## 10. 검증 (각 ticket 공통)

### Provider 추가 ticket (CL-173, CL-190)
- Tools > LostMemory > Balance Editor 열고 새 카테고리 노드 표시 확인
- 검색창에 SO 이름 일부 입력 → 필터링 확인
- 우측 InspectorElement 에 필드 자동 표시 확인
- Ctrl+S 저장 → 디스크 반영 확인
- JSON Export/Import 라운드트립 확인

### SO 신설 ticket (CL-172, CL-188)
- `[CreateAssetMenu]` 메뉴에서 신규 SO 생성 가능 확인
- Inspector 에서 모든 필드 직렬화/편집 가능 확인
- `[FormerlySerializedAs]` 필요 시 추가 (마이그레이션 ticket)

### Inventory Test Window (CL-182 ~ CL-187)
- Play 모드 진입 → 창 열어서 좌측 트리에 모든 RelicData 표시 확인
- 더블클릭 → 우측 인벤토리 슬롯에 즉시 표시 + `OnRelicAcquired` 발화 확인
- BuildManager 가 effect 적용 확인 (Permanent 효과 → Stat multiplier 반영)
- Consumable 슬롯 4개 채움/비움 확인
- Play 종료 후 Edit 모드로 돌아오면 창은 안내 문구로 전환 확인
- 검색바 (이름/태그/효과) — 각각 키워드 입력 시 필터링 확인
- 드래그앤드롭 — UI Toolkit Manipulator 동작 확인
- Quick Add 프리셋 — 저장 후 다시 Play 진입 시 로드 확인 (EditorPrefs 영속)

---

## 11. 참고 파일 (수정 예상)

### 신규 (Claude 작성)
- `Assets/_Project/Scripts/Runtime/Enemies/EnemyData.cs` (CL-172)
- `Assets/_Project/Scripts/Runtime/Enemies/BossData.cs` (CL-172)
- `Assets/_Project/Scripts/Editor/BalanceEditor/Providers/EnemyDataCategoryProvider.cs` (CL-173)
- `Assets/_Project/Scripts/Editor/BalanceEditor/Providers/BossDataCategoryProvider.cs` (CL-173)
- `Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs` 등 별도 폴더 + asmdef (CL-182~187)

### 신규 (사용자 Unity Editor 작업)
- `Assets/_Project/ScriptableObjects/Enemies/Bertha_Boss.asset` (CL-172)
- (Player 트랙 진행 시) `PlayerStatsCategoryProvider.cs`, `PlayerStatsData.cs`, `PlayerStatsData.asset` (CL-188~190 보류)

### 수정 예상 (보류 ticket 진행 시)
- `KhiDashController.cs`, `PlayerHealthStatApplier.cs`, `PlayerMovementStatApplier.cs` (base 값 SO 참조로 전환 — CL-189)
- `BerthaBossPhaseController.cs` (SO 참조 전환 — CL-180)

---

## 12. 다음 Claude 진입점 — 첫 액션

### 1. 현재 브랜치 상태 확인

```bash
git status
git log -5 --oneline
```

CL-166 작업이 머지됐는지 확인. 안 됐으면 CL-166 마무리 우선.

### 2. 어느 ticket 부터?

권장 우선순위 (제 7장 참조):
- CL-166 마무리 → **CL-182** (Inventory Test Window 셸) 또는 **CL-172** (Enemy SO)
- 두 트랙 독립이라 병렬 가능. 사용자 의사 확인 후 진입

### 3. 첫 ticket 진입 (예: CL-172)

[cl172_plan.md](cl172_plan.md) 가 이미 작성됨. 그대로 따라가면 됨:
1. `EnemyData.cs` / `BossData.cs` 작성
2. 컴파일 확인
3. 사용자에게 Bertha_Boss.asset Unity Editor 작업 안내
4. CL-173 으로 이동

### 4. 새 ticket 의 plan doc 작성

CL-173, CL-182 등은 plan doc 미작성. 진입 시점에 `cl1XX_plan.md` 작성 후 코드.

### 5. 메모리 가이드 — 반드시 준수

- **`.unity` / `.prefab` / `.meta` 직접 편집 금지** — Unity Editor 작업은 사용자 영역
- **브랜치 스코프 엄격** — 브랜치명/CL 범위 외 작업 금지. 새 ticket 들이 어느 브랜치에 갈지 사용자 확인 후 진입
- **Git/PR/Jira 는 사용자 직접** — 커밋/push/MR/Jira 상태 변경은 Claude 가 안 함

---

## 13. 관련 문서

| 문서 | 역할 |
|---|---|
| [client1_tasks_master_plan.md](client1_tasks_master_plan.md) | 클라1 전체 마스터 (CL-001~104) — 본 plan 은 그 후속 |
| [cl161_plan.md](cl161_plan.md) | Epic U 진입 |
| [cl162_plan.md](cl162_plan.md) ~ [cl166_plan.md](cl166_plan.md) | Balance Editor 인프라 |
| [cl164_phase_1_handoff_20260505.md](cl164_phase_1_handoff_20260505.md) | Editor Window 패턴 4가지 (§12) |
| [cl165_phase_1_handoff_20260505.md](cl165_phase_1_handoff_20260505.md) | 추가 인사이트 3가지 (§12) |
| [cl172_plan.md](cl172_plan.md) | 본 plan 의 첫 ticket 상세 |

---

## 14. 변경 이력

| 일자 | 변경 |
|---|---|
| 2026-05-06 | 최초 작성 (Epic U/V 마스터 plan). 이전 plan: [balance-editor-snuggly-naur.md](file://C:/Users/AD/.claude/plans/balance-editor-snuggly-naur.md) (사용자 홈 — 비공유) 내용 + Bridge 정보 통합 |
