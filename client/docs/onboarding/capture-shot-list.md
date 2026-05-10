# 적 / 보스 시스템 — 인게임 캡처 컷 리스트

신규 합류 개발자용 노션 온보딩 페이지에 첨부할 인게임 플레이 캡처를 모은 가이드.
한 번의 플레이 세션으로 24 컷을 모두 모을 수 있도록 발생 순서대로 정리했다.

## 사용 방법

1. 노션에 새 페이지를 만들고 이 파일을 그대로 paste 한다 (체크박스가 todo 블록으로 변환됨).
2. 게임을 플레이하면서 컷 번호 순서대로 캡처를 찍는다.
3. 각 컷의 체크박스 자리에 캡처 이미지를 노션 image 블록으로 드래그&드롭 한다.
4. 캡처 직후 체크박스를 체크해 진행 상태를 표시한다.

## 촬영 환경 권장

- **씬**: `Assets/_Project/Scenes/Dungeon/Dungeon.unity` 에서 시작. 마을(Town) 은 #1~#6 일반 적 학습이 끝난 뒤 보스방으로 이어진다.
- **해상도**: 1920×1080 (16:9). 노션은 가로폭이 좁으므로 캡처는 가로형이 잘 어울린다.
- **디버그 모드**: TopDown Engine 의 `MMDebugMenu` 를 켜 무적/즉사/위치 이동을 활용하면 컷별로 빠르게 이동 가능.
- **GIF**: 텔레그래프→공격 같은 동작은 정지 컷보다 짧은 GIF 가 이해도가 높다. 노션은 .gif 도 inline 표시한다.

## 사실 관계 표기

각 컷에는 **관련 코드** 와 **관련 SO/Prefab** 를 함께 적었다. 신규 개발자가 캡처를 본 직후 같은 자리에서 코드/데이터를 열어볼 수 있도록 하기 위함이다.

---

## A. 일반 적 6 종 (#1~#6)

플레이어가 던전 진입 후 처음 만나는 적들. 각 적의 핵심 행동(텔레그래프 → 공격) 을 보여주는 컷을 1 장씩 확보한다.

- [ ] **#1 Orc — 근접 공격 텔레그래프**
  - 장면: Orc 가 플레이어 전방에서 근접 공격 전조를 띄우는 순간
  - 발생 조건: Dungeon 1 층 진입, Orc 와 약 1.5~2 셀 거리에서 정면 대치
  - 타이밍: 공격 시작 직전 0.3 초 (전조 이펙트가 가장 잘 보이는 프레임)
  - 관련 코드: `Assets/_Project/Scripts/Runtime/Enemies/EnemyData.cs`, [cl037_cl038_melee_enemy_plan.md](../cl037_cl038_melee_enemy_plan.md)
  - 관련 SO/Prefab: `EnemyData_Orc.asset`, `Orc_CL037.prefab`
  - 노션 캡션 추천: `Orc — 근접형. 전조 → 공격 → 회수 루프`

- [ ] **#2 OrcRider — 돌진 전조와 충돌**
  - 장면: OrcRider 가 방향을 고정하고 돌진을 시작하는 순간 (+ 충돌 직전 1 컷 추가 권장)
  - 발생 조건: OrcRider 의 감지 범위 진입, 일직선 시야 확보
  - 타이밍: 돌진 모션 1~2 프레임째
  - 관련 코드: `OrcRiderChargeAttack` 류, [cl039_cl040_charge_enemy_plan.md](../cl039_cl040_charge_enemy_plan.md)
  - 관련 SO/Prefab: `EnemyData_OrcRider.asset`, `OrcRider_CL039.prefab`
  - 노션 캡션 추천: `OrcRider — 돌진형. 전조 후 직선 돌진, 충돌 시 큰 피해 + 넉백`

- [ ] **#3 SkeletonArcher — 조준선과 발사**
  - 장면: 조준 표시(레이저/라인) 가 켜진 직후 화살이 발사되는 순간
  - 발생 조건: SkeletonArcher 와 4~6 셀 거리, 시야가 트인 위치
  - 타이밍: 발사 모션 직후, 화살이 약 1 셀 진행한 시점
  - 관련 코드: `Assets/_Project/Scripts/Runtime/Enemies/`, [cl041_cl042_ranged_enemy_plan.md](../cl041_cl042_ranged_enemy_plan.md)
  - 관련 SO/Prefab: `EnemyData_SkeletonArcher.asset`, `SkeletonArcher_CL041.prefab`
  - 노션 캡션 추천: `SkeletonArcher — 원거리형. 거리 유지하며 조준 → 투사체 발사`

- [ ] **#4 Chobomb — 자폭 카운트다운**
  - 장면: Chobomb 가 자폭 직전 점멸·확장하는 순간
  - 발생 조건: Chobomb 가 플레이어 근접 범위 진입 후 자폭 트리거 발동
  - 타이밍: 폭발 직전 0.5 초 + 폭발 순간 1 컷 (총 2 컷 권장)
  - 관련 코드: `ChobombSelfDestructController.cs`, `ChobombExplosionKnockback.cs`
  - 관련 SO/Prefab: `EnemyData_Chobomb.asset`, `Chobomb_CL212.prefab`
  - 노션 캡션 추천: `Chobomb — 자폭형. 카운트다운 후 폭발, 광범위 넉백`

- [ ] **#5 StoneGolem — 슬램 공격과 넉백**
  - 장면: StoneGolem 의 슬램 모션이 끝나며 플레이어가 넉백되는 순간
  - 발생 조건: StoneGolem 의 근접 공격 범위 진입
  - 타이밍: 슬램 임팩트 프레임 (지면 흔들림/이펙트가 가장 큼)
  - 관련 코드: `StoneGolemAttackKnockback.cs`
  - 관련 SO/Prefab: `EnemyData_StoneGolem.asset`, `StoneGolem_Test.prefab`
  - 노션 캡션 추천: `StoneGolem — 중장형. 슬램 1 회당 큰 피해 + 강한 넉백`

- [ ] **#6 Moose1 — 기본 동작**
  - 장면: Moose1 의 기본 추적/공격 패턴 1 컷 (테스트 적이라 단순)
  - 발생 조건: 테스트 룸 또는 디버그 스폰
  - 타이밍: 임의
  - 관련 SO/Prefab: `EnemyData_Moose1.asset`, `Moose1_Test.prefab`
  - 노션 캡션 추천: `Moose1 — 테스트용 적. 기본 추적/공격 골격만`

---

## B. 보스방 진입 (#7~#9)

선행 방을 모두 클리어한 직후 보스방 게이트가 열리는 흐름.

- [ ] **#7 보스방 문 — 잠김 상태**
  - 장면: 선행 방이 미클리어 상태일 때 보스 도어가 잠겨 있는 모습
  - 발생 조건: 보스방 진입 조건 미충족 상태 그대로 보스 도어 앞 도달
  - 타이밍: 도어 정면 1 셀 거리에서 정지
  - 관련 코드: [cl049_boss_room_entry_condition_plan.md](../cl049_boss_room_entry_condition_plan.md)
  - 관련 Prefab: `BossDoor Variant.prefab`, `RoomEntryRuntimeController.prefab`
  - 노션 캡션 추천: `진입 게이트 — 미충족. 선행 룸 클리어 전에는 잠김`

- [ ] **#8 보스방 문 — 열림 상태**
  - 장면: 선행 방을 모두 클리어한 직후 보스 도어가 잠금 해제되는 연출
  - 발생 조건: 진입 조건 만족 (RunManager 가 해당 상태로 전이)
  - 타이밍: 잠금 해제 이펙트가 가장 큰 프레임
  - 관련 코드: [cl050_boss_room_door_and_entry_flow_plan.md](../cl050_boss_room_door_and_entry_flow_plan.md)
  - 노션 캡션 추천: `진입 게이트 — 충족. 잠금 해제 후 보스방 입장 가능`

- [ ] **#9 Bertha 인트로 — 보스 등장**
  - 장면: 보스방 입장 직후 Bertha 가 등장하는 인트로 컷 (애니메이션 절정 프레임)
  - 발생 조건: 보스방 입장 트리거
  - 타이밍: 인트로 모션 클라이맥스 (뇌 노출/포효 등 시각적 임팩트가 가장 큰 순간)
  - 관련 코드: `BerthaBossEncounterController.cs`, [cl051_boss_pattern1_plan.md](../cl051_boss_pattern1_plan.md)
  - 관련 Prefab: `BerthaBossRoom.prefab`, `BerthaRoot.prefab`
  - 노션 캡션 추천: `Bertha 인트로 — 보스방 입장 직후 등장 시퀀스`

---

## C. 보스 페이즈 변화 (#10~#12)

체력 임계치(70 % / 30 %) 도달 시 외형/공격 패턴 풀이 바뀌는 핵심 시스템.

- [ ] **#10 Phase 1 — 100~80 % 구간**
  - 장면: 전투 시작 직후, Bertha 가 페이즈 1 상태에서 라이트 어택 또는 일반 돌진을 사용하는 모습
  - 발생 조건: 전투 시작 후 ~10 초 이내, 보스 체력 80 % 초과
  - 타이밍: HP 바와 보스 외형이 함께 보이는 구도
  - 관련 코드: `BerthaBossPhaseController.cs`, `BossData.cs` (`Phase2ThresholdNormalized`)
  - 비고: 코드 default 는 0.7 이지만 `Bertha_Boss.asset` 인스턴스는 **0.8 로 오버라이드**. 추후 default 로 되돌리는 결정이 나면 본 컷 캡션도 함께 업데이트
  - 노션 캡션 추천: `Phase 1 — 기본 패턴 풀(라이트 어택 1·2, 일반 돌진)`

- [ ] **#11 Phase 2 — 80 % 임계 도달 직후**
  - 장면: 80 % 체력 도달로 페이즈 2 전환 연출이 발생하는 순간 (외형/뇌 색상 변화 권장)
  - 발생 조건: 보스 체력 정확히 80 % 도달 직후 1~2 초
  - 타이밍: 페이즈 전환 이펙트가 가장 큰 프레임
  - 관련 코드: `BerthaBossPhaseController.cs`, `BerthaBrainAnimationController.cs`
  - 노션 캡션 추천: `Phase 2 — 80 % 임계. 헤비 어택 / 대시 어택 추가 해금`

- [ ] **#12 Phase 3 — 30 % 임계 도달 직후**
  - 장면: 30 % 체력 도달로 페이즈 3 전환 연출이 발생하는 순간
  - 발생 조건: 보스 체력 정확히 30 % 도달 직후 1~2 초
  - 타이밍: 외형/이펙트 변화가 가장 큰 프레임
  - 관련 코드: `BerthaBossPhaseController.cs`, `BossData.cs` (`Phase3ThresholdNormalized`)
  - 노션 캡션 추천: `Phase 3 — 30 % 임계. 풀콤보 + 공격 빈도 가속`

---

## D. 보스 패턴 6 종 (#13~#18)

`BerthaCombatPatternSelector` 가 거리·페이즈·쿨다운을 보고 선택하는 6 가지 패턴. 각 패턴의 텔레그래프 순간을 1 컷씩 확보한다.

- [ ] **#13 LightAttack 1 — 텔레그래프**
  - 장면: 라이트 어택 1 의 전조 표시가 켜진 직후
  - 타이밍: 전조 0.2~0.4 초 사이
  - 관련 코드: `BerthaLightAttack1Controller.cs`, `BerthaLightAttack1Bootstrap.cs`
  - 노션 캡션 추천: `LightAttack 1 — 가장 자주 쓰이는 근거리 견제 패턴`

- [ ] **#14 LightAttack 2 — 텔레그래프**
  - 장면: 라이트 어택 2 의 전조 표시가 켜진 직후
  - 관련 코드: `BerthaLightAttack2Controller.cs`, `BerthaLightAttack2Bootstrap.cs`
  - 노션 캡션 추천: `LightAttack 2 — 후속 견제 / 콤보 연결용`

- [ ] **#15 HeavyAttack — 8 방향 투사체**
  - 장면: 헤비 어택 시전 후 8 방향 투사체가 퍼져나가는 순간
  - 발생 조건: Phase 2 이상 진입 후 거리 중간
  - 타이밍: 투사체가 보스 주변 1~2 셀 떨어진 프레임 (방사형 패턴이 가장 잘 보임)
  - 관련 코드: `BerthaHeavyAttackController.cs`, `BerthaProjectileBurstEmitter.cs`, [cl052_boss_pattern2_and_telegraph_plan.md](../cl052_boss_pattern2_and_telegraph_plan.md)
  - 노션 캡션 추천: `HeavyAttack — 8 방향 투사체. 거리 무관 전역 압박`

- [ ] **#16 NormalDash — 일반 돌진**
  - 장면: 보스가 일반 돌진을 시작하는 순간 (피해 없이 위치 이동만)
  - 관련 코드: `BerthaDashAttackController.cs` 군 내 normal-dash 분기
  - 노션 캡션 추천: `NormalDash — 거리 좁히기용 무피해 이동`

- [ ] **#17 DashAttack — 돌진 + 타격**
  - 장면: 돌진 도중 플레이어와 충돌해 추가 타격이 발생하는 순간
  - 발생 조건: Phase 2 이상, 플레이어와 일정 거리 이상
  - 타이밍: 충돌 임팩트 프레임
  - 관련 코드: `BerthaDashAttackController.cs`, `BerthaDashHitGate.cs`, `BerthaDashStartAction.cs`
  - 노션 캡션 추천: `DashAttack — 돌진 + 추가 타격. 거리 회복 패턴`

- [ ] **#18 FullCombo — fan + omni burst**
  - 장면: 풀콤보 중 fan burst 2 회 후 omni burst 가 터지는 순간
  - 발생 조건: Phase 3 진입 후 일정 거리
  - 타이밍: omni burst 시작 직후 (방사형 투사체가 가장 큰 프레임)
  - 관련 코드: `BerthaFullComboController.cs`, `BerthaProjectilePatternDriver.cs`
  - 노션 캡션 추천: `FullCombo — fan ×2 + omni. 페이즈 3 핵심 위협`

---

## E. 보스 클리어 (#19~#21)

처치 → 보상 흐름.

- [ ] **#19 보스 처치 순간**
  - 장면: Bertha 의 사망 모션 절정 프레임
  - 관련 코드: [cl054_boss_defeat_room_clear_plan.md](../cl054_boss_defeat_room_clear_plan.md)
  - 노션 캡션 추천: `OnDeath → RoomCleared 이벤트 트리거`

- [ ] **#20 BossClearPortal 등장**
  - 장면: 보스 처치 직후 클리어 포탈이 생성되는 연출
  - 관련 Prefab: `BossClearPortal.prefab`
  - 노션 캡션 추천: `BossClearPortal — 다음 스테이지로 이동하는 입구`

- [ ] **#21 RunResultPanel — 결과 화면**
  - 장면: 보스 클리어 후 결과 패널이 표시된 화면 (런 통계가 모두 채워진 상태)
  - 관련 코드: `RunResultPanelView.cs`, [cl055_boss_clear_result_flow_plan.md](../cl055_boss_clear_result_flow_plan.md)
  - 노션 캡션 추천: `RunResultPanel — RunManager 의 RunCleared 상태 전이 후 표시`

---

## F. 핵심 매커닉 보조 (#22~#24)

적/보스 시스템 이해를 보강하는 부가 컷. 시간이 남으면 추가한다.

- [ ] **#22 메모리 회피 — 적 공격을 기억으로 회피**
  - 장면: 플레이어가 메모리 매커닉을 발동해 적 공격을 회피하는 순간
  - 발생 조건: 메모리 시스템이 활성화된 던전 룸
  - 노션 캡션 추천: `메모리 매커닉 — 게임의 핵심 회피 자원`

- [ ] **#23 패링 성공 — KhiParrySpark**
  - 장면: 플레이어 패링 성공 시 KhiParrySpark 이펙트가 터지는 순간
  - 관련 Prefab: `KhiParrySpark.prefab`
  - 노션 캡션 추천: `패링 성공 — 적 공격 무력화 + 반격 윈도우`

- [ ] **#24 상태이상 동시 적용 — 우선순위 가시화**
  - 장면: 적에게 Slow/Burn/Freeze 가 동시에 걸린 상태 (효과는 모두 적용되지만 VFX 는 1 개만 표시되는 정책 D 가시화)
  - 관련 코드: `EnemyStatusEffect.cs`, OnHitEffectRegistry
  - 노션 캡션 추천: `상태이상 정책 D — 효과 모두 적용, VFX 우선순위 Freeze > Burn > Slow`

---

## 진행 체크리스트

- [ ] A. 일반 적 6 종 — #1 ~ #6
- [ ] B. 보스방 진입 — #7 ~ #9
- [ ] C. 보스 페이즈 변화 — #10 ~ #12
- [ ] D. 보스 패턴 6 종 — #13 ~ #18
- [ ] E. 보스 클리어 — #19 ~ #21
- [ ] F. 핵심 매커닉 보조 — #22 ~ #24

24 컷이 모두 채워지면 노션 페이지 상단에 한 줄 설명을 추가하고 신규 합류자에게 공유한다.

## 참고

- 적/보스 코드 트리: [Assets/\_Project/Scripts/Runtime/Enemies/](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/)
- 적/보스 데이터: [Assets/\_Project/ScriptableObjects/Enemies/](../../LostMemory/Assets/_Project/ScriptableObjects/Enemies/)
- 적/보스 프리팹: [Assets/\_Project/Prefabs/Enemies/](../../LostMemory/Assets/_Project/Prefabs/Enemies/)
- 메인 던전 씬: [Dungeon.unity](../../LostMemory/Assets/_Project/Scenes/Dungeon/Dungeon.unity)
- 기존 작업 계획: CL-037 ~ CL-055 (일반 적 / 보스), CL-172 ~ CL-173 (데이터 정식화)
