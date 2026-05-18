# CL-207 마을씬 보강 범위 확정 메모

- 작성일: 2026-05-07
- Epic: Epic R. 마을/귀환 흐름
- 작업 코드: CL-207
- 작업명: 마을씬 보강 범위 확정
- 산출물: 추가 작업 범위 메모
- 기준 문서: `docs/town/CL-206-town-scene-status-memo.md`

## 결론

CL-207의 결론은 다음과 같다.

MVP1 마을 작업은 정식 마을 콘텐츠 제작이 아니라, 기존 `Town.unity`를 기준으로 마을 시작, 던전 이동, 던전 종료 후 마을 귀환이 안정적으로 이어지게 만드는 범위로 제한한다.

이번 Epic R에서 우선 해결할 흐름:

```text
Town 시작
-> Portal_ToDungeon 상호작용
-> Dungeon 진입
-> Run 종료 또는 귀환 요청
-> Town 복귀
-> 지정된 귀환 위치에서 플레이어 재배치
```

## 아트 및 레이아웃 방향

첨부 레퍼런스 기준으로 마을은 단순 빈 테스트 공간이 아니라, 기능 건물과 포탈이 배치된 허브 마을 형태로 잡는다.

핵심 분위기:

- 밝은 판타지 톤의 숲속 마을
- 2D 탑다운 또는 쿼터뷰 느낌의 타일 기반 배치
- 중앙 돌길을 기준으로 기능 건물이 갈라지는 구조
- 숲, 꽃, 울타리, 표지판, 가로등, 수레 같은 장식으로 마을감을 만든다.
- 각 기능 구역은 간판이나 포탈 색으로 목적을 바로 알아볼 수 있어야 한다.

레퍼런스 기준 주요 구역:

```text
좌상단: 기억 건물
좌하단: 플레이어 집
중앙 하단: NPC 집
우상단: 던전 입구
우상단 옆: 멀티 던전 입구
우하단: 게시판, 수레, NPC 대화 구역
중앙: 이동 동선이 모이는 돌길
```

MVP1에서는 이 구역들을 모두 완성된 기능으로 만들지는 않는다. 다만 마을 레이아웃을 짤 때 위치만 먼저 잡아서 이후 기능 연결이 쉬운 구조로 둔다.

MVP1에 실제 배치할 구역:

- `MemoryHouse_Placeholder`: 기억 시스템용 건물 자리
- `PlayerHouse_Placeholder`: 플레이어 집 자리
- `NpcHouse_Placeholder`: 기본 NPC 상호작용 자리
- `Portal_ToDungeon`: 기본 던전 입구
- `Portal_ToMultiDungeon_Placeholder`: 멀티 던전 자리만 확보
- `NoticeBoard_Placeholder`: 게시판 또는 퀘스트 보드 자리
- `TownSpawnPoint`: 마을 기본 시작 위치
- `TownReturnSpawnPoint`: 던전/전투 종료 후 귀환 위치

아트 적용 기준:

- 확정된 아트 에셋이 있으면 기존 `TownMap_Temp`를 단계적으로 교체한다.
- 에셋이 아직 프리팹/타일맵으로 정리되지 않았다면, CL-208에서는 위치와 동선만 먼저 잡고 실제 아트 교체는 별도 작업으로 분리한다.
- 외부 에셋 원본은 직접 수정하지 않고, 프로젝트 소유 씬과 `_Project` 아래 복제본 또는 프로젝트용 에셋만 사용한다.

## MVP1 필수 범위

아래 항목은 마을/귀환 흐름이 깨지지 않기 위해 이번 범위에 포함한다.

### 1. Build Settings 정상 등록 확인

필수 이유:

- `RunManager.ReturnToTown()`이 `Application.CanStreamedLevelBeLoaded("Town")`에 의존한다.
- `Town` 씬이 Build Settings에 정상 등록되어 있지 않으면 귀환이 실패한다.

작업 방식:

- Unity Editor에서 `File > Build Settings`를 연다.
- `Assets/_Project/Scenes/Town/Town.unity`가 enabled 상태인지 확인한다.
- `Assets/_Project/Scenes/Dungeon/Dungeon.unity`가 enabled 상태인지 확인한다.
- 누락되어 있으면 Unity Editor에서 추가한다.

주의:

- `ProjectSettings/EditorBuildSettings.asset`는 Unity가 관리하는 설정 파일이므로 CLI 직접 편집은 피한다.

### 2. 마을 시작 위치 확정

필수 이유:

- 마을 최초 진입 위치와 귀환 위치를 구분해야 이후 흐름이 명확해진다.

확정안:

- 기존 `TownSpawnPoint`는 마을 최초 진입 또는 기본 시작 위치로 사용한다.
- 위치는 우선 현재 `(0, 0, 0)`을 유지한다.
- 플레이어가 시작 직후 포탈이나 벽과 겹치지 않는지만 Play Mode에서 확인한다.

후속 작업:

- CL-209에서 `TownSpawnPoint` 위치와 방향을 최종 확인한다.

### 3. 귀환 전용 위치 추가

필수 이유:

- 던전/전투 종료 후 마을로 돌아왔을 때 일반 시작 위치와 다른 위치를 사용할 수 있어야 한다.
- 이후 상점, 회복, NPC 동선이 붙어도 귀환 위치를 안정적으로 유지할 수 있다.

확정안:

- 새 오브젝트 이름은 `TownReturnSpawnPoint`로 한다.
- 위치는 포탈과 겹치지 않는 마을 중앙 근처 또는 시작 위치 근처로 둔다.
- 초기 MVP에서는 방향값보다 위치값을 우선한다.

후속 작업:

- CL-210에서 Unity Editor로 `TownReturnSpawnPoint`를 추가한다.
- CL-214에서 귀환 시 해당 지점을 사용하도록 연결한다.

### 4. 던전 이동 포탈 유지 및 위치 확인

필수 이유:

- 마을에서 던전으로 진입하는 최소 흐름은 이미 `Portal_ToDungeon`이 담당하고 있다.
- 이번 단계에서는 새 포탈 시스템을 만들기보다 기존 포탈을 검증하고 필요한 위치만 보정하는 편이 안전하다.

확정안:

- 기존 `Portal_ToDungeon`을 유지한다.
- 대상 씬명은 현재 값인 `Dungeon`을 유지한다.
- 상호작용 방식은 현재처럼 `Interact` 입력 또는 fallback `E` 키를 사용한다.
- 포탈은 플레이어 시작 위치와 너무 가깝지 않게 둔다.

후속 작업:

- CL-213에서 포탈 위치, 반경, 표시 오브젝트를 Play Mode 기준으로 확인한다.

### 5. 카메라 추적 및 경계 보정

필수 이유:

- 현재 카메라는 플레이어를 따라가는 단서가 있지만, 맵 밖을 비추지 않도록 막는 경계 설정은 확인되지 않았다.
- 마을 레이아웃을 조금만 넓혀도 카메라 보정이 필요하다.

확정안:

- 현재 `Main Camera`와 `Town Input Manager` 기반 추적 구조는 유지한다.
- MVP1에서는 복잡한 Cinemachine 재구성보다 현재 구조에 맞는 최소 경계 보정 방식을 우선 검토한다.
- 정식 카메라 시스템 도입은 후순위로 둔다.

후속 작업:

- CL-211에서 실제 마을 크기에 맞춰 카메라가 맵 밖을 과하게 비추지 않는지 확인한다.

### 6. 이동 가능 영역과 충돌 검증

필수 이유:

- `Floor`와 `Walls` 모두 `TilemapCollider2D`가 있어, 실제 이동 가능 여부를 Play Mode에서 확인해야 한다.
- 벽 충돌이 없거나 바닥 충돌이 플레이어 이동을 방해하면 마을 기본 이동이 깨진다.

확정안:

- `Walls`는 이동 제한용 Collider로 사용한다.
- `Floor` Collider는 플레이어 이동에 문제를 만들면 제거 또는 설정 변경 대상으로 본다.
- 맵 밖으로 나갈 수 없도록 외곽 충돌을 유지한다.

후속 작업:

- CL-212에서 충돌 레이어와 실제 플레이어 이동을 확인한다.

### 7. 마을 UI 노출 규칙 확인

필수 이유:

- 던전에서 마을로 돌아온 뒤 전투 UI, 결과 UI, 보스 UI가 남아 있으면 귀환 흐름 품질이 떨어진다.

확정안:

- 마을씬 자체에는 당장 새 UI를 추가하지 않는다.
- 마을 진입 시 전투 전용 UI가 남지 않는지만 확인한다.
- 필요하면 CL-215에서 마을용 HUD만 남기고 전투 UI를 숨기는 방식으로 정리한다.

## MVP1 후순위 범위

아래 항목은 있으면 좋지만, 귀환 흐름 완성보다 우선하지 않는다.

### NPC Placeholder

- CL-216에서 위치만 확보한다.
- 실제 NPC 기능, 상점 기능, 회복 기능, 퀘스트 기능은 별도 작업으로 분리한다.

### 마을 사운드

- CL-217에서 임시 BGM 또는 환경음 연결 여부만 정한다.
- 최종 음원 선정, 믹싱, 사운드 연출은 이번 범위에서 제외한다.

### 마을 레이아웃 장식

- CL-208에서 이동 동선을 알아볼 수 있을 정도로만 보강한다.
- 정식 건물 아트, 장식물 밀도, 연출용 오브젝트는 후순위다.

## 이번 범위에서 제외

아래 항목은 CL-207 기준 이번 Epic R MVP1 범위에서 제외한다.

- 정식 마을 아트 완성
- 상점 UI 완성
- 회복 NPC 기능 완성
- 퀘스트 NPC 기능 완성
- 재능/기억/성장 시스템 마을 연동 완성
- 저장/로드 시스템과 마을 위치 연동
- 정식 BGM/SFX 리소스 확정
- 멀티플레이 귀환 RPC 완성
- Bootstrap/Lobby 전체 구조 개편
- TopDown Engine 원본 수정

## 확정된 후속 작업 범위

CL-208부터 CL-218까지는 다음 기준으로 진행한다.

```text
CL-208 마을 레이아웃 보강
-> 임시 타일맵 기준으로 동선과 중심 공간만 정리

CL-209 플레이어 시작 위치 정리
-> TownSpawnPoint 유지, 시작 충돌 여부 확인

CL-210 귀환 위치 설정
-> TownReturnSpawnPoint 추가

CL-211 카메라 추적/경계 보정
-> 현재 카메라 구조 유지, 최소 경계 보정

CL-212 이동 가능 영역/충돌 보강
-> Walls Collider 검증, Floor Collider 영향 확인

CL-213 씬 이동 트리거 배치
-> Portal_ToDungeon 유지, 위치/반경/대상 씬명 확인

CL-214 귀환 흐름 연동
-> RunManager의 Town 귀환 이후 TownReturnSpawnPoint 사용 방식 연결

CL-215 마을 UI 노출 상태 정리
-> 전투 UI 잔존 여부 확인, 필요 시 숨김 처리

CL-216 NPC/상호작용 위치 확보
-> Placeholder만 배치

CL-217 마을 사운드 설정
-> 임시 BGM 또는 환경음 연결 여부 결정

CL-218 마을씬 통합 테스트
-> 시작, 이동, 포탈, 던전 진입, 귀환, UI 상태 확인
```

## Unity Editor 작업 체크리스트

CL-207 이후 실제 씬 작업에 들어가기 전, Unity Editor에서 먼저 확인할 항목이다.

```text
1. Town.unity 열기
2. Build Settings에서 Town/Dungeon 씬 등록 상태 확인
3. Play Mode로 Town 단독 실행
4. Player가 움직이는지 확인
5. Floor Collider가 이동을 막지 않는지 확인
6. Walls Collider가 맵 밖 이동을 막는지 확인
7. Portal_ToDungeon에서 Interact/E 입력으로 Dungeon 이동되는지 확인
8. Dungeon 종료 후 Town으로 돌아오는 흐름이 있는지 확인
```

## 완료 기준

- MVP1에서 반드시 할 마을 보강 범위가 정리되어 있다.
- 후순위 작업과 제외 범위가 분리되어 있다.
- `TownSpawnPoint`, `TownReturnSpawnPoint`, `Portal_ToDungeon`의 역할이 정해져 있다.
- CL-208 이후 작업자가 무엇을 먼저 확인해야 하는지 정리되어 있다.
- Unity serialized 파일은 아직 수정하지 않았다.

## 주의 사항

- 이번 CL-207은 범위 확정 문서 작업이다.
- 실제 `Town.unity`, 프리팹, `.asset`, `.meta` 파일은 수정하지 않는다.
- Unity 씬 변경은 CL-208 이후 Unity Editor에서 진행한다.
- `ProjectSettings/EditorBuildSettings.asset` 수정이 필요하면 Unity Editor에서 Build Settings UI로 처리한다.
