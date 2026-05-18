# 빌드 외부 의존성 점검 리포트

작성일: 2026-05-15  
프로젝트: `client/LostMemory`

## 점검 범위

`ProjectSettings/EditorBuildSettings.asset`에서 **빌드에 enabled 상태인 씬**을 기준으로 확인했다.
각 씬의 serialized GUID 참조를 재귀적으로 따라가며, `Assets/_Project` 밖에 있는 의존성을 폴더별로 묶었다.

이번 점검은 Unity Editor의 `AssetDatabase.GetDependencies()`가 아니라 YAML/text 기반 정적 스캔이다.
최종 확정용으로는 Editor 스크립트 기반 점검을 한 번 더 돌릴 수 있지만, 지금 단계에서
“외부 에셋을 `_Project`로 모아야 하는가”를 판단하기에는 충분하다.

## 현재 빌드에 켜진 씬

```text
Assets/_Project/Scenes/Title/Title.unity
Assets/_Project/Scenes/Town/Town.unity
Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity
Assets/_Project/Scenes/Dungeon/Dungeon_1F_2R.unity
Assets/_Project/Scenes/Dungeon/Dungeon_1F_Shop.unity
Assets/_Project/Scenes/Dungeon/Dungeon_1F_3R.unity
Assets/_Project/Scenes/Dungeon/Dungeon_1F_4R.unity
Assets/_Project/Scenes/Dungeon/Dungeon_1F_Boss.unity
Assets/_Project/Scenes/Town/Town_testkhi.unity
```

주의: `Town_testkhi.unity`가 현재 enabled 상태다. 릴리즈 빌드에 의도적으로 포함하는 게 아니라면
Build Settings에서 끄는 것이 맞다.

## Serialized 의존성 요약

```text
도달한 전체 serialized 의존성: 1971개
Assets/_Project 밖 외부 의존성: 825개
```

외부 의존성 폴더별 요약:

| 폴더 | 개수 | 참조 용량 MB |
|---|---:|---:|
| `Assets/RafaelMatos` | 684 | 4.55 |
| `Assets/TextMesh Pro` | 5 | 2.51 |
| `Assets/TopDownEngine` | 66 | 2.22 |
| `Assets/Casual Game Sounds U6` | 5 | 1.34 |
| `Assets/Pixel Art Top Down Basic` | 1 | 0.08 |
| `Assets/CodeRespawn` | 18 | 0.05 |
| `Assets/InputSystem_Actions.inputactions` | 1 | 0.05 |
| `Assets/UI` | 45 | 0.01 |

외부 의존성 확장자별 요약:

| 확장자 | 개수 |
|---|---:|
| `.asset` | 456 |
| `.png` | 178 |
| `.anim` | 67 |
| `.cs` | 61 |
| `.prefab` | 21 |
| `.controller` | 19 |
| `.wav` | 14 |
| `.mat` | 3 |
| `.shader` | 3 |
| `.mixer` | 1 |
| `.inputactions` | 1 |
| `.ttf` | 1 |

## 큰 외부 참조 파일

| MB | 경로 |
|---:|---|
| 2.15 | `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` |
| 1.01 | `Assets/TopDownEngine/ThirdParty/MoreMountains/MMInterface/Common/Sounds/Ding.wav` |
| 0.53 | `Assets/RafaelMatos/ERW-Ancient Ruins/Tilesets/Tileset-Terrain2.png` |
| 0.51 | `Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-43.wav` |
| 0.51 | `Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-48.wav` |
| 0.35 | `Assets/RafaelMatos/ERW - The Village/buildings/Premade_houses3.png` |
| 0.33 | `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` |
| 0.22 | `Assets/RafaelMatos/ERW - The Village/tilesets and props/props.png` |
| 0.22 | `Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-08.wav` |
| 0.16 | `Assets/RafaelMatos/ERW-Ancient Ruins/Props/animated/portal-all animations-cemetery.png` |
| 0.15 | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaMachineGun3.wav` |
| 0.15 | `Assets/RafaelMatos/ERW-Ancient Ruins/Props/animated/portal-all animations-sewers.png` |
| 0.15 | `Assets/RafaelMatos/ERW-Ancient Ruins/Props/animated/portal-all animations-grass land.png` |
| 0.15 | `Assets/RafaelMatos/ERW-Ancient Ruins/Props/animated/portal-all animations-grass land2.0.png` |
| 0.14 | `Assets/RafaelMatos/ERW-Ancient Ruins/Props/animated/portal-all animations-crypt.png` |
| 0.14 | `Assets/RafaelMatos/ERW-Ancient Ruins/Props/animated/portal-all animations-old prison.png` |
| 0.14 | `Assets/RafaelMatos/ERW-Ancient Ruins/Props/animated/portal-all animations-ancient ruins.png` |
| 0.13 | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaDash.wav` |
| 0.11 | `Assets/RafaelMatos/ERW-Ancient Ruins/Props/animated/altar-no grass.png` |
| 0.11 | `Assets/RafaelMatos/ERW-Ancient Ruins/Props/animated/altar-grass.png` |

## `_Project` 밖에서 컴파일되는 런타임 C# 코드

아래 스크립트들은 `_Project` 밖에 있지만 runtime-side C#이므로, 실제 참조 파일 크기와 별개로
컴파일/반복 빌드 시간에 영향을 줄 수 있다.

| 폴더 | C# 파일 수 | KB |
|---|---:|---:|
| `Assets/TopDownEngine` | 1008 | 5501.2 |
| `Assets/CodeRespawn` | 516 | 2690.6 |
| `Assets/TextMesh Pro` | 34 | 198.6 |
| `Assets/Scripts` | 3 | 42.4 |
| `Assets/Layer Lab` | 2 | 5.6 |
| `Assets/TestKhi` | 2 | 5.6 |

## `_Project` 전체 프리팹 기준 추가 점검

위의 빌드 씬 기준 점검과 별도로, `Assets/_Project` 아래 모든 프리팹을 시작점으로 다시 스캔했다.

```text
대상 프리팹 수: 139개
도달한 전체 serialized 의존성: 2706개
Assets/_Project 밖 외부 의존성: 1193개
```

프리팹 전체 기준 외부 의존성 폴더별 요약:

| 폴더 | 개수 | 참조 용량 MB |
|---|---:|---:|
| `Assets/RafaelMatos` | 1043 | 6.30 |
| `Assets/TextMesh Pro` | 5 | 2.51 |
| `Assets/TopDownEngine` | 73 | 2.20 |
| `Assets/Casual Game Sounds U6` | 5 | 1.34 |
| `Assets/Pixel Art Top Down Basic` | 2 | 0.09 |
| `Assets/CodeRespawn` | 18 | 0.05 |
| `Assets/Scripts` | 1 | 0.02 |
| `Assets/UI` | 45 | 0.01 |
| `Assets/DefaultNetworkPrefabs.asset` | 1 | 0.00 |

프리팹 전체 기준 외부 의존성 확장자별 요약:

| 확장자 | 개수 |
|---|---:|
| `.asset` | 736 |
| `.png` | 278 |
| `.anim` | 66 |
| `.cs` | 60 |
| `.controller` | 16 |
| `.prefab` | 16 |
| `.wav` | 14 |
| `.shader` | 3 |
| `.mat` | 3 |
| `.ttf` | 1 |

프리팹 전체 기준에서도 결론은 동일하다. `_Project` 프리팹들이 외부 에셋을 많이 참조하긴 하지만,
실제 참조 용량은 외부 폴더 전체 크기에 비해 작다. 예를 들어 `Assets/RafaelMatos`는 전체 약 570MB지만,
`_Project` 프리팹 전체에서 따라가는 참조 용량은 약 6.3MB다.

따라서 프리팹 의존성까지 포함해도, 외부 에셋을 통째로 `_Project`로 모으는 방식은 비효율적이다.
수정이 필요한 프리팹/머티리얼/ScriptableObject만 `_Project` 복제본으로 만들고, 원본 텍스처/오디오/폰트는
외부 에셋 참조를 유지하는 편이 낫다.

## 해석

외부 의존성을 전부 `_Project`로 복사해서 모으는 방식은 권장하지 않는다.

빌드에 켜진 씬이 실제로 참조하는 외부 에셋은 외부 폴더 전체 용량에 비해 작다.
예를 들어 `Assets/RafaelMatos` 전체는 약 570MB지만, 현재 enabled 씬에서 serialized 참조로
도달하는 용량은 약 4.55MB 정도다. 폴더 전체를 `_Project`로 복사하면 빌드 시간이 줄기보다
중복 용량, GUID 관리 비용, 참조 꼬임 위험이 커진다.

다음 외부 에셋은 그대로 참조 유지하는 편이 낫다.

```text
Assets/RafaelMatos
Assets/TextMesh Pro
Assets/TopDownEngine
Assets/Casual Game Sounds U6
Assets/CodeRespawn
Assets/UI
Assets/InputSystem_Actions.inputactions
```

`_Project`로 복사해야 하는 경우는 “우리 프로젝트용으로 수정해야 하는 복제본”이 필요한 경우에 한정한다.

```text
커스텀 프리팹 variant
커스텀 머티리얼
커스텀 ScriptableObject
프로젝트 전용 애니메이션 컨트롤러
```

외부 에셋 원본은 직접 수정하지 않는다.

## 권장 정리 순서

1. 릴리즈 빌드라면 `Assets/_Project/Scenes/Town/Town_testkhi.unity`를 Build Settings에서 끈다.
2. 네트워크 테스트만 빌드할 때는 `Assets/_Project/Scenes/Test/Test_Network_AD.unity`만 켠 별도 build profile을 사용한다.
3. 외부 아트/오디오는 `_Project`로 복사하지 말고 참조 유지한다.
4. `Assets/Scripts` 아래의 프로젝트 소유 runtime 코드는 별도 작업으로 `Assets/_Project/Scripts/Runtime`으로 옮긴다.
   단, 스크립트 GUID를 prefab/scene이 참조할 수 있으므로 Unity Editor에서 move하는 방식이 안전하다.
5. 그래도 반복 빌드가 느리면 병목이 스크립트 컴파일, 셰이더/URP variant, 에셋 import, IL2CPP player build 중 무엇인지 분리해서 확인한다.

## 현재 결론

느린 빌드는 외부 참조를 전부 `_Project`로 모은다고 해결될 가능성이 낮다.

더 안전한 1차 대응은 **빌드에 포함되는 씬을 줄이고 테스트 씬을 제외하는 것**이다.
컴파일 시간이 문제라면, 주요 원인은 외부 아트 용량보다 `TopDownEngine`, `CodeRespawn` 같은
외부 runtime script assembly일 가능성이 더 크다.
