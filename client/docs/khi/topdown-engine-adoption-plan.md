# TopDown Engine 활용 후보 정리

> 기준 문서: `docs/khi/game-design-draft.md`  
> 기준 에셋: `LostMemory/Assets/TopDownEngine`

현재 기획은 2D 탑다운 액션 로그라이크 구조이며, TopDownEngine에서 바로 가져오기 좋은 영역과 직접 구현해야 할 영역이 비교적 명확하게 나뉜다.

## 1. 결론 요약

TopDownEngine은 다음 영역에서 적극 활용하는 것이 좋다.

- 2D 플레이어 이동, 대시, 방향 전환
- 체력, 피해, 피격, 사망, 회복
- 근접/원거리 무기 기반
- 적 AI의 순찰, 감지, 추적, 공격
- 방 클리어 조건, 적 전멸 감지
- 문, 포탈, 탈출구, 상호작용 트리거
- 드랍, 픽업, 회복 아이템
- 카메라, 룸 단위 전환, 기본 매니저
- 피격/공격/획득 연출 피드백

반대로 다음 영역은 게임 고유 시스템이므로 직접 구현하는 편이 맞다.

- 유물 3택 보상 UI
- 유물 중복 제외와 세트 태그 효과
- 재능 포인트 투자 UI와 영구 성장 적용
- 기억/기억의 조각/기억의 파편 퍼즐 시스템
- 무기 강화 단계 해금과 런 시작 전 강화 선택
- 상점의 상품 선정, 가격, 구매 규칙
- 스테이지의 큰방/작은방/상점/보스방 생성 규칙

## 2. 채택 기준

| 판단 | 의미 |
|---|---|
| 그대로 채택 | 엔진 기능이나 프리팹 구조를 거의 그대로 사용할 수 있음 |
| 수정 채택 | 엔진 기능을 기반으로 프로젝트용 래퍼나 커스텀 스크립트를 붙여야 함 |
| 직접 구현 | 엔진에는 기반만 있고, 게임 규칙 자체는 새로 만들어야 함 |
| 보류 | 현재 기획과 직접 관련이 낮거나 MVP 이후 검토 |

## 3. 기획 시스템별 활용 후보

| 기획 시스템 | 필요한 기능 | TopDownEngine 후보 | 판단 | 비고 |
|---|---|---|---|---|
| 플레이어 기본 조작 | 2D 이동, 방향, 애니메이터 연동 | `Character`, `TopDownController2D`, `CharacterMovement`, `CharacterOrientation2D` | 그대로 채택 | 모든 시스템의 기반 |
| 대시 | 대시 거리, 시간, 쿨타임, 대시 중 무적 | `CharacterDash2D` | 수정 채택 | 질주 태그/재능/기억 조각 효과가 쿨타임과 무적시간을 바꾸도록 연결 |
| 체력 | 최대 체력, 현재 체력, 피격, 회복, 사망 | `Health`, `HealthChangeEvent`, `HealthAutoRefill` | 그대로 채택 | 유물/재능은 `MaximumHealth`, 회복량에 수치 보정 |
| 피해 | 접촉 피해, 무기 피해, 넉백, 무적 시간 | `DamageOnTouch`, `MeleeWeapon`, `Projectile` | 그대로 채택 | 근접/원거리 공통 피해 기반 |
| 검/칼 | 근접 공격, 3타 콤보, 마무리 타격 | `MeleeWeapon`, `ComboWeapon`, `Weapon` | 수정 채택 | 맹공 4세트의 3타 마무리 추가 피해를 위해 콤보 단계 이벤트가 필요 |
| 망치 | 느린 근접 공격, 강공격, 넉백 | `MeleeWeapon`, `ChargeWeapon`, `Weapon` | 수정 채택 | 강공격을 차지 또는 별도 무기로 처리 가능 |
| 활 | 투사체 발사, 조준, 탄속, 산탄/다중 발사 | `ProjectileWeapon`, `Projectile`, `WeaponAim2D`, `WeaponAutoAim2D` | 그대로 채택 | 활의 조작감만 별도 튜닝 |
| 무기 선택 | 시작 무기 장착 | `CharacterHandleWeapon`, `PickableWeapon` | 수정 채택 | 로비 선택 결과로 초기 무기 프리팹을 장착하도록 커스텀 |
| 무기 강화 단계 | 강화 단계 해금, 시작 전 단계 선택 | `Weapon` 수치 필드 | 직접 구현 | 엔진은 무기 런타임 기반만 제공, 해금/선택은 별도 데이터 필요 |
| 적 기본 AI | 순찰, 랜덤 이동, 추적, 도망 | `AIBrain`, `AIActionMovePatrol2D`, `AIActionMoveRandomly2D`, `AIActionMoveTowardsTarget2D`, `AIActionMoveAwayFromTarget2D` | 그대로 채택 | 일반몹 MVP에 적합 |
| 적 감지 | 원형 감지, 시야각 감지, 라인 오브 사이트 | `AIDecisionDetectTargetRadius2D`, `AIDecisionDetectTargetConeOfVision2D`, `AIDecisionLineOfSightToTarget2D` | 그대로 채택 | Deadline/Koala2D 데모 참고 |
| 적 공격 | 근접 공격, 원거리 사격, 재장전 | `AIActionShoot2D`, `AIActionAimWeaponAtTarget2D`, `AIActionReload`, `MeleeWeapon`, `ProjectileWeapon` | 그대로 채택 | 무기 프리팹을 AI에게 장착 |
| 작은방 클리어 | 방 안 적 전멸 시 탈출구 활성화 | `KillsManager`, `Health`, `MMLifeCycleEvent` | 수정 채택 | `KillsManager.OnLastDeath`로 문/포탈 활성화 |
| 방 입장 잠금 | 입장 시 탈출구 비활성화, 적 처치 후 활성화 | `CharacterDetector`, `ButtonActivatedZone`, `Switch`, `AppearDisappear` | 수정 채택 | 방 상태 관리 스크립트는 직접 필요 |
| 방 이동 | 다음 방/다음 씬/진입점 이동 | `FinishLevel`, `GoToLevelEntryPoint`, `LevelManager` | 수정 채택 | 작은방을 씬으로 쪼갤지 한 씬 내부 룸으로 둘지 먼저 결정 필요 |
| 룸 카메라 | 방마다 카메라 제한과 진입 이벤트 | `Room`, `TopDownCinemachineZone2D`, `LevelLimits` | 수정 채택 | `KoalaRooms`, `KoalaCinemachineZones` 참고 |
| 상점 | 상호작용, 구매, 아이템 제공 | `ButtonActivatedZone`, `DialogueZone`, `PickableItem`, `InventoryEngine` | 직접 구현 | 구매/가격/상품 선정 규칙은 직접 필요 |
| 포션 | 체력 25%, 50% 회복 | `Stimpack`, `InventoryEngineHealth` | 수정 채택 | 퍼센트 회복은 커스텀 Stimpack 파생 클래스 권장 |
| 돈 | 코인 획득, 보유량 | `Coin`, `PickableItem`, `InventoryEngine` | 수정 채택 | 단순 화폐면 커스텀 CurrencyManager가 더 가벼움 |
| 유물 보상 | 유물 3개 중 1개 선택 | `PickableItem`, `MMFeedbacks`, `MMLootTable` | 직접 구현 | 핵심 규칙은 직접. 엔진 픽업/연출만 활용 |
| 유물 확률 | 등급별 확률, 중복 제외 | `MMLootTable` | 수정 채택 | 기본 가중치 테이블은 가능하지만 중복 제외/등급별 3택은 직접 로직 필요 |
| 유물 효과 | 공격력, 공속, 체력, 대시 쿨타임 등 수치 변경 | `Weapon`, `Health`, `CharacterMovement`, `CharacterDash2D` | 직접 구현 | 효과 적용 대상은 엔진 컴포넌트, 효과 시스템은 직접 |
| 세트 태그 | 2세트/4세트 효과 | 없음 | 직접 구현 | RelicSetManager 같은 별도 런 상태 관리자 필요 |
| 수호 방패 | 궤도 방패, 투사체 방어, 재생성 | `DamageResistance`, `DamageOnTouch`, `Projectile` 참고 | 직접 구현 | 엔진 기본 기능만으로는 부족 |
| 패링 | 패링 성공 시 보호막 | 없음 | 직접 구현 | 현재 기획의 미정 사항. 패링 시스템부터 필요 |
| 재능 | 치명타, 공속, 방어력, 마나 회복, 체력 | `Health`, `Weapon`, `DamageResistance` | 직접 구현 | 로비 UI/포인트/저장 구조는 직접 |
| 기억 시스템 | 퍼즐 조각, 메타 재화, 무기 해금 | `MMSaveLoadManager`, `MMPersistenceManager` 참고 | 직접 구현 | TopDownEngine의 게임플레이 기능과 별개 |
| 보스방 | 보스 체력, 공격, 사망 후 진행 | `Health`, `AIBrain`, `Weapon`, `FinishLevel`, `Loot` | 수정 채택 | 보스 패턴은 직접, 기반은 엔진 활용 |
| 연출 | 피격, 사망, 획득, 보상 선택 효과 | `MMFeedbacks`, `MMF_Player`, `SoundManager` | 그대로 채택 | 체감 품질 대비 효율 좋음 |
| UI | 체력바, 탄약, 대화, 일시정지 | `HealthBar.prefab`, `AmmoDisplay`, `DialogueBox`, `PauseButton` | 수정 채택 | 디자인은 교체하고 로직만 참고 |
| ComfyUI | 이미지 생성/공유 서버 | 없음 | 직접 구현 | TopDownEngine과 무관 |

## 4. MVP 우선 활용 순서

### 1단계: 플레이 가능한 전투 프로토타입

가장 먼저 TopDownEngine으로 검증할 묶음이다.

| 목표 | 사용할 기능 | 참고 위치 |
|---|---|---|
| 플레이어 이동 | `Character`, `TopDownController2D`, `CharacterMovement`, `CharacterOrientation2D` | `Demos/Minimal2D/MinimalScene2D.unity` |
| 대시 | `CharacterDash2D` | `Demos/Koala2D/Prefabs/PlayableCharacters/Koala.prefab` |
| 체력/피격 | `Health`, `DamageOnTouch` | `Demos/Koala2D/KoalaHealth.unity` |
| 근접 공격 | `MeleeWeapon`, `ComboWeapon` | `Demos/Koala2D/Prefabs/Weapons/Weapons/KoalaSword.prefab` |
| 원거리 공격 | `ProjectileWeapon`, `Projectile`, `WeaponAim2D` | `Demos/Koala2D/Prefabs/Weapons/Weapons/KoalaGun.prefab` |
| 적 AI | `AIBrain`, 2D AI Actions/Decisions | `Demos/Koala2D/Prefabs/AI` |

### 2단계: 작은방 클리어 루프

기획의 “작은방 안 적을 다 죽이기 전까지 탈출 불가”를 검증한다.

| 목표 | 사용할 기능 | 추가 구현 |
|---|---|---|
| 방 진입 감지 | `CharacterDetector`, `Room` | RoomCombatController |
| 입장 시 문 닫기 | `Switch`, `ButtonActivated`, `AppearDisappear` | 문 상태 제어 |
| 적 전멸 감지 | `KillsManager.OnLastDeath` | 방 단위 적 목록 연결 |
| 탈출구 활성화 | `FinishLevel`, `GoToLevelEntryPoint` | 다음 방 선택 로직 |
| 방 카메라 | `Room`, `TopDownCinemachineZone2D` | 방 경계 설정 |

### 3단계: 보상과 상점

TopDownEngine의 픽업/루트 기능은 활용하되, 유물 선택 로직은 직접 만든다.

| 목표 | 사용할 기능 | 추가 구현 |
|---|---|---|
| 회복약 | `Stimpack` | 퍼센트 회복 버전 |
| 코인 | `Coin`, `PickableItem` | 화폐 관리 방식 결정 |
| 랜덤 드랍 | `Loot`, `MMLootTable` | 중복 제외 규칙 |
| 보상 선택 UI | `MMFeedbacks` 참고 | RelicRewardUI 직접 구현 |
| 상점 구매 | `ButtonActivatedZone`, `DialogueZone` | ShopManager 직접 구현 |

### 4단계: 런 전용 성장

유물/재능/기억은 TopDownEngine을 수정하는 방식이 아니라, 별도 시스템이 엔진 컴포넌트 값을 조정하는 방식이 좋다.

```text
RelicManager / TalentManager / MemoryManager
  ├─ Health.MaximumHealth 조정
  ├─ Weapon.TimeBetweenUses 조정
  ├─ MeleeWeapon.MinDamageCaused / MaxDamageCaused 조정
  ├─ CharacterMovement.MovementSpeed 조정
  └─ CharacterDash2D.Cooldown / InvincibleWhileDashing 조정
```

## 5. 직접 구현해야 하는 핵심 매니저 제안

| 매니저 | 역할 | TopDownEngine과 연결되는 지점 |
|---|---|---|
| `RunManager` | 현재 런 상태, 스테이지 진행, 보상 지급 | `LevelManager`, `FinishLevel` |
| `RoomCombatController` | 방 입장, 적 스폰, 문 잠금, 방 클리어 | `Room`, `CharacterDetector`, `KillsManager` |
| `StageRouteManager` | 큰방 4개, 상점 1개, 보스방 1개 순서 관리 | `GoToLevelEntryPoint` 또는 룸 이동 |
| `RelicManager` | 보유 유물, 중복 제외, 세트 태그, 효과 적용 | `Health`, `Weapon`, `CharacterDash2D`, `CharacterMovement` |
| `RelicRewardManager` | 유물 3택, 등급 확률, 랜덤 박스 | `MMLootTable` 참고 가능 |
| `ShopManager` | 상품 3종, 포션 1종, 가격, 구매 처리 | `PickableItem`, `Stimpack` |
| `TalentManager` | 시작 전 재능 포인트 투자, 런 시작 시 적용 | `Health`, `Weapon`, `DamageResistance` |
| `MemoryManager` | 기억의 파편, 기억의 조각, 퍼즐 완성, 무기 해금 | `MMSaveLoadManager` 참고 가능 |
| `WeaponUpgradeManager` | 무기 강화 단계 해금과 시작 전 선택 | `CharacterHandleWeapon`, `Weapon` |

## 6. 데모별 확인 포인트

| 데모 | 확인할 것 | 왜 봐야 하는가 |
|---|---|---|
| `Minimal2D/MinimalScene2D.unity` | 플레이어, 카메라, 매니저 최소 구성 | 프로젝트 시작 기준점 |
| `Minimal2D/MinimalSandbox2D.unity` | 문, 포탈, 데미지존, 킬존, 이동 플랫폼 | 작은방 기믹 구성 참고 |
| `Minimal2D/Minimal2DDoors1.unity` | 문/포탈 이동 | 탈출구 구현 참고 |
| `Koala2D/KoalaDungeon.unity` | 던전, 무기, AI, 아이템, 문, 열쇠 | 현재 기획과 가장 유사 |
| `Koala2D/KoalaRooms.unity` | 룸 단위 카메라/구역 | 작은방 구조 참고 |
| `Koala2D/KoalaProceduralTilemap.unity` | 타일맵 생성 | 방 자동 생성 검토 시 참고 |
| `Deadline/Scenes/DeadlineLevel1.unity` | 순찰 적, 시야 감지, 레벨 진행 | 적 감지/추적 참고 |
| `Grasslands/Grasslands.unity` | 근접 전투 감각 | 검/망치/칼 참고 |

## 7. 기획별 추천 판단

### 적극 채택

- 플레이어 이동
- 대시
- 체력/피해
- 근접 무기
- 원거리 무기
- AI 순찰/추적/공격
- 방 클리어 적 카운트
- 포탈/탈출구
- 회복 아이템
- 피드백/사운드/카메라

### 수정해서 사용

- 작은방 잠금/해제
- 스테이지 방 이동
- 상점 상호작용
- 코인/화폐
- 랜덤 드랍
- 보스방 클리어 후 다음 단계 이동
- 무기 선택과 장착

### 직접 구현

- 유물 선택 UI
- 유물 효과 적용 시스템
- 세트 태그 시스템
- 재능 시스템
- 기억 시스템
- 무기 강화 해금
- 상점 상품 생성/가격 규칙
- 방 배치 규칙
- 패링
- 마나/스킬

## 8. 주의점

TopDownEngine은 기능들이 서로 연결되어 있다. 특히 캐릭터/무기/입력/매니저는 다음 묶음으로 봐야 한다.

```text
Character
  ├─ TopDownController2D
  ├─ CharacterMovement
  ├─ CharacterOrientation2D
  ├─ CharacterDash2D
  ├─ CharacterHandleWeapon
  └─ Health

Scene
  ├─ GameManager
  ├─ LevelManager
  ├─ GUIManager
  ├─ InputManager
  └─ Camera setup
```

따라서 스크립트만 하나씩 복사하기보다, `Minimal2D` 또는 `Koala2D` 프리팹을 기준으로 작은 실험 씬을 만들고 거기서 프로젝트 구조에 맞게 줄이는 방식이 좋다.

## 9. 다음 작업 제안

1. `MinimalScene2D` 기준으로 우리 플레이어 프리팹 후보를 만든다.
2. `KoalaSword`, `KoalaGun`을 참고해 검/활 프로토타입을 만든다.
3. `KoalaNinja` 계열 AI를 참고해 일반 적 1종을 만든다.
4. `KillsManager.OnLastDeath`로 방 클리어 시 문이 열리는 작은방 프로토타입을 만든다.
5. 이후 유물/재능/기억 시스템은 별도 매니저로 붙인다.

