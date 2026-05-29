# 2026-05-22 개발 기록 — RenaRoot 멀티 동기화 + 시연 보조 3종

세션에서 작업한 모든 변경/신규/의사결정 한 페이지 요약. 다음 commit / PR / 시연 준비 참고용.

## 한 줄 요약

| # | 주제 | 결과물 |
|---|---|---|
| 1 | RenaRoot 보스 멀티플레이 동기화 audit | 진단 + 게스트 공격 → 보스 HP 반영 구현 (4파일 수정) |
| 2 | 자동 포션 사용 (시연 보조) | `AutoPotionController` 신규, F8 토글 |
| 3 | 시연용 아이템 일괄 지급 | `DemoItemGranter` 신규, F9 트리거 |
| 4 | 시연용 사운드 조절 패널 | `DemoSoundPanel` 신규, F10 토글, 자동 부트스트랩 |

---

## 1. RenaRoot 보스 멀티플레이 동기화

### 진단 (audit 결과)

| 항목 | 상태 |
|---|---|
| HP / MaxHP sync | ✅ `MonsterHealthSync` (server-authoritative NetworkVariable) |
| 위치/회전 | ✅ `NetworkTransform` |
| 방향 (facing) | ✅ `MonsterNetSync._isFacingRight` |
| 애니메이션 | ✅ `NetworkAnimator` (Visual 자식) |
| 사망 | ✅ `TriggerDeathVisualsClientRpc` + `Despawn(2s)` |
| **투사체 시각화** | ⚠ 실측 필요 — `RenaBossProjectile` 이 MonoBehaviour + NGO 없음 + `new GameObject()` 동적 생성 구조 |
| **게스트 공격 → 보스 HP** | ❌ → ✅ 본 세션에서 구현 |

### 수정한 4파일 (게스트 공격이 보스 HP 깎이도록)

기존 [PlayerDamageRelay.cs:31](LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/PlayerDamageRelay.cs:31) `RelayDamage` 패턴 활용. `KhiMeleeHitbox` 처럼 캐시 + 가드 우회 + 분기 추가.

- [LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeteor.cs](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeteor.cs) — 메테오 폭발 데미지
- [LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiFlameZone.cs](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiFlameZone.cs) — 화염방사기 직접 분기 (burn DOT 는 별도 follow-up)
- [LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs](LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs) — Chain + WindBlade on-hit
- [LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryDamageOnTouch.cs](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryDamageOnTouch.cs) — 패리 반격 직접 분기 (DOT 분기는 `RelayDamageOverTime` 미존재로 보류)

이미 처리되어 있던 곳:
- `KhiMeleeHitbox.cs:118` ✅
- `KhiArrowProjectile.cs:387` ✅ (`AttackBroadcast.RelayProjectileDamage` 경유)

### audit 정본 문서

[rena-boss-multiplayer-sync-audit.md](rena-boss-multiplayer-sync-audit.md)

### 남은 follow-up (P1~P3, 별도 plan)

- **P1**: 투사체 게스트 시각화 — 2P 실측으로 Scenario 1/2 (자체 Update 로 도는가 idle 인가) 판별 후 결정
- **P2**: `RenaBossSpellCombatController` 게스트 측 명시적 disable 또는 RPC 동기화
- **P3**: `NetworkAnimator` AuthorityMode (현재 Owner)
- **별도**: EnemyStatusEffect burn/freeze/slow DOT 도 server-relay 필요
- **별도**: PlayerDamageRelay 에 `RelayDamageOverTime` 추가 (KhiParryDamageOnTouch DOT 분기용)

---

## 2. Auto-Potion (F8)

### 신규 파일
- [LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/AutoPotionController.cs](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/AutoPotionController.cs)

### 동작
- HP < 임계값(기본 40%) 이면 단축바 슬롯 1→2→3→4 순으로 첫 회복약 자동 사용
- 회복 로직 0줄 — 기존 `PlayerHealing.TryUseConsumable` + `PlayerConsumableInventory` 재사용
- **F8** 으로 ON/OFF 토글, 인스펙터 기본값 `autoEnabled=false`
- owner-side 만 발동 (`NetworkObject.IsOwner`), Down 상태 가드 (`KhiPlayerActionGate.IsBlocked`)
- HP sync 는 기존 `PlayerHealthSync` 가 자동 처리

### 남은 수동 작업
- Player prefab 에 컴포넌트 부착 (Add Component → Lost Memory → Test Khi → Auto Potion Controller)
- 인스펙터 의존성은 Awake 자동 해결로 비워둬도 됨

### 메모
[auto-potion-demo-todo.md](auto-potion-demo-todo.md)

---

## 3. DemoItemGranter (F9)

### 신규 파일
- [LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/DemoItemGranter.cs](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/DemoItemGranter.cs)

### 동작
- **F9** 한 번 → 인스펙터 `itemsToGrant[]` (RelicData 배열) 일괄 지급
- 회복약(IsConsumable=true) / 유물(false) 혼합 가능 — `PlayerRelicInventory.TryAdd` 가 자동 분기
- 회복약 → 단축바 슬롯 / 유물 → 그리드 + 스탯 효과 자동 적용
- 0.5s 쿨다운 + owner-side 게이트

### 멀티 주의
`PlayerRelicInventory` 가 NetworkBehaviour 아님 → 호스트/게스트 각자 자기 인스턴스에서 F9 눌러야 양쪽 다 받음.

### 남은 수동 작업
- Player prefab 부착 (AutoPotionController 옆)
- 인스펙터 `itemsToGrant[]` 에 RelicData asset 드래그

### 메모
[demo-item-granter-todo.md](demo-item-granter-todo.md)

---

## 4. DemoSoundPanel (F10)

### 신규 파일
- [LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/DemoSoundPanel.cs](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/DemoSoundPanel.cs)

### 동작
- **F10** 토글 IMGUI 패널 (드래그 가능)
- 글로벌 슬라이더 Master/Music/SFX — 기존 `GameAudioSettings` 재사용
- 씬 내 모든 AudioSource 를 0.5s 마다 자동 스캔 → 클립 단위 그룹화 → 개별 슬라이더 + Mute
- PlayerPrefs 자동 저장 (`DemoSoundPanel.Clip.{name}.Volume/Mute`)
- **자동 부트스트랩** (`[RuntimeInitializeOnLoadMethod]`) — prefab 부착 작업 0

### 증폭 한계
슬라이더 0~2.0 노출하지만 Unity `AudioSource.volume` 이 [0,1] clamp 라 1.0 초과는 표시(`⚠`) 만. 진짜 증폭은 mixer asset 의 클립별 group 분리 필요 (follow-up).

### 남은 수동 작업
**없음** — F10 누르면 바로 작동.

### 메모
[demo-sound-panel-todo.md](demo-sound-panel-todo.md)

---

## 단축키 매핑 (시연용 통합)

| 키 | 기능 | 컴포넌트 |
|---|---|---|
| **F8** | Auto-Potion on/off 토글 | `AutoPotionController` (Player prefab 부착 필요) |
| **F9** | 시연용 아이템 일괄 지급 | `DemoItemGranter` (Player prefab 부착 필요) |
| **F10** | 사운드 패널 토글 | `DemoSoundPanel` (자동 부트스트랩) |
| **F11** | 글로벌 mute 토글 (모든 사운드 차단/재개) | `DemoSoundPanel` (자동 부트스트랩) |

### 시연 흐름
1. 빌드 실행
2. (선택) **F10** → 발표장 음향 환경에 맞춰 사운드 튜닝 (PlayerPrefs 저장)
3. town_preview 또는 보스전 씬 진입
4. (선택) **F9** → 시연용 아이템 일괄 지급
5. (선택) **F8** → AutoPotion ON
6. 보스 패턴 시연

---

## 남은 수동 작업 체크리스트 (Unity 에디터)

- [ ] Khi player prefab 루트에 `AutoPotionController` 추가
- [ ] Khi player prefab 루트에 `DemoItemGranter` 추가
- [ ] `DemoItemGranter.itemsToGrant[]` 에 회복약 + 유물 RelicData asset 채우기
- [ ] (선택) 인스펙터에서 임계값 / 토글 키 / 시연용 기본값 미세 조정
- [ ] 단축키(F8/F9/F10) 가 다른 기능과 충돌 없는지 한 번 확인

---

## 정식 빌드 분리 전략

시연 끝나면:
- **Auto-Potion / DemoItemGranter**: prefab 에서 컴포넌트 제거 또는 인스펙터 `autoEnabled=false` / `itemsToGrant=[]` 로 출고
- **DemoSoundPanel**: 파일 삭제 또는 `[RuntimeInitializeOnLoadMethod]` 를 `#if UNITY_EDITOR || DEMO_BUILD` 가드로 감싸기 (또는 그냥 두기 — F10 안 누르면 화면에 아무것도 안 뜸)

---

## 다음 commit / PR 메시지 후보

```
feat(multiplayer): 게스트 공격이 RenaRoot 보스 HP 에 반영되도록 PlayerDamageRelay 경유 추가

- KhiMeteor / KhiFlameZone / OnHitEffectRegistry(Chain+WindBlade) / KhiParryDamageOnTouch 의
  직접 Health.Damage 호출을 PlayerDamageRelay.RelayDamage 로 교체
- 게스트 측 DamageDisabled 가드 우회 (server-relay 경로 시)
- KhiMeleeHitbox / KhiArrowProjectile 은 이미 relay 사용 중 (무수정)
- RenaRoot prefab 검증: Health 가 NetworkObject 루트에 정상 부착

feat(demo): 시연 보조 3종 추가
- AutoPotionController (F8): HP 임계값 이하 자동 회복약 사용
- DemoItemGranter (F9): RelicData[] 일괄 지급
- DemoSoundPanel (F10): 자동 부트스트랩 IMGUI, 글로벌 + 클립별 볼륨/뮤트
```

git 작업은 사용자 commit policy 따라 진행하지 않음 — 수동 확인 후 커밋 권장.

---

## 관련 plan 파일

- 시스템 plan (ExitPlanMode 표시용): `C:\Users\SSAFY\.claude\plans\assets-project-prefabs-enemies-boss-rena-iridescent-newell.md`
- 정본: [rena-boss-multiplayer-sync-audit.md](rena-boss-multiplayer-sync-audit.md) (audit 부분)

## 관련 todo 메모 (도메인별)

- [auto-potion-demo-todo.md](auto-potion-demo-todo.md)
- [demo-item-granter-todo.md](demo-item-granter-todo.md)
- [demo-sound-panel-todo.md](demo-sound-panel-todo.md)
