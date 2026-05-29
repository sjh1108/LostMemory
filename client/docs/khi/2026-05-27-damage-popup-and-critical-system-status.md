# 데미지 Popup + 크리티컬 시스템 — 작업 현황

## 작업 개요

2026-05-27 세션. **플레이어 데미지 popup UI + 전체 무기 크리티컬 시스템 확장**을 함께 진행. 코드 구현은 거의 완료, **남은 이슈는 `StatId.Critical` stat 자체가 0%로 적용되는 별개 문제** 하나.

## 완료된 작업

### Phase 1 — 데미지 Popup UI 시스템

| 구성 | 위치 | 상태 |
|------|------|------|
| `DamagePopupStyle` ScriptableObject | [`_Project/Scripts/Runtime/UI/DamagePopup/DamagePopupStyle.cs`](LostMemory/Assets/_Project/Scripts/Runtime/UI/DamagePopup/DamagePopupStyle.cs) | ✅ |
| `DamagePopupSpawner` 싱글톤 | [`DamagePopupSpawner.cs`](LostMemory/Assets/_Project/Scripts/Runtime/UI/DamagePopup/DamagePopupSpawner.cs) | ✅ |
| Style SO 인스턴스 | [`_Project/ScriptableObjects/UI/DamagePopupStyle.asset`](LostMemory/Assets/_Project/ScriptableObjects/UI/DamagePopupStyle.asset) | ✅ |
| Floating Text Prefab | [`_Project/Prefabs/UI/DamageFloatingText.prefab`](LostMemory/Assets/_Project/Prefabs/UI/DamageFloatingText.prefab) | ✅ (TDE MMFloatingTextMeshPro 기반) |
| MMFloatingTextSpawner GameObject | 씬 (`Test_Title_Copy`) 직접 배치 | ✅ (사용자 작업) |

**채택 폰트**: Galmuri9 Bold (`Galmuri9 1.asset` GUID `1f760a28ffcf16a4e97d6841a9343642`). Bitmap shader 유지 (SDF 변환 시 픽셀 느낌 깨짐).

### Phase 2 — 4방향 픽셀 Outline

- `DamagePopupOutlineMirror` 컴포넌트 — 메인 TMP 의 text 를 4방향(상/하/좌/우) outline TMP 에 mirror + Style SO 의 color/offset/font size/character spacing/line spacing 매 LateUpdate 마다 적용
- prefab YAML 직접 편집으로 outline 자식 4개 + Mirror 컴포넌트 wiring 완료 ([`DamageFloatingText.prefab`](LostMemory/Assets/_Project/Prefabs/UI/DamageFloatingText.prefab))

### Phase 3 — 모든 튜닝 값을 SO로 통합

[`DamagePopupStyle.asset`](LostMemory/Assets/_Project/ScriptableObjects/UI/DamagePopupStyle.asset) 한 곳에서 다음 실시간 조절:

| 섹션 | 필드 | 실시간 |
|------|------|--------|
| Normal/Critical/SubEffect | color, intensity, criticalPrefix | trigger 시점 |
| Spawn | spawnOffset, direction, lifetime | trigger 시점 |
| Layout | **fontSize, characterSpacing, lineSpacing** | ✅ 매 LateUpdate (Mirror) |
| Outline | enabled, color, offset | ✅ 매 LateUpdate (Mirror) |
| Motion | **riseDistance** | ✅ 매 LateUpdate (Spawner → MMFloatingTextSpawner.RemapYOne 전파) |

### Phase 4 — 크리티컬 시스템 전체 무기 확장

[2026-05-27-critical-system-expand-all-attacks-plan.md](2026-05-27-critical-system-expand-all-attacks-plan.md) 계획대로 9개 진입점 모두 적용:

**신규 유틸**:
- [`Combat/CriticalRoller.cs`](LostMemory/Assets/_Project/Scripts/Runtime/Combat/CriticalRoller.cs) — `Roll(stats, dmg, out wasCrit)` 정적 헬퍼

**적용 진입점**:
| # | 진입점 | 파일 | 상태 |
|---|--------|------|------|
| 1 | 평타 | `KhiMeleeComboController.cs` | ✅ (기존 인라인 로직 → 유틸 호출로 교체) |
| 2 | 화살 (KhiBow) | `KhiArrowProjectile.cs` + `KhiBowController.cs` | ✅ (`_wasCritical` 필드 + Launch 시그니처 확장) |
| 3-4 | 스태프 Bolt / Fireball | `KhiStaffController.cs` | ✅ (화살 경로 재사용) |
| 5 | 메테오 | `KhiMeteor.cs` | ✅ (Detonate 시 1회 roll, AOE 전체 동일 적용) |
| 6 | 패리 반격 | `KhiParryDamageOnTouch.cs` | ✅ (stats lazy resolve + roll) |
| 7-8 | 체인/풍속 보조효과 | `Combat/OnHitEffectRegistry.cs` | ✅ (대상별 roll) |
| 9 | 마법소녀 투사체 | `MagicalGirl/MagicalGirlProjectile.cs` | ✅ (Init 시 owner stats resolve) |
| 10 | 마법소녀 AOE | `MagicalGirl/MagicalGirlAOE.cs` | ✅ (DoTick 시 대상별 roll) |

**Popup API**: `DamagePopupSpawner.NotifySubEffectDamage(target, amount, isCritical)` 시그니처 확장 — 보조효과도 크리 발동 시 노란 Critical 카테고리.

### Phase 5 — 디버그 도구 (2026-05-28 cleanup 완료)

진단 끝나고 모두 제거됨:
- `CriticalRoller.DebugForceCritChance` / `DebugLogRolls` — 정적 필드 삭제
- `CriticalDebugController.cs` — 파일 삭제

## 남은 이슈 — Critical Stat 0% 미스터리 → ✅ **해결 (2026-05-28)**

→ root cause 와 fix 는 [troubleshooting/2026-05-28-relic-stat-not-applied.md](troubleshooting/2026-05-28-relic-stat-not-applied.md) 참조.

요약: stat 자체가 0% 였던 게 아니라 **3중 누락 버그**가 겹쳐 stat 등록은 되더라도 (A) 솔로 InventoryTestWindow 차단 / (B) `_effects[]` array 미순회로 secondary 효과 드롭 / (C) 원거리 무기 6종이 AttackPower 안 곱해서 게임플레이 반영 0. Fix B 가 사냥꾼의 표적 등 12종의 Critical effect 를 부활시키며 동시에 해결.

진단 도구 (`CriticalDebugController`, `CriticalRoller.DebugForceCritChance`, `DebugLogRolls`) 모두 cleanup 완료 — 더 이상 코드에 없음.

## 의도적으로 미완성

- **TestKhiDamageTrap** — 환경 트랩, 사용자 공격 아님 (스코프 제외)
- **근접/원거리 stat 분리** — 사용자 결정대로 단일 `StatId.Critical` 통일
- **호스트 권위 RPC 동기화 강제** — 일단 각자 local roll. desync 보고되면 강화
- **`DamageCalculator` 통합** — 무기별 데미지 공식 분산 (Fix C 가 6개 무기 별도 패치) → 다음 stat 추가 직전에 한 단계 통합 권장. 자세한 논의는 troubleshooting md 의 "교훈 2" 절.

## 핵심 파일 일람

**신규 (4)**:
- `_Project/Scripts/Runtime/UI/DamagePopup/DamagePopupStyle.cs`
- `_Project/Scripts/Runtime/UI/DamagePopup/DamagePopupSpawner.cs`
- `_Project/Scripts/Runtime/UI/DamagePopup/DamagePopupOutlineMirror.cs`
- `_Project/Scripts/Runtime/Combat/CriticalRoller.cs`
- `_Project/Scripts/Runtime/Combat/CriticalDebugController.cs` (임시 디버그)

**수정 (10)**:
- `TestKhi/KhiMeleeComboController.cs` — 평타 크리티컬 로직 → 유틸로 교체
- `TestKhi/KhiBowController.cs` — 화살 spawn 시 Roll
- `TestKhi/KhiStaffController.cs` — Bolt/Fireball spawn 시 Roll
- `TestKhi/KhiArrowProjectile.cs` — `_wasCritical` 필드, Launch 확장, popup
- `TestKhi/KhiMeteor.cs` — Detonate 시 Roll, ApplyDamage popup
- `TestKhi/KhiParryDamageOnTouch.cs` — stats lazy resolve, Roll, popup
- `Combat/OnHitEffectRegistry.cs` — ApplyChain/ApplyWindBlade 각 대상별 Roll
- `MagicalGirl/MagicalGirlProjectile.cs` — Init 시 owner stats Roll
- `MagicalGirl/MagicalGirlAOE.cs` — DoTick 시 대상별 Roll
- `_Project/Prefabs/UI/DamageFloatingText.prefab` — outline 자식 4 + Mirror wiring (YAML 직접)
- `_Project/ScriptableObjects/UI/DamagePopupStyle.asset` — 모든 튜닝 값

**기타 시그니처 깨짐 따라잡기**: `KhiSfxBinder.cs`, `KhiCombatFeedbackBinder.cs`, `Tarot/TarotSystem.cs` — `TargetHit` 이벤트 시그니처 변경에 따른 파라미터 추가
