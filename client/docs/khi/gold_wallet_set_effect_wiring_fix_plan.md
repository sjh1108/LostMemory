# GoldWallet ↔ SetEffectApplicator 와이어링 픽스 플랜

## 증상
플레이 로그에서 반복적으로 다음이 찍힘:

```
[SetEffectApplicator] OnEnable — wiring: ... goldWallet=⚠ NULL (탐욕 적용 X) ...
```

→ 탐욕(GoldGainPercent) set 효과가 절대 적용되지 않음.

## 근본 원인
`SetEffectApplicator` 와 `GoldWallet` 의 라이프사이클이 어긋남.

- `SetEffectApplicator` 는 Player prefab(또는 PlayerHost) 의 자식 컴포넌트로,
  Player 가 spawn 되는 시점(타이틀/타운/던전 어디든) 에 `OnEnable` 이 돈다.
  `OnEnable` 한 번에 인스펙터 와이어링된 `goldWallet` 필드만 본다.
- 반면 `GoldWallet` 은 던전 빌드(`DungeonRunBootstrap`) 가 만드는 GameObject 위에서
  `Dungeon_1F_1R` 씬이 로드될 때 비로소 `Awake` 한다.
  Title / Lobby / Town 씬에서는 존재 자체가 없음.

따라서 인스펙터에서 `goldWallet` 을 prefab 단계에서 사전 와이어링하는 것이
불가능 (씬 인스턴스이거나, 런타임 spawn). `OnEnable` 시점엔 항상 null.

## 픽스 옵션

### Option A (권장) — 지연 resolve + 변경 시 재라우팅
1. `SetEffectApplicator` 에서 `goldWallet` 직렬화 필드는 유지하되,
   `goldWallet == null` 일 때 `HandleSetTierChanged` 진입 직후
   `FindFirstObjectByType<GoldWallet>()` 로 1회 resolve 한다 (cache).
2. 더불어 `GoldWallet.Awake` 끝에 정적 이벤트 `GoldWallet.Spawned`
   (Action<GoldWallet>) 를 발화. `SetEffectApplicator` 가 OnEnable 에서 구독,
   wallet 이 늦게 생기면 그때 wallet 을 들고와서 *이미 활성인 GoldGainPercent
   tier 가 있다면 재적용*.
3. 이로써 와이어링 인스펙터 의존성 자체를 제거 가능 (필드는 디버그용으로 보존).

### Option B — `GoldWallet` 을 세션-스코프 싱글톤으로 승격
1. `GoldWallet` 을 `DungeonRunBootstrap` 이 아니라 Run 진입 시
   `RunManager` (이미 DontDestroyOnLoad / 세션 라이프) 의 자식으로 spawn.
2. Player prefab 단계에서 와이어링 불가 점은 그대로지만, 적어도 Run 시작
   직후에는 `FindFirstObjectByType<GoldWallet>()` 결과가 안정.
3. Option A 의 지연 resolve 와 같이 가야 안전.

### Option C — Player prefab 안으로 합치기
1. `GoldWallet` 을 Player prefab 의 자식 컴포넌트로 옮기고
   인스펙터에서 같은 prefab 의 `SetEffectApplicator` 와 와이어링.
2. 문제: 골드는 *플레이어 단위 자원* 으로 디자인된 게 아니라 *Run 단위 자원*.
   4인 멀티에서 wallet 이 플레이어마다 따로 있으면 골드 공유 모델이 깨짐.
3. 권장하지 않음.

## 결정
**Option A 채택.** 구현은 별도 CL 로:
- `SetEffectApplicator` 의 OnEnable wiring 로그에 lazy-resolve 안내 추가
- `HandleSetTierChanged` 의 `GoldGainPercent` 케이스에 진입 시점 lazy-resolve
- `GoldWallet.Awake` 에 정적 `Spawned` 이벤트 발화
- `SetEffectApplicator` 는 `Spawned` 구독해서 wallet 이 늦게 생긴 경우
  현재 활성인 탐욕 tier 의 multiplier 를 즉시 적용
- OnDisable 에서 정적 이벤트 unsubscribe

## 검증 체크리스트
- [ ] Town 씬에서 인벤토리에 탐욕 1단 보유 → 던전 진입 시 wallet 생성 후
      multiplier 가 1f + magnitude 로 세팅되는지 console 확인
- [ ] 던전 안에서 탐욕 5세트 달성 → 즉시 multiplier 갱신
- [ ] Run 종료 후 새 Run → multiplier 1.0 으로 복구 (RemoveTierEffect 호출 확인)
- [ ] Host / Guest 양쪽에서 wallet null 경고가 사라지는지 (HostAuthority 가드
      때문에 호스트에서만 set 효과가 도는 점은 유지)
