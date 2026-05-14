# TopDownEngine 제거 후보 목록

작성일: 2026-05-15  
프로젝트: `client/LostMemory`

## 목적

TopDownEngine을 프로젝트에서 뺐을 때 빌드가 빨라졌다는 현상을 기준으로,
현재 프로젝트에서 제거하거나 축소할 수 있는 TopDownEngine 하위 폴더 후보를 정리한다.

이 문서는 **삭제 실행 문서가 아니라 후보 목록**이다. 실제 삭제/이동은 외부 에셋 원본 보호 규칙 때문에
Unity Editor에서 백업/브랜치/컴파일 검증 후 진행해야 한다.

## 현재 관찰값

TopDownEngine 전체:

| 항목 | 값 |
|---|---:|
| 전체 파일 수 | 6082 |
| 전체 용량 | 약 218 MB |
| C# 파일 | 1101 |
| asmdef | 9 |
| asmref | 22 |
| shader | 17 |

TopDownEngine 상위 폴더별:

| 폴더 | 파일 수 | 용량 |
|---|---:|---:|
| `Assets/TopDownEngine/ThirdParty` | 4576 | 173.47 MB |
| `Assets/TopDownEngine/Demos` | 853 | 41.07 MB |
| `Assets/TopDownEngine/Common` | 642 | 3.35 MB |

`_Project` 런타임 코드의 MoreMountains 의존:

| 네임스페이스/패턴 | 참조 파일 수 | 참조 수 |
|---|---:|---:|
| `MoreMountains.TopDownEngine` | 95 | 95 |
| `MoreMountains.Tools` | 40 | 41 |
| `MoreMountains.InventoryEngine` | 0 | 0 |
| `MoreMountains.Feedbacks` | 0 | 0 |
| `MoreMountains.Interface` / `MMInterface` | 0 | 0 |

해석:

- `TopDownEngine/Common/Scripts`와 `MMTools` 계열은 지금 코드와 프리팹이 실제로 사용 중이다.
- `InventoryEngine`, `MMFeedbacks`, `MMInterface`는 우리 C# 코드에서 직접 using 하지는 않지만,
  일부 프리팹이 `MMF_Player`, `Ding.wav`, 데모 스프라이트 등을 직접 참조한다.
- 따라서 TopDownEngine 전체 삭제는 아직 불가능하고, 먼저 데모/미사용 샘플을 줄이는 방식이 현실적이다.

## 즉시 삭제 금지

아래는 현재 상태에서 삭제하면 컴파일 오류나 Missing Script/Missing Reference가 날 가능성이 높다.

| 경로 | 이유 |
|---|---|
| `Assets/TopDownEngine/Common/Scripts` | `_Project` 코드 95개 파일이 `MoreMountains.TopDownEngine`을 사용한다. 프리팹도 `Character`, `Health`, `DamageOnTouch`, `AIBrain` 등을 직접 참조한다. |
| `Assets/TopDownEngine/Common/ScriptsInputSystem` | 현재 입력 시스템 연동 가능성이 있다. TDE 캐릭터 입력 경로가 남아 있으면 필요하다. |
| `Assets/TopDownEngine/ThirdParty/MoreMountains/MMTools/Core` | `_Project` 코드 40개 파일이 `MoreMountains.Tools`를 사용한다. |
| `Assets/TopDownEngine/ThirdParty/MoreMountains/MMTools/Foundation` | `AIBrain` 등 TDE/MoreMountains 기반 로직과 연결된다. |
| `Assets/TopDownEngine/ThirdParty/MoreMountains/MMTools/Accessories` 전체 | `MMHealthBar.cs`가 `_Project` 프리팹에서 직접 참조된다. 전체 제거는 위험하다. |
| `Assets/TopDownEngine/ThirdParty/MoreMountains/MMFeedbacks/MMFeedbacks` | `_Project` 프리팹들이 `MMF_Player.cs`를 직접 참조한다. |
| `Assets/TopDownEngine/ThirdParty/MoreMountains/MMInterface` 전체 | `Ding.wav`가 캐릭터 프리팹에서 직접 참조된다. 전체 제거는 먼저 참조 정리가 필요하다. |

## 1차 제거 후보: 참조 해소 후 제거

아래는 제거 효과가 있고, 현재 직접 참조가 명확한 후보들이다.
먼저 참조를 `_Project` 복제본이나 대체 에셋으로 바꾼 뒤 제거할 수 있다.

### 1. `Assets/TopDownEngine/Demos/Koala2D`

| 항목 | 값 |
|---|---:|
| 파일 수 | 633 |
| 용량 | 35.68 MB |
| C# 파일 | 3 |

현재 직접 참조:

| 참조 대상 | 참조하는 `_Project` 파일 |
|---|---|
| `KoalaSwordAnimatorController.controller` | `Assets/_Project/Prefabs/Weapons/TestKhi_Sword.prefab` |
| `KoalaGunBulletMaterial.mat` | `Assets/_Project/Prefabs/Weapons/Projectiles/ArcherArrow.prefab` |
| `KoalaPixelDustMaterial.mat` | 적 프리팹 다수 |
| `KoalaWeaponsBulletRifleMaterial.mat` | `Assets/_Project/Prefabs/Weapons/ArcherBow.prefab` |
| `KoalaUIReticle.prefab` | `Assets/_Project/Prefabs/Weapons/ArcherBow.prefab` |
| `KoalaDash.wav`, `KoalaHurt3.wav`, `KoalaLoot.wav` | `TestKhi_MinimalCharacter2D.prefab` |
| `KoalaSword1/2/3.wav` | `TestKhi_MinimalCharacter2D.prefab`, `TestKhi_Sword.prefab` |
| `KoalaMachineGun3.wav`, `KoalaImpact.wav` | `ArcherBow.prefab`, `ArcherArrow.prefab` |
| `KoalaDungeonGroundGrey.png`, tile asset | `BerthaBossRoom.prefab` |
| `KoalaSwordIdle.png` | `TestKhi_Sword.prefab` |

권장 작업:

1. 위 에셋 중 실제로 계속 쓸 것만 `Assets/_Project/Audio`, `Assets/_Project/Art`, `Assets/_Project/Materials` 등으로 복제한다.
2. 해당 프리팹 참조를 복제본으로 교체한다.
3. `Koala2D` 직접 참조가 0인지 다시 스캔한다.
4. 제거 또는 `Assets/_Disabled/TopDownEngine_Demos_Koala2D` 같은 외부 보관 위치로 이동한다.

우선순위: 높음.  
주의: 참조가 많아서 한 번에 삭제하면 Missing Reference가 많이 난다.

### 2. `Assets/TopDownEngine/Demos/Grasslands`

| 항목 | 값 |
|---|---:|
| 파일 수 | 218 |
| 용량 | 5.39 MB |
| C# 파일 | 2 |

현재 직접 참조:

| 참조 대상 | 참조하는 `_Project` 파일 |
|---|---|
| `Sprites/GrasslandsLeaf.png` | `Assets/_Project/Prefabs/UI/RunResultPanel.prefab` |

권장 작업:

1. `RunResultPanel.prefab`의 leaf 이미지를 `_Project` 소유 UI/스프라이트로 교체한다.
2. 참조가 사라진 것을 확인한 뒤 `Demos/Grasslands` 제거를 검토한다.

우선순위: 높음.  
주의: 참조가 1개라 정리 난이도가 낮다.

### 3. `Assets/TopDownEngine/ThirdParty/MoreMountains/InventoryEngine/Demos/PixelRogue`

| 항목 | 값 |
|---|---:|
| 파일 수 | 145 |
| 용량 | 1.84 MB |

현재 직접 참조:

| 참조 대상 | 참조하는 `_Project` 파일 |
|---|---|
| `Sprites/Adventurer.png` | `Chobomb_CL212`, `Moose1_Test`, `Orc_CL037`, `OrcRider_CL039`, `SkeletonArcher_CL041`, `StoneGolem_Test` |

권장 작업:

1. 적 프리팹들이 왜 `Adventurer.png`를 참조하는지 확인한다. 임시/누락 스프라이트일 가능성이 높다.
2. `_Project` 소유 적 스프라이트나 placeholder로 교체한다.
3. 참조가 사라진 뒤 `PixelRogue` 데모 제거를 검토한다.

우선순위: 중간.  
주의: 여러 적 프리팹이 물고 있으므로 교체 후 시각 확인 필요.

## 2차 제거 후보: 직접 참조는 적지만 컴파일 검증 필요

아래는 프로젝트 직접 참조가 거의 없거나 데모 성격이 강하지만, asmdef/asmref나 패키지 조건부 컴파일 때문에
삭제 후 컴파일 검증이 필요하다.

### 4. `Assets/TopDownEngine/ThirdParty/MoreMountains/MMFeedbacks/Demos`

| 하위 폴더 | 파일 수 | 용량 |
|---|---:|---:|
| `MMFeedbacksDemo` | 147 | 32.06 MB |
| `SequencingDemo` | 271 | 25.08 MB |

현재 `_Project` 직접 참조:

- 데모 폴더 자체에 대한 직접 참조는 발견하지 못했다.
- 단, `MMFeedbacks/MMFeedbacks/Core/MMF_Player/MMF_Player.cs`는 `_Project` 프리팹들이 직접 사용 중이다.

권장 작업:

1. `Demos`만 제거 후보로 본다. `MMFeedbacks/MMFeedbacks` core는 제거하지 않는다.
2. 제거 전 브랜치에서 폴더를 임시로 프로젝트 밖으로 이동한다.
3. Unity 컴파일과 주요 적/무기 프리팹 Missing Script 여부를 확인한다.

우선순위: 높음.  
기대 효과: 용량 약 57MB 감소, 데모 에셋 import 부담 감소.

### 5. `Assets/TopDownEngine/ThirdParty/MoreMountains/MMTools/Demos`

| 항목 | 값 |
|---|---:|
| 파일 수 | 226 |
| 용량 | 33.04 MB |
| C# 파일 | 4 |

현재 `_Project` 직접 참조:

- 직접 참조는 발견하지 못했다.

권장 작업:

1. `MMTools/Core`, `Foundation`, `Accessories`는 남긴다.
2. `MMTools/Demos`만 제거 후보로 테스트한다.
3. Unity 컴파일과 Play 진입을 확인한다.

우선순위: 높음.  
기대 효과: 데모 에셋 import 부담 감소.

### 6. `Assets/TopDownEngine/ThirdParty/MoreMountains/MMFeedbacks/MMFeedbacksForThirdParty`

| 항목 | 값 |
|---|---:|
| 파일 수 | 488 |
| 용량 | 1.22 MB |
| C# 파일 | 212 |

현재 `_Project` 직접 참조:

- 직접 참조는 발견하지 못했다.

검토 대상:

```text
Cinemachine
HDRP
NiceVibrations
PostProcessing
TextMeshPro
UIToolkit
URP
VisualEffectGraph
```

권장 작업:

1. 실제 사용하는 Feedback 타입을 확인한다.
2. 사용하지 않는 연동 모듈만 부분 제거한다.
3. C# 파일 수가 많으므로 용량보다 컴파일 시간 개선 효과를 기대할 수 있다.

우선순위: 중간.  
주의: `MMF_Player` core를 쓰는 프리팹이 있으므로 Feedbacks 전체 제거는 불가.

### 7. `Assets/TopDownEngine/ThirdParty/MoreMountains/InventoryEngine`

| 항목 | 값 |
|---|---:|
| 전체 파일 수 | 244 |
| 전체 용량 | 2.82 MB |
| C# 파일 | 38 |

현재 `_Project` C# 직접 의존:

- `MoreMountains.InventoryEngine` 사용 없음.

현재 serialized 직접 참조:

- `Demos/PixelRogue/Sprites/Adventurer.png`만 직접 참조 발견.

권장 작업:

1. 먼저 `Demos/PixelRogue` 참조를 제거한다.
2. 이후 InventoryEngine 전체 제거 가능 여부를 별도 브랜치에서 컴파일 테스트한다.

우선순위: 중간.  
주의: TopDownEngine 내부 asmdef/스크립트가 InventoryEngine을 참조할 수 있으므로 바로 삭제하지 않는다.

## 보류 후보: 효과 대비 위험이 큼

| 경로 | 보류 이유 |
|---|---|
| `Assets/TopDownEngine/ThirdParty/MoreMountains/MMTools/Accessories` | 37.67MB로 크지만 `MMHealthBar.cs` 직접 참조가 있다. 하위 폴더 단위로 더 잘라야 한다. |
| `Assets/TopDownEngine/ThirdParty/MoreMountains/MMInterface` | 37.48MB로 크지만 `Ding.wav` 직접 참조와 UI/Interface 계열 잠재 의존성이 있다. 전체 제거보다는 사용 중인 사운드 교체 후 세부 분석 필요. |
| `Assets/TopDownEngine/Common/ScriptsCinemachine` | 작고 위험 대비 효과가 낮다. 카메라 연동이 남아 있을 수 있다. |
| `Assets/TopDownEngine/Common/ScriptsPostProcessing` | 작고 효과가 낮다. 우선순위 낮음. |

## 권장 실행 순서

1. `Demos/Grasslands` 참조 1개 정리
2. `Koala2D`에서 실제 사용하는 사운드/머티리얼/타일/컨트롤러를 `_Project` 소유 복제본으로 이전
3. `InventoryEngine/Demos/PixelRogue`의 `Adventurer.png` 참조 제거
4. `MMFeedbacks/Demos` 임시 제거 후 컴파일 검증
5. `MMTools/Demos` 임시 제거 후 컴파일 검증
6. 그 다음 `MMFeedbacksForThirdParty`, `InventoryEngine` 전체 제거 가능성 검토

## 검증 체크리스트

각 단계마다 다음을 확인한다.

```text
Unity Console 컴파일 에러 없음
Missing Script 없음
Missing Material / Missing Sprite / Missing AudioClip 없음
Title -> Town 진입
Dungeon_1F_1R 진입
TestKhi 캐릭터 이동/공격/대시/피격
적 프리팹 스폰 및 공격
RunResultPanel 표시
ArcherBow / ArcherArrow 동작
BerthaBossRoom 타일/시각 요소 정상
```

## 결론

TopDownEngine을 뺐을 때 빌드가 빨라진 주 원인은 최종 빌드 포함량보다
TopDownEngine이 가진 스크립트/asmdef/Editor/데모/서드파티 모듈 처리 비용일 가능성이 높다.

가장 먼저 줄일 후보는 다음이다.

```text
Assets/TopDownEngine/Demos/Grasslands
Assets/TopDownEngine/Demos/Koala2D
Assets/TopDownEngine/ThirdParty/MoreMountains/InventoryEngine/Demos/PixelRogue
Assets/TopDownEngine/ThirdParty/MoreMountains/MMFeedbacks/Demos
Assets/TopDownEngine/ThirdParty/MoreMountains/MMTools/Demos
```

단, 현재 `_Project` 프리팹이 Koala2D와 PixelRogue 데모 에셋을 직접 참조하고 있으므로,
삭제보다 먼저 참조 이전 작업을 해야 한다.
