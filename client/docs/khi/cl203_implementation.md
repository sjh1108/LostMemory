# CL-203 구현 요약 — 유물 트리거 피드백 VFX 4종

**상태**: 검증 완료 (2026-05-09)
**관련 plan**: [cl203_relic_trigger_fx_plan.md](cl203_relic_trigger_fx_plan.md)

## 목적

개별 유물 효과의 4가지 트리거 시점(패링 성공 / 적 처치 / 대시 종료 / 회복약 사용)에 시각·청각 피드백이 0이라 플레이어가 효과 발동을 인지 불가. VFX + SFX + 만료 페이드아웃을 추가해 직관적으로 표시.

## Plan과 달라진 점

| 항목 | Plan 원안 | 실제 구현 | 사유 |
|---|---|---|---|
| 프리팹 경로 | `_Project/Prefabs/VFX/` | `_Project/Prefabs/Effect/` | CL-201/CL-202 일치 |
| HealVFX | 신규 제작 | **기존 `Heal_VFX.prefab` 활용** | 기존 자산 재활용, 와이어링만 |
| 데이터 정비 | 언급 없음 | **2개 RelicData 자산 정비 포함** | 옛 시스템에서 누락된 트리거 효과 등록. 본 CL 검증 가능하게 함 |

## 결정 사항 (사용자 확정)

- **보호막**: 청록 에너지 막 `(0.4, 0.9, 1.0)` + 별빛 발동
- **공속 오라**: 빨강 `(1, 0.3, 0.3, 1)` (발 밑)
- **이속 오라**: 파랑 `(0.3, 0.6, 1, 1)` (발 밑)
- **회복**: 녹색 (Heal_VFX.prefab 색상)
- **버프 갱신 정책 B**: duration만 갱신, VFX 인스턴스 유지 (시각 깜빡임 방지)
- **만료 처리**: Update tick에서 expiresAt 비교 → FadeOutAndDestroy 코루틴
- **Authority 게이트**: 기존 `_authority.IsAuthority` 안에서 처리 (host-only)

## 변경 파일

### 수정 스크립트

[RelicEffectRegistry.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs)
- `using System.Collections; using LostMemory.VFX;` 추가
- 신규 SerializeField 8개 (VFX prefab 2 + Aura color 2 + SFX 3 + sfxVolume + fadeOut)
- 활성 인스턴스 추적 필드 7개 (Shield/AttackSpeed/MoveSpeed × prefab+expiresAt + playerTransform)
- `Awake` 신규 — `_playerTransform` 캐시 (`playerHealth.transform` 우선)
- `Update` 신규 — 3개 expiresAt 만료 체크 → 페이드아웃 코루틴
- `HandleEnemyKilled` — 빨강 BuffAura SpawnAttached + SFX (정책 B)
- `HandleParrySuccess` — Shield VFX SpawnAttached + SFX (정책 B)
- `HandleDashEnded` — 파랑 BuffAura SpawnAttached + SFX (정책 B)
- `HandleRunCleared` — 활성 VFX 즉시 정리
- 헬퍼: `ApplyAuraColor` (ParticleSystem startColor 일괄 변경), `FadeOutAndDestroy` (CL-202 패턴 동일)

[PlayerHealing.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerHealing.cs)
- `using LostMemory.VFX;` 추가
- 신규 SerializeField 3개 (`_healVFXPrefab`, `_healSfx`, `_sfxVolume`)
- `UseConsumable` — `VFXSpawner.Spawn(_healVFXPrefab, ..., 1.5f)` + SFX

### 신규 프리팹

| 프리팹 | 구성 |
|---|---|
| `_Project/Prefabs/Effect/ShieldVFX.prefab` | EnergyShell(SpriteRenderer) + EnergyParticles(Loop) + GrantBurst(1회 12 burst) |
| `_Project/Prefabs/Effect/BuffAuraVFX.prefab` | GroundLight(회전 발광 원판) + Sparkles(위로 떠오름). 색은 `ApplyAuraColor`로 인스턴스화 후 덮어씀 |

### 기존 자산 활용

`_Project/Prefabs/Effect/Heal_VFX.prefab` — 와이어링만, 신규 제작 없음.

### 데이터 정비 (검증 blocker fix)

옛 RelicData 2종이 `_effects` 배열 시스템 마이그레이션 시 트리거 효과 누락. `_effectDescription`은 트리거 효과를 명시하는데 데이터는 None/영구로 셋업됨 → 버그 픽스.

**`RelicData_붉은송곳니.asset`** (`_effects[]`에 추가):
```yaml
- Type: 2          # AttackSpeedOnKillTimed
  Magnitude: 0.12  # 공속 +12%
  Duration: 3
  Threshold: 0
```

**`RelicData_추적자의망토.asset`** (`_effects[]`에 추가):
```yaml
- Type: 10         # MoveSpeedAfterDashTimed
  Magnitude: 0.2   # 이속 +20%
  Duration: 2
  Threshold: 0
```

Legacy 필드는 그대로 보존 (`RelicData.EffectType` getter가 `_effects[0]` 우선 사용).

## 핵심 동작

### 트리거 → VFX 흐름

```
이벤트 발화 (ParrySucceeded / EnemyKilled / DashEnded / UseConsumable)
  ↓
Authority 게이트 통과 (host-only)
  ↓
효과 등록 (PlayerShield.GrantShield / container.AddTimed / Heal)
  ↓
maxDuration 추출
  ↓
VFX 인스턴스 정책:
  - 활성 인스턴스 없으면: SpawnAttached(prefab) + ApplyAuraColor(색)
  - 활성 인스턴스 있으면: 그대로 두고 expiresAt만 갱신
  ↓
SFX 1회 재생 (자산 미할당 시 silent skip)
```

### Update 만료 흐름

```
매 프레임 _activeShieldVFX/AttackSpeed/MoveSpeed 의 expiresAt 비교
  → 만료된 것 있으면 StartCoroutine(FadeOutAndDestroy)
  → null 처리

FadeOutAndDestroy:
  ParticleSystem.Stop(StopEmitting) → 자연 흩어짐
  SpriteRenderer 알파 0.25초 lerp → 0
  Destroy(vfx)
```

## 검증 방법

1. **보호막**: `반격의표식` 1개 → 패링 성공 시 청록 ShieldVFX + 별빛, 3초 유지
2. **보호막 갱신**: 활성 중 다시 패링 → 인스턴스 유지, 시각 깜빡임 없음
3. **공속 오라**: 자산 정비된 `붉은송곳니` 1개 → 적 처치 시 빨강 BuffAura, 3초
4. **이속 오라**: 자산 정비된 `추적자의망토` 1개 → 대시 종료 시 파랑 BuffAura, 2초
5. **공속+이속 동시**: 두 오라 발 밑 겹쳐서 동시 표시
6. **회복**: 회복약 사용 → Heal_VFX 1.5초 떠오르며 fade
7. **만료 페이드아웃**: 0.25초 알파 fade 후 destroy
8. **Run 종료**: HandleRunCleared 호출 시 활성 VFX 즉시 정리

## 검증 결과 (2026-05-09)

검증 8 항목 인게임 통과 — 보호막 패링, 보호막 갱신(정책 B), 공속 오라(적 처치), 이속 오라(대시 종료), 공속+이속 동시, 회복약, 만료 페이드아웃, Run 종료 정리.

### 검증 단계 발견 / 처리

검증 진입 시 본 CL 외 흔적으로 인스펙터 와이어링 누락이 발견되어 보완.

| # | 발견 | 영향 | 처리 |
|---|---|---|---|
| W1 | `TestKhi_MinimalCharacter2D.prefab` 의 `PlayerHealing._healVFXPrefab` 미할당 | 검증 #6 회복 VFX 미생성 | `Heal_VFX.prefab` 슬롯 와이어링 |
| W2 | `PlayerRelicInventory._debugRelicsToAdd` 가 점성술 7종(타로/별자리/수정구/카드/수레바퀴/신탁/별의운명)으로 채워져 본 CL 검증 3종 미부여 | 검증 #1, #3, #4 트리거 자체 발생 안 함 | `반격의표식` / `붉은송곳니` / `추적자의망토` 추가 |

W1, W2 는 코드/자산 외 인스펙터 영역 — prefab 변경으로 본 CL 에 포함.

## 인스펙터 와이어링 (플레이어 프리팹)

### `RelicEffectRegistry`

| 슬롯 | 값 |
|---|---|
| Shield VFX Prefab | `Assets/_Project/Prefabs/Effect/ShieldVFX.prefab` |
| Buff Aura VFX Prefab | `Assets/_Project/Prefabs/Effect/BuffAuraVFX.prefab` |
| Attack Speed Aura Color | `(1, 0.3, 0.3, 1)` |
| Move Speed Aura Color | `(0.3, 0.6, 1, 1)` |
| Shield Granted/Kill Buff/Dash Buff Sfx | (미할당 OK) |
| Sfx Volume | 0.6 |
| VFX Fade Out Seconds | 0.25 |

### `PlayerHealing`

| 슬롯 | 값 |
|---|---|
| Heal VFX Prefab | `Assets/_Project/Prefabs/Effect/Heal_VFX.prefab` (기존 활용) |
| Heal Sfx | (미할당 OK) |
| Sfx Volume | 0.6 |

## 알려진 이슈 / 보류

- **PlayerShield 자체 만료 vs ShieldVFX 만료가 별개** — 보호막이 데미지로 먼저 소진되어도 VFX는 duration까지 유지. 추후 `OnShieldExpired` 이벤트 추가해 동기화 검토 (별도 티켓)
- **SFX 자산 미할당** — 슬롯 노출됨, 자산 결정 후 인스펙터에서 채우기
- **AudioSource.PlayClipAtPoint 풀링 미적용** — CL-201/202 동일 이슈, 추후 통합 풀링
- **Heal_VFX 색감** — plan 권장(`(0.3, 1, 0.4)`)과 다를 수 있음, 인게임 확인 후 어색하면 프리팹 색만 조정 또는 신규 제작으로 전환
- **호스트 권위 → 비호스트 클라이언트 VFX/SFX 미표시** — `_authority.IsAuthority` 가 4 핸들러 + Update + HandleAcquired 에 걸려 있어 멀티플레이 시 클라이언트는 본인 화면에서 자기 유물 VFX 를 못 봄. 본 CL 에서는 의도된 동작(host-only)으로 두고, 클라 자기 화면 표시 필요 여부는 별도 티켓 검토.
- **OnDisable 시 활성 VFX 정리 미존재** — `HandleRunCleared` 외 명시적 정리 경로 없음. SpawnAttached 라 부모 Destroy 시 함께 사라져 일반 케이스는 안전. 본 컴포넌트만 비활성화 + 부모 살아있는 경계 케이스만 leak 가능. 가드 추가 여부는 발생 빈도에 따라 별도 판단.

## 미래 개선

- **A→B 마이그레이션** — 활성 VFX 추적 멤버 필드 3개가 더 늘어나면 `Dictionary<TriggerType, ActiveVFXState>`로 일반화
- **SFX 풀링** — Chain/Wind/Status/Trigger 모두 통합
- **다른 옛 자산 정비** — 비슷한 마이그레이션 누락 자산이 있을 수 있음 (전수 검사 별도 티켓)

## 변경 파일 정리

| 파일 | 변경 |
|---|---|
| [RelicEffectRegistry.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs) | 코드 — using/필드/Awake/Update/4핸들러/헬퍼 |
| [PlayerHealing.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerHealing.cs) | 코드 — using/필드/UseConsumable |
| `_Project/Prefabs/Effect/ShieldVFX.prefab` | **신규** |
| `_Project/Prefabs/Effect/BuffAuraVFX.prefab` | **신규 (공통 베이스)** |
| `_Project/Prefabs/Effect/Heal_VFX.prefab` | 기존 활용 (와이어링만) |
| `_Project/ScriptableObjects/Relics/RelicData_붉은송곳니.asset` | 데이터 정비 — `_effects[]`에 Type 2 |
| `_Project/ScriptableObjects/Relics/RelicData_추적자의망토.asset` | 데이터 정비 — `_effects[]`에 Type 10 |
| 플레이어 프리팹 (RelicEffectRegistry / PlayerHealing) | 인스펙터 와이어링 |
