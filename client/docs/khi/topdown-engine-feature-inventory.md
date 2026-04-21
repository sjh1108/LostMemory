# TopDown Engine Feature Inventory

`Assets/TopDownEngine` 기준으로 2D 탑다운 게임 제작에 활용 가능한 기능을 정리한 문서입니다.

## 기능 목록

| 분류 | 만들 수 있는 기능 | 대표 기능/컴포넌트 | 참고 데모 |
|---|---|---|---|
| 캐릭터 조작 | 2D 이동, 방향 전환, 달리기, 대시, 그리드 이동 | `Character`, `TopDownController2D`, `CharacterMovement`, `CharacterRun`, `CharacterDash2D`, `CharacterGridMovement` | `Minimal2D`, `Koala2D` |
| 전투 | 근접 공격, 원거리 공격, 투사체, 폭탄, 차지 공격, 콤보 공격 | `CharacterHandleWeapon`, `MeleeWeapon`, `ProjectileWeapon`, `Projectile`, `Bomb`, `ChargeWeapon`, `ComboWeapon` | `Koala2D`, `Grasslands` |
| 조준/사격 보조 | 마우스 조준, 자동 조준, 자동 사격, 탄약 표시 | `WeaponAim2D`, `WeaponAutoAim2D`, `WeaponAutoShoot`, `WeaponAmmo`, `AmmoDisplay` | `Koala2D` |
| 체력/피해 | 체력, 회복, 접촉 데미지, 즉사 구역, 피해 타입, 저항 | `Health`, `HealthAutoRefill`, `DamageOnTouch`, `KillZone`, `DamageType`, `DamageResistance` | `Koala2D`, `Minimal2D` |
| 적 AI | 순찰, 랜덤 이동, 추적, 도망, 시야 감지, 반경 감지, AI 사격 | `AIBrain`, `AIActionMovePatrol2D`, `AIActionMoveTowardsTarget2D`, `AIDecisionDetectTargetConeOfVision2D`, `AIActionShoot2D` | `Koala2D` |
| 아이템/루팅 | 코인, 회복템, 무기 줍기, 능력 해금, 루트 드랍 | `PickableItem`, `Coin`, `Stimpack`, `PickableWeapon`, `PickableAbility`, `Loot` | `Koala2D` |
| 인벤토리 | 아이템 보관, 사용, 장착, 핫바, 상자, 키 아이템 | `Inventory`, `InventoryItem`, `InventoryDisplay`, `InventoryHotbar`, `InventoryEngineChest`, `InventoryEngineKey` | `PixelRogue`, `Koala2D` |
| 맵/환경 | 문, 버튼, 스위치, 포탈, 함정, 이동 플랫폼, 대화 구역 | `ButtonActivatedZone`, `KeyOperatedZone`, `Switch`, `FinishLevel`, `DamageOnTouch`, `MovingPlatform2D`, `DialogueZone` | `Minimal2D`, `Koala2D` |
| 진행 구조 | 체크포인트, 리스폰, 레벨 이동, 캐릭터 선택, 레벨 선택 | `CheckPoint`, `AutoRespawn`, `FinishLevel`, `GoToLevelEntryPoint`, `CharacterSelector`, `LevelSelector` | `Deadline`, `Minimal2D` |
| UI/입력 | HUD, 체력바, 탄약 UI, 일시정지, 모바일 조작 | `GUIManager`, `AmmoDisplay`, `PauseButton`, `InputManager`, `InputSystemManager`, `MMTouchJoystick` | `Koala2D`, `Deadline` |
| 카메라/연출 | 카메라 추적, 화면 제한, 흔들림, 플래시, 파티클, 사운드 피드백 | `LevelLimits`, Cinemachine 프리팹, `MMFeedbacks`, `MMF_Player`, `SoundManager` | `Koala2D`, `Minimal2D` |
| 절차적/타일맵 | 타일맵 기반 맵 생성, 던전형 방 구조 참고 | `TilemapLevelGenerator` | `KoalaProceduralTilemap`, `KoalaRooms` |

## 가져올 후보군

| 우선순위 | 후보 기능 | 이유 |
|---|---|---|
| 1순위 | 2D 캐릭터 이동 | 거의 모든 탑다운 게임의 기본 기능이며, 엔진 구조를 먼저 검증하기 좋음 |
| 1순위 | 체력/피해 시스템 | 전투, 함정, 회복템, 적 처치까지 연결되는 기반 시스템 |
| 1순위 | 무기 시스템 | 근접, 원거리, 투사체가 이미 준비되어 있어 프로토타입 속도가 빠름 |
| 1순위 | 픽업 아이템 | 회복템, 무기 획득, 능력 해금에 바로 활용 가능 |
| 1순위 | 적 AI 기본 | 순찰, 추적, 감지, 공격 구조가 이미 있어 적 제작에 유리함 |
| 2순위 | 버튼/문/스위치 | 던전, 퍼즐, 방 단위 진행에 유용함 |
| 2순위 | 체크포인트/리스폰 | 스테이지형 게임이면 거의 필요함 |
| 2순위 | 대화 존 | 스토리, 힌트, 상호작용이 있으면 활용 가능 |
| 2순위 | MMFeedbacks 연출 | 타격감, 피격, 획득, 화면 흔들림 같은 피드백 제작에 유용함 |
| 3순위 | 인벤토리 | 게임이 아이템 중심이면 채택하고, 단순 액션이면 과할 수 있음 |
| 3순위 | 탄약 시스템 | 총기와 자원 관리가 중요하면 채택 |
| 3순위 | 그리드 이동 | 로그라이크나 퍼즐형이면 적합하고, 자유 이동 액션이면 불필요할 수 있음 |
| 3순위 | 캐릭터 교체 | 캐릭터별 능력 차이가 핵심일 때만 채택 |
| 3순위 | 절차적 타일맵 | 랜덤 던전이 핵심일 때만 검토 |

## 먼저 확인할 위치

| 순서 | 위치 | 확인할 것 |
|---|---|---|
| 1 | `Assets/TopDownEngine/Demos/Minimal2D/MinimalScene2D` | 기본 캐릭터 이동과 씬 구성 |
| 2 | `Assets/TopDownEngine/Demos/Koala2D/KoalaDungeon` | 실제 2D 던전형 기능 묶음 |
| 3 | `Assets/TopDownEngine/Demos/Koala2D/Prefabs/PlayableCharacters/Koala` | 플레이어 프리팹 구조 |
| 4 | `Assets/TopDownEngine/Demos/Koala2D/Prefabs/Weapons` | 무기, 투사체, 탄약 구조 |
| 5 | `Assets/TopDownEngine/Demos/Koala2D/Prefabs/AI` | 적 AI 프리팹 구조 |
| 6 | `Assets/TopDownEngine/Demos/Koala2D/Prefabs/ItemPickers` | 아이템 픽업 구조 |
| 7 | `Assets/TopDownEngine/Demos/Koala2D/Prefabs/Props` | 문, 상자, 함정, 스위치 구조 |

## 사용 판단 메모

| 판단 | 기능군 | 메모 |
|---|---|---|
| 거의 바로 가져올 후보 | 이동, 체력, 무기, 픽업, 루팅, 체크포인트, 대화 존, 버튼/문, 2D AI | 프로토타입에 바로 연결하기 좋음 |
| 기획 보고 결정할 후보 | 인벤토리, 탄약, 캐릭터 교체, 그리드 이동, 자동 조준, 차지/콤보 공격 | 게임의 핵심 루프에 따라 채택 여부가 달라짐 |
| 조심해서 쓸 후보 | `GameManager`, `GUIManager`, `InputManager` 전체 구조 | 편하지만 프로젝트 구조가 엔진 방식에 묶일 수 있음 |
