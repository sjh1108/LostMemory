# DemoItemGranter — 남은 수동 작업 메모

> 코드 구현은 끝. Unity 에디터 작업만 남음. 나중에 본인이 직접 처리.

## 구현 상태

- [x] 신규 컴포넌트: [LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/DemoItemGranter.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/DemoItemGranter.cs)
  - F9 단축키 → 인스펙터 `RelicData[]` 일괄 `PlayerRelicInventory.TryAdd`
  - IsConsumable 자동 분기 (회복약/유물 혼합 OK)
  - owner-side 게이트 + 쿨다운 0.5s
- [ ] **Player prefab 에 컴포넌트 부착** ← 남은 작업
- [ ] **인스펙터 `itemsToGrant[]` 채우기** ← 남은 작업
- [ ] **시연 빌드에 포함 / 정식 빌드에서 제거 정책 결정** ← 남은 작업

## 남은 수동 작업 (Unity 에디터에서)

### 1. Player prefab 부착
- 부착 위치: Khi player prefab 루트 (NetworkObject + PlayerRelicInventory 가 있는 자리, `AutoPotionController` 와 같은 자리)
  - 경로 후보: `LostMemory/Assets/_Project/Prefabs/Player/` 아래 Khi 계열
- 추가 방법: `Add Component > Lost Memory > Test Khi > Demo Item Granter`
- 의존성 필드 (PlayerRelicInventory / NetworkObject) 는 비워둬도 됨 — Awake 자동 해결

### 2. 인스펙터 `itemsToGrant[]` 채우기
- `RelicData` ScriptableObject asset 들을 드래그앤드롭으로 배열에 추가
- **회복약 asset** 위치: `LostMemory/Assets/_Project/ScriptableObjects/Relics/...` 아래 소모품 (IsConsumable=true) 찾아서 1~4개
- **유물 asset** 위치: `LostMemory/Assets/_Project/ScriptableObjects/Relics/Generated/` 또는 `Buildset/Relics/` 에서 시연용으로 보이고 싶은 유물 3~5개
- 순서대로 TryAdd 호출되므로 우선순위 높은 회복약/유물을 앞에 배치

### 3. 인스펙터 옵션
| 필드 | 권장값 | 비고 |
|---|---|---|
| `triggerKey` | `F9` | 기본값. AutoPotion F8 과 겹치지 않게 |
| `internalCooldownSeconds` | `0.5` | 같은 프레임 중복 방지 |
| `verboseLog` | true (시연 디버그) → 정식 빌드 false | 콘솔 노이즈 |

## 동작 요약
- **F9** 한 번 → 인스펙터 `itemsToGrant[]` 순회하면서 일괄 `PlayerRelicInventory.TryAdd`
  - 회복약은 단축바 슬롯으로
  - 유물은 그리드 배치 + 스탯 효과 자동 적용
- 콘솔: `[DemoItemGranter] F9 → added=N, rejected=M`
- 중복 유물 / 인벤토리 가득은 rejected 카운트로 잡힘 (silent 진행)

## 멀티플레이 주의
`PlayerRelicInventory` 는 NetworkBehaviour 가 아니라 **각 클라가 독립 인벤토리**.
- 호스트가 F9 → 호스트만 받음
- 게스트도 받고 싶으면 게스트도 자기 인스턴스에서 F9
- 한 번에 양쪽 다 받게 하려면 별도 ServerRpc 인프라 필요 (현재 plan 범위 밖)

## Verification (부착 후 체크)
1. **솔로**: `itemsToGrant` 에 회복약 1개 + 유물 3개 채우고 F9 → added=4, 단축바에 회복약 + 그리드에 유물 + 스탯 변경 확인
2. **중복 방지**: F9 두 번 → 같은 유물 rejected
3. **쿨다운**: F9 빠르게 연타 → 0.5s 안에 두 번 안 됨
4. **AutoPotion 연계**: F9 (회복약 채움) → F8 (AutoPotion ON) → HP 40% 떨어뜨림 → 자동 사용
5. **멀티 2P**: 호스트/게스트 각자 F9 → 자기 인벤토리만 채워짐

## 시연 운영 (UX)
1. 보스전 씬 진입
2. **F9** → 시연용 아이템 일괄 지급 (콘솔로 확인)
3. (옵션) **F8** → AutoPotion ON
4. 보스 패턴 시연 시작

## 정식 빌드 분리 전략
- 시연 끝나면: prefab 에서 컴포넌트 제거 또는 `itemsToGrant[]` 비우기
- F9 키가 다른 기능과 충돌 안 하는지 확인 (현재 코드에서 F9 사용처 없음)

## 향후 follow-up (선택)
- 한쪽이 F9 누르면 양쪽 다 받게 (ServerRpc + ClientRpc 인프라)
- 시연 모드 인디케이터 UI (F8/F9 상태 화면 표시)
- 회복약 / 유물 별도 배열 분리 (지금은 단일 RelicData[] 로 자동 분기 — 깔끔하지만 명시성 약함)

## 관련 파일
- [AutoPotionController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/AutoPotionController.cs) — F8 자동 포션
- [auto-potion-demo-todo.md](auto-potion-demo-todo.md) — F8 부착 가이드
