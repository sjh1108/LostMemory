# CL-032 Dungeon Architect 1.22 셋업 및 검증 결과

## 문서 목적

`test_mapmaker` 샌드박스에서 Dungeon Architect 1.22의 LostMemory 호환성을 검증한 결과와, LostMemory에 이식할 때 필요한 절차/패치를 정리한다. 본 문서는 CL-032(방 타입·방 데이터 구조 정의)의 일부로 진행한다.

## 결론 요약

- DA 1.22.0이 Unity 6.3 (6000.3.13f1) + URP 17.3에서 **3개 컴파일 패치 + URP 머티리얼 변환** 후 정상 동작한다.
- **Snap_SideScroller 빌더**(이름과 달리 실제로는 2D 범용 모듈 스티처)가 LostMemory의 "작은방 미리 만들고 조합" 패러다임에 정확히 매치한다.
- 시드 결정론 검증 통과 → **NGO 멀티 동기화는 시드 RPC 방식**으로 가능.
- 데모 자산은 MVP 단계까지 임시 사용 가능 (라이선스 OK, 5단계에서 자기 도트로 교체).

## 환경 정보

| 항목 | 값 |
|---|---|
| Dungeon Architect 버전 | 1.22.0 |
| 검증 Unity 버전 | 6000.3.13f1 (Unity 6.3) |
| 렌더 파이프라인 | URP 17.3 |
| 검증 일자 | 2026-04 |
| 검증 위치 | `C:\Users\AD\test_mapmaker` (샌드박스, 별도 프로젝트) |

## 컴파일 패치 (3건, 필수)

DA 1.22 코드에 Unity 6.3 컴파일러가 거부하는 잘못된 어트리뷰트 사용 3곳이 있다. 패치 안 하면 어셈블리 컴파일 실패 → 프리팹들이 "missing script"로 표시됨.

### Patch 1. `GraphEditor.cs:1694`

파일: `Assets/CodeRespawn/DungeonArchitect/Scripts/Modules/UI/Widgets/GraphEditor/GraphEditor.cs`

문제: `[SerializeField]`가 `event` 선언에 붙어 있음 (event는 직렬화 불가, attribute 무효)

```csharp
// 변경 전
[SerializeField]
private event EventHandler<T> _Event;

// 변경 후 (attribute 한 줄 제거)
private event EventHandler<T> _Event;
```

### Patch 2. `FlowItemMetadataHandler.cs:8`

파일: `Assets/CodeRespawn/DungeonArchitect/Scripts/Modules/Flow/Core/Items/Metadata/FlowItemMetadataHandler.cs`

문제: `[SerializeField]`가 `class` 선언 위에 붙어 있음. 의도는 `[Serializable]`로 추정.

```csharp
// 변경 전
[SerializeField]
public class FlowItemMetadata

// 변경 후
[System.Serializable]
public class FlowItemMetadata
```

### Patch 3. `SnapEdResultGraphEditor.cs:24`

파일: `Assets/CodeRespawn/DungeonArchitect/Editor/Editors/SnapEditor/Widgets/GraphEditors/SnapEdResultGraphEditor.cs`

문제: `[SerializeField]`가 auto-property 위에 붙어 있음. 백킹 필드를 직렬화하려면 `[field: SerializeField]` 사용해야 함.

```csharp
// 변경 전
[SerializeField]
public SnapEdResultGraphEditorConfig ResultGraphPanelConfig { get; private set; }

// 변경 후
[field: SerializeField]
public SnapEdResultGraphEditorConfig ResultGraphPanelConfig { get; private set; }
```

## URP 머티리얼 변환 (필수)

DA 데모 머티리얼들이 Built-in Render Pipeline의 `Sprites/Diffuse` 셰이더를 사용한다. URP에서는 동작 안 함 → 스프라이트가 화면에 안 그려짐.

### 일괄 변환 절차

1. 메뉴 `Window > Rendering > Render Pipeline Converter`
2. 상단 드롭다운에서 **"Built-in to URP"** 선택
3. **`Initialize Converters`** 클릭
4. `Material Upgrader`만 체크 (다른 항목 불필요)
5. **`Convert Assets`** 클릭

### 개별 머티리얼만 빠르게 고치고 싶으면

- 해당 머티리얼 선택 → Inspector → `Shader` 드롭다운 → `Sprites > Default` 또는 `Universal Render Pipeline/2D/Sprite-Lit-Default`로 변경

## 빌더 선택 가이드

LostMemory의 "작은방 미리 만들고 → 조합" 패러다임에 매치되는 빌더를 빠른 비교.

| 빌더 | 특성 | LostMemory 적합도 |
|---|---|---|
| **Snap_SideScroller** (= 사실상 Snap2D) | 모듈 스티칭, 4방향 도어 | ⭐ 즉시 사용 가능 |
| SnapGridFlow (SGF) 2D | 모듈 + Layout Graph로 페이싱 표현 | ⭐⭐ 큰방4+상점+보스 페이싱 명시적 표현 시 |
| GridFlow 2D | Tilemap 단위 자동 생성 | ❌ 미리 만든 방 활용 X |
| Grid (3D) | 클래식 3D 방+복도 | ❌ 2D 아님 |

### 중요한 명명 함정

**Snap2D 데모 씬(`DemoBuilder_Snap2D/Test2DScene.unity`)이 `DungeonSnapSideScroller` 프리팹을 그대로 사용한다.** Code Respawn은 별도 `DungeonSnap2D` 프리팹을 만들지 않았다. Snap_SideScroller 빌더는 이름과 달리 4방향 도어로 모듈을 잇는 범용 2D 스티처이며, 모듈 디자인에 따라 사이드스크롤로도 탑다운으로도 동작한다.

→ 따라서 LostMemory에서도 `DungeonSnapSideScroller` 프리팹이 출발점이다. "SideScroller라서 안 맞는다"고 오해하지 말 것.

## 검증 결과

### A. 모듈 조합 + 자기 자산 호환성 (검증 ✅)

- Snap_SideScroller 빌더 + Module_Horizontal/Vertical/Bridge 모듈 조합으로 깔끔한 탑다운 던전 출력 확인
- 모듈 프리팹 안의 SpriteRenderer는 직접 편집 가능
- 자기 도트(64x64, PPU=64, Filter=Point) 교체 시 정상 반영

### B. 시드 결정론 (검증 ✅)

- `Dungeon.Config.Seed`에 같은 값 입력 → 매번 동일 결과 출력
- → NGO 멀티 동기화 전략 1번(시드 RPC + 클라 로컬 빌드) 사용 가능

### C. Runtime Build (필요)

샌드박스에서는 에디터 Inspector 버튼으로만 빌드 검증 완료. 실제 게임에서는 코드로 호출해야 하므로 LostMemory에서 한 번 더 검증 필요.

```csharp
using UnityEngine;
using DungeonArchitect;

public class RuntimeBuildTest : MonoBehaviour {
    public Dungeon dungeon;

    void Start() {
        dungeon.Config.Seed = 12345;
        dungeon.Build(new RuntimeDungeonSceneObjectInstantiator());
    }
}
```

## LostMemory 이식 절차

### Step 1. DA 임포트

방법 A (권장, 빠름):
1. test_mapmaker에서 `Assets/CodeRespawn` 우클릭 → `Export Package...`
2. `Include dependencies`, `Include all` 체크
3. 원하는 위치에 `.unitypackage` 저장
4. LostMemory에서 `Assets > Import Package > Custom Package...`로 임포트

방법 B (깨끗한 상태, 느림):
1. Unity Asset Store에서 DA 1.22 재다운
2. LostMemory에 임포트
3. 위 패치 3건 수동 적용

### Step 2. 패치 검증

- LostMemory에서 컴파일 → Console에 빨간 에러 0건 확인
- 에러 있으면 위 패치 부분으로 돌아가서 재적용

### Step 3. URP 머티리얼 변환

- 위 절차 따라 일괄 변환

### Step 4. 동작 확인

- 데모 씬 하나 열어서 빌드 정상 동작 확인
- Runtime Build 스크립트 작성 + Play 모드 검증

### Step 5. 폴더 정리

LostMemory 자체 게임 코드는 `Assets/_Project/...` 또는 `Assets/Game/...` 등 분리된 곳에. DA 원본 폴더(`Assets/CodeRespawn`)는 직접 수정 금지 (TDE와 동일 원칙).

DA 모듈 작업 시작할 때 권장 폴더:

```
Assets/_Project/
├── Dungeons/
│   ├── Modules/        // 자기 모듈 프리팹
│   ├── Themes/         // 자기 테마 그래프
│   ├── ModuleDB/       // 자기 Module Database SO
│   └── PlaceableMarkers/ // 자기 스폰 마커 정의
└── ...
```

## 영향 받는 후속 작업

CL-032에 통합하기로 결정함. 다른 티켓들은 구현 단계에서 자연스럽게 DA 활용:

| 티켓 | DA 활용 부분 |
|---|---|
| CL-033 (스폰 포인트 규칙) | DA `PlaceableMarker` 컴포넌트로 룸 안에 적/아이템 스폰 위치 표시 |
| CL-034 (방 진입 시 전투 시작) | DA Query API로 현재 룸 추적 + 트리거로 전투 시작 |
| CL-035 (적 전멸 기준 방 클리어) | 룸 내부 적 카운트 추적 (DA Query + 자체 적 매니저) |
| CL-036 (문 열림 흐름) | DA `SnapConnection` 컴포넌트 활성/비활성으로 문 제어 |
| CL-049 ~ CL-050 (보스방 진입) | Layout Graph에서 보스 노드 별도 정의, 선행 룸 클리어 시점에 잠금 해제 |

## NGO 멀티 동기화 설계 메모

### 권장 전략: 시드 RPC + 로컬 빌드

```
호스트
  └─ 시드 결정 (예: System.DateTime 기반)
  └─ ServerRpc → 클라들에게 시드 전달
  └─ 자기 던전 로컬 빌드

각 클라
  └─ 시드 받음
  └─ 같은 시드로 자기 던전 로컬 빌드
  └─ 결과는 픽셀 단위로 동일

→ 던전 GameObject는 NetworkObject로 만들지 않음
→ 적/아이템만 NetworkObject로 (호스트가 스폰, NGO가 복제)
```

### 비권장 전략: GameObject 전체 복제

호스트가 빌드 후 던전 안의 모든 오브젝트를 NGO로 복제 → 트래픽 폭발, 입장 지연. 절대 금지.

## 라이선스 메모

- DA 데모 자산은 SSAFY 데모 발표/MVP 검증까지 그대로 사용 OK
- 상용 배포 전(이번 프로젝트는 해당 X) 자기 도트로 교체 권장
- 정확한 조건: `Assets/CodeRespawn/DungeonArchitect/LICENSE.txt` 확인

## 미해결 / 차후 검증 필요

- LostMemory에서 Runtime Build 동작 (Step 4)
- TDE Character + DA Dungeon 공존 시 어셈블리 충돌 여부
- 자기 도트로 모듈 교체 시 PPU/Pivot/Filter 일관성 (5단계에서 작업)
- Layout Graph로 큰방4 + 상점 + 보스 페이싱 표현 (CL-049~050 진행 시 또는 SGF 전환 시)
- NGO RPC로 시드 전달 → 모든 클라 동일 빌드 검증 (CL-022~CL-031 진행 시)

## 참고 경로

- 샌드박스 프로젝트: `C:\Users\AD\test_mapmaker`
- 샌드박스 검증 씬: `Assets/CodeRespawn/DungeonArchitect_Samples/DemoBuilder_Snap2D/Test2DScene.unity`
- DA 코어: `Assets/CodeRespawn/DungeonArchitect/`
- DA Launch Pad: 메뉴 `Dungeon Architect > Launch Pad`
- 공식 문서: https://docs.dungeonarchitect.dev/unity/
