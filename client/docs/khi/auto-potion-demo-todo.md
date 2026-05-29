# Auto-Potion 시연 기능 — 남은 수동 작업 메모

> 코드 구현은 끝남. Unity 에디터 작업만 남음. 나중에 본인이 직접 처리.

## 구현 상태

- [x] 신규 컴포넌트 작성: [LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/AutoPotionController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/AutoPotionController.cs)
  - HP 임계값 감시 + F8 토글 + owner-side 게이트 + 슬롯 1→2→3→4 fallback
  - 기존 `PlayerHealing.TryUseConsumable` + `PlayerConsumableInventory` 재사용 (회복 로직 0줄 신규)
- [ ] **Player prefab 에 컴포넌트 부착** ← 남은 작업
- [ ] **인스펙터 기본값 확인** ← 남은 작업
- [ ] **시연 시작 전 단축바 1번 슬롯에 회복약 충전** ← 운영 시 매번

## 남은 수동 작업 (Unity 에디터에서)

### 1. Player prefab 부착
- 부착 위치: Khi player prefab 루트 (NetworkObject / Health / PlayerHealing / PlayerConsumableInventory 가 있는 자리)
  - 경로 후보: `LostMemory/Assets/_Project/Prefabs/Player/` 아래의 Khi 계열 prefab
  - 실측 시 NetworkObject 가 붙은 GameObject 가 정답
- 추가 방법: prefab 인스펙터에서 `Add Component > Lost Memory > Test Khi > Auto Potion Controller`
- 의존성 필드 (Health / PlayerHealing / PlayerConsumableInventory / KhiDownController / NetworkObject) 는 **비워둬도 OK** — Awake 가 `GetComponentInParent` 로 자동 해결

### 2. 인스펙터 기본값
| 필드 | 시연 빌드 권장값 | 비고 |
|---|---|---|
| `autoEnabled` | false (시작 시 off, F8 로 켬) 또는 true (시작 즉시 on) | 발표 시나리오에 따라 |
| `toggleKey` | `F8` | 기본값 그대로 |
| `hpThresholdPercent` | `0.4` (40%) | 시연 난이도에 따라 조정 |
| `internalCooldownSeconds` | `0.25` | 도배 방지 안전벨트 |
| `verboseLog` | true (시연 디버그) → 정식 빌드는 false | 콘솔 노이즈 |

### 3. 시연 운영 (매 시연 시작 전)
- 단축바 **1번 슬롯**에 회복약 (`RelicData.IsConsumable=true` + `EffectType==HealConsumablePercent`) 채우기
  - 1번이 비어 있으면 자동으로 2→3→4 순으로 fallback 되지만 우선순위 확정용으로 1번에
- F8 토글 상태를 시연 전에 점검 (콘솔 로그로 확인 가능)

## 동작 요약
- **F8**: on/off 토글 (콘솔 `[AutoPotion] toggled → ON/OFF`)
- HP 비율이 `hpThresholdPercent` 미만이면 단축바 1→2→3→4 순서로 첫 회복약 자동 사용
- 0.25s 내부 쿨다운으로 같은 프레임 중복 방지
- Down / Defeated 상태에서는 차단 (`KhiPlayerActionGate.IsBlocked`)
- 멀티: `NetworkObject.IsOwner` 만 발동 — 호스트/게스트 각자 자기 캐릭터에만. HP sync 는 `PlayerHealthSync` 가 처리

## Verification (부착 후 체크)
1. **솔로**: F8 → ON, 1번 슬롯 회복약, 피해 받아 HP 40% 이하 → 콘솔 `[AutoPotion] used slot 0 ...` + HP 회복 + 슬롯 비워짐
2. **슬롯 fallback**: 1번 비우고 2/3/4번에만 회복약 → 그 슬롯이 사용됨
3. **임계값**: HP 50% 인 상태에선 발동 안 함
4. **쿨다운**: 회복약 여러 개 있을 때 같은 프레임/0.25s 안에 중복 사용 안 함
5. **Down 상태**: down 진입 직후 자동 회복으로 down 자체가 무시되지 않는지 (의도라면 게이트 제외 옵션 추가 검토)
6. **멀티 2P**: 양쪽 각자 F8 토글 — 자기 owner 캐릭터에만 적용. 상대방 인벤토리 안 건드림. PlayerHealthSync 로 양쪽 화면에 HP 변동 보임

## 정식 빌드 분리 전략
- 시연 끝나면: `autoEnabled = false` 로 출고하거나, prefab 에서 컴포넌트 자체 제거
- F8 키가 다른 기능과 충돌 안 하는지 확인 (현재 코드에서 F8 사용처 없음 — 안전)

## 향후 follow-up (선택)
- 시연 모드라는 걸 화면에 작은 인디케이터로 표시 (`autoEnabled=true` 일 때 우상단 텍스트)
- DebugCommand / Cheat 콘솔로 통합 (이미 있다면)
- `KhiPlayerActionGate` 게이트 제외 옵션 (down 직전에도 자동 사용해서 down 자체 회피 시연용)
