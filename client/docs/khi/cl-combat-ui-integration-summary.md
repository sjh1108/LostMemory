# CL 전투-UI 통합 — 작업 요약

> **브랜치**: `feat/S14P31C201-250/cl-전투-ui-네트워크-1-차-통합-테스트`
> **상태**: 전부 unstaged (working tree). 커밋 분할 권장.
> **작성일**: 2026-05-13

## Context

이번 브랜치는 단일 기능 티켓이 아니라 "전투/UI/네트워크 1차 통합 테스트"를 위해 누적된 폴리시·연결 작업 묶음이다. 6~7개의 독립 작업이 하나의 working tree에 쌓여있어, 커밋 시 영역별로 쪼개는 게 좋다.

[docs/khi/](.) 안의 plan md 7개가 각 작업의 가이드 문서이고, 실제 변경은 그 plan 대부분을 따라 이미 적용된 상태다.

---

## 작업 영역별 요약

### 1. 패링/대시 쿨다운 HUD (신규)

라디얼 게이지 + 카운트다운 숫자로 패링/대시 잔여 쿨타임 시각화. HP 바 옆에 슬롯 2개 + 인벤토리 키 힌트.

**신규 스크립트** ([cooldown_ui_plan.md](cooldown_ui_plan.md))
- [CooldownIndicatorView.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/CooldownIndicatorView.cs) — Image Filled/Radial360 + TMP
- [DashCooldownPresenter.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/DashCooldownPresenter.cs) — MMCooldown 상태별 progress 직접 계산 (`Progress` getter 함정 회피)
- [ParryCooldownPresenter.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/ParryCooldownPresenter.cs) — FailureRecovery+Cooldown 합산 게이지

**수정**: [PlayerHUD.prefab](../../LostMemory/Assets/_Project/Prefabs/UI/PlayerHUD.prefab) (자식 추가만, 기존 HP/MP 미터치)

### 2. 대시 무적 + 패링 폴리시

`KhiDashController` 에 i-frames 옵션, `KhiParryController` 에 hitbox/range 인스펙터 노출, 피드백 Presenter 가 `VFXSpawner.SpawnAttached` 표준 패턴으로 마이그레이션.

**수정** ([dash_invuln_parry_feedback_plan.md](dash_invuln_parry_feedback_plan.md))
- [KhiDashController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDashController.cs) — `dashInvulnerabilityEnabled/Duration/health` 필드 추가
- [KhiParryController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs) — `parryHitbox` + `parryHitboxScale` 노출
- [KhiParryFeedbackPresenter.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryFeedbackPresenter.cs) — 직접 Instantiate → VFXSpawner

### 3. 패링 헬스 안전장치 (신규)

패링 상태에서 stuck 시 health 일관성 보장.
- [KhiParryHealth.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryHealth.cs) (신규)

### 4. 상호작용 프롬프트 시스템 (신규)

E 키 + Trigger collider + UnityEvent로 상점/NPC/문 등 범용 상호작용.
- [KhiInteractionPrompt.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiInteractionPrompt.cs) (신규)
- [ButtonInteract.prefab](../../LostMemory/Assets/_Project/Prefabs/System/ButtonInteract.prefab) (신규)
- TopDownEngine [ButtonPrompt.prefab](../../LostMemory/Assets/TopDownEngine/Common/Prefabs/GUI/ButtonPrompt.prefab) 미세 조정

### 5. 재능 시스템 Town 연결 + ManaRegen → MoveSpeed

ManaRegen 슬롯이 데드스탯이라 MoveSpeed로 교체 + Town 씬에 TalentNPC/TalentPanel wiring.

**수정** ([talent_manaregen_to_movespeed_plan.md](talent_manaregen_to_movespeed_plan.md), [talent_town_wiring_plan.md](talent_town_wiring_plan.md))
- [TalentType.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Talents/TalentType.cs)
- [RunStartStats.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Talents/RunStartStats.cs)
- [TalentCalculator.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Talents/TalentCalculator.cs)
- [TalentStartupApplier.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Talents/TalentStartupApplier.cs)
- [TalentPanel.prefab](../../LostMemory/Assets/_Project/Prefabs/UI/TalentPanel.prefab)
- [TalentStartupApplier.prefab](../../LostMemory/Assets/_Project/Prefabs/System/TalentStartupApplier.prefab) (신규 — 던전 부트스트랩 부착용)

### 6. 사운드 카탈로그 + SFX 바인더

프로젝트에 자체 사운드 없고 외부 패키지 191개 분산 → 카탈로그 문서화 + 캐릭터에 SFX 바인딩 컴포넌트.

- [sound.md](sound.md) — TopDownEngine/MMInterface/MMFeedbacks 등 191개 정리, 상황별 치트시트 포함
- [KhiSfxBinder.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiSfxBinder.cs) (신규)

### 7. 아이템 아이콘 자동 할당 (방금 작업)

신규 PNG 53개 ↔ 96개 RelicData 매핑 자동화.
- [icon_mapping.csv](../../LostMemory/Assets/_Project/ScriptableObjects/Relics/_source/icon_mapping.csv) — 51개 매핑
- [RelicIconAutoAssignMenu.cs](../../LostMemory/Assets/_Project/Scripts/Editor/Relics/RelicIconAutoAssignMenu.cs)
- [Art/UI/Icon/](../../LostMemory/Assets/_Project/Art/UI/Icon/) — PNG 53개
- 상세: [item-icon-auto-assign.md](item-icon-auto-assign.md)

### 8. 동반 변경 (영향 받음)

- **씬 일괄**: `Dungeon_1F_1R`~`4R`, `Shop`, `Shop_testkhi`, `Test/1R_khi`, `1F_SHOP.prefab` — HUD/상호작용 prefab 연결
- **플레이어 prefab**: [TestKhi_MinimalCharacter2D.prefab](../../LostMemory/Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab) — 신규 컴포넌트(쿨다운/SFX/패링헬스) 부착
- **VFX**: [Flash_VFX.prefab](../../LostMemory/Assets/_Project/Prefabs/Effect/Flash_VFX.prefab) — 패링 피드백 연결
- **폰트**: [malgun SDF.asset](../../LostMemory/Assets/_Project/Art/Fonts/malgun%20SDF.asset) — 한글 글리프 추가
- **RelicData**: Generated 78개 `.asset` 일괄 변경 (CSV 재생성 또는 사이즈/등급 일괄 적용 결과로 추정)

---

## 미커밋 상태

```
modified : 17 files (scripts/prefabs/scenes/font/talent SO)
modified : 78 files (Generated RelicData *.asset)
untracked: Art/UI/Icon/ + 신규 스크립트 6개 + 신규 prefab 2개 + CSV + plan md 7개
```

## 커밋 분할 권장 (영역별)

미커밋이 한 working tree에 쌓여 있어, 머지 PR 만들 때 영역별로 쪼개면 리뷰가 쉽다:

1. **쿨다운 HUD** — Cooldown* 스크립트 3 + PlayerHUD.prefab + KhiParryController getter
2. **대시 무적/패링 폴리시** — KhiDashController + KhiParryController + KhiParryFeedbackPresenter + Flash_VFX
3. **패링 헬스 안전장치** — KhiParryHealth
4. **상호작용 시스템** — KhiInteractionPrompt + ButtonInteract.prefab + ButtonPrompt.prefab
5. **재능 ManaRegen→MoveSpeed + Town 연결** — Talent* 4파일 + TalentPanel/TalentStartupApplier prefab
6. **사운드 카탈로그 + SFX 바인더** — sound.md + KhiSfxBinder
7. **아이콘 자동 할당** — Art/UI/Icon + icon_mapping.csv + RelicIconAutoAssignMenu
8. **씬 통합** — Dungeon_1F_*, Shop*, TestKhi prefab, 1F_SHOP.prefab (마지막 — 위 모두 연결)
9. **RelicData 78개 일괄** — Generated/*.asset (별도, 어떤 작업 결과인지 확인 필요)
10. **폰트** — malgun SDF
11. **문서** — docs/khi/*.md

## 미해결 / 확인 필요

- **RelicData Generated 78개** 가 왜 변경됐는지 추적 필요 (icon 자동 할당은 아직 Apply 안 했으니 별개 원인). CSV 재생성 또는 `RelicSizeApplyMenu` 결과일 가능성.
- **아이콘 자동 할당 Dry Run/Apply 미실행** — Unity 에디터 측 작업 남음.
- **재능 시스템 End-to-End 검증 미실행** — [talent_town_wiring_plan.md](talent_town_wiring_plan.md) §3 시나리오.
- **쿨다운 HUD 임시 sprite** — 디자이너 sprite 교체 v2.
- 패링 후 공격 안 되는 버그 — [dash_invuln_parry_feedback_plan.md](dash_invuln_parry_feedback_plan.md) Task 4 안전장치는 Optional, 본격 진단은 별도 세션.
