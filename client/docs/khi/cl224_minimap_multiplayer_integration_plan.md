# CL-224: 미니맵 멀티플레이 통합 (팀원 마커 + 공유 fog)

**Epic**: H. UI / 연출 / 아트 적용
**상태**: 코드 ⏳ 대기 / Editor 작업 ⏳ 대기 / 검증 ⏳ 대기
**선행**: CL-222 (미니맵 카메라/HUD/마커), CL-223 (Fog of War)
**후속**: 미니맵 폴리시 (아이콘/줌/AutoFromBounds 등) 별 CL

---

## Context

CL-222·223 으로 싱글플레이 미니맵(카메라+HUD+마커+fog) 완성. 본 CL: **2인 코옵 멀티플레이 통합**.

**핵심 작업**:
1. **팀원 마커 색 분리** — 자기 자신=파랑, 팀원=초록 (NGO `IsOwner` 기반)
2. **공유 fog** — 팀이 본 곳은 팀원에게도 reveal (CL-223 코드 그대로 자동 작동, 추가 코드 0)
3. **적 권위 검증** — 클라이언트 측에서 적 GameObject 가 보이는지 / 적 마커가 뜨는지 확인. 안 뜨면 분기 옵션 결정

**구조 결정 — 별도 `PlayerNetMinimapTinter` 컴포넌트 (option b)**:
`MinimapAgent` 자체를 `NetworkBehaviour` 화하는 것 (option a) 은 적/픽업/포탈도 NetworkObject 강제 → 네트워크 sync 영역 폭발. (option b) 는 Player prefab 에만 추가 컴포넌트 1개 → 깔끔. 기존 `PlayerMovementSync : NetworkTransform` 패턴과 일관.

---

## 시스템 사실

- **CL-222 산출물**:
  - `MinimapAgent.cs` — `MonoBehaviour`, `OnEnable`/`OnDisable` 등록, `Kind` enum (PlayerLocal/PlayerRemote/Enemy/Boss/Pickup/Portal), `public void SetKind/SetTint/SetIcon`
  - Player prefab default: `Kind=PlayerLocal`, `Tint=파랑` (인스펙터 값, CL-222 Editor 작업)
- **CL-223 산출물**:
  - `MinimapFog.cs` — `revealKinds = PlayerLocal | PlayerRemote` (AgentKindMask). 클라이언트 측 모든 활성 player agent (local + remote 둘 다) 를 reveal source 로 사용 → 자동 공유 fog
  - `MinimapMarkerOverlay.cs` 의 `IsAlwaysVisible` — `PlayerLocal/PlayerRemote` 만 fog 무시
- **NGO 패턴 (기존)**:
  - `Unity.Netcode.GameObjects 2.11.1`
  - `PlayerMovementSync.cs` — `NetworkTransform` 상속, owner-authoritative 이동 sync
  - `RelaySessionHost/Client` — Relay 기반 호스트/참가
  - `HostAuthority.IsHost` — 싱글 실행 시 NetworkManager 비활성 → `true` 반환. 멀티 실행 시 호스트만 `true`
- **NetworkBehaviour 라이프사이클**:
  - 같은 또는 부모 GameObject 에 `NetworkObject` 필수
  - `OnNetworkSpawn()` — 네트워크 spawn 직후 호출. `IsOwner`/`IsServer`/`IsClient` 확정됨
  - `OnNetworkDespawn()` — despawn 시
  - **NetworkManager 가 비활성 또는 NotRunning 이면 OnNetworkSpawn 안 불림** → 싱글플레이는 자연스럽게 default 동작 유지

---

## 선검증 단계 (CL-224 시작 직전 30초~수분)

본 CL 코드 작성 전에 **반드시** 다음 검증.

### 검증 항목
1. 호스트 + 클라이언트 2인 NGO 세션 띄우기 (`Test_Network_AD.unity` 또는 동등 씬)
2. 호스트가 적을 spawn 시키고 클라이언트 화면에서 확인:
   - **A. 적 GameObject 가 클라 hierarchy 에 존재하는가?** (호스트가 NetworkObject 로 spawn 하면 자동 복제)
   - **B. 클라 측 `MinimapAgent.All` 에 적이 등록되는가?** (Inspector debug 또는 Debug.Log)
   - **C. 클라 측 미니맵에 적 빨강 마커가 뜨는가?** (CL-222 검증 끝났다고 가정)
3. 호스트 측 적이 움직일 때 클라 측에서도 같은 위치로 보이는가? (NetworkTransform 복제 여부)

### 결과 분기

| 결과 | 본 CL 범위 |
|---|---|
| **모두 ✅** (적이 NetworkObject 로 spawn + 위치 복제됨) | 본 CL 코드 = Tinter 만 추가. 추가 sync 작업 0 |
| **A ❌** (클라엔 적 GameObject 자체 없음) | **본 CL 범위 확장** — 옵션 결정 필요 (아래 옵션 표) |
| **A ✅, B ❌** (적 GameObject 는 있는데 MinimapAgent 등록 안 됨) | 적 prefab 의 MinimapAgent 가 OnEnable 호출되지 않는 케이스. NetworkObject 활성화 타이밍 검토 |
| **A ✅, B ✅, C ❌** (등록은 되는데 마커 안 뜸) | CL-222 의 `MinimapMarkerOverlay` 좌표 변환 / culling mask 점검 |

### A ❌ 일 경우 옵션

| 옵션 | 작업량 | 영향 |
|---|---|---|
| **B-Net1**: 적 prefab 을 NetworkObject 화 | 모든 적 prefab 수정 + MMSimpleObjectPooler ↔ NetworkObjectPool 마이그레이션 | 큼. **본 CL 범위 외** — 별도 CL |
| **B-Net2**: 호스트가 적 위치만 broadcast — 신규 `NetworkMinimapPing` 컴포넌트가 NetworkVariable<Vector2[]> sync, 클라가 가짜 마커 객체 생성 | 중간. NetworkObjectPool 무관 | 본 CL 에서 가능. 적 위치만 sync 라 비용 작음 |
| **B-Det1**: 클라이언트도 같은 시드로 자체 던전 빌드 (deterministic) | `DungeonRunBootstrap.IsAuthority` 가드 제거 + 시드 RPC sync | 큼. 던전 시스템 변경 — **별도 CL** |
| **B-Skip**: 적 마커는 호스트만 보고 클라는 fog reveal 만 (적 마커 없는 미니맵) | 0. CL-222/223 동작 그대로 | 코옵 UX 저하 — 비추천 |

**선검증 결과 A ❌ 일 경우 본 CL 진입 전 의사결정 필요** (사용자 + Claude 협의).

본 plan md 의 **이후 섹션은 "검증 결과 모두 ✅" 가정**으로 작성. ❌ 일 경우 plan 갱신 후 진행.

---

## 결정 사항 (사용자 확정)

| 결정 | 값 | 근거 |
|---|---|---|
| NGO 통합 구조 | **(b) PlayerNetMinimapTinter 별도 컴포넌트** | MinimapAgent 무수정. 적/픽업/포탈 NetworkObject 강제 회피. 기존 `PlayerMovementSync` 패턴 일관 |
| 팀원 마커 색 | **단일 초록** (`PlayerRemote = 초록`) | 단순. 4명 코옵 다색은 후속 폴리시 |
| 다운/사망 시 마커 | **자동 사라짐** | Health.OnDeath → GameObject 비활성화 → MinimapAgent.OnDisable 자동. 부활 시 자동 복귀. 추가 코드 0 |
| 공유 fog | **CL-223 `revealKinds = PlayerLocal\|PlayerRemote` 자동 작동** | 추가 RPC/sync 0. 클라이언트마다 자기 fog 마스크가 모든 활성 player agent 를 reveal source 로 사용 |
| Tinter 부착 위치 | **Player prefab 루트** (MinimapAgent 와 같은 GO) | 단일 GameObject. GetComponent 참조 단순 |
| OnNetworkSpawn 분기 | **IsOwner → PlayerLocal+파랑 / 그 외 → PlayerRemote+초록** | 싱글 케이스 |
| 싱글플레이 호환 | **자동** — NetworkManager 미가동 시 OnNetworkSpawn 안 불림 → MinimapAgent 인스펙터 default 유지 | 분기 코드 X |
| 적 권위 | **선검증 후 결정** — 본 plan 은 "검증 결과 ✅" 가정 | 검증 안 한 채 결정 무용 |

---

## 작업 범위

### Phase A — 코드 (⏳ 대기)

- [ ] `PlayerNetMinimapTinter.cs` 신규 — `NetworkBehaviour` 상속, `OnNetworkSpawn` 에서 IsOwner 검사 후 MinimapAgent 의 Kind/Tint 갱신
- [ ] (선검증 결과에 따라) 적 위치 sync 코드 — 본 plan 은 ✅ 가정이라 0줄

### Phase B — Editor 작업 (⏳ 사용자 / khi)

- [ ] **선검증 30초** — 위 "선검증 단계" 수행
- [ ] Player prefab 에 `PlayerNetMinimapTinter` 컴포넌트 부착
- [ ] 인스펙터: Local Color = 파랑, Remote Color = 초록 (CL-222 default 색과 정확히 일치)
- [ ] Player prefab 에 `NetworkObject` 가 이미 있는지 확인 (기존 `PlayerMovementSync` 동작에 필수 — 이미 있을 것)
- [ ] (선택) 적 prefab 에 `MinimapAgent` 가 이미 부착되어 있고 NetworkObject 가 자동 복제되는지 확인 (선검증과 동일)

### Phase C — 검증 (⏳ 사용자 / khi)

- [ ] 호스트 + 클라 2인 세션 시작
- [ ] 양쪽 클라이언트 모두 미니맵 정상 표시 (CL-222 검증)
- [ ] 자기 마커 = 파랑 (양쪽에서 자기 자신은 파랑)
- [ ] 팀원 마커 = 초록 (양쪽에서 상대는 초록)
- [ ] 양쪽 마커가 서로 같은 world 위치에 표시 (NetworkTransform 동기화 확인)
- [ ] 한 명이 새 영역 탐색 → 양쪽 fog 둘 다 reveal (공유 fog)
- [ ] 한 명이 적을 만남 → 양쪽 미니맵에 적 마커 등장
- [ ] 한 명이 적을 처치 → 양쪽에서 사라짐
- [ ] 한 명 다운 → 그쪽 마커 사라짐, 다른 쪽 마커는 유지
- [ ] 다운된 플레이어 부활 → 마커 복귀
- [ ] 호스트 이탈 → 클라이언트도 정상 종료 (`SessionLifecycle` 처리, 미니맵 state 누수 확인)
- [ ] 싱글플레이 (NetworkManager OFF) 진입 → 자기 마커 파랑, 모든 동작 정상 (Tinter 가 OnNetworkSpawn 안 불려도 default 유지)

### 작업 외 (Out of scope)

- 4명 이상 코옵 다색 팀원 마커 — 후속
- 호스트 이탈 시 미니맵 fade out / 정리 애니메이션 — 후속
- 죽은 플레이어 마커 회색 처리 (현재는 사라짐) — 후속 디자인 결정 시
- AutoFromBounds 자동 fit + 클라이언트 측 던전 영역 동기화 — 후속
- 적 권위 ❌ 시 옵션 (B-Net2 등) — 별도 CL
- 음성/채팅과의 미니맵 통합 (예: 핑 시스템) — 별도 epic

---

## 변경 파일

### 신규

| 파일 | 책임 |
|---|---|
| `Assets/_Project/Scripts/Runtime/UI/Minimap/PlayerNetMinimapTinter.cs` | NetworkBehaviour 상속, OnNetworkSpawn 에서 IsOwner 검사 → MinimapAgent.SetKind/SetTint 호출. NetworkManager 비가동 싱글플레이엔 무영향 |

### 수정

본 CL 범위에서 기존 코드 **수정 없음** (CL-222 의 `MinimapAgent`/`MarkerOverlay`/`HUD`/`CameraRig` + CL-223 의 `MinimapFog` 모두 그대로 작동).

선검증 결과 ❌ 일 경우 별도 patch 필요 — 그 시점에 plan md 갱신.

---

## 코드 spec

### PlayerNetMinimapTinter.cs (신규)

```csharp
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.UI.Minimap
{
    /// <summary>
    /// Player prefab 에 부착. NetworkBehaviour 상속이라 NetworkObject 동반 필수.
    /// OnNetworkSpawn 에서 IsOwner 검사 후 MinimapAgent 의 Kind/Tint 를 로컬/원격 으로 갱신.
    /// 싱글플레이 (NetworkManager 비가동) 에서는 OnNetworkSpawn 이 안 불려서 MinimapAgent 인스펙터 default 가 그대로 유지됨 — 분기 코드 X.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Minimap/Player Net Minimap Tinter")]
    [RequireComponent(typeof(MinimapAgent))]
    public sealed class PlayerNetMinimapTinter : NetworkBehaviour
    {
        [SerializeField] private MinimapAgent minimapAgent;
        [SerializeField] private Color localColor = new Color(0.2f, 0.5f, 1f, 1f);
        [SerializeField] private Color remoteColor = new Color(0.2f, 0.9f, 0.4f, 1f);

        private void Reset()
        {
            minimapAgent = GetComponent<MinimapAgent>();
        }

        private void Awake()
        {
            if (minimapAgent == null)
            {
                minimapAgent = GetComponent<MinimapAgent>();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (minimapAgent == null) return;

            if (IsOwner)
            {
                minimapAgent.SetKind(MinimapAgent.AgentKind.PlayerLocal);
                minimapAgent.SetTint(localColor);
            }
            else
            {
                minimapAgent.SetKind(MinimapAgent.AgentKind.PlayerRemote);
                minimapAgent.SetTint(remoteColor);
            }
        }
    }
}
```

**동작 흐름**:
```
Unity 객체 생성
  ↓ Awake (MinimapAgent / Tinter 등 서로 GetComponent)
  ↓ OnEnable (MinimapAgent.ActiveAgents 에 추가)
  ↓ Start
  [NGO 활성 시] OnNetworkSpawn (Tinter 가 IsOwner 검사 + SetKind/SetTint 호출)
  [NGO 비활성 시] OnNetworkSpawn 호출 X — default 그대로
```

**잠깐의 default kind**: NGO 활성 케이스에서 OnEnable~OnNetworkSpawn 사이 1프레임 정도 default kind 로 그려질 가능성. 시각적으로 무의미 — 미니맵이 첫 프레임에 자기 자신을 잘못된 색으로 잠깐 보이는 정도. 의도적으로 무시.

**MinimapAgent 인스펙터 default 와의 일관성**:
- CL-222 Editor 작업에서 Player prefab 의 MinimapAgent 인스펙터 → Kind=PlayerLocal, Tint=파랑 으로 설정했음
- 본 CL Tinter 의 `localColor` 도 같은 파랑 으로 맞춰야 NGO 활성/비활성 케이스 모두에서 시각 일관 (Editor 작업 체크리스트에 명시)

---

## Editor 작업 체크리스트 (Phase B 상세)

### 1. 선검증 (필수, 본 CL 진입 조건)
- 호스트 + 클라 2인 세션 띄우고 위 "선검증 단계" 4가지 항목 확인
- 결과 ✅ 면 다음 단계로
- 결과 ❌ 면 본 plan md 의 "옵션 표" 검토 + 사용자 협의 → plan md 업데이트 후 재개

### 2. PlayerNetMinimapTinter 부착
- Player prefab 열기 (`Assets/_Project/Prefabs/Characters/<Player>.prefab`)
- 루트 GameObject 의 MinimapAgent 와 같은 GO 에 `PlayerNetMinimapTinter` 컴포넌트 추가
- 인스펙터 설정:
  - **Minimap Agent**: 같은 GameObject 의 MinimapAgent (Reset 시 자동 GetComponent)
  - **Local Color**: `(0.2, 0.5, 1.0, 1.0)` — CL-222 의 MinimapAgent.tint 와 정확히 일치
  - **Remote Color**: `(0.2, 0.9, 0.4, 1.0)` — 초록

### 3. NetworkObject 확인
- Player prefab 의 루트에 `NetworkObject` 가 이미 부착되어 있는지 확인 (CL-202 / CL-7 NGO 도입 시 추가됨, `PlayerMovementSync` 동작에 필수)
- 없으면 본 CL 범위 외 — NGO 도입 누락 이슈로 별도 ticket

### 4. 적 prefab — MinimapAgent 점검
- 선검증에서 적 마커가 클라 측에서 정상 표시됨을 확인했다면 추가 작업 0
- 적 prefab 의 MinimapAgent 인스펙터 (CL-222 에서 부착됨) 그대로 유지

### 5. 멀티 인스턴스 테스트 환경
- Unity Editor 1 인스턴스 + Build & Run 1 인스턴스 (또는 ParrelSync 등 멀티 에디터)
- 두 인스턴스에서 호스트/클라 역할 나눠 검증

---

## 검증 방법 (Phase C 상세)

### 기본 동작
1. 멀티 인스턴스 (호스트 + 클라) 모두 같은 던전 씬 진입
2. **Expected**: 양쪽 미니맵이 정상 표시 (검정 fog + 자기 위치 reveal)
3. 자기 마커 색 — 호스트 인스턴스: 파랑, 클라 인스턴스: 파랑 (각자 자기 자신은 PlayerLocal=파랑)
4. 팀원 마커 색 — 호스트 인스턴스에서 클라 player: 초록, 클라 인스턴스에서 호스트 player: 초록

### 위치 sync
5. 호스트 player 이동 → 클라 미니맵에서 호스트 마커가 같이 움직임
6. 양 마커 위치가 정확히 같은 world 위치 (jitter < 100ms 수준 — `PlayerMovementSync` NetworkTransform 정상)

### 공유 fog
7. 호스트가 새 영역 탐색 → 호스트 미니맵 reveal
8. **클라 미니맵에서도 같은 영역 reveal** (호스트 player 가 클라 측에서 PlayerRemote 로 등록되어 있어 클라 fog 가 reveal)
9. 클라가 다른 영역 탐색 → 양쪽 모두 reveal
10. 한 명이 본 곳을 다른 사람이 안 가도 영구 reveal 유지

### 적 마커
11. 호스트 측에서 적 spawn → 양쪽 클라이언트에서 빨강 마커 표시
12. 적이 fog 안에 있으면 양쪽 다 안 보임 (Player 위치가 reveal 한 곳만 보임)
13. 한 명이 적 가까이 가서 reveal → 양쪽 다 적 마커 보임
14. 적 처치 → 양쪽에서 마커 동시 사라짐

### 다운/부활
15. 한 player 다운 (Health=0) → 그 player GameObject 비활성화 → MinimapAgent.OnDisable → 양쪽 미니맵에서 그 마커 사라짐
16. 부활 → 마커 복귀

### 호스트 이탈
17. 호스트 인스턴스 강제 종료 → 클라이언트 SessionLifecycle 정리 (`docs` per CL-7)
18. 클라 미니맵 state 누수 확인 — Tinter 가 OnNetworkDespawn 처리 없어도 GameObject 자체가 파괴되어 자동 정리됨. Inspector 에서 `MinimapAgent.All` 비어있는지 확인

### 싱글플레이 회귀
19. NetworkManager 비활성 상태로 단독 실행 (`Test_Network_AD.unity` 또는 일반 던전 씬)
20. 자기 마커 파랑 (Tinter OnNetworkSpawn 안 불려도 인스펙터 default 유지)
21. 모든 CL-222/223 동작 정상

---

## 알려진 이슈 / 보류

- **OnEnable~OnNetworkSpawn 사이 1프레임 default kind**: NGO 활성 케이스에서 잠깐 default 색으로 보일 수 있음. 시각적으로 무의미 — 의도적으로 무시. 이슈 되면 Tinter 의 `Awake` 에서도 임시 처리 추가 가능
- **OnNetworkSpawn 가 Awake 보다 늦게 호출**: NGO 라이프사이클 — Awake → OnEnable → Start → OnNetworkSpawn (NetworkObject 가 spawn 된 시점). MinimapAgent 의 OnEnable 에서 `ActiveAgents` 에 추가됨. Tinter 의 SetKind 는 이미 등록된 agent 의 데이터만 변경하므로 안전
- **NetworkManager 가 Start 전에 disposed**: 비정상 종료 시 OnNetworkDespawn 안 불릴 수 있음. MinimapAgent 의 OnDisable 이 ActiveAgents 에서 제거하므로 GameObject 파괴 시 자동 정리됨. Tinter 가 별도 cleanup 코드 추가할 필요 X
- **공유 fog 의 sync 정확도**: 클라이언트마다 자기 fog 마스크는 자기가 보고 있는 player agent 들의 위치 기반. 네트워크 jitter 때문에 호스트의 player 위치가 클라에서 약간 늦거나 보간됨 → fog reveal 영역이 약간 늦게/부정확하게 그려짐. 영구 reveal 이라 시간 지나면 자기 보정. 1차에서 무시
- **새로 접속한 클라이언트의 fog**: 늦게 들어온 클라는 이전 탐색 영역이 fog 검정 — 호스트가 이미 본 곳을 자기는 처음 들어와서 보는 셈. 코옵 UX 표준 (이게 "공유 fog 의 자연스러운 동작"). 만약 이전 탐색 fog 까지 sync 하려면 별도 NetworkVariable<byte[]> 마스크 broadcast 필요 — 후속 CL
- **MinimapAgent 인스펙터 default Tint vs Tinter localColor 불일치 위험**: CL-222 에서 설정한 Tint 와 Tinter 의 localColor 가 다르면 싱글/멀티 케이스에서 색 다르게 보임. Editor 작업 체크리스트에 일치 강조
- **PlayerNetMinimapTinter 가 Player prefab 에만 있는지 보장**: 적/픽업/포탈 prefab 에 실수로 부착되면 NetworkObject 강제 + 비싸짐. `[RequireComponent(typeof(MinimapAgent))]` 정도로 가드. 코드 리뷰 시 점검

---

## 후속 후보

| 내용 | 우선도 |
|---|---|
| 4명 코옵 다색 팀원 마커 (파스텔 4색 자동 할당) | P3 |
| 다운/사망 시 회색 처리 + "X" 아이콘 (사라짐 대신) | P3 |
| 핑 시스템 — 미니맵 클릭 시 팀에게 위치 broadcast | P2 |
| 음성/채팅과 미니맵 통합 (말한 사람 마커 강조) | P3 |
| 늦게 접속한 클라이언트의 fog 사전 sync (NetworkVariable<byte[]>) | P3 |
| 적 위치 sync 옵션 B-Net2 (선검증 ❌ 시 별도 CL 으로 분리) | 검증 결과 따라 P1 |
