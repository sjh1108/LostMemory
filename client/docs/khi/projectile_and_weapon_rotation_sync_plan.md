# 발사체/무기 회전 멀티 동기화 픽스 플랜

## 증상
- **활/지팡이 사격** (`KhiArrowProjectile`, `KhiIceBolt`, `KhiFireball`, `KhiMeteor`)
  호스트가 쏜 화살을 게스트가 못 보고, 반대도 마찬가지.
- **활/지팡이 회전** (owner 시점 mouse-aim 으로 회전하는 무기 transform)
  비-owner 측에 회전이 sync 안 됨.
- **단검 콤보 VFX/sprite**
  AttackBroadcast 로 이벤트는 도착하지만(`ClientRpc received event=Started`) presenter 의
  실제 swing/slash/vfx 가 안 보임 — 비-owner 측 presenter wiring 또는 active 상태 의심.

## 근본 원인

### 1) 발사체: owner-local `Instantiate`
`KhiStaffController.TryCastBolt`(line 164), `KhiBowController.SpawnArrow`(line 134),
`KhiStaffController.TryCastFireball/TryCastMeteor` 모두 owner 의 화면에서만 prefab 을 `Instantiate`.
NGO `NetworkObject.Spawn()` 호출이 없어서 다른 클라이언트의 NGO 매니저는 화살 존재 자체를 모름.

`KhiStaffController.cs:117` 주석에 이미 명시:
> "Staff bolt 는 호스트 권위 spawn 이 아니라 owner-local Instantiate 라 다른 클라엔 안 보임
>  — 향후 NetworkObject 화 별도 작업."

### 2) 무기 회전: PlayerMovementSync 의 transform 범위
`PlayerMovementSync` 는 캐릭터 루트의 position/rotation 만 sync. 무기는 자식 transform 의
local rotation 을 `KhiPlayerAim` 이 owner-only 로 매 프레임 갱신 → 비-owner 측 무기는 회전하지 않음.

### 3) 단검 VFX (AttackBroadcast 도착하지만 안 보임)
가능한 원인 후보 (런타임 진단 필요):
- a. 비-owner 측 `KhiAttackVisualPresenter` / `KhiSlashAnimator` / `KhiWeaponPresenter`
     컴포넌트가 prefab 자식에 안 붙어 있거나 wiring 누락.
- b. `PlayerMovementSync` 가 비-owner 측에서 `KhiMeleeComboController` 만 disable 하지만
     visual presenter 들도 같이 disable 되는 부수 효과 (확인 필요).
- c. `weaponData` resolve race — `ResolveStep` 가 null 리턴 (이미 로그로 잡힘).

## 픽스 옵션

### Option A — 발사체 NetworkObject 화 (정공)
1. 각 발사체 prefab 에 `NetworkObject` 부착, `NetworkTransform`(또는 직접 sync) 으로 위치 sync.
2. owner 가 직접 spawn 하는 대신 **ServerRpc** 로 호스트에 spawn 요청 → 호스트가
   `Instantiate(prefab).GetComponent<NetworkObject>().SpawnWithOwnership(ownerClientId)` 호출.
3. KhiArrowProjectile.Launch 의 일부 로컬 처리(homing, damage, lifetime) 는 호스트 권위로 이동,
   클라이언트 측은 visual-only.
4. 비용 ↑: pooled NetworkObject 같은 최적화 권장. NGO 의 in-scene pool 이나
   `NetworkObjectPool` 샘플 참고.

### Option B — Visual-only broadcast (가벼움, 권장 우선)
1. 발사체를 owner 측 `Instantiate` + 다른 클라이언트에 *시각 전용* 사본을 broadcast.
2. `AttackBroadcast` 패턴 확장 — `ProjectileSpawnedClientRpc(prefabId, origin, direction, speed, ownerId, seqId)`.
3. 비-owner 측은 받은 정보로 *시각만* 표시하고 damage/hit 처리는 안 함 (또는 owner 측 결과를
   추가 ClientRpc 로 broadcast).
4. 단점: 발사체 충돌/데미지 권위가 owner-client 에 있어 cheat-prone 하지만 PvE 단기 픽스로는 충분.

### Option C — 단검 VFX 만 우선 (가장 작은 작업)
1. `AttackBroadcast.BroadcastAttackEventClientRpc` 의 `IsOwner return` 직후
   `attackVisualPresenter==null`, `slashAnimator==null`, `weaponPresenter==null` 각각의
   상태와 `.enabled`, `.gameObject.activeInHierarchy` 를 한 줄 dump.
2. 로그 보고 wiring 인지 disable 인지 판단 후 fix.

## 무기 회전 픽스 (별건)

### Option D — 무기 transform 의 local rotation 도 sync
1. 가장 가벼운 방법: `KhiPlayerAim` 에 NetworkVariable<float> aimAngle 추가, owner-write/everyone-read.
2. 비-owner 측 Update 에서 무기 transform 의 local rotation 을 그 값으로 갱신.
3. NetworkAnimator/NetworkTransform 까진 안 가도 됨 — 1개 float 만 sync 되어도 회전 따라옴.
4. 대안: `KhiWeaponPresenter` 에 같은 패턴.

## 권장 우선순위
1. **Option C** (당장 — 진단 로그 추가, 30분 작업).
2. **Option D** (무기 회전 — float 1개 NetworkVariable, 1-2시간).
3. **Option B** (발사체 visual broadcast — 단검 VFX 와 같은 패턴 재사용, 반나절).
4. **Option A** (전면 NetworkObject 화 — 발사체 충돌 권위 정리. 후순위, 하루+).

## 검증 체크리스트 (Option B+D 까지 가정)
- [ ] 호스트 시점에서 게스트의 캐릭터 회전이 마우스 aim 방향으로 따라옴
- [ ] 게스트가 쏜 화살이 호스트 화면에서 보이고, 그 반대도 됨
- [ ] 비-owner 측 화살은 visual-only 라 충돌 데미지는 owner 측에서만 처리되는지 확인
- [ ] 단검 콤보 VFX/swing 이 비-owner 측에서도 정상 재생
- [ ] AttackBroadcast.verboseLog = false 로 두고도 sync 정상 (스팸 없음)
