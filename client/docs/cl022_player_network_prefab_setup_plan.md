# Phase B-2 — 플레이어 네트워크 프리팹 + 이동 동기화 절차

## 문서 목적

Phase B-2 에 해당하는 *Unity Editor 작업 절차서*. 다음 두 가지를 수행한다.

1. `TestKhi_MinimalCharacter2D.prefab` 의 *네트워크 복제본* `TestKhi_Net_AD.prefab` 생성
2. NetworkObject + `PlayerMovementSync` 부착, NetworkManager 의 PlayerPrefab 슬롯 교체

C# 코드(`PlayerMovementSync.cs`) 는 이미 작성되어 있다. 본 문서는 Editor 작업만 다룬다.

> 본 문서의 작업은 `docs/commonness/agent-unity-safety-rules.md` 에 따라 *사람 작업자* 가 Unity Editor 에서 직접 수행한다.

## 사전 조건

- Phase B-1 완료 (싱글 플레이 정상 동작 확인됨)
- Phase A 의 `Test_Network_AD.unity` 씬 존재
- `TestKhi_MinimalCharacter2D.prefab` 위치 확인 (`Assets/_Project/Prefabs/Characters/`)

## 1. 프리팹 복제

### 1-1. 원본 위치 확인

1. Unity Project 창에서 `Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` 으로 이동
2. 우클릭 → `Copy` (또는 `Ctrl + D`)

### 1-2. 이름 변경

1. 복제된 파일 이름이 `TestKhi_MinimalCharacter2D 1.prefab` 정도로 생성됨
2. **F2 누르거나 우클릭 → Rename** → 이름을 **`TestKhi_Net_AD`** 로 변경

> 작업자 이니셜(`_AD`)은 commonness 규칙(`scene-ownership-and-prefab-edit-rules.md`) 의 복제 명명 권장. 본인 이니셜로 변경 가능.

## 2. NetworkObject 추가

1. Project 창에서 **`TestKhi_Net_AD.prefab`** 더블클릭 → 프리팹 편집 모드 진입
2. Hierarchy 에서 *루트 GameObject* 클릭 (가장 위 요소, 보통 프리팹 이름과 동일)
3. Inspector → `Add Component` → 검색 `Network Object` → **`Network Object`** 추가
4. 추가된 NetworkObject 컴포넌트의 옵션은 **모두 기본값**으로 둠 (특별히 변경하지 않음)

> 이미 NetworkObject 가 있다면 (혹시 모를 케이스) 이 단계 skip.

## 3. PlayerMovementSync 추가

1. 같은 루트 GameObject 에 `Add Component` → 검색 `Player Movement Sync` → **`Lost Memory / Networking / Player Movement Sync`** 추가
2. 인스펙터 옵션:
   - **`Character`**: 비워둠 (Awake 시 자동으로 같은 GameObject 의 `Character` 컴포넌트 검색)
   - **`State Aggregator`**: 비워둠 (자동 검색)
   - **`Convert Non Owner To Ai`**: ✅ 체크 (기본 true)
   - **그 외 NetworkTransform 의 필드** (Position/Rotation/Scale Threshold, Sync Position, Interpolate 등): 기본값
     - 2D 게임이라 `Sync Position Z` 는 끄는 게 깔끔하지만 기본값으로 두어도 무방
     - `Interpolate` ✅ 체크 (기본값) — 부드러운 이동 보간

## 4. 프리팹 저장

1. 좌상단 `<` 버튼 또는 Hierarchy 의 `Save` 버튼 클릭으로 프리팹 편집 모드 종료
2. 자동 저장됨

## 5. NetworkManager 의 PlayerPrefab 슬롯 교체

지금까지 Phase A 검증용 `Stub_NetworkPlayer.prefab` 이 들어있던 자리를 새 프리팹으로 교체.

1. `Test_Network_AD.unity` 씬 열기
2. Hierarchy 에서 **`NetworkManager`** 클릭
3. Inspector → `Network Manager` 컴포넌트 펼치기
4. **`Player Prefab`** 슬롯에 현재 들어있는 `Stub_NetworkPlayer (NetworkObject)` 가 보임
5. Project 창에서 `TestKhi_Net_AD.prefab` 을 잡아서 **`Player Prefab`** 슬롯에 드래그 앤 드롭
6. 슬롯 내용이 `TestKhi_Net_AD (NetworkObject)` 로 바뀜

### 5-1. NetworkPrefabs List 확인 (자동일 가능성 높음)

1. `Network Manager` 컴포넌트 안에 **`Network Prefabs`** 리스트 있는지 확인
2. 그 리스트에 `TestKhi_Net_AD` 가 자동으로 포함됐는지 확인
   - 보통 Player Prefab 으로 지정하면 자동 등록됨
   - 안 됐으면 수동으로 `+` 클릭 → `TestKhi_Net_AD.prefab` 추가

### 5-2. Stub 정리 (선택)

- `Stub_NetworkPlayer.prefab` 은 백업으로 보관해도 되고 삭제해도 무방
- Phase A 회귀 테스트 시 다시 쓸 수 있으니 *보관 권장*

## 6. 씬 저장

`Ctrl + S` 로 씬 저장.

## 7. 검증

### 7-1. 컴파일 확인

- Editor 의 Console 창에 빨간 에러 없는지 확인
- `PlayerMovementSync` 가 정상 컴파일됐는지 확인

### 7-2. 단일 인스턴스 동작

1. ▶ Play
2. 호스트 클릭 → 코드 발급 후 화면에 *내 플레이어 캐릭터가 spawn 되어 보이는지* 확인
   - 이전엔 Stub 이라 안 보였는데 이제 진짜 캐릭터 보여야 함
3. WASD 로 이동 가능한지 확인
4. Console 에 `[Net][Player] Local player spawned. ClientId=0 OwnerClientId=0` 로그 떠야 함

### 7-3. 2인 인스턴스 동작 (핵심)

MPPM 또는 빌드 + Editor 로 2 인스턴스 띄움.

#### 시나리오

1. **인스턴스 A**: 호스트 → 코드 발급 → 캐릭터 A 가 spawn 되어 보임 (자기 + 자기)
2. **인스턴스 B**: 코드 입력 → 참가 → 화면에 *2개 캐릭터* 가 보여야 함
   - 자기 캐릭터 (B 본인)
   - 호스트 캐릭터 (A)
3. **A 측에서도 마찬가지**: 자기 + B 캐릭터 2개 보여야 함

#### 이동 검증

1. A 가 WASD 로 이동 → A 측 화면뿐 아니라 **B 측 화면에서도 A 캐릭터가 이동하는 게 보여야 함**
2. B 가 WASD 로 이동 → A 측 화면에서 B 캐릭터가 이동하는 게 보여야 함
3. 지터(딸깍거림) 가 100ms 이하로 자연스러운지 확인

#### 입력 격리 검증 (중요)

각 인스턴스의 WASD 가 *자기 캐릭터만* 움직이는지 확인:

- A 측에서 WASD → A 캐릭터만 움직임. B 캐릭터는 가만히 (A 가 보기에)
- B 측에서 WASD → B 캐릭터만 움직임. A 캐릭터는 가만히 (B 가 보기에)
- A 가 안 움직이는데 B 캐릭터가 A 측 화면에서 움직이고 있다면 → B 의 입력이 broadcast 되고 있는 정상 동작

### 7-4. Console 로그 확인

다음 로그가 양쪽에 각각 한 번씩 떠야 함:

호스트 측:
```
[Net][Player] Local player spawned. ClientId=0 OwnerClientId=0
[Net][Player] Remote player spawned. OwnerClientId=1 LocalClientId=0
```

클라이언트 측:
```
[Net][Player] Remote player spawned. OwnerClientId=0 LocalClientId=1
[Net][Player] Local player spawned. ClientId=1 OwnerClientId=1
```

## 8. 문제 해결

### Q1. Spawn 직후 빨간 NRE 에러

`NetworkConnectionManager.HandleConnectionApproval` 에서 NRE → Phase A 의 stub 문제 해결과 동일. PlayerPrefab 슬롯이 비어있거나 NetworkObject 가 없는 프리팹이 들어가있으면 발생. **5단계 다시 확인**.

### Q2. 2 인스턴스에서 캐릭터가 *한쪽만* 보임

NetworkPrefabs 리스트에 `TestKhi_Net_AD` 가 없을 가능성. **5-1 단계 확인**.

### Q3. 자기 캐릭터 이동 시 상대편 화면에서 *순간이동* 같음

NetworkTransform 의 `Interpolate` 가 꺼져있을 수 있음. PlayerMovementSync 인스펙터에서 `Interpolate` ✅ 체크.

### Q4. 양쪽 인스턴스에서 *둘 다* 양쪽 캐릭터를 동시에 조작하는 것 같음

`Convert Non Owner To Ai` 가 체크 해제되어 있을 가능성. PlayerMovementSync 인스펙터에서 ✅ 체크.

이래도 해결 안 되면 TDE `InputManager` 가 양쪽 Character 를 모두 발견해서 입력 보내고 있는 것. 본 컴포넌트는 spawn 시 비-owner 의 `CharacterType` 을 AI 로 바꾸는데, `InputManager` 가 이미 binding 한 후라면 효과가 늦을 수 있다. 그 경우는 후속 작업으로 InputManager 측 binding 시점 조정 필요. 일단 *대부분의 경우 정상 동작* 한다.

### Q5. 비-owner 캐릭터가 *벽에 끼인 듯* 떨림

Rigidbody2D 의 물리 충돌이 NetworkTransform 의 위치 갱신과 충돌. 비-owner 측의 Rigidbody2D 를 Kinematic 으로 전환하는 후속 작업 필요. 본 단계 검증에는 큰 영향 없음.

### Q6. 호스트 인스턴스에는 보이는데 클라 인스턴스에는 *내 캐릭터* 가 안 보임

NGO 가 OwnerClientId 매칭으로 자동 spawn 하므로 보통 자동. NetworkManager 의 `Connection Approval` 같은 옵션이 켜져있으면 spawn 이 막힐 수 있음. 일반적으로 기본값 (꺼짐) 으로 두면 OK.

## 9. 다음 단계

검증 통과 시 → **Phase B-3 (KhiPlayerStateNetSync — 행동 상태 동기화)** 진입.

검증 실패 시 → 8장 트러블슈팅 참고 후 미해결되면 보고.

## 10. 관련 파일

```text
LostMemory/Assets/_Project/Scripts/Runtime/Networking/Player/
  PlayerMovementSync.cs               (Phase B-2 신규)
  LocalPlayerResolver.cs              (Phase B-1 신규)

LostMemory/Assets/_Project/Prefabs/Characters/
  TestKhi_MinimalCharacter2D.prefab   (원본 — 무수정)
  TestKhi_Net_AD.prefab               (신규 — 본 절차서로 생성)
  Stub_NetworkPlayer.prefab           (Phase A 산출 — 보관 또는 삭제)
```

## 11. 관련 문서

- `cl020_network_test_scene_setup_plan.md` — Phase A 절차서
- `commonness/networking-integration-rules.md` — 통합 규칙 (시그니처 안정성, LocalPlayerResolver 패턴)
- `commonness/scene-ownership-and-prefab-edit-rules.md` — 프리팹 복제 명명 규칙
- `commonness/topdown-engine-extension-and-original-protection.md` — TDE 확장 원칙
